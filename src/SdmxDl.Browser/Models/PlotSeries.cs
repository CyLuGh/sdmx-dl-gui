using LanguageExt;
using ScottPlot;

namespace SdmxDl.Browser.Models;

public record PlotSeries
{
    public required string Name { get; init; }
    public required Seq<Coordinates> Points { get; init; }
}
