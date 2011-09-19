using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Configuration;
using MongoDB.Driver;
using System.Text;
using MongoDB.Driver.Builders;

namespace RealtimeWeb.Controllers
{
	public class RealtimeController : Controller
	{
		//
		// GET: /Realtime/

		public ActionResult Index(string src=null, string filter=null, int limit=1)
		{
			if (string.IsNullOrWhiteSpace(src))
				return View(Sources());
			var js = new JavaScriptResult();
			var result = QueryTable(src, filter, limit);
			js.Script = result;
			return string.IsNullOrWhiteSpace(result) ? new HttpNotFoundResult("The requested realtime source does not exist") : (ActionResult)js;
		}

       


		/// <summary>
		/// An array of the table names in the realtime database which can be used as sources.
		/// </summary>
		/// <returns></returns>
		string[] Sources()
		{
			MongoServer server = MongoServer.Create(ConfigurationManager.ConnectionStrings["mongodb"].ConnectionString);
			var db = server.GetDatabase("realtime");
			return db.GetCollectionNames().Where(s => !s.StartsWith("system")).ToArray();
		}

		/// <summary>
		/// Performs a query on the given table using the filter (if any) and limit.  Results
		/// are returned in newest first order.
		/// </summary>
		/// <param name="tableName"></param>
		/// <param name="filter"></param>
		/// <param name="limit"></param>
		/// <returns>The requested results as JSON or null if the table does not exist or is a protected system table.</returns>
		string QueryTable(string tableName, string filter, int limit)
		{
			MongoServer server = MongoServer.Create(ConfigurationManager.ConnectionStrings["mongodb"].ConnectionString);
			var db = server.GetDatabase("realtime");
			if (!db.CollectionExists(tableName) || tableName.StartsWith("system"))
				return null;
			var iisEvents = db.GetCollection(tableName);
			var cursor = string.IsNullOrWhiteSpace(filter) ? iisEvents.FindAll().SetSortOrder(SortBy.Descending("$natural")) : iisEvents.Find(new QueryDocument(QueryDocument.Parse(filter))).SetSortOrder(SortBy.Descending("$natural"));
			if (limit > 0)
				cursor.SetLimit(limit);
			StringBuilder sb = new StringBuilder();
			sb.Append("[");
			foreach (var bd in cursor)
			{
				// These extra wrappers are supported by the console javascript but kill our browser js
				var elementStr = bd.ToString().Replace("ObjectId(", "").Replace("\")", "\"").Replace("ISODate(", "");
				sb.AppendFormat("{0},", elementStr);
			}
			if (sb.Length > 1)
				sb.Remove(sb.Length - 1, 1); // strip the last comma.
			sb.Append("]");
			return sb.ToString();
		}
	}
}
