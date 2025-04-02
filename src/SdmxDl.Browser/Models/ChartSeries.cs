using System;
using System.Collections.Generic;
using System.Linq;
using LanguageExt;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using SdmxDl.Client.Models;

namespace SdmxDl.Browser.Models;

public readonly record struct ChartItem(DateTime Date, double Value);

public class ChartSeries
{
    public string Title { get; }
    public IEnumerable<ChartItem> Values { get; }

    public ChartSeries(Series series)
    {
        var format = series.Meta.Find("TIME_FORMAT", s => s, () => string.Empty);
        Title = series.Meta.Find("TITLE", s => $"{s} ({series.Key})", () => series.Key);

        Values = series
            .Obs.Map(o => new ChartItem(DateTime.Parse(o.Period.Split('/')[0]), o.Value))
            .OrderBy(x => x.Date)
            .ToSeq()
            .Strict();
    }

    public LineSeries<DateTimePoint> ToLineSeries() =>
        new LineSeries<DateTimePoint>()
        {
            Values = this.Values.Select(x => new DateTimePoint(x.Date, x.Value)).ToArray(),
            GeometrySize = 0,
            LineSmoothness = 0,
            Name = Title,
        };
}

public static class ChartSeriesExtensions
{
    public static Seq<LineSeries<DateTimePoint>> ToLineSeries(this Seq<ChartSeries> series) =>
        series.Map(s => s.ToLineSeries()).Strict();
}
