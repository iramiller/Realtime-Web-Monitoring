using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using WebMonitoringSink.Messages;
using MongoDB.Bson;

namespace WebMonitoringSink.EventMessages
{
	/// <summary>
	/// This class represents a Log4Net message delivered via the UdpAppender.
	/// </summary>
	/// <remarks>The following pattern should be used for the message 
	/// <c>"&lt;110&gt;%date{MMM dd HH:mm:ss} %property{log4net:HostName} L4N: %p YourApplicationName %c - %m"</c>
	/// Note the Marker "L4N:" followed by the Log4net priority "%p" then the application
	/// name, log4net logging identifier "%c" and finally the message "%m"
	/// </remarks>
	public class ErrorLoggingEvent : IMessage, IConvertibleToBsonDocument
	{
		private IMessage _baseMessage;
		private Priority _loggedPriority = Priority.Unknown;
		private string _messageBody;

		public ErrorLoggingEvent(IMessage message)
		{
			if (message == null || !message.Message.StartsWith("L4N:"))
				throw new ArgumentException("Invalid Message","message");
			_baseMessage = message;
			ParseMessage(_baseMessage.Message);
		}

		/// <summary>
		/// Parses the given message into the properties of an L4N message.
		/// </summary>
		/// <param name="p">the message data.</param>
		private void ParseMessage(string p)
		{
			int splitIndex = p.IndexOf('-');
			_messageBody = p.Substring(splitIndex + 2);

			string[] parts = p.Substring(0, splitIndex-1).Split(' ');
			if (parts.Length == 4) // the full header should be here...
			{
				if (!parts[0].Equals("L4N:", StringComparison.Ordinal))
					throw new InvalidOperationException("An invalid message format was detected.");
				_loggedPriority = ParsePriority(parts[1]);
				SourceApplication = parts[2];
				LogSource = parts[3];
			} else
				throw new InvalidOperationException("An invalid message format was detected - The ErrorLoggingEvent message header provided contained an invalid number of items.");
		}

		/// <summary>
		/// Converts a Log4Net message priority into a priority value that matches the ones used by Syslog.
		/// </summary>
		/// <param name="p"></param>
		/// <returns></returns>
		private Messages.Priority ParsePriority(string p)
		{
			switch (p)
			{
				case "DEBUG": return Messages.Priority.Debug;
				case "INFO": return Messages.Priority.Information;
				case "WARN": return Messages.Priority.Warning;
				case "ERROR": return Messages.Priority.Error;
				case "FATAL": return Messages.Priority.Emergency;
				default : return Messages.Priority.Unknown;
			}
		}
		/// <summary>
		/// The name of the application which generated this event
		/// </summary>
		public string SourceApplication { get; private set; }
		/// <summary>
		/// The Source of the Logged Event.
		/// </summary>
		public string LogSource { get; private set; }

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
			get { return _messageBody; }
		}

		public Priority Priority
		{
			get { return _loggedPriority; }
		}

		public DateTime ProcessedTime
		{
			get { return _baseMessage.ProcessedTime; }
		}

		public string RawMessage
		{
			get { return _baseMessage.RawMessage; }
		}

		#endregion

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
				{ "SourceHost", this.SourceHost},
				{ "SourceApplication", this.SourceApplication},
				{ "LogSource", this.LogSource}
			};
		}

		#endregion
	}
}
