using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Globalization;
using MongoDB.Bson;

namespace WebMonitoringSink.Messages
{
	/// <summary>
	/// Class which represents a RFC3164 Syslog Message
	/// </summary>
	public class SyslogMessage : IMessage
	{
		private int _parseSeek = 0;
		private string _forwardedFrom;

		/// <summary>
		/// Creates a new syslog message given the components
		/// </summary>
		/// <param name="m">the message body</param>
		/// <param name="p">The message priority</param>
		/// <param name="f">The facility code for the message source</param>
		public SyslogMessage(string m, Priority p, Facility f)
		{
			SourceHost = Environment.MachineName;
			HostName = Environment.MachineName;
			ProcessedTime = DateTime.Now;
			EventDate = ProcessedTime;
			Facility = f;
			Priority = p;
			IsValid = TryParseHeaderPriority();
			Message = m;
			RawMessage = String.Format("<{0}>{1} {2} {3}", (int)Facility << 3 + (int)Priority, ProcessedTime.ToString("MMM dd HH:mm:ss.fff"), HostName, Message);
		}

		/// <summary>
		/// Parses out a syslog message given raw data and a raw machine source
		/// </summary>
		/// <param name="remoteHost"></param>
		/// <param name="raw">message as raw ASCII bytes</param>
		public SyslogMessage(string remoteHost, IList<byte> raw) : this(remoteHost,ASCIIEncoding.ASCII.GetString(raw.ToArray())) { }

		/// <summary>
		/// Parses out a syslog message given raw data and a raw machine source
		/// </summary>
		/// <param name="remoteHost"></param>
		/// <param name="raw"></param>
		public SyslogMessage(string remoteHost, string raw)
		{
			RawMessage = raw;
			SourceHost = remoteHost;
			HostName = remoteHost;
			ProcessedTime = DateTime.Now;
			EventDate = ProcessedTime;
			Facility = Facility.Unknown;
			Priority = Priority.Unknown;
			IsValid = TryParseHeaderPriority();
			CheckForwardHeader();
			// if we capture the time header then the machine name should follow.
			if (ParseTimeHeader())
				ParseMachineName();
			// whatever is left is the message data
			Message = RawMessage.Substring(_parseSeek);
		}

		/// <summary>
		/// Captures the information in the field where the host name is supposed to be.
		/// </summary>
		private void ParseMachineName()
		{
			int pLength = RawMessage.IndexOf(' ', _parseSeek);
			HostName = RawMessage.Substring(_parseSeek, pLength - _parseSeek);
			_parseSeek += HostName.Length + 1; // catches the extra space separator
		}

		/// <summary>
		/// Parses a time header if given.
		/// </summary>
		/// <returns><c>true</c>if a time value was captured in the proper location</returns>
		private bool ParseTimeHeader()
		{
			var formats = new string[] { "MMM dd HH:mm:ss", "MMM dd HH:mm:ss.f", "MMM dd HH:mm:ss.ff", "MMM dd HH:mm:ss.fff", "MMM dd HH:mm:ss.ffff", "MMM dd HH:mm:ss.fffff", "MMM dd HH:mm:ss.ffffff", "MMM dd HH:mm:ss.fffffff",
										"MMM  d HH:mm:ss", "MMM  d HH:mm:ss.f", "MMM  d HH:mm:ss.ff", "MMM  d HH:mm:ss.fff", "MMM  d HH:mm:ss.ffff", "MMM  d HH:mm:ss.fffff", "MMM  d HH:mm:ss.ffffff", "MMM  d HH:mm:ss.fffffff"};
			string t = RawMessage.Substring(_parseSeek, RawMessage.IndexOf(' ', _parseSeek + 8) - _parseSeek); // skip spaces in month, date, hour.
			DateTime result;
			if (DateTime.TryParseExact(t, formats, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out result))
			{
				EventDate = result;
				_parseSeek += t.Length + 1; // extra to catch the space after the field
				return true;
			}
			return false;
		}

		/// <summary>
		/// Looks for the original address header which indicates a forwarded message. If one is found then the
		/// forwarded from values and host name are set appropriately
		/// </summary>
		private void CheckForwardHeader()
		{
			string sig = "Original Address=";
			if (RawMessage.Substring(_parseSeek).StartsWith(sig))
			{
				ForwardedFrom = HostName;
				_parseSeek += sig.Length;
				int pLength = RawMessage.IndexOf(' ', _parseSeek);
				SourceHost = RawMessage.Substring(_parseSeek, pLength - _parseSeek);
				_parseSeek += SourceHost.Length + 1; // catches the extra space separator
			}
		}

		/// <summary>
		/// Attempts to parse the Priority and Facility codes out of the message header
		/// </summary>
		/// <returns><c>true</c> if the message header is a valid match</returns>
		/// <remarks>Also updates the _parseSeek with the new parse offset and sets Facility and Priority values</remarks>
		private bool TryParseHeaderPriority()
		{
			if (string.IsNullOrWhiteSpace(RawMessage))
				return false;
			Regex pri = new Regex(@"^\<(\d{1,3})\>");
			var m = pri.Match(RawMessage);
			if (m.Success)
			{
				_parseSeek += m.Value.Length;
				int p;
				int.TryParse(m.Groups[1].Value, out p);
				try
				{
					Facility = (Facility)(p >> 3);
					Priority = (Priority)(p % 8);
				}
				catch (InvalidCastException) { return false; }
			}
			return m.Success;
		}

