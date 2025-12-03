using LanguageExt;
using ScottPlot.Plottables;

namespace SdmxDl.Browser.Infrastructure.Plots;

internal readonly record struct PlotInteractivity(
    Seq<Scatter> Series,
    PlotDecorations Decorations,
    InteractivityMode InteractivityMode,
    bool IsTimeSeries
);
