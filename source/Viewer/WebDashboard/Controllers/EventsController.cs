using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using MongoDB.Driver;
using System.Configuration;
using System.Text;
using MongoDB.Driver.Builders;

namespace RealtimeWeb.Controllers
{
	public class EventsController : Controller
	{
		public ActionResult Index()
		{
			return View();
		}

		public ActionResult IIS(string filter = null, int limit = 20)
		{
			var js = new JavaScriptResult();
			js.Script = QueryTable("iis", filter, limit);
			return js;
		}
		public ActionResult L4N(string filter = null, int limit = 20)
		{
			var js = new JavaScriptResult();
			js.Script = QueryTable("l4n", filter, limit);
			return js;
		}

		string QueryTable(string tableName, string filter, int limit)
		{
			if (filter != null && filter.StartsWith("%"))
				filter = Server.UrlDecode(filter); // catch encoding errors.
			MongoServer server = MongoServer.Create(ConfigurationManager.ConnectionStrings["mongodb"].ConnectionString);
			var db = server.GetDatabase("events");
			var iisEvents = db.GetCollection(tableName);
			var cursor = string.IsNullOrWhiteSpace(filter) ? iisEvents.FindAll().SetSortOrder(SortBy.Descending("$natural")).SetLimit(limit) : iisEvents.Find(new QueryDocument(QueryDocument.Parse(filter))).SetSortOrder(SortBy.Descending("$natural")).SetLimit(limit);
			StringBuilder sb = new StringBuilder();
			sb.Append("[");
			foreach (var bd in cursor)
			{
				// These extra wrappers are in BSON but they kill our JSON parsing so we will remove them.
				var elementStr = bd.ToString().Replace("ObjectId(", "").Replace("),", ",").Replace("ISODate(", "");
				sb.AppendFormat("{0},", elementStr);
			}
			if (sb.Length > 1)
				sb.Remove(sb.Length - 1, 1); // strip the last comma.
			sb.Append("]");
			return sb.ToString();
		}
	}
}
