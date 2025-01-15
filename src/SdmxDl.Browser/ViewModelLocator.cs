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
        SplatRegistrations.RegisterLazySingleton<Consumer>();
        SplatRegistrations.RegisterLazySingleton<BrowserViewModel>();

        SplatRegistrations.SetupIOC();
    }

    public static BrowserViewModel BrowserViewModel =>
        Locator.Current.GetService<BrowserViewModel>()!;
}
