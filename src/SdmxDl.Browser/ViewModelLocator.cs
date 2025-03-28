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

        SplatRegistrations.RegisterLazySingleton<SettingsViewModel>();
        SplatRegistrations.RegisterLazySingleton<ClientFactory>();
        SplatRegistrations.RegisterLazySingleton<BrowserViewModel>();
        SplatRegistrations.RegisterLazySingleton<SourceSelectorViewModel>();
        SplatRegistrations.RegisterLazySingleton<DataFlowSelectorViewModel>();
        SplatRegistrations.RegisterLazySingleton<DimensionsSelectorViewModel>();

        SplatRegistrations.SetupIOC();
    }

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
