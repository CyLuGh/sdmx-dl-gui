using System;
using System.Collections.Generic;
using System.Linq;
using LanguageExt;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using SdmxDl.Client.Models;

namespace SdmxDl.Browser.Models;

public readonly record struct ChartItem(DateTime Date, double Value);

public enum Frequency
{
    Unknown = 0,
    Annual = 1,
    Yearly = 1,
    HalfYearly = 2,
    Quarterly = 3,
    Monthly = 12,
    Daily = 365
}

public static class FrequencyExtensions
{
    public static Frequency ToFrequency(this string format) =>
        format switch
        {
            "P1M" => Frequency.Monthly,
            _ => Frequency.Unknown
        };
}

public class ChartSeries
{
    public string Title { get; }
    public HashMap<DateTime, Option<double>> Values { get; }
    public Frequency Frequency { get; }

    public ChartSeries(Series series)
    {
        // P1M / P3M / P1Y / P1D
        Frequency = series.Meta.Find("TIME_FORMAT", s => s, () => string.Empty).ToFrequency();
        Title = series.Meta.Find("TITLE", s => $"{s} ({series.Key})", () => series.Key);

        var data = series
            .Obs.Map(o => new ChartItem(DateTime.Parse(o.Period.Split('/')[0]), o.Value))
            .OrderBy(x => x.Date)
            .ToSeq()
            .Strict();

        var start = data.Min(x => x.Date);
        var end = data.Max(x => x.Date);

        var map = data.Map(x => (x.Date, x.Value)).ToHashMap();

        Values = GetDates(start, end, d => d.AddMonths(1))
            .Select(d => (d, map.Find(d)))
            .ToHashMap();
    }

    private IEnumerable<DateTime> GetDates(
        DateTime start,
        DateTime end,
        Func<DateTime, DateTime> increment
    )
    {
        if (end < start)
            throw new ArgumentException("End date cannot be before start date");

        var current = start;

        while (current < end)
        {
            yield return current;
            current = increment(current);
        }
    }

    public LineSeries<DateTimePoint> ToLineSeries() =>
        new LineSeries<DateTimePoint>()
        {
            Values = this
                .Values.OrderBy(x => x.Key)
                .Select(x =>
                    x.Value.Match(
                        v => new DateTimePoint(x.Key, v),
                        () => new DateTimePoint(x.Key, null)
                    )
                )
                .ToArray(),
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
