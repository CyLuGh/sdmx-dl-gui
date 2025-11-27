using System;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using ReactiveUI;
using ReactiveUI.Avalonia;
using ScottPlot;
using ScottPlot.Avalonia;
using SdmxDl.Browser.Infrastructure.Plots;
using SdmxDl.Browser.ViewModels;

namespace SdmxDl.Browser;

public partial class DataView : ReactiveUserControl<DataViewModel>
{
    private PlotInteractivity _standAloneInteractivity;
    private PlotInteractivity _linkedInteractivity;

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

        viewModel
            .DrawLinkedChartInteraction.RegisterHandler(ctx =>
            {
                view._linkedInteractivity = view.LinkedAvaPlot.DrawScatterLines(
                    InteractivityMode.SingleSeries,
                    ctx.Input
                );
                ctx.SetOutput(RxUnit.Default);
            })
            .DisposeWith(disposables);

        InitializePointerEvents(
            [
                (view.StandAloneAvaPlot, view._standAloneInteractivity),
                (view.LinkedAvaPlot, view._linkedInteractivity),
            ],
            disposables
        );

        viewModel
            .HighlightChartInteraction.RegisterHandler(ctx =>
            {
                var odt = ctx.Input;
                //TODO
                /*var dateTimePoints = odt.Match(
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
                    view.LinkedChart.Tooltip?.Show(dateTimePoints, view.LinkedChart.CoreChart);*/

                ctx.SetOutput(RxUnit.Default);
            })
            .DisposeWith(disposables);
    }

    private static void InitializePointerEvents(
        ReadOnlySpan<(AvaPlot, PlotInteractivity)> plots,
        CompositeDisposable disposables
    )
    {
        foreach (var (avaPlot, interactivity) in plots)
        {
            Observable
                .FromEventPattern<EventHandler<PointerEventArgs>, PointerEventArgs>(
                    _ => (_, args) => avaPlot.HandleMouseOver(args, interactivity),
                    handler => avaPlot.PointerMoved += handler,
                    handler => avaPlot.PointerMoved -= handler
                )
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe()
                .DisposeWith(disposables);

            Observable
                .FromEventPattern<EventHandler<PointerEventArgs>, PointerEventArgs>(
                    _ => (_, args) => avaPlot.HandleMouseLeft(args, interactivity),
                    handler => avaPlot.PointerExited += handler,
                    handler => avaPlot.PointerExited -= handler
                )
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe()
                .DisposeWith(disposables);
        }
    }
}
