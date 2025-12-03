using System;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using LanguageExt;
using ReactiveUI;
using ReactiveUI.Avalonia;
using ScottPlot;
using SdmxDl.Browser.Infrastructure.Plots;
using SdmxDl.Browser.ViewModels;
using Splat;
using SukiUI.Dialogs;

namespace SdmxDl.Browser;

public partial class DataView : ReactiveUserControl<DataViewModel>
{
    private PlotInteractivity _standAloneInteractivity;
    private PlotInteractivity _linkedInteractivity;

    public DataView()
    {
        InitializeComponent();

        StandAloneAvaPlot.Plot.ConfigurePlot((Orientation.Horizontal, Edge.Bottom));
        LinkedAvaPlot.Plot.ConfigurePlot((Orientation.Horizontal, Edge.Bottom));

        this.WhenAnyValue(x => x.ViewModel)
            .WhereNotNull()
            .Subscribe(vm =>
            {
                vm.RenameInteraction.RegisterHandler(ctx =>
                {
                    var rvm = Locator.Current.GetService<RenameViewModel>()!;
                    rvm.Original = ctx.Input;

                    ViewModelLocator
                        .DialogManager.CreateDialog()
                        .WithContent(new RenameView() { ViewModel = rvm })
                        .TryShow();
                });
            });

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
            .DrawLinkedChartInteraction.RegisterHandler(ctx =>
            {
                view._linkedInteractivity = view.LinkedAvaPlot.DrawScatterLines(
                    InteractivityMode.SingleSeries,
                    ctx.Input
                );
                ctx.SetOutput(view._linkedInteractivity.Series);
            })
            .DisposeWith(disposables);

        Observable
            .FromEventPattern<EventHandler<PointerEventArgs>, PointerEventArgs>(
                _ =>
                    (_, args) =>
                    {
                        var hoveredInfo = view.LinkedAvaPlot.HandleMouseOver(
                            args,
                            view._linkedInteractivity
                        );

                        hoveredInfo
                            .Some(info =>
                            {
                                var date = DateTime.FromOADate(info.Item2.X);
                                viewModel.HighlightedPoint = (date, info.Item1);
                            })
                            .None(() =>
                            {
                                viewModel.HighlightedPoint = Option<(
                                    DateTime,
                                    ScottPlot.Plottables.Scatter
                                )>.None;
                            });
                    },
                handler => view.LinkedAvaPlot.PointerMoved += handler,
                handler => view.LinkedAvaPlot.PointerMoved -= handler
            )
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe()
            .DisposeWith(disposables);

        Observable
            .FromEventPattern<EventHandler<PointerEventArgs>, PointerEventArgs>(
                _ =>
                    (_, args) =>
                    {
                        view.LinkedAvaPlot.HandleMouseLeft(args, view._linkedInteractivity);
                        viewModel.HighlightedPoint = Option<(
                            DateTime,
                            ScottPlot.Plottables.Scatter
                        )>.None;
                    },
                handler => view.LinkedAvaPlot.PointerExited += handler,
                handler => view.LinkedAvaPlot.PointerExited -= handler
            )
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe()
            .DisposeWith(disposables);

        viewModel
            .HighlightChartInteraction.RegisterHandler(ctx =>
            {
                var odt = ctx.Input;

                odt.IfNone(() =>
                {
                    view.LinkedAvaPlot.HideDecorations(view._linkedInteractivity);
                    view.LinkedAvaPlot.Refresh();
                });
                odt.IfSome(t =>
                {
                    var (period, series) = t;
                    var point = series
                        .Data.GetScatterPoints()
                        .Find(c => c.X.Equals(period.ToOADate()));
                    point
                        .Some(coord =>
                        {
                            view._linkedInteractivity.HighlightPoint(series, coord);

                            view._linkedInteractivity.ShowText(
                                view.LinkedAvaPlot,
                                coord,
                                coord,
                                view._linkedInteractivity.IsTimeSeries
                                    ? $"{series.LegendText} - {DateTime.FromOADate(coord.X):yyyy-MM-dd}: {coord.Y:N}"
                                    : $"{series.LegendText} - {coord.X:0}: {coord.Y:N}",
                                series.MarkerStyle.FillColor
                            );

                            view.LinkedAvaPlot.Refresh();
                        })
                        .None(() =>
                        {
                            view.LinkedAvaPlot.HideDecorations(view._linkedInteractivity);
                            view.LinkedAvaPlot.Refresh();
                        });
                });

                ctx.SetOutput(RxUnit.Default);
            })
            .DisposeWith(disposables);
    }
}
