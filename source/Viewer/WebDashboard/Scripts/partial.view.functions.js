// setup details for the jit graphs
var labelType, useGradients, nativeTextSupport, animate;

(function () {
	var ua = navigator.userAgent,
		iStuff = ua.match(/iPhone/i) || ua.match(/iPad/i),
		typeOfCanvas = typeof HTMLCanvasElement,
		nativeCanvasSupport = (typeOfCanvas == 'object' || typeOfCanvas == 'function'),
		textSupport = nativeCanvasSupport
			&& (typeof document.createElement('canvas').getContext('2d').fillText == 'function');
	//I'm setting this based on the fact that ExCanvas provides text support for IE
	//and that as of today iPhone/iPad current text support is lame
	labelType = (!nativeCanvasSupport || (textSupport && !iStuff)) ? 'Native' : 'HTML';
	nativeTextSupport = labelType == 'Native';
	useGradients = nativeCanvasSupport;
	animate = !(iStuff || !nativeCanvasSupport);
})();	

function showTooltip($el) {
	$tip.html($el.attr('title'));
}
function hideTooltip() {
	$tip.hide();
}

function FormatDate(dt) {
	var local = new Date();
	var eDate = Date.parse(dt);
	var elapsed = Math.abs(eDate - Date.now());
	var hours = Math.floor(elapsed / 3600000);
	elapsed -= hours * 3600000;
	var mins = Math.floor(elapsed / 60000);
	hours -= (local.getTimezoneOffset() / 60);
	if (hours > 0)
		return hours + "h " + mins + "m";
	else
		return mins + "m";
}
function FormatDateTime(dt) {
	var local = new Date();
	var eDate = Date.parse(dt);
	var cDate = new Date(eDate); // - (local.getTimezoneOffset() * 60000));
	return ((cDate.getHours() < 10) ? "0" + cDate.getHours() : cDate.getHours()) + ":" +
			((cDate.getMinutes() < 10) ? "0" + cDate.getMinutes() : cDate.getMinutes()) + ":" +
			((cDate.getSeconds() < 10) ? "0" + cDate.getSeconds() : cDate.getSeconds());
}

function FormatDateTime2(dt) {
	var local = new Date();
	var eDate = Date.parse(dt);
	var cDate = new Date(eDate); // - (local.getTimezoneOffset() * 60000));
	return cDate.toDateString().substr(0,11) + ((cDate.getHours() < 10) ? "0" + cDate.getHours() : cDate.getHours()) + ":" +
			((cDate.getMinutes() < 10) ? "0" + cDate.getMinutes() : cDate.getMinutes());
}
