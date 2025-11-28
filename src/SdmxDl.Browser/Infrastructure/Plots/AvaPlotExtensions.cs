using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Avalonia.Input;
using LanguageExt;
using ScottPlot;
using ScottPlot.Avalonia;
using ScottPlot.Palettes;
using ScottPlot.Plottables;
using ScottPlot.TickGenerators;
using SdmxDl.Browser.Models;

namespace SdmxDl.Browser.Infrastructure.Plots;

internal static class AvaPlotExtensions
{
    internal static void ConfigurePlot(
        this Plot plot,
        Option<(Orientation, Edge)> legendLayout,
        bool useDateTimeAxis = true
    )
    {
        legendLayout.IfSome(o =>
        {
            var (orientation, edge) = o;
            plot.Legend.Orientation = orientation;
            plot.ShowLegend(edge);
        });
        legendLayout.IfNone(() => plot.HideLegend());

        plot.Legend.BackgroundColor = Colors.Transparent;
        plot.Legend.BackgroundHatchColor = Colors.Transparent;
        plot.Legend.ShadowColor = Colors.Transparent;
        plot.Legend.OutlineColor = Colors.Transparent;
        plot.Legend.OutlineStyle = LineStyle.None;
        plot.Legend.FontColor = Colors.Gray;

        plot.FigureBackground.Color = Colors.Transparent;
        if (useDateTimeAxis)
        {
            plot.Axes.DateTimeTicksBottom();
        }
        else
        {
            plot.Axes.Bottom.TickGenerator = new NumericAutomatic { IntegerTicksOnly = true };
        }

        plot.Add.Palette = new Microcharts();

        plot.Axes.Color(Colors.Gray);
    }

    internal static void HandleMouseLeft(
        this AvaPlot plot,
        PointerEventArgs evt,
        PlotInteractivity interactivity
    )
    {
        plot.HideDecorations(interactivity);
        plot.Refresh();
        evt.Handled = true;
    }

    internal static Option<(Scatter, DataPoint)> HandleMouseOver(
        this AvaPlot plot,
        PointerEventArgs evt,
        PlotInteractivity interactivity
    )
    {
        var position = evt.GetPosition(plot);
        Pixel mousePixel = new(position.X, position.Y);
        Coordinates mouseLocation = plot.Plot.GetCoordinates(mousePixel);
        var series = interactivity.Series;

        var hoveredInfo = Option<(Scatter, DataPoint)>.None;

        switch (interactivity.InteractivityMode)
        {
            case InteractivityMode.SingleSeries:
                hoveredInfo = SingleSeriesMouseOver(plot, interactivity, mouseLocation, series);
                break;
            case InteractivityMode.AllSeries:
                AllSeriesMouseOver(plot, interactivity, mouseLocation, series);
                break;
        }
        evt.Handled = true;
        return hoveredInfo;
    }

    private static void ShowCrosshair(
        this PlotInteractivity interactivity,
        Coordinates coordinates,
        Color color,
        bool horizontalLine = true
    ) => ShowCrosshair(interactivity.Decorations.Crosshair, coordinates, color, horizontalLine);

    private static void ShowCrosshair(
        Crosshair crosshair,
        Coordinates coordinates,
        Color color,
        bool horizontalLine = true
    )
    {
        crosshair.IsVisible = true;
        crosshair.HorizontalLine.IsVisible = horizontalLine;
        crosshair.Position = coordinates;
        crosshair.HorizontalLine.Color = color;
        crosshair.VerticalLine.Color = color;
    }

    private static void AllSeriesMouseOver(
        AvaPlot plot,
        PlotInteractivity interactivity,
        Coordinates mouseLocation,
        Seq<Scatter> series
    )
    {
        var points = series
            .Select(s =>
                (Scatter: s, Point: s.Data.GetNearestX(mouseLocation, plot.Plot.LastRender))
            )
            .Where(t => !double.IsNaN(t.Point.Y));

        if (points.IsEmpty)
        {
            plot.HideDecorations(interactivity);
            return;
        }

        interactivity.ShowCrosshair(points.Head.Point.Coordinates, Colors.Gray, false);

        var text = new StringBuilder()
            .AppendLine(
                interactivity.IsTimeSeries
                    ? $"{DateTime.FromOADate(points.Head.Point.X):yyyy-MM-dd}"
                    : $"{points.Head.Point.X:0}"
            )
            .AppendJoin(
                Environment.NewLine,
                points.Select(t => $"{t.Scatter.LegendText}: {t.Point.Y:N}")
            )
            .ToString();

        interactivity.ShowText(
            plot,
            mouseLocation,
            mouseLocation,
            text,
            Colors.Gray,
            Colors.WhiteSmoke
        );

        plot.Refresh();
    }

    /// <summary>
    /// Hide the crosshair, marker and text when no point is selected
    /// </summary>
    internal static void HideDecorations(this AvaPlot plot, PlotInteractivity interactivity)
    {
        if (
            interactivity.Decorations.HighlightMarker?.IsVisible == true
            || interactivity.Decorations.HighlightText?.IsVisible == true
        )
        {
            interactivity.Decorations.Crosshair.IsVisible = false;
            interactivity.Decorations.HighlightMarker.IsVisible = false;
            interactivity.Decorations.HighlightText.IsVisible = false;
            plot.Refresh();
        }
    }

