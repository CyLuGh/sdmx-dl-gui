using System;
using System.IO;
using System.Reactive.Linq;
using Irihi.Avalonia.Shared.Contracts;
using Jot;
using ReactiveUI;
using SdmxDl.Client.Models;

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
    public ReactiveCommand<RxUnit, string?> PickJavaPath { get; }
    public ReactiveCommand<RxUnit, string?> PickJarPath { get; }
    public Interaction<string, string?> PickPathInteraction { get; } =
        new(RxApp.MainThreadScheduler);

    public SettingsViewModel(Tracker tracker)
    {
        ServerUri = "http://localhost:4557";
        tracker.Track(this);

        Connect = CreateCommandConnect();
        Cancel = ReactiveCommand.Create(Close);

        PickJavaPath = CreateCommandPickJavaPath();
        PickJarPath = CreateCommandPickJarPath();
    }

    private ReactiveCommand<RxUnit, string?> CreateCommandPickJavaPath()
    {
        var cmd = ReactiveCommand.CreateFromObservable(
            () => PickPathInteraction.Handle("Pick Java path")
        );

        ObservableExtensions.Subscribe(
            cmd.Where(p => !string.IsNullOrWhiteSpace(p)).ObserveOn(RxApp.MainThreadScheduler),
            path =>
            {
                JavaPath = path;
            }
        );

        return cmd;
    }

    private ReactiveCommand<RxUnit, string?> CreateCommandPickJarPath()
    {
        var cmd = ReactiveCommand.CreateFromObservable(
            () => PickPathInteraction.Handle("Pick jar path")
        );

        ObservableExtensions.Subscribe(
            cmd.Where(p => !string.IsNullOrWhiteSpace(p)).ObserveOn(RxApp.MainThreadScheduler),
            path =>
            {
                JarPath = path;
            }
        );

        return cmd;
    }

    private RxCommand CreateCommandConnect()
    {
        var canConnect = this.WhenAnyValue(
                x => x.UseRunningServer,
                x => x.ServerUri,
                x => x.JavaPath,
                x => x.JarPath
            )
            .Select(t =>
            {
                var (useServer, serverUri, javaPath, jarPath) = t;

                if (useServer)
                    return !string.IsNullOrWhiteSpace(serverUri);

                return !string.IsNullOrWhiteSpace(javaPath)
                    && !string.IsNullOrWhiteSpace(jarPath)
                    && File.Exists(javaPath)
                    && File.Exists(jarPath);
            })
            .ObserveOn(RxApp.MainThreadScheduler);

        return ReactiveCommand.Create(
            () =>
            {
                Close(Settings);
            },
            canConnect
        );
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
