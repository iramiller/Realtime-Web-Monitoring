using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Configuration;
using System.Collections.Specialized;
using MongoDB.Driver;
using MongoDB.Bson;

namespace WebMonitoringSink.Configuration
{
	class CollectionConfiguration
	{
		/// <summary>
		/// This method parses data in the app.config file to pull out mongo db and collection names
		/// these values are created it they do not already exist.
		/// </summary>
		public static void EnsureCollections(string connectionString, string sectionName)
		{
			var cols = ConfigurationManager.GetSection(sectionName) as NameValueCollection;
			MongoServer server = MongoServer.Create(connectionString);
			foreach (var c in cols.AllKeys)
			{
				string[] name = c.Split('.');
				if (name.Length != 2)
					throw new ArgumentException("Collection names must be in the format of DatabaseName.CollectionName");
				var db = server.GetDatabase(name[0]);
				if (!db.CollectionExists(name[1]))
					db.CreateCollection(name[1], new CollectionOptionsDocument(BsonDocument.Parse(cols[c])));
			}
		}
	}
}