    private static (int scatterIndex, Option<DataPoint> nearestPoint) GetHoveredSeriesIndex(
        AvaPlot plot,
        Coordinates mouseLocation,
        Seq<Scatter> series
    )
    {
        // get the nearest point of each scatter
        var nearestPoints = series
            .Select((s, i) => (i, s.Data.GetNearest(mouseLocation, plot.Plot.LastRender)))
            .ToHashMap();

        // determine which scatter's nearest point is nearest to the mouse
        int scatterIndex = -1;
        double smallestDistance = double.MaxValue;
        for (int i = 0; i < nearestPoints.Count; i++)
        {
            if (nearestPoints[i].IsReal)
            {
                // calculate the distance of the point to the mouse
                double distance = nearestPoints[i].Coordinates.Distance(mouseLocation);
                if (distance < smallestDistance)
                {
                    // store the index
                    scatterIndex = i;
                    smallestDistance = distance;
                }
            }
        }

        return (scatterIndex, nearestPoints.Find(scatterIndex));
    }

    private static Option<(Scatter, DataPoint)> SingleSeriesMouseOver(
        AvaPlot plot,
        PlotInteractivity interactivity,
        Coordinates mouseLocation,
        Seq<Scatter> series
    )
    {
        var (scatterIndex, nearestPoint) = GetHoveredSeriesIndex(plot, mouseLocation, series);

        // Hide the crosshair, marker and text when no point is found
        if (scatterIndex == -1 || nearestPoint.IsNone)
        {
            plot.HideDecorations(interactivity);
            return Option<(Scatter, DataPoint)>.None;
        }

        // place the crosshair, marker and text over the selected point
        var scatter = series[scatterIndex];
        DataPoint point = nearestPoint.Match(p => p, () => default);
        HighlightPoint(interactivity, scatter, point.Coordinates);

        interactivity.ShowText(
            plot,
            mouseLocation,
            point.Coordinates,
            interactivity.IsTimeSeries
                ? $"{DateTime.FromOADate(point.X):yyyy-MM-dd}: {point.Y:N}"
                : $"{point.X:0}: {point.Y:N}",
            Color.FromHex("f7f8fa").WithAlpha(180),
            scatter.MarkerStyle.FillColor
        );

        plot.Refresh();

        return (scatter, point);
    }

    public static void HighlightPoint(
        this PlotInteractivity interactivity,
        Scatter scatter,
        Coordinates coordinates
    )
    {
        interactivity.Decorations.HighlightMarker.IsVisible = true;
        interactivity.Decorations.HighlightMarker.Location = coordinates;
        interactivity.Decorations.HighlightMarker.MarkerStyle.OutlineColor = scatter
            .MarkerStyle
            .FillColor;
        interactivity.Decorations.HighlightMarker.MarkerStyle.FillColor = scatter
            .MarkerStyle
            .FillColor;
    }

    public static void ShowText(
        this PlotInteractivity interactivity,
        AvaPlot plot,
        Coordinates mouseLocation,
        Coordinates pointLocation,
        string text,
        Color backgroundColor,
        Option<Color> fontColor
    )
    {
        // Change X and Y offset compare to mouse position
        var midX =
            plot.Plot.Axes.Bottom.Min
            + ((plot.Plot.Axes.Bottom.Max - plot.Plot.Axes.Bottom.Min) / 2);
        var midY =
            plot.Plot.Axes.Left.Min + ((plot.Plot.Axes.Left.Max - plot.Plot.Axes.Left.Min) / 2);

        const float offset = 8;

        if (mouseLocation.Y <= midY)
        {
            interactivity.Decorations.HighlightText.LabelAlignment = Alignment.LowerLeft;
            interactivity.Decorations.HighlightText.OffsetY = -offset;

            if (mouseLocation.X <= midX)
            {
                interactivity.Decorations.HighlightText.OffsetX = offset;
            }
            else
            {
                interactivity.Decorations.HighlightText.OffsetX = -offset;
                interactivity.Decorations.HighlightText.LabelAlignment = Alignment.LowerRight;
            }
        }
        else
        {
            interactivity.Decorations.HighlightText.LabelAlignment = Alignment.UpperLeft;
            interactivity.Decorations.HighlightText.OffsetY = offset;
            interactivity.Decorations.HighlightText.OffsetX = offset;

            if (mouseLocation.X <= midX)
            {
                interactivity.Decorations.HighlightText.OffsetX = offset;
            }
            else
            {
                interactivity.Decorations.HighlightText.OffsetX = -offset;
                interactivity.Decorations.HighlightText.LabelAlignment = Alignment.UpperRight;
            }
        }

        interactivity.Decorations.HighlightText.IsVisible = true;
        interactivity.Decorations.HighlightText.Location = pointLocation;

        interactivity.Decorations.HighlightText.LabelText = text;

        interactivity.Decorations.HighlightText.LabelFontColor = fontColor.Match(
            c => c,
            () => Colors.Gray
        );
        interactivity.Decorations.HighlightText.LabelBackgroundColor = backgroundColor;
        interactivity.Decorations.HighlightText.LabelPadding = 5;
    }

    internal static PlotInteractivity DrawScatterLines(
        this AvaPlot avaPlot,
        InteractivityMode interactivityMode,
        params IEnumerable<PlotSeries> series
    )
    {
        var plot = avaPlot.Plot;
        plot.Clear();

        List<Scatter> scatters = [];

        foreach (var cs in series)
        {
            var scatterLine = plot.Add.ScatterLine(cs.Points.ToArray());
            scatterLine.LegendText = cs.Name;
            scatterLine.MarkerStyle.Shape = MarkerShape.None;
            scatterLine.MarkerStyle.Size = 10;
            scatterLine.LineWidth = 3;
            scatters.Add(scatterLine);
        }

        avaPlot.UserInputProcessor.UserActionResponses.Clear();
        var deco = new PlotDecorations(
            plot.Add.Crosshair(0, 0),
            plot.Add.Marker(0, 0),
            plot.Add.Text("", 0, 0)
        );
        plot.Axes.AutoScale();

        avaPlot.Refresh();

        return new(scatters.ToSeq(), deco, interactivityMode, true);
    }
}
