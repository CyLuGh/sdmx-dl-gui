using System;
using Irihi.Avalonia.Shared.Contracts;
using ReactiveUI;
using SdmxDl.Engine;

namespace SdmxDl.Browser.ViewModels;

[Reactive]
public partial class SettingsViewModel : BaseViewModel, IDialogContext
{
    public partial string? JavaPath { get; set; }
    public partial string? JarPath { get; set; }
    public partial string? ServerUri { get; set; }
    public partial bool UseRunningServer { get; set; }

    public RxCommand Connect { get; }
    public RxCommand Cancel { get; }

    public SettingsViewModel()
    {
        ServerUri = @"http://localhost:4567";

        Connect = ReactiveCommand.Create(() => Close(Settings));
        Cancel = ReactiveCommand.Create(Close);
    }

    public Settings Settings =>
        new()
        {
            JavaPath = UseRunningServer || string.IsNullOrEmpty(JavaPath) ? string.Empty : JavaPath,
            JarPath = UseRunningServer || string.IsNullOrEmpty(JarPath) ? string.Empty : JarPath,
            ServerUri =
                UseRunningServer && !string.IsNullOrEmpty(ServerUri) ? ServerUri : string.Empty,
            IsHosting = !UseRunningServer,
        };

    public void Close() => Close(Settings.None);

    public void Close(Settings settings)
    {
        RequestClose?.Invoke(this, settings);
    }

    public event EventHandler<object?>? RequestClose;
}
