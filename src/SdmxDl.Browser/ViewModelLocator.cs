global using RxCommand = ReactiveUI.ReactiveCommand<System.Reactive.Unit, System.Reactive.Unit>;
global using RxInteraction = ReactiveUI.Interaction<System.Reactive.Unit, System.Reactive.Unit>;
global using RxUnit = System.Reactive.Unit;
using Jot;
using ReactiveUI;
using SdmxDl.Browser.Infrastructure;
using SdmxDl.Browser.ViewModels;
using SdmxDl.Client;
using Splat;

namespace SdmxDl.Browser;

public class ViewModelLocator
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

        var exceptionHandler = new ExceptionHandler();
        RxApp.DefaultExceptionHandler = exceptionHandler;
        SplatRegistrations.RegisterConstant(exceptionHandler);

        SplatRegistrations.RegisterLazySingleton<SettingsViewModel>();
        SplatRegistrations.RegisterLazySingleton<ClientFactory>();
        SplatRegistrations.RegisterLazySingleton<BrowserViewModel>();
        SplatRegistrations.RegisterLazySingleton<SourceSelectorViewModel>();

        SplatRegistrations.SetupIOC();
    }

    public static BrowserViewModel BrowserViewModel =>
        Locator.Current.GetService<BrowserViewModel>()!;

    public static SettingsViewModel SettingsViewModel =>
        Locator.Current.GetService<SettingsViewModel>()!;

    public static SourceSelectorViewModel SourceSelectorViewModel =>
        Locator.Current.GetService<SourceSelectorViewModel>()!;
}
