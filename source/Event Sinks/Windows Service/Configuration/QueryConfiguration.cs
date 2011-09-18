using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Configuration;
using System.Collections.Specialized;
using com.espertech.esper.client;
using com.espertech.esper.client.util;

namespace WebMonitoringSink.Configuration
{
	/// <summary>
	/// This class encapsulates logic that parses statements in a web.config file
	/// and creates EP statements.  The goal is to make it easy to add new statements
	/// when the application is deployed.
	/// </summary>
	class QueryConfiguration
	{
		IList<EPStatement> _statements;
		private IDictionary<string, JSONEventRenderer> _eventRenderers;

		/// <summary>
		/// Creates a new instance of the EP
		/// </summary>
		/// <param name="sectionName"></param>
		/// <param name="admin"></param>
		public QueryConfiguration(string sectionName, EPAdministrator admin, EPServiceProvider epService)
		{
			_statements = new List<EPStatement>();
			_eventRenderers = new Dictionary<string, JSONEventRenderer>();
			CreateStatements(sectionName, admin, epService);
		}

		/// <summary>
		/// Creates an EPStatement for each configuration entry
		/// </summary>
		/// <param name="sectionName">the name of the configuration section to look for</param>
		/// <param name="admin">the EPAdministrator which will create the statements</param>
		/// <param name="epService">the EPServiceProvider used to build the JSONEventRenderers</param>
		void CreateStatements(string sectionName, EPAdministrator admin, EPServiceProvider epService)
		{
			var queries = ConfigurationManager.GetSection(sectionName) as NameValueCollection;
			foreach (var c in queries.AllKeys)
			{
				var s = admin.CreateEPL(queries[c], c);
				_statements.Add(s);
				_eventRenderers.Add(c, epService.EPRuntime.EventRenderer.GetJSONRenderer(s.EventType));
			}
		}

		public void RegisterGlobalHandler(UpdateEventHandler handler)
		{
			foreach (var s in _statements)
				s.Events += handler;
		}

		/// <summary>
		/// Returns a JSONEventRender for the event with the given name.
		/// </summary>
		/// <param name="eventName"></param>
		/// <returns></returns>
		public JSONEventRenderer GetEventRenderer(string eventName)
		{
			return _eventRenderers[eventName];
		}

		/// <summary>
		/// Gets a list of EPStatements that have been loaded from configuration
		/// </summary>
		public IEnumerable<EPStatement> Statements { get { return _statements; } }
	}
}
