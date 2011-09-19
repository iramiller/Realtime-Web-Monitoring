using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Configuration;
using MongoDB.Driver;
using MongoDB.Bson;
using MongoDB.Driver.Builders;
using System.Text;
using System.Text.RegularExpressions;
using System.Globalization;

namespace RealtimeWeb.Controllers
{
	public class AnalysisController : Controller
	{
		public ActionResult Index()
		{
			return View();
		}

		/// <summary>
		/// Returns GEOIP data for a given IP address.
		/// </summary>
		/// <remarks>While technically this is not data in the analysys portion of the database it is related</remarks>
		/// <param name="ip">a string that is parsable into an ip address</param>
		/// <returns>any location information that can be found for the ip address or null if no entry was found.</returns>
		public ActionResult Location(string ip)
		{
			var result = new JavaScriptResult();
			var dbPath = Server.MapPath(ConfigurationManager.ConnectionStrings["geoip"].ConnectionString);
			if (System.IO.File.Exists(dbPath))
			{
				var ls = new MaxMind.GeoIP.LookupService(dbPath, MaxMind.GeoIP.LookupService.GEOIP_STANDARD);
				MaxMind.GeoIP.Location loc;
				if (!ip.StartsWith("10."))
					loc = ls.getLocation(System.Net.IPAddress.Parse(ip));
				else
					loc = new MaxMind.GeoIP.Location() { countryName = "US", countryCode = "US", city = "LAN", regionName = "Internal Address" };
				if (loc != null)
					result.Script = "{" + string.Format("areaCode:{0}, city:'{1}', countryCode:'{2}', countryName:'{3}', latitude:{4}, longitude:{5}, metroCode:{6}, postalCode:'{7}', region:'{8}', regionName:'{9}'",
						loc.area_code, loc.city, loc.countryCode, loc.countryName, loc.latitude, loc.longitude, loc.metro_code, loc.postalCode, loc.region, loc.regionName) + "}";
				else
					result.Script = "{}";
			}
			else
				result.Script = "{}";
			return result;
		}

		/// <summary>
		/// Given an ip string this method will return the correct image directly.  This allows specifying
		/// this endpoint as the src attribute of an image for lists.
		/// </summary>
		/// <param name="ip">a string that is parsable into an ip address</param>
		/// <returns>a flag image as a gif.</returns>
		public ActionResult LocationFlag(string ip)
		{
			var fName = "us.gif";
			var dbPath = Server.MapPath(ConfigurationManager.ConnectionStrings["geoip"].ConnectionString);
			if (System.IO.File.Exists(dbPath))
			{
				var ls = new MaxMind.GeoIP.LookupService(dbPath, MaxMind.GeoIP.LookupService.GEOIP_STANDARD);
				MaxMind.GeoIP.Location loc;
				if (!ip.StartsWith("10."))
					loc = ls.getLocation(System.Net.IPAddress.Parse(ip));
				else
					loc = new MaxMind.GeoIP.Location() { countryName = "US", countryCode = "US", city = "LAN", regionName = "Internal Address" };
				if (loc != null)
					fName = String.Format("{0}.gif", loc.countryCode);
			}
			return new FilePathResult(Server.MapPath(String.Format("~/Contents/countryflags/{0}", fName)), "image/gif");
		}

		/// <summary>
		/// Returns the data on record for a given URL at a specific time
		/// </summary>
		/// <param name="d">An integer in the format of 2011013023 (YYYY MM DD HH)</param>
		/// <param name="p">the hostname and path to a page (no query string).</param>
		/// <param name="h">number of hours of data to include</param>
		/// <returns>The page analysis record for the given page.</returns>
		public ActionResult PageViews(int? d, string p, int h=1)
		{
			// our database size contrains the number of samples we can hold.
			if (h < 1 || h > 480)
				throw new ArgumentOutOfRangeException("h");
			// if no value is passed in then we want to use a sample from 10 minutes ago
			// (solves an issue where there may be no data for the current hour in each 
			// minute upto 10 minutes past the hour (NOTE: based on 10 min M/R schedule)
			DateTime dDate;
			if (!d.HasValue || !DateTime.TryParseExact(d.ToString(), "yyyyMMddHH", CultureInfo.CurrentCulture, DateTimeStyles.AssumeUniversal, out dDate))
				dDate = DateTime.UtcNow.AddMinutes(-10);

			var js = new JavaScriptResult();
			MongoServer server = MongoServer.Create(ConfigurationManager.ConnectionStrings["mongodb"].ConnectionString);
			var pages = server.GetDatabase("analysis").GetCollection("pages");

			StringBuilder sb = new StringBuilder();
			sb.Append("[");
			for (int i = 0; i < h; i++)
			{
				var pageData = pages.FindOne(Query.EQ("_id", String.Format("{0}:{1}", dDate.ToString("yyyyMMddHH"), p)));
				if (pageData != null)
					sb.Append(pageData.ToString().Replace("ObjectId(", "").Replace("),", ",").Replace("ISODate(", ""));
				else
				{
					sb.Append("{ ");
					sb.AppendFormat("\"_id\" : \"{0}:{1}", dDate.ToString("yyyy-MM-ddTHH"), p);
					sb.Append("\", \"value\" : { ");
					sb.AppendFormat("\"dateLow\" : \"{0}:00:00Z\", \"dateHigh\" : \"{0}:59:59Z\", \"host\" : \"{1}\", ", dDate.ToString("yyyy-MM-ddTHH"), p.Substring(0, (p.IndexOf('/') > 0 ? p.IndexOf('/') : p.Length)));
					sb.Append("\"views\" : 0, \"method\" : { }, \"referer\" : {  }, \"keywords\" : { }, \"mime\" : \"n/a\", \"latency\" : { \"avg\" : 0, \"count\" : 0, \"min\" : 0, \"max\" : 0 } } }");
				}
				if (i < (h - 1))
					sb.Append(","); // separator
				dDate = dDate.AddHours(-1);
			}
			sb.Append("]");
			js.Script = sb.ToString();
			return js;
		}
	}
}
