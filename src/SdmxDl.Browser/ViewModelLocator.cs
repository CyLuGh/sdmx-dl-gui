global using RxCommand = ReactiveUI.ReactiveCommand<System.Reactive.Unit, System.Reactive.Unit>;
global using RxInteraction = ReactiveUI.Interaction<System.Reactive.Unit, System.Reactive.Unit>;
global using RxUnit = System.Reactive.Unit;
using System;
using Jot;
using Polly;
using Polly.Retry;
using ReactiveUI;
using SdmxDl.Browser.Infrastructure;
using SdmxDl.Browser.ViewModels;
using SdmxDl.Client;
using Splat;
using SukiUI.Dialogs;
using SukiUI.Toasts;

namespace SdmxDl.Browser;

public static class ViewModelLocator
{
    static ViewModelLocator()
    {
        Locator.CurrentMutable.InitializeSplat();

        var tracker = new Tracker();
        tracker
            .Configure<SettingsViewModel>()
            .Property(x => x.JavaPath)
            .Property(x => x.JarPath)
            .Property(x => x.ServerUri)
            .Property(x => x.UseRunningServer);

        SplatRegistrations.RegisterConstant(tracker);

        var pipeline = new ResiliencePipelineBuilder()
            .AddRetry(
                new RetryStrategyOptions()
                {
                    MaxRetryAttempts = 4,
                    Delay = TimeSpan.FromSeconds(1.5),
                    BackoffType = DelayBackoffType.Exponential,
                }
            )
            .Build();

        SplatRegistrations.RegisterConstant(pipeline);

        var exceptionHandler = new ExceptionHandler();
        RxApp.DefaultExceptionHandler = exceptionHandler;
        SplatRegistrations.RegisterConstant(exceptionHandler);
        SplatRegistrations.RegisterConstant<ISukiDialogManager>(new SukiDialogManager());
        SplatRegistrations.RegisterConstant<ISukiToastManager>(new SukiToastManager());

        SplatRegistrations.RegisterLazySingleton<SettingsViewModel>();
        SplatRegistrations.RegisterLazySingleton<ClientFactory>();
        SplatRegistrations.RegisterLazySingleton<BrowserViewModel>();
        SplatRegistrations.RegisterLazySingleton<SourceSelectorViewModel>();
        SplatRegistrations.RegisterLazySingleton<DataFlowSelectorViewModel>();
        SplatRegistrations.RegisterLazySingleton<DimensionsSelectorViewModel>();

        SplatRegistrations.Register<DataViewModel>();
        SplatRegistrations.Register<SeriesFinderViewModel>();

        var current = Locator.CurrentMutable;

        current.Register(() => new SourceSelector(), typeof(IViewFor<SourceSelectorViewModel>));
        current.Register(() => new DataView(), typeof(IViewFor<DataViewModel>));

        SplatRegistrations.SetupIOC();
    }

    public static ISukiDialogManager DialogManager =>
        Locator.Current.GetService<ISukiDialogManager>()!;

    public static ISukiToastManager ToastManager =>
        Locator.Current.GetService<ISukiToastManager>()!;

    public static BrowserViewModel BrowserViewModel =>
        Locator.Current.GetService<BrowserViewModel>()!;

    public static SettingsViewModel SettingsViewModel =>
        Locator.Current.GetService<SettingsViewModel>()!;

    public static SourceSelectorViewModel SourceSelectorViewModel =>
        Locator.Current.GetService<SourceSelectorViewModel>()!;

    public static DataFlowSelectorViewModel DataFlowSelectorViewModel =>
        Locator.Current.GetService<DataFlowSelectorViewModel>()!;

    public static DimensionsSelectorViewModel DimensionsSelectorViewModel =>
        Locator.Current.GetService<DimensionsSelectorViewModel>()!;
}