		/// <summary>
		/// Indicates if the data passed in for initialization represents a valid syslog message according to RFC3164
		/// </summary>
		public bool IsValid { get; private set; }
		/// <summary>
		/// Indicates that this message was sent with an "Original Address=" header that marks a forwarded message.
		/// </summary>
		public bool IsForwarded { get; private set; }
		/// <summary>
		/// The address of the server which forwarded this message on
		/// </summary>
		public string ForwardedFrom
		{
			get { if (!IsForwarded) return null; else return _forwardedFrom; }
			private set { IsForwarded = true; _forwardedFrom = value; }
		}
		/// <summary>
		/// The facility code associated with this event
		/// </summary>
		public Facility Facility { get; private set; }
		/// <summary>
		/// The priority assigned to this event
		/// </summary>
		public Priority Priority { get; private set; }
		/// <summary>
		/// The hostname given in the event message
		/// </summary>
		public string HostName { get; private set; }
		/// <summary>
		/// The hostname of the host which sent this message.
		/// </summary>
		public string SourceHost { get; private set; }
		/// <summary>
		/// The time at which this LogMessage was parsed
		/// </summary>
		public DateTime ProcessedTime { get; private set; }
		/// <summary>
		/// The date and time reported in the syslog message for the event
		/// </summary>
		public DateTime EventDate { get; private set; }
		/// <summary>
		/// The message body
		/// </summary>
		public string Message { get; protected set; }
		/// <summary>
		/// The raw message data recieved from the source
		/// </summary>
		public string RawMessage { get; private set; }
		/// <summary>
		/// Returns the log message in unprocessed string form
		/// </summary>
		/// <returns>a string of the log message data</returns>
		public override string ToString()
		{
			return RawMessage;
		}
		#region IConvertibleToBsonDocument Members

		/// <summary>
		/// Returns this event as a BSON document.
		/// </summary>
		/// <returns></returns>
		public BsonDocument ToBsonDocument()
		{
			return new BsonDocument { 
				{ "EventDate", this.EventDate},
				{ "Facility", this.Facility},
				{ "Priority", this.Priority},
				{ "HostName", this.HostName},
				{ "ProcessedTime", this.ProcessedTime},
				{ "Message", this.Message},
				{ "SourceHost", this.SourceHost}
			};
		}

		#endregion
	}
	/// <summary>
	/// Returns an integer specifying the facility.  The following are commonly used:
	///</summary>
	public enum Facility
	{
		Unknown = -1,
		/// <summary>
		/// Kernel messages (0)
		/// </summary>
		Kernel = 0,
		/// <summary>
		/// User-level messages (1)
		/// </summary>
		UserLevel = 1,
		/// <summary>
		/// Mail system (2)
		/// </summary>
		MailSystem = 2,
		/// <summary>
		/// System daemons (3)
		/// </summary>
		System = 3,
		/// <summary>
		/// Security/authorization messages (4)
		/// </summary>
		SecureAuth = 4,
		/// <summary>
		/// Messages generated internally by syslogd (5)
		/// </summary>
		SyslogMessage = 5,
		/// <summary>
		/// Line printer subsystem (6)
		/// </summary>
		LinePrinter = 6,
		/// <summary>
		/// Network news subsystem (7)
		/// </summary>
		NetworkNews = 7,
		/// <summary>
		/// UUCP [Unix to Unix Copy] subsystem (8)
		/// </summary>
		UUCP = 8,
		/// <summary>
		/// Clock daemon (9)
		/// </summary>
		Clock = 9,
		/// <summary>
		/// Security/authorization messages (10)
		/// </summary>
		SecureAuthAlt = 10,
		/// <summary>
		/// FTP daemon (11)
		/// </summary>
		FTP = 11,
		/// <summary>
		/// NTP subsystem (12)
		/// </summary>
		NTP = 12,
		/// <summary>
		/// Audit Log Messages (13)
		/// </summary>
		LogAudit = 13,
		/// <summary>
		/// Alert Log Messages (14)
		/// </summary>
		LogAlert = 14,
		/// <summary>
		/// Clock daemon sync alternate (15)
		/// </summary>
		ClockAlt = 15,
		/// <summary>
		/// Local Use (16)
		/// </summary>
		Local0 = 16,
		/// <summary>
		/// Local Use (17)
		/// </summary>
		Local1 = 17,
		/// <summary>
		/// Local Use (18)
		/// </summary>
		Local2 = 18,
		/// <summary>
		/// Local Use (19)
		/// </summary>
		Local3 = 19,
		/// <summary>
		/// Local Use (20)
		/// </summary>
		Local4 = 20,
		/// <summary>
		/// Local Use (21)
		/// </summary>
		Local5 = 21,
		/// <summary>
		/// Local Use (22)
		/// </summary>
		Local6 = 22,
		/// <summary>
		/// Local Use (23)
		/// </summary>
		Local7 = 23,
	}
	/// <summary>
	/// Indiciates the suggested priority of the message
	/// </summary>
	public enum Priority
	{
		Unknown = -1,
		/// <summary>
		/// Emergency: system is unusable
		/// </summary>
		Emergency = 0,
		/// <summary>
		/// Alert: action must be taken immediately
		/// </summary>
		Alert = 1,
		/// <summary>
		/// Critical condition
		/// </summary>
		Critical = 2,
		/// <summary>
		/// Error condition
		/// </summary>
		Error = 3,
		/// <summary>
		/// Warning condition
		/// </summary>
		Warning = 4,
		/// <summary>
		/// Notice: normal but significant condition
		/// </summary>
		Notice = 5,
		/// <summary>
		/// Informational: informational messages
		/// </summary>
		Information = 6,
		/// <summary>
		/// Debug-level messages
		/// </summary>
		Debug = 7
	}
}
