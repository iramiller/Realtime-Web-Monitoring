using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using MongoDB.Driver;
using System.Configuration;
using System.Collections;
using System.Text;

namespace RealtimeWeb.Controllers
{
	public class HomeController : Controller
	{
		/// <summary>
		/// Renders a webpage that contains overview graphs of the entire system
		/// </summary>
		/// <returns></returns>
		public ActionResult Index()
		{
			return View();
		}

		/// <summary>
		/// Renders a page that displays information about all of the websites on a specific host
		/// </summary>
		/// <param name="n">hostname of the server</param>
		/// <returns></returns>
		public ActionResult Host(string n)
		{
			if (string.IsNullOrWhiteSpace(n))
				return new HttpNotFoundResult();

			ViewBag.HostName = n;
			return View();
		}

		/// <summary>
		/// Renders a page that displays information about a specific web site
		/// </summary>
		/// <param name="n">hostname of the website</param>
		/// <returns></returns>
		public ActionResult Site(string n)
		{
			if (string.IsNullOrWhiteSpace(n))
				return new HttpNotFoundResult();

			ViewBag.HostName = n;
			return View();
		}

		/// <summary>
		/// Renders a page that displays information about a specific client ip address
		/// </summary>
		/// <returns></returns>
		public ActionResult Client(string ip)
		{
			if (string.IsNullOrWhiteSpace(ip))
				return new HttpNotFoundResult();

			MongoServer server = MongoServer.Create(ConfigurationManager.ConnectionStrings["mongodb"].ConnectionString);
			var db = server.GetDatabase("events");
			var iisEvents = db.GetCollection("iis");
			var results = iisEvents.Distinct("UserAgent", new QueryDocument(QueryDocument.Parse("{ClientAddress:'" + ip+ "'}")));
			ViewBag.UserAgents = results.Select(b => GetBrowserCap(b.ToString().Replace('+',' '))).ToArray();

			var dbPath = Server.MapPath(ConfigurationManager.ConnectionStrings["geoip"].ConnectionString);
			if (System.IO.File.Exists(dbPath)) {
				var ls = new MaxMind.GeoIP.LookupService(dbPath, MaxMind.GeoIP.LookupService.GEOIP_STANDARD);
				if (!ip.StartsWith("10."))
					ViewBag.Location = ls.getLocation(System.Net.IPAddress.Parse(ip));
				else
					ViewBag.Location = new MaxMind.GeoIP.Location() { countryName="US", countryCode="US", city="LAN", regionName="Internal Address" };
			}
			ViewBag.ClientAddress = ip;
			return View();
		}

		/// <summary>
		/// Given a user agent string a lookup is performed which returns a browser cap object 
		/// for the useragent.
		/// </summary>
		/// <param name="userAgent"></param>
		/// <returns></returns>
		private HttpBrowserCapabilities GetBrowserCap(string userAgent)
		{
			if (string.IsNullOrWhiteSpace(userAgent))
				return null;
			var browser = new HttpBrowserCapabilities
			{
				Capabilities = new Hashtable { { string.Empty, userAgent } }
			};
			var factory = new System.Web.Configuration.BrowserCapabilitiesFactory();
			factory.ConfigureBrowserCapabilities(new System.Collections.Specialized.NameValueCollection(), browser);
			return browser;
		}

		/// <summary>
		/// Returns a page that displays information about a specific page on a given site
		/// </summary>
		/// <param name="n">hostname of the site</param>
		/// <param name="r">requested page</param>
		/// <returns></returns>
		public ActionResult Page(string n, string r, int h=72)
		{
			if (string.IsNullOrWhiteSpace(n) || string.IsNullOrWhiteSpace(r))
				return new HttpNotFoundResult();
			if (h < 0 || h > 168)
				h = 72;
			ViewBag.Hours = h;
			ViewBag.HostName = n;
			ViewBag.RequestPage = r;
			return View();
		}

	}
}
