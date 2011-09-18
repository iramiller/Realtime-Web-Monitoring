
/*

Page Traffic Map Reduce
-------------------------------------------------------------------------------
This file contains the javascript functions which reduce the web server request data into 
page view summaries.
-------------------------------------------------------------------------------
Example:
	> db.iis.mapReduce('IISRequestMapHourly', 'IISRequestReduceHourly', { query: { EventDate: { $lte: new Date(2011,7,30) }  }, out:{ reduce:'pages', db:'analysis'} });
-------------------------------------------------------------------------------
Note: The map function is built to emit keys for each page on a given website with a one hour
frequency. If a smaller unit is required then the eventTime calculation should be changed to
provide the desired precision.

*/

db.system.js.save({ _id: "IISRequestMapHourly", value: function () {
		var p = this.RequestPage;
		// re-alias the sharpcontent pages which are seen as a file parameter on the request
		if (p == '/content/processRequest.ashx' && (this.RequestParams && this.RequestParams.file))
			p = this.RequestParams.file;

		var ltc = (this.ProcessingTime ? this.ProcessingTime : 0);

		var result = {
			dateLow: this.EventDate,
			dateHigh: this.EventDate,
			host: this.HostName,
			views: 1,
			method: {},
			referer: {},
			keywords: {},
			mime: this.ContentType,
			latency: { avg: ltc, count: 1, min: ltc, max: ltc }
		};

		result.method[this.HttpVerb + '-' + this.StatusCode] = 1;

		var refr = this.Referer + '';
		refr = (refr.indexOf('?') > 0 ? this.Referer.substr(0, this.Referer.indexOf('?')) : refr);
		refr = (refr.indexOf(';') > 0 ? this.Referer.substr(0, this.Referer.indexOf(';')) : refr);
		result.referer[refr] = 1;

		var kw = ExtractKeywords(this.Referer);
		if (kw) result.keywords[kw] = 1;

		// Create a number with YYYYMMDDHH which will allow easy querying into date heirarchy of month, day and hour
		var eventTime = (this.EventDate.getUTCFullYear() * 1000000) + ((this.EventDate.getUTCMonth() + 1) * 10000) +
			(this.EventDate.getUTCDate() * 100) + this.EventDate.getUTCHours();
		emit(eventTime + ':' + this.HostName + p, result);
	}
});

db.system.js.save({ _id: "IISRequestReduceHourly", value: function (key, values) {
	var result = values[0];
	for (var i = 1; i < values.length; i++) {
		result.dateLow = (result.dateLow < values[i].dateLow ? result.dateLow : values[i].dateLow);
		result.dateHigh = (result.dateHigh > values[i].dateHigh ? result.dateHigh : values[i].dateHigh);

		for (var m in values[i].method) {
			result.method[m] = values[i].method[m] + (result.method[m] ? result.method[m] : 0);
		}
		for (var r in values[i].referer) {
			result.referer[r] = values[i].referer[r] + (result.referer[r] ? result.referer[r] : 0);
		}
		for (var k in values[i].keywords) {
			result.keywords[k] = values[i].keywords[k] + (result.keywords[k] ? result.keywords[k] : 0);
		}
		result.views += values[i].views;
		result.latency.avg = ((result.latency.avg * result.latency.count) + (values[i].latency.avg * values[i].latency.count)) / (result.latency.count + values[i].latency.count);
		result.latency.count += values[i].latency.count;
		result.latency.min = result.latency.min < values[i].latency.min ? result.latency.min : values[i].latency.min;
		result.latency.max = result.latency.max > values[i].latency.max ? result.latency.max : values[i].latency.max;
	}
	return result;
}
});

