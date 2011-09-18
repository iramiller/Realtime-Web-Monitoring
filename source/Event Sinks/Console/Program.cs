using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Sockets;
using System.Net;
using System.Globalization;
using System.Threading.Tasks;

using WebMonitoringSink;
using WebMonitoringSink.Messages;

namespace WebMonitoringConsole
{
	/// <summary>
	/// This program represents one of the simpliest examples of reading event
	/// messages possible.  A reference to the Windows Event Service DLL is
	/// required.
	/// </summary>
	class Program
	{
		static void Main(string[] args)
		{
			int port = Int32.Parse(ConfigurationManager.AppSettings["port"]);
			int mtu = Int32.Parse(ConfigurationManager.AppSettings["mtu"]);
			
			SyslogListener syslogServer = new SyslogListener(IPAddress.Any, port, mtu);

			Console.WriteLine("Starting Event Sink...");
			syslogServer.Start();
			Task.Factory.StartNew(() => PrintMessage(syslogServer));
			Console.WriteLine("Ready.");
			Console.ReadLine();
			syslogServer.Stop();
		}

		private static void PrintMessage(SyslogListener syslogServer)
		{
			IMessage message;
			while (syslogServer.TryPickupMessage(System.Threading.Timeout.Infinite, out message)) {
				Console.WriteLine("{0}, {1}, {2}, {3}", message.Facility, message.Priority, message.IsForwarded, message.Message);
				Console.WriteLine("------------------------------------------------------------------");
			}
		}
	}
}
