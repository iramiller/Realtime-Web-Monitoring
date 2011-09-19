function CreateSessionGraph(hostName, resultCount, incCount, titleId, chartId, legendId) {
    var _Graph = {
        sChart: {},
        firstTime: 1,
        dataSet: { 'label': [], 'values': [] },
        hostNames: {},
        updates: {},

        loadRealtimeResults: function () {
            $.ajax({
                url: "./Realtime",
                dataType: "json",
                data: {
                    src: "SessionMonitor",
                    limit: (this.firstTime ? 30 : 1)
                },
                success: function (data) {
                    $.each(data.reverse(), function (index, dta) {
                        $.each(dta.Results, function (index, rs) {
                            if (!hostName || rs.SessionMonitor.HostName == hostName) {
                                if (!_Graph.hostNames[rs.SessionMonitor.HostName] && rs.SessionMonitor.SessionCount > 50) {
                                    _Graph.hostNames[rs.SessionMonitor.HostName] = 1;
                                    _Graph.dataSet.label.push(rs.SessionMonitor.HostName);
                                    $.each(_Graph.dataSet.values, function (index, dval) {
                                        dval.values.push(0);
                                    });
                                }
                                _Graph.updates[rs.SessionMonitor.HostName] = rs.SessionMonitor.SessionCount;
                            }
                        });
                        var measures = [];
                        $.each(_Graph.dataSet.label, function (index, dl) {
                            measures.push((_Graph.updates[dl] ? _Graph.updates[dl] : 0));
                        });
                        _Graph.dataSet.values.push({ 'label': 'time', 'values': measures.slice(0, measures.length) });
                    });
                    if (_Graph.dataSet.values.length > 30)
                        _Graph.dataSet.values.splice(0, 1);
                    $.each(_Graph.dataSet.values, function (index, dsv) {
                        dsv.label = "-" + ((29 - index) * 10) + "s";
                    });
                    if (_Graph.firstTime) {
                        _Graph.sChart.loadJSON(_Graph.dataSet);
                        _Graph.firstTime = 0;
                    } else
                        _Graph.sChart.updateJSON(_Graph.dataSet);
                },
                error: function (xhr, status, err) {
                    window.alert(status + ' ' + err);
                }
            });
        },

        createSessionChart: function () {
            this.sChart = new $jit.AreaChart({
                injectInto: 'sessionChart',
                animate: true,
                Margin: {
                    top: 5,
                    left: 5,
                    right: 5,
                    bottom: 5
                },
                labelOffset: 10,
                showAggregates: false,
                type: 'stacked',
                showLabels: true,
                Label: {
                    size: 8,
                    family: 'Arial',
                    color: 'white'
                },
                //tooltip options
                Tips: {
                    enable: true,
                    onShow: function (tip, elem) {
                        tip.innerHTML = "<b>" + elem.name + "</b>: " + elem.value + " sessions";
                    }
                },
                filterOnClick: true,
                restoreOnRightClick: true
            });
        },

        legendUpdate: function () {
            if (this.dataSet.values.length > 1) {
                var legend = this.sChart.getLegend(), listItems = [], ts = 0;
                for (var name in this.updates) {
                    var bcolor = ((legend[name]) ? legend[name] : "none");
                    ts += this.updates[name];
                    listItems.push('<div class=\'query-color\' style=\'background-color:' + bcolor + '\'>&nbsp;</div>' + name + " (" + this.updates[name] + ")");
                }
                listItems.sort(function (a, b) {
                    var aval = a.substring(a.lastIndexOf("(") + 1, a.lastIndexOf(")"));
                    var bval = b.substring(b.lastIndexOf("(") + 1, b.lastIndexOf(")"));
                    return bval - aval;
                });
                $(titleId)[0].innerHTML = ts + ' Sessions';
                $(legendId)[0].innerHTML = '<li>' + listItems.join('</li><li>') + '</li>';
            }
        },

        dataRefresh: function () {
            this.loadRealtimeResults();
            setTimeout('sessionGraph.legendUpdate()', 1000); 
            setTimeout('sessionGraph.dataRefresh()', 10000);
        }
    };

    return _Graph;
}