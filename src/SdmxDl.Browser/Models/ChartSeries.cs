﻿using System;
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
    Daily = 365,
}

public static class FrequencyExtensions
{
    public static Frequency ToFrequency(this string format) =>
        format switch
        {
            "P1M" => Frequency.Monthly,
            "P1Y" => Frequency.Yearly,
            "P1D" => Frequency.Daily,
            "P3M" => Frequency.Quarterly,
            _ => Frequency.Unknown,
        };

    public static Func<DateTime, DateTime> GetIncrement(this Frequency frequency) =>
        frequency switch
        {
            Frequency.Monthly => x => x.AddMonths(1),
            Frequency.Yearly => x => x.AddYears(1),
            Frequency.Daily => x => x.AddDays(1),
            Frequency.Quarterly => x => x.AddMonths(3),
            Frequency.HalfYearly => x => x.AddMonths(6),
            _ => throw new ArgumentOutOfRangeException(nameof(frequency), frequency, null),
        };

    public static Func<DateTime, string> GetFormatter(this Frequency frequency) =>
        frequency switch
        {
            Frequency.Monthly => x => x.ToString("yyyy-MM"),
            Frequency.Yearly => x => x.ToString("yyyy"),
            Frequency.Daily => x => x.ToString("yyyy-MM-dd"),
            Frequency.Quarterly => x => x.ToString("yyyy-MM"),
            Frequency.HalfYearly => x => x.ToString("yyyy-MM"),
            _ => throw new ArgumentOutOfRangeException(nameof(frequency), frequency, null),
        };
}

public class ChartSeries
{
    public string Key { get; }
    public string Title { get; }
    public HashMap<DateTime, Option<double>> Values { get; }
    public Frequency Frequency { get; }
    public string Format { get; }

    public ChartSeries(Series series)
    {
        Key = series.Key;

        Frequency = series
            .Meta.Find(
                "TIME_FORMAT",
                s => s,
                () =>
                    series
                        .Obs.HeadOrNone()
                        .Match(
                            o =>
                                Prelude
                                    .Try(() => o.Period.Split('/')[1])
                                    .Match(s => s, _ => string.Empty),
                            () => string.Empty
                        )
            )
            .ToFrequency();
        Title = series.Meta.Find("TITLE", s => $"{s} ({series.Key})", () => series.Key);
        Format = series.Meta.Find("DECIMALS", s => $"N{s}", () => "N");

        var data = series
            .Obs.Map(o => new ChartItem(DateTime.Parse(o.Period.Split('/')[0]), o.Value))
            .OrderBy(x => x.Date)
            .ToSeq()
            .Strict();

        var start = data.Min(x => x.Date);
        var end = data.Max(x => x.Date);

        var map = data.Map(x => (x.Date, x.Value)).ToHashMap();

        Values =
            Frequency != Frequency.Unknown
                ? GetDates(start, end, Frequency.GetIncrement())
                    .Select(d => (d, map.Find(d)))
                    .ToHashMap()
                : map.Map(x => (x.Key, Option<double>.Some(x.Value))).ToHashMap();
    }

    internal static IEnumerable<DateTime> GetDates(
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

    public LineSeries<DateTimePoint> ToLineSeries(DateTimeOffset start, DateTimeOffset end) =>
        new LineSeries<DateTimePoint>()
        {
            Values = this
                .Values.Where(x => x.Key >= start && x.Key <= end)
                .OrderBy(x => x.Key)
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
    public static Seq<LineSeries<DateTimePoint>> ToLineSeries(
        this Seq<ChartSeries> series,
        DateTimeOffset start,
        DateTimeOffset end
    ) => series.Map(s => s.ToLineSeries(start, end)).Strict();

    public static Seq<LineSeries<DateTimePoint>> ToLineSeries(this Seq<ChartSeries> series) =>
        series.Map(s => s.ToLineSeries()).Strict();

    public static Frequency GetHighestFreq(this Seq<ChartSeries> series) =>
        (Frequency)series.Max(x => (int)x.Frequency);

    public static Seq<DateTime> GetDates(this Seq<ChartSeries> series)
    {
        var highestFreq = series.GetHighestFreq();
        var dates = series.Map(s => s.Values.Keys.ToSeq()).Flatten();
        var start = dates.Min();
        var end = dates.Max();

        return ChartSeries.GetDates(start, end, highestFreq.GetIncrement()).ToSeq();
    }
}
