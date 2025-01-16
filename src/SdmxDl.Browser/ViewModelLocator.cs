global using RxCommand = ReactiveUI.ReactiveCommand<System.Reactive.Unit, System.Reactive.Unit>;
global using RxInteraction = ReactiveUI.Interaction<System.Reactive.Unit, System.Reactive.Unit>;
global using RxUnit = System.Reactive.Unit;
using SdmxDl.Browser.ViewModels;
using SdmxDl.Client;
using Splat;

namespace SdmxDl.Browser;

public class ViewModelLocator
{
    static ViewModelLocator()
    {
        Locator.CurrentMutable.InitializeSplat();

        SplatRegistrations.RegisterLazySingleton<SettingsViewModel>();
        SplatRegistrations.RegisterLazySingleton<ClientFactory>();
        SplatRegistrations.RegisterLazySingleton<BrowserViewModel>();

        SplatRegistrations.SetupIOC();
    }

    public static BrowserViewModel BrowserViewModel =>
        Locator.Current.GetService<BrowserViewModel>()!;

    public static SettingsViewModel SettingsViewModel =>
        Locator.Current.GetService<SettingsViewModel>()!;
}
