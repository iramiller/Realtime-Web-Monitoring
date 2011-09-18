using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading;

namespace WebMonitoringSink
{
	static class Program
	{
		/// <summary>
		/// The main entry point for the application.
		/// </summary>
		static void Main()
		{
			// When running in VS in debug mode we start this way for simple debugging
			if (System.Diagnostics.Debugger.IsAttached)
			{
				var wes = new WebEventSinkService();
				wes.ConsoleStart();
				Thread.Sleep(Timeout.Infinite);
			}
			else
			{
				ServiceBase[] ServicesToRun = new ServiceBase[] { new WebEventSinkService() };
				ServiceBase.Run(ServicesToRun);
			}
		}
	}
}
