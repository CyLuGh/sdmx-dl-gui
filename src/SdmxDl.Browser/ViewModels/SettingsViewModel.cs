using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using ReactiveUI;
using SdmxDl.Client;
using SdmxDl.Engine;

namespace SdmxDl.Browser.ViewModels;

[Reactive]
public partial class SettingsViewModel : BaseViewModel
{
    public partial string? JavaPath { get; set; }
    public partial string? JarPath { get; set; }
    public partial string? ServerUri { get; set; }
    public partial bool UseRunningServer { get; set; }

    public SettingsViewModel(ClientFactory clientFactory)
    {
        this.WhenAnyValue(
                x => x.JavaPath,
                x => x.JarPath,
                x => x.UseRunningServer,
                x => x.ServerUri
            )
            .Throttle(TimeSpan.FromMilliseconds(500))
            .Subscribe(_ =>
            {
                clientFactory.Settings = Settings;
            });
    }

    public Settings Settings =>
        new()
        {
            JavaPath = UseRunningServer || string.IsNullOrEmpty(JavaPath) ? string.Empty : JavaPath,
            JarPath = UseRunningServer || string.IsNullOrEmpty(JarPath) ? string.Empty : JarPath,
            ServerUri =
                UseRunningServer && !string.IsNullOrEmpty(ServerUri) ? ServerUri : string.Empty,
        };
}
