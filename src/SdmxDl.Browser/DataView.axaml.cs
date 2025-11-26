using System;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using LanguageExt;
using LiveChartsCore.Kernel;
using ReactiveUI;
using ReactiveUI.Avalonia;
using ScottPlot;
using SdmxDl.Browser.Infrastructure.Plots;
using SdmxDl.Browser.ViewModels;

namespace SdmxDl.Browser;

public partial class DataView : ReactiveUserControl<DataViewModel>
{
    private PlotInteractivity _standAloneInteractivity;

    public DataView()
    {
        InitializeComponent();

        StandAloneAvaPlot.Plot.ConfigurePlot((Orientation.Horizontal, Edge.Bottom));

        this.WhenActivated(disposables =>
        {
            this.WhenAnyValue(x => x.ViewModel)
                .WhereNotNull()
                .Do(vm => PopulateFromViewModel(this, vm, disposables))
                .Subscribe()
                .DisposeWith(disposables);
        });
    }

    private static void PopulateFromViewModel(
        DataView view,
        DataViewModel viewModel,
        CompositeDisposable disposables
    )
    {
        viewModel
            .CopyToClipboardInteraction.RegisterHandler(async ctx =>
            {
                var clipboard = TopLevel.GetTopLevel(view)?.Clipboard;
                if (clipboard is not null)
                    await clipboard.SetTextAsync(ctx.Input);
                ctx.SetOutput(RxUnit.Default);
            })
            .DisposeWith(disposables);

        viewModel
            .DrawStandAloneChartInteraction.RegisterHandler(ctx =>
            {
                view._standAloneInteractivity = view.StandAloneAvaPlot.DrawScatterLines(
                    InteractivityMode.AllSeries,
                    ctx.Input
                );
                ctx.SetOutput(RxUnit.Default);
            })
            .DisposeWith(disposables);

        Observable
            .FromEventPattern<EventHandler<PointerEventArgs>, PointerEventArgs>(
                _ =>
                    (_, args) =>
                        view.StandAloneAvaPlot.HandleMouseOver(args, view._standAloneInteractivity),
                handler => view.StandAloneAvaPlot.PointerMoved += handler,
                handler => view.StandAloneAvaPlot.PointerMoved -= handler
            )
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe()
            .DisposeWith(disposables);

        Observable
            .FromEventPattern<EventHandler<PointerEventArgs>, PointerEventArgs>(
                _ =>
                    (_, args) =>
                        view.StandAloneAvaPlot.HandleMouseLeft(args, view._standAloneInteractivity),
                handler => view.StandAloneAvaPlot.PointerExited += handler,
                handler => view.StandAloneAvaPlot.PointerExited -= handler
            )
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe()
            .DisposeWith(disposables);

        viewModel
            .HighlightChartInteraction.RegisterHandler(ctx =>
            {
                var odt = ctx.Input;

                var dateTimePoints = odt.Match(
                    date =>
                        view.LinkedChart.Series.ToSeq()
                            .Map(s =>
                                s.Fetch(view.LinkedChart.CoreChart)
                                    .Where(cp =>
                                        new DateTime((long)cp.Coordinate.SecondaryValue).Equals(
                                            date
                                        )
                                    )
                                    .ToSeq()
                            )
                            .Flatten(),
                    () => Seq<ChartPoint>.Empty
                );

                if (dateTimePoints.IsEmpty)
                    view.LinkedChart.Tooltip?.Hide(view.LinkedChart.CoreChart);
                else
                    view.LinkedChart.Tooltip?.Show(dateTimePoints, view.LinkedChart.CoreChart);

                ctx.SetOutput(RxUnit.Default);
            })
            .DisposeWith(disposables);
    }
}
