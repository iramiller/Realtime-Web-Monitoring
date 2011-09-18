using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using WebMonitoringSink.Messages;
using System.Net;
using System.Globalization;
using MongoDB.Bson;
using System.Web;
using System.Collections.Specialized;
using System.Security.Cryptography;

namespace WebMonitoringSink.EventMessages
{
	public class NativeSyslogEvent : IMessage, IConvertibleToBsonDocument
	{
		/*
		 * There are multiple potential parts to a message.  Each one will start with the following
		 * [Marker, Id, Status.Substatus, TTFB, Page#, Total#]
		 * 
		 * The message body (which can span multiple messages) will contain the following
		 * [HttpVerb, RequestURI, MIME, ClientIP, AppId, Referrer, UserAgent]
		 * 
		 * IISNSL: -1352584613 200.0 156 1 1 POST /folder/file.ext text/html 10.0.0.1 / http://yourserver.com/default.html Mozilla/4.0+(compatible;+MSIE+7.0;+Windows+NT+5.1;+Trident/4.0;)
		 */
		private IMessage _baseMessage;
		/// <summary>
		/// holds a reference to an MD5 convertor for making unique client ip/user agent hash strings.
		/// </summary>
		private static readonly MD5 _mdFive = MD5.Create();

		private string _hash;

		public NativeSyslogEvent(IMessage message)
		{
			if (message == null || !message.Message.StartsWith("IISNSL:"))
				throw new ArgumentException("Invalid Message","message");
			_baseMessage = message;
			ParseMessage(_baseMessage.Message);
		}

		/// <summary>
		/// Parses the given message into the properties of an IISNSL message.
		/// </summary>
		/// <param name="p">the message data.</param>
		private void ParseMessage(string p)
		{
			string[] parts = p.Split(' ');
			if (parts.Length >= 6) // the full header should be here...
			{
				// simple detection for garbage in.
				if (!parts[0].Equals("IISNSL:", StringComparison.Ordinal))
					throw new InvalidOperationException("An invalid message format was detected.");
				Id = parts[1]; // -1352584613
				ParseStatus(parts[2]);
				ProcessingTime = ParseIntValue(parts[3], "Could not parse the Request Time to Process.");
				MessageNumber = ParseIntValue(parts[4], "Could not parse the message number");
				TotalMessages = ParseIntValue(parts[5], "Could not parse the total messages number");
			}
			if (MessageNumber == 1)
			{
				if (parts.Length >= 7)
					HttpVerb = parts[6];
				if (parts.Length >= 8)
					RequestUri = new Uri(String.Format(CultureInfo.InvariantCulture, "{0}{1}{2}{3}",Uri.UriSchemeHttp, Uri.SchemeDelimiter, HostName, parts[7]));
				if (parts.Length >= 9)
					ContentType = parts[8] == "-" ? null : parts[8];
				if (parts.Length >= 10)
					ClientAddress = parts[9];
				if (parts.Length >= 11)
					ApplicationId = parts[10];
				if (parts.Length >= 12)
					Referer = parts[11];
				if (parts.Length >= 13)
					UserAgent = parts[12];

				IsFragment = parts.Length < 13;
			}
			else
			{
				IsFragment = true;
				MessageFragment = p.Substring(string.Join(" ", parts).Length);
			}
		}

		/// <summary>
		/// Allows a split message to be rebuilt into a single message for processing.
		/// </summary>
		/// <param name="fragments">the remaining message fragments to append</param>
		/// <remarks>For a safer implementation we should take in an array of NativeSyslogEvent messages and 
		/// error when message headers do not add up or if potentially if the parts are in the wrong order.</remarks>
		public void MergeParse(params string[] fragments)
		{
			if (fragments == null)
				throw new ArgumentNullException("fragments");
			if (MessageNumber != 1)
				throw new InvalidOperationException("A merged parse must be performed on the first message in the sequence");
			if (fragments.Length != TotalMessages -1)
				throw new ArgumentException(String.Format("Expected {0} message fragments to rebuild message and {1} were found.", TotalMessages, fragments.Length), "fragments");
			ParseMessage(_baseMessage.Message + String.Join("", fragments));
			TotalMessages = 1;
		}

		/// <summary>
		/// Parses out an integer from the given fragment of data, if this fails then an
		/// invalidoperationexception is thrown with the given message.
		/// </summary>
		/// <param name="rawInt">the string to parse into an integer</param>
		/// <param name="exceptionMessage">The message to include in the InvalidOperationException if the call fails</param>
		/// <returns>The integer parsed from rawInt</returns>
		private int ParseIntValue(string rawInt, string exceptionMessage)
		{
			int parsedInt;
			if (!Int32.TryParse(rawInt, out parsedInt))
				throw new InvalidOperationException(exceptionMessage);
			return parsedInt;
		}

		/// <summary>
		/// Parses out the status and sub status codes
		/// </summary>
		/// <param name="rawStatus">the proposed value to parse into the status codes</param>
		private void ParseStatus(string rawStatus)
		{
			var status = rawStatus.Split('.');
			int priStatus;
			int subStatus;
			if (status.Length != 2 || !Int32.TryParse(status[0], out priStatus) || !Int32.TryParse(status[1], out subStatus))
				throw new InvalidOperationException("An invalid message format was detected.  The status code must be in the primary.sub format");
			StatusCode = priStatus;
			SubStatus = subStatus;
		}

