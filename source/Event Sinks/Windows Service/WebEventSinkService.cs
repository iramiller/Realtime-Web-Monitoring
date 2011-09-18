using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Net;
using System.ServiceProcess;
using System.Threading;
using System.Threading.Tasks;

using com.espertech.esper.client;
using com.espertech.esper.core;
using log4net;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Builders;

using WebMonitoringSink.Configuration;
using WebMonitoringSink.EventMessages;
using WebMonitoringSink.Messages;

namespace WebMonitoringSink
{
	public partial class WebEventSinkService : ServiceBase
	{
		static readonly ILog Log = LogManager.GetLogger(typeof(WebEventSinkService));
		SyslogListener _listener;
		MongoDatabase _realtimeEvents;
		QueryConfiguration _queries;
		Timer _pageAnalysisTimer;

		public WebEventSinkService()
		{
			InitializeComponent();
			log4net.Config.XmlConfigurator.Configure();
			// create a referenc to the mongo database that will be used by the event service delegate for storing events
			MongoServer server = MongoServer.Create(ConfigurationManager.ConnectionStrings["mongodb"].ConnectionString);
			_realtimeEvents = server.GetDatabase("realtime");
			var epService = ConfigureEventService();
			_listener = new SyslogListener(IPAddress.Any, 514, 2000);
			TaskFactory factory = new TaskFactory();
			factory.StartNew(() => ProcessMessages(_listener, epService.EPRuntime));
		}

		/// <summary>
		/// Invokes a map reduce operation on the iis event table which calculates the
		/// page view traffic data.
		/// </summary>
		/// <param name="state"></param>
		void PerformPageAnalysis(object state)
		{
			try
			{
				var invokeDate = DateTime.Now;
				MongoServer server = MongoServer.Create(ConfigurationManager.ConnectionStrings["mongodb"].ConnectionString);
				MongoDatabase dbEvents = server.GetDatabase("events");
				MongoDatabase dbAnalysis = server.GetDatabase("analysis");

				var iisEvents = dbEvents.GetCollection<BsonDocument>("iis");
				QueryConditionList dateRangeLimit = new QueryConditionList("EventDate");
				dateRangeLimit.GTE(LastPageAnalysis(dbAnalysis));
				dateRangeLimit.LT(invokeDate);
				dbAnalysis.GetCollection("history").Save(new BsonDocument { { "_id", "lastPageAnalysis" }, { "eventDate", invokeDate }, { "eventResult", "" } });
				try
				{
					var result = iisEvents.MapReduce(dateRangeLimit, "function() {var p = this.RequestPage;if (p == '/content/processRequest.ashx' && (this.RequestParams && this.RequestParams.file)) p = this.RequestParams.file; var ltc = (this.ProcessingTime ? this.ProcessingTime : 0); var result = {dateLow: this.EventDate, dateHigh: this.EventDate, host: this.HostName, views: 1, method: {}, referer: {}, keywords: {}, mime: this.ContentType, latency: { avg: ltc, count: 1, min: ltc, max: ltc } }; result.method[this.HttpVerb + '-' + this.StatusCode] = 1; var refr = this.Referer + '';refr = (refr.indexOf('?') > 0 ? this.Referer.substr(0, this.Referer.indexOf('?')) : refr); refr = (refr.indexOf(';') > 0 ? this.Referer.substr(0, this.Referer.indexOf(';')) : refr); result.referer[refr] = 1; var kw = ExtractKeywords(this.Referer); if (kw) result.keywords[kw] = 1;var eventTime = (this.EventDate.getUTCFullYear() * 1000000) + ((this.EventDate.getUTCMonth() + 1) * 10000) + (this.EventDate.getUTCDate() * 100) + this.EventDate.getUTCHours(); emit(eventTime + ':' + this.HostName + p, result);	}", "function (key, values) {var result = values[0];for (var i = 1; i < values.length; i++) {result.dateLow = (result.dateLow < values[i].dateLow ? result.dateLow : values[i].dateLow);result.dateHigh = (result.dateHigh > values[i].dateHigh ? result.dateHigh : values[i].dateHigh);for (var m in values[i].method) {	result.method[m] = values[i].method[m] + (result.method[m] ? result.method[m] : 0);}for (var r in values[i].referer) {	result.referer[r] = values[i].referer[r] + (result.referer[r] ? result.referer[r] : 0);}for (var k in values[i].keywords) {	result.keywords[k] = values[i].keywords[k] + (result.keywords[k] ? result.keywords[k] : 0);}result.views += values[i].views;result.latency.avg = ((result.latency.avg * result.latency.count) + (values[i].latency.avg * values[i].latency.count)) / (result.latency.count + values[i].latency.count);result.latency.count+=values[i].latency.count;result.latency.min = result.latency.min < values[i].latency.min ? result.latency.min : values[i].latency.min;result.latency.max = result.latency.max > values[i].latency.max ? result.latency.max : values[i].latency.max;}return result;}", MapReduceOptions.SetOutput(MapReduceOutput.Reduce("analysis", "pages")));
					dbAnalysis.GetCollection("history").Save(new BsonDocument { { "_id", "lastPageAnalysis" }, { "eventDate", invokeDate }, { "eventResult", result.Response } });
				}
				catch (Exception mrEx)
				{
					dbAnalysis.GetCollection("history").Save(new BsonDocument { { "_id", "lastPageAnalysis" }, { "eventDate", invokeDate }, { "eventResult", mrEx.ToString() } });
				}
				
			}
			catch (Exception ex)
			{
				Log.Error("A problem occured processing the page reduce job.", ex);
			}
		}

