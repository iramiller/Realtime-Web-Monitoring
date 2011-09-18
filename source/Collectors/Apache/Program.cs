using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Sockets;
using System.Configuration;
using log4net;

namespace ConsoleSyslogPublisher
{
	/// <summary>
	/// This console program is designed to be a target for Apache piped logs on 
	/// windows.  The output of this console app will match the format of data
	/// sent from the IIS7 Event source (IISNSL) so that data from Apache web
	/// servers can seamlessly be combined with that from Windows Servers.
	/// </summary>
	public class Program
	{
		private static long _globalId = DateTime.Now.Ticks;
		private static readonly ILog Log = LogManager.GetLogger(typeof(Program));
		/*
		 * There are multiple potential parts to a message.  Each one will start with the following
		 * [Marker, Id, Status.Substatus, TTFB, Page#, Total#]
		 * 
		 * The message body (which can span multiple messages) will contain the following
		 * [HttpVerb, RequestURI, MIME, ClientIP, AppId, Referrer, UserAgent]
		 * 
		 * APACHE:www.website.com % 200.0 156 MPS POST /path/to/file.ext text/html 10.0.0.1 / "http://www.website.com/default.html" "Mozilla/4.0 (compatible; MSIE 7.0; Windows NT 5.1; Trident/4.0; .NET4.0C;)"
		 * "APACHE:%v %% %>s.0 %D MPS %m \"%U%q\" %{Content-Type}o %h / \"%{Referer}i\" \"%{User-agent}i\""
		 */
		static void Main(string[] args)
		{
			try
			{
				string server = ConfigurationManager.AppSettings["server"];
				int port = Int32.Parse(ConfigurationManager.AppSettings["port"]);
				int mtu = Int32.Parse(ConfigurationManager.AppSettings["mtu"]);
				do
				{
					var message = Console.ReadLine();
					if (string.IsNullOrWhiteSpace(message) ||
						!message.StartsWith("APACHE:", StringComparison.Ordinal) ||
						!message.Contains("MPS"))
						throw new FormatException(String.Format("Unexpected Message Format '{0}'", message));
					var header = FormatSyslogHeader(13, 6, message.Substring(7, message.IndexOf("MPS") - 7));
					header = header.Replace("%", GenerateMessageHeader(ref _globalId));
					var body = StripSpaces(message.Substring(message.IndexOf("MPS") + 3));
					SendMessage(server, port, 1500, header, body);
				} while (Console.In.Peek() != '\0');
			}
			catch (Exception ex)
			{
				Log.Error(ex.Message, ex);
			}
		}

		/// <summary>
		/// Changes terms from "quoted escaped space" to nonquoted+with+out+spaces except for 
		/// the first term which is converted using %20 url encoding syntax.
		/// </summary>
		/// <param name="s">the message to convert</param>
		/// <returns>the body with quoted identifiers changed to unquoted and spaces changed to plus signs</returns>
		private static string StripSpaces(string s)
		{
			bool inEscape = false;
			int token = 0; // the first token we escape is converted into '%20' instead of '+'
			StringBuilder result = new StringBuilder(s.Length);
			foreach (var c in s)
			{
				if (c == '"')
				{
					inEscape = !inEscape;
					token++;
				}
				else
				{
					if (token == 1) // the first term is a url and should not be escaped with plus signs
						result.Append(c == ' ' && inEscape ? "%20" : c.ToString());
					else
						result.Append(c == ' ' && inEscape ? '+' : c);
				}
			}
			return result.ToString();
		}

		/// <summary>
		/// Sends the message using the given header and body with a calculation of the number of 
		/// parts required to meet the given mtu.
		/// </summary>
		/// <param name="header">the syslog header</param>
		/// <param name="body">the message body</param>
		/// <param name="mtu">the mtu we are trying to keep the message under</param>
		private static void SendMessage(string server, int port, int mtu, string header, string body)
		{
			if (mtu < header.Length + 3)
				throw new ArgumentException("Can not send a message when the mtu is smaller than the header size");
			int tMessages = (header.Length + body.Length + 3) / mtu;
			for (int i = 0; i <= tMessages; i++)
			{
				SendSyslogMessage(server, port, String.Format("{0}{1} {2}{3}", header, i+1, tMessages+1, RemainderSubstring(body,i*mtu,mtu)));
			}
		}

		/// <summary>
		/// Returns the substring for the specified offset and upto the given length as available
		/// </summary>
		/// <param name="s">the string to substring</param>
		/// <param name="offset">an offset</param>
		/// <param name="length">the number of chars to return as a maximum</param>
		/// <returns>a substring</returns>
		private static string RemainderSubstring(string s, int offset, int length)
		{
			if (s.Length - offset > length)
				return s.Substring(offset, length);
			else
				return s.Substring(offset);
		}
		/// <summary>
		/// Using a reference to a messageId which is an integer that can be incremented by one for
		/// each message sent a header is returned.
		/// </summary>
		/// <param name="messageId">The global messageId which will be incremented then used</param>
		/// <returns>the message header string.</returns>
		private static string GenerateMessageHeader(ref long messageId)
		{
			messageId++;
			messageId = messageId % 100000000;
			return String.Format("IISNSL: {0}", messageId);
		}

		/// <summary>
		/// Returns a SYSLOG header for the current time.
		/// </summary>
		/// <param name="facility"></param>
		/// <param name="priority"></param>
		/// <param name="hostStatus">the rest of the message header including the hostname, httpstatus and time taken</param>
		/// <returns>&lt;110&rt; Feb 04 12:00:00.000 mine.mt.gov % 200.0 156 MPS</returns>
		private static string FormatSyslogHeader(int facility, int priority, string hostStatus)
		{
			return String.Format("<{0}>{1} {2}", (facility << 3) + priority, DateTime.Now.ToString("MMM dd HH:mm:ss"), hostStatus);
		}

		/// <summary>
		/// Sends the message to the given server and port using a UDPClient
		/// </summary>
		/// <param name="server">the server ip address</param>
		/// <param name="port">the remote port (usually 514)</param>
		/// <param name="p">the message to send</param>
		private static void SendSyslogMessage(string server, int port, string p)
		{
			if (!string.IsNullOrWhiteSpace(p))
			{
				using (UdpClient c = new UdpClient(server, port))
				{
					c.Send(UTF8Encoding.ASCII.GetBytes(p), p.Length);
				}
			}
		}
	}
}