		#region Message Header Properties
		/// <summary>
		/// An Id assigned by the web server which should be unique for 10-15 minutes or so
		/// </summary>
		public string Id { get; private set; }
		/// <summary>
		/// The HTTP status code for the request.  Typically 200, 302, 401, 500, etc
		/// </summary>
		public int StatusCode { get; private set; }
		/// <summary>
		/// The HTTP substatus code for the request.
		/// </summary>
		public int SubStatus { get; private set; }
		/// <summary>
		/// The amount of time in milliseconds the request took to process
		/// </summary>
		public int ProcessingTime { get; private set; }
		/// <summary>
		/// The part number of this message.  ie [1] of 2
		/// </summary>
		public int MessageNumber { get; private set; }
		/// <summary>
		/// Total number of parts to this message.
		/// </summary>
		public int TotalMessages { get; private set; }
		#endregion
		#region Message Body Properties
		/// <summary>
		/// If <c>true</c> then this message did not contain the entire message.
		/// </summary>
		public bool IsFragment { get; private set; }
		/// <summary>
		/// The fragment of the message that should be combined with the other messages in
		/// the set in order to full parse the message.
		/// </summary>
		public string MessageFragment { get; private set; }
		/// <summary>
		/// The request method verb (GET, POST, etc)
		/// </summary>
		public string HttpVerb { get; private set; }
		/// <summary>
		/// The URI path of the request
		/// </summary>
		public Uri RequestUri { get; private set; }
		/// <summary>
		/// The path to the page without any querystring
		/// </summary>
		public string RequestPage { get { return RequestUri.AbsolutePath; } }
		/// <summary>
		/// The MIME content type of the request.
		/// </summary>
		public string ContentType { get; private set; }
		/// <summary>
		/// The IP address associated with the client that made the request
		/// </summary>
		public string ClientAddress { get; private set; }
		/// <summary>
		/// An identifier that represents the application pool servicing the request
		/// </summary>
		public string ApplicationId { get; private set; }
		/// <summary>
		/// The Referring URL as given by the client for the current request
		/// </summary>
		public string Referer { get; private set; }
		/// <summary>
		/// The client's user agent string provided with the request
		/// </summary>
		public string UserAgent { get; private set; }
		/// <summary>
		/// Computes a unique signature for the client
		/// </summary>
		/// <remarks>Recognizes that some users may share an IP (in the case of proxies for example)</remarks>
		public string ClientHash
		{
			get
			{
				if (string.IsNullOrWhiteSpace(_hash))
				{
					if (string.IsNullOrWhiteSpace(ClientAddress))
						throw new InvalidOperationException("Could not compute hash when client address is unavailable");
					_hash = System.BitConverter.ToString(_mdFive.ComputeHash(Encoding.Default.GetBytes(ClientAddress + UserAgent)));
					_hash = string.Join("",_hash.Where(c => c != '-').ToArray());
				}
				return _hash;
			}
		}
		#endregion
		#region IMessage Members

		public DateTime EventDate
		{
			get { return _baseMessage.EventDate; }
		}

		public Facility Facility
		{
			get { return _baseMessage.Facility; }
		}

		public bool IsForwarded
		{
			get { return _baseMessage.IsForwarded; }
		}

		public string ForwardedFrom
		{
			get { return _baseMessage.ForwardedFrom; }
		}

		public string SourceHost
		{
			get { return _baseMessage.SourceHost; }
		}

		public string HostName
		{
			get { return _baseMessage.HostName; }
		}

		public bool IsValid
		{
			get { return _baseMessage.IsValid; }
		}

		public string Message
		{
			get { return _baseMessage.Message; }
		}

		public Priority Priority
		{
			get { return _baseMessage.Priority; }
		}

		public DateTime ProcessedTime
		{
			get { return _baseMessage.ProcessedTime;  }
		}

		public string RawMessage
		{
			get { return _baseMessage.RawMessage; }
		}

		#endregion
		#region IConvertibleToBsonDocument Members

		/// <summary>
		/// helper function that computes any query string values as Bson Document elements which are used when
		/// serializing to Bson for easy querying.
		/// </summary>
		/// <returns>query string parameters as bson elements</returns>
		protected IEnumerable<BsonElement> QueryStringElements()
		{
			if (RequestUri == null || string.IsNullOrWhiteSpace(RequestUri.Query))
				yield break;
			var elements = HttpUtility.ParseQueryString(RequestUri.Query);
			foreach (var k in elements.AllKeys)
			{
				if (!string.IsNullOrWhiteSpace(k))
					yield return new BsonElement(k, elements.Get(k));
			}
		}
		/// <summary>
		/// Returns this event as a BSON document.
		/// </summary>
		/// <returns></returns>
		public BsonDocument ToBsonDocument()
		{
			return new BsonDocument { 
				{ "ApplicationId", this.ApplicationId },
				{ "ClientAddress", this.ClientAddress },
				{ "ClientHash", this.ClientHash},
				{ "ContentType", this.ContentType},
				{ "EventDate", this.EventDate},
				{ "Facility", this.Facility},
				{ "Priority", this.Priority},
				{ "HostName", this.HostName},
				{ "SourceHost", this.SourceHost},
				{ "HttpVerb", this.HttpVerb},
				{ "ProcessedTime", this.ProcessedTime},
				{ "ProcessingTime", this.ProcessingTime},
				{ "Referer", this.Referer},
				{ "RequestPage", this.RequestPage },
				{ "RequestParams", new BsonDocument(QueryStringElements()) },
				{ "StatusCode", this.StatusCode},
				{ "SubStatus", this.SubStatus},
				{ "UserAgent", this.UserAgent}
			};
		}

		#endregion
	}
}