db.system.js.save({ _id: "ExtractKeywords", value:

function ExtractKeywords(refUrl) {
	// important for speed that the most popular entries are first.
	var seArray = {
		'Google': { 'url': 'google.com', 'query': 'q=', 'query2': 'query=' },
		'Yahoo': { 'url': 'yahoo.com', 'query': 'p=' },
		'Bing': { 'url': 'bing.com', 'query': 'q=' },
		'Google Images': { 'url': 'images.google.com', 'query': 'q=' },
		'Google Groups': { 'url': 'groups.google.com', 'query': 'q=' },
		'Google Directory': { 'url': 'directory.google.com', 'query': 'q=' },
		'MSN Search': { 'url': 'search.msn.com', 'query': 'q=' },
		'MSN': { 'url': 'search.msn.com', 'query': 'q=' },
		'Yahoo/Google': { 'url': 'google.yahoo.com', 'query': 'p=' },
		'Google WAP': { 'url': 'wap.google.com', 'query': 'q=', 'query2': 'query=' },
		'Google/Gotonet': { 'url': 'gotonet.google.com', 'query': 'q=' },
		'Google (Canada)': { 'url': 'google.ca', 'query': 'q=' },
		'Search.com': { 'url': 'search.com', 'query': 'q=' },
		'Yahoo/Google Canada': { 'url': 'ca.google.yahoo.com', 'query': 'p=' },
		'Yahoo Search Canada': { 'url': 'ca.search.yahoo.com', 'query': 'p=' },
		'MSN Canada (French)': { 'url': 'fr.ca.search.msn.com', 'query': 'q=' },
		'About': { 'url': 'about.com', 'query': 'terms=' },
		'AllTheWeb': { 'url': 'alltheweb.com', 'query': 'query=' },
		'Altavista Canada (English)': { 'url': 'ca-en.altavista.com', 'query': 'q=' },
		'Altavista Canada (French)': { 'url': 'ca-fr.altavista.com', 'query': 'q=' },
		'Altavista': { 'url': 'altavista.com', 'query': 'q=' },
		'AOL': { 'url': 'aolsearch.aol.com', 'query': 'query=' },
		'AOL Canada': { 'url': 'aolsearch.aol.ca', 'query': 'query=' },
		'Ask Jeeves': { 'url': 'askjeeves.com', 'query': 'ask' },
		'Open Directory Project': { 'url': 'dmoz.org', 'query': 'search=' },
		'Dogpile': { 'url': 'dogpile.com', 'query': 'q=' },
		'Go/Infoseek': { 'url': 'infoseek.go.com', 'query': 'qt=' },
		'HotBot': { 'url': 'click.hotbot.com', 'query': 'query=' },
		'Lycos': { 'url': 'home.lycos.com', 'query': 'query=' },
		'Weather underground': { 'url': 'autobrand.wunderground.com', 'query': 'q,search=' }
	};

	// replace alternate se hosts with their parent values for detection purposes
	var replacements = {
		'216\.239\.[0-9]+\.[0-9]+': 'google.com',
		'go[o]?[o]?gle\.[net|org]': 'google.com',
		'[googil|goolge|wwwgoogle]\.com': 'google.com',
		'aj\.com': 'askjeeves.com',
		'askgeeves.com': 'askjeeves.com',
		'askjeevs.com': 'askjeeves.com',
		'ask.com': 'askjeeves.com',
		'av\.com': 'altavista.com',
		'alta-vista.com': 'altavista.com'
	};

	if (refUrl.indexOf('?') > 0) {
		var refHost = refUrl.substr(0, refUrl.indexOf('?'));
		var refQuery = refUrl.substr(refUrl.indexOf('?'));
		var searched = '';

		// merge se variants into their parent form for detection
		for (var exp in replacements) {
			var domainExp = new RegExp("^http:\/\/(www)?" + exp + "\/");
			if (domainExp.test(refHost)) {
				refHost = "http://" + replacements[exp] + "/";
				break; // with a match we are done here
			}
		}

		// we only attempt to pull search queries from our list of known search engines
		for (var seng in seArray) {
			if (refHost.indexOf(seArray[seng]['url']) > 0) {
				// grab the portion of the query string that matches our primary or secondary params
				if (refQuery.indexOf(seArray[seng]['query']) > 0) {
					searched = refQuery.substr(refQuery.indexOf(seArray[seng]['query']));
				} else if (seArray[seng]['query2'] && refQuery.indexOf(seArray[seng]['query2']) > 0) {
					searched = refQuery.substr(refQuery.indexOf(seArray[seng]['query2']));
				}
				// remove any trailing parameters
				if (searched.indexOf('&') > 0)
					searched = searched.substr(0, searched.indexOf('&'));
				// return the value with any spaces restored.
				return searched.substr(searched.indexOf('=') + 1).replace(/\+/g, ' ').replace(/\%20/g, ' ');
			}
		}
	}
	return null;
}
});


