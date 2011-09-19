using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace RealtimeWeb
{
	/// <summary>
	/// A single place to define the cache interval of content on the website.
	/// </summary>
	public class ExpiresControllerFilter : System.Web.Mvc.ActionFilterAttribute
	{
		public override void OnActionExecuted(System.Web.Mvc.ActionExecutedContext filterContext)
		{
			base.OnActionExecuted(filterContext);
			if (filterContext.HttpContext.Request.Url.AbsoluteUri.Contains("Realtime"))
				filterContext.HttpContext.Response.Cache.SetMaxAge(TimeSpan.FromSeconds(10)); // our minimum update interval for realtime is 10 sec.
			else if (filterContext.HttpContext.Response.ContentType.Equals("image/gif")) // small images like country flags for ip addresses
				filterContext.HttpContext.Response.Cache.SetMaxAge(TimeSpan.FromHours(1));
			else
				filterContext.HttpContext.Response.Cache.SetMaxAge(TimeSpan.FromMinutes(1));
			filterContext.HttpContext.Response.Cache.SetLastModified(DateTime.Now);
			filterContext.HttpContext.Response.Cache.SetOmitVaryStar(false);
		}
	}

}