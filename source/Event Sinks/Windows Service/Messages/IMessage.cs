using System;
using MongoDB.Bson;
namespace WebMonitoringSink.Messages
{
	public interface IMessage : IConvertibleToBsonDocument
	{
		/// <summary>
		/// The date and time reported in the syslog message for the event
		/// </summary>
		DateTime EventDate { get; }
		/// <summary>
		/// The facility code associated with this event
		/// </summary>
		Facility Facility { get; }
		/// <summary>
		/// Indicates that this message was sent with an "Original Address=" header that marks a forwarded message.
		/// </summary>
		bool IsForwarded { get; }
		/// <summary>
		/// The address of the server which forwarded this message on
		/// </summary>
		string ForwardedFrom { get; }
		/// <summary>
		/// The hostname of the host which sent this message.
		/// </summary>
		string SourceHost { get; }
		/// <summary>
		/// The hostname given in the event message
		/// </summary>
		string HostName { get; }
		/// <summary>
		/// Indicates if the data passed in for initialization represents a valid syslog message according to RFC3164
		/// </summary>
		bool IsValid { get; }
		/// <summary>
		/// The message body
		/// </summary>
		string Message { get; }
		/// <summary>
		/// The priority assigned to this event
		/// </summary>
		Priority Priority { get; }
		/// <summary>
		/// The time at which this LogMessage was parsed
		/// </summary>
		DateTime ProcessedTime { get; }
		/// <summary>
		/// The raw message data recieved from the source
		/// </summary>
		string RawMessage { get; }
	}
}
