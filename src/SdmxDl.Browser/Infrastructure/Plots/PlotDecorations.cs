using ScottPlot;
using ScottPlot.Plottables;

namespace SdmxDl.Browser.Infrastructure.Plots;

internal readonly record struct PlotDecorations
{
    public PlotDecorations(Crosshair crosshair, Marker highlightMarker, Text highlightText)
    {
        Crosshair = crosshair;
        crosshair.IsVisible = false;

        highlightMarker.Shape = MarkerShape.FilledCircle;
        highlightMarker.Size = 8;
        highlightMarker.LineWidth = 2;
        HighlightMarker = highlightMarker;
        highlightMarker.IsVisible = false;

        highlightText.LabelAlignment = Alignment.LowerLeft;
        highlightText.LabelBold = true;
        highlightText.OffsetX = 7;
        highlightText.OffsetY = -7;
        highlightText.LabelFontSize = 14f;
        HighlightText = highlightText;
        highlightText.IsVisible = false;
    }

    internal Crosshair Crosshair { get; }
    internal Marker HighlightMarker { get; }
    internal Text HighlightText { get; }
}
