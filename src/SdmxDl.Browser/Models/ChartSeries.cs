using System;
using System.Collections.Generic;
using LanguageExt;
using SdmxDl.Client.Models;

namespace SdmxDl.Browser.Models;

public class ChartSeries
{
    public string Title { get; }
    public IEnumerable<(DateTime Date, double Value)> Values { get; }

    public ChartSeries(Series series)
    {
        var format = series.Meta.Find("TIME_FORMAT", s => s, () => string.Empty);
        Title = series.Meta.Find("TITLE", s => s, () => series.Key);
        Values = series.Obs.Map(o => (DateTime.Parse(o.Period.Split('/')[0]), o.Value)).Strict();
    }
}