		/// <summary>
		/// Determines the date and time of the last page reduce job.
		/// </summary>
		/// <param name="dbAnalysis">a reference to the mongo database that contains the analysis history table</param>
		/// <returns>the date and time of the last processing job or 60 minutes ago if not available</returns>
		DateTime LastPageAnalysis(MongoDatabase dbAnalysis)
		{
			var lastProcessed = DateTime.Now.AddDays(-10);
			if (!dbAnalysis.CollectionExists("history"))
				dbAnalysis.CreateCollection("history");

			var lastEvent = dbAnalysis.GetCollection("history").FindOne(Query.EQ("_id", "lastPageAnalysis"));

			BsonValue lastProc;
			if (lastEvent != null && lastEvent.TryGetValue("eventDate", out lastProc))
				lastProcessed = lastProc.AsDateTime;

			// if there is no record we use a sufficiently old value to indicate the start of the database.
			return lastProcessed;
		}

		/// <summary>
		/// Uses the configuration data in the app.config file to configure the esper event processing environment
		/// </summary>
		/// <returns>an esper event service processing environment</returns>
		EPServiceProvider ConfigureEventService()
		{
			CollectionConfiguration.EnsureCollections(ConfigurationManager.ConnectionStrings["mongodb"].ConnectionString, "eventCollections");
			com.espertech.esper.client.Configuration configuration = new com.espertech.esper.client.Configuration();
			configuration.EngineDefaults.EventMeta.ClassPropertyResolutionStyle = PropertyResolutionStyle.CASE_INSENSITIVE;
			configuration.AddEventType("IIS", typeof(NativeSyslogEvent).FullName);
			configuration.AddEventType("L4N", typeof(ErrorLoggingEvent).FullName);

			EPServiceProviderManager.PurgeDefaultProvider();
			var epService = EPServiceProviderManager.GetDefaultProvider(configuration);
			_queries = new QueryConfiguration("realtimeQueries", EPServiceProviderManager.GetDefaultProvider().EPAdministrator, epService);
			_queries.RegisterGlobalHandler((sender, e) => StoreResults(sender, e));
			return epService;
		}

		/// <summary>
		/// A global event handler that takes an event from the processing runtime and 
		/// stores the data returned in the database in a collection with the same name
		/// as the event
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="updateEventArgs"></param>
		public void StoreResults(object sender, UpdateEventArgs updateEventArgs)
		{
			var n = updateEventArgs.Statement.Name;
			var c = _realtimeEvents.GetCollection(n);
			var r = _queries.GetEventRenderer(updateEventArgs.Statement.Name);

			if (updateEventArgs.NewEvents == null)
			{
				var s = ((StatementResultServiceImpl)sender).LastIterableEvent;
				var js = r.Render(n, s);
				js = js.Replace("NaN.0", "-1");
				c.Insert(new BsonDocument { { "Results", BsonDocument.Parse(js) }, { "Time", new BsonDateTime(DateTime.Now) } });
			}
			else
			{
				List<BsonDocument> newEvents = new List<BsonDocument>();
				foreach (var newEvent in updateEventArgs.NewEvents)
				{
					var bs = newEvent.Underlying as IConvertibleToBsonDocument;
					if (bs != null)
						newEvents.Add(bs.ToBsonDocument());
					else
					{
						var js = r.Render(n, newEvent);
						js = js.Replace("NaN.0", "-1");
						newEvents.Add(BsonDocument.Parse(js));
					}
				}
				var ad = new BsonDocument { { "Results", new BsonArray(newEvents) }, { "Time", new BsonDateTime(DateTime.Now) } };
				c.Insert(ad);
				Debug.Print(ad.ToJson());
			}
		}

		/// <summary>
		/// Starts the service listening for syslog events
		/// </summary>
		/// <param name="args"></param>
		protected override void OnStart(string[] args)
		{
			_listener.Start();
			_pageAnalysisTimer = new Timer((s) => PerformPageAnalysis(s), null, 10000, 10 * 60 * 1000);
			Log.Info("Service Started");
		}
		/// <summary>
		/// Stops the service from processing syslog events.
		/// </summary>
		protected override void OnStop()
		{
			Log.Info("Service Stopped"); 
			_listener.Stop();
			_pageAnalysisTimer.Dispose();
		}

		/// <summary>
		/// The message processing delegate.  Takes messages from the syslog listener and persists
		/// them in the database as well as registers them against the event processing runtime.
		/// </summary>
		/// <param name="listener">the syslog listener to pickup events from</param>
		/// <param name="rtime">the event processing runtime to register the events against</param>
		static void ProcessMessages(SyslogListener listener, EPRuntime rtime)
		{
			MongoServer server = MongoServer.Create(ConfigurationManager.ConnectionStrings["mongodb"].ConnectionString);
			MongoDatabase dbEvents = server.GetDatabase("events");
			var iisEvents = dbEvents.GetCollection<BsonDocument>("iis");
			var logEvents = dbEvents.GetCollection<BsonDocument>("l4n");

			IMessage m;
			while (listener.TryPickupMessage(System.Threading.Timeout.Infinite, out m))
			{
				try
				{
					if (m.Message.StartsWith("IISNSL:"))
					{
						m = new NativeSyslogEvent(m);
						iisEvents.Insert(m.ToBsonDocument());
						rtime.SendEvent(m);
					}
					else if (m.Message.StartsWith("L4N:"))
					{
						m = new ErrorLoggingEvent(m);
						logEvents.Insert(m.ToBsonDocument());
						rtime.SendEvent(m);
					}
					else
						Log.InfoFormat("An Unsupported Message Was Received - {0}", m.Message);
				}
				catch (Exception e)
				{
					Log.Warn("Message Format Exception.", e);
					Debug.Print(m.Message);
					Debug.Print(e.Message);
				}
			}
		}

#region Debug Start/Stop Methods
		[Conditional("DEBUG")]
		public void ConsoleStart()
		{
			OnStart(null);
		}

		[Conditional("DEBUG")]
		public void ConsoleStop()
		{
			OnStop();
		}
#endregion

	}
}
