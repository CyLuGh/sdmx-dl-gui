using System;
using System.IO;
using System.Reactive.Linq;
using Jot;
using LanguageExt;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using SdmxDl.Client.Models;

namespace SdmxDl.Browser.ViewModels;

public class SettingsViewModel : BaseViewModel
{
    public Settings CurrentSettings
    {
        [ObservableAsProperty]
        get;
    }

    [Reactive]
    public string? JavaPath { get; set; }

    [Reactive]
    public string? JarPath { get; set; }

    [Reactive]
    public string? ServerUri { get; set; }

    [Reactive]
    public bool UseRunningServer { get; set; }

    public ReactiveCommand<RxUnit, Settings> Connect { get; }
    public ReactiveCommand<RxUnit, Settings> Cancel { get; }
    public ReactiveCommand<RxUnit, string?> PickJavaPath { get; }
    public ReactiveCommand<RxUnit, string?> PickJarPath { get; }
    public Interaction<string, string?> PickPathInteraction { get; } =
        new(RxApp.MainThreadScheduler);

    public RxCommand Close { get; }
    public RxInteraction CloseInteraction { get; } = new(RxApp.MainThreadScheduler);

    public ReactiveCommand<RxUnit, Settings> ReloadSettings { get; }

    public SettingsViewModel(Tracker tracker)
    {
        ServerUri = "http://localhost:4557";
        tracker.Track(this);

        Close = ReactiveCommand.CreateFromObservable(() => CloseInteraction.Handle(RxUnit.Default));

        Connect = CreateCommandConnect();
        Cancel = ReactiveCommand.Create(() => Settings.None);
        ReloadSettings = ReactiveCommand.CreateRunInBackground(GetSettings);

        Connect
            .Merge(Cancel)
            .Merge(ReloadSettings)
            .Merge(Connect.IsExecuting.Where(x => x).Select(_ => Settings.None)) // Reset settings on new connection configuration
            .ToPropertyEx(this, x => x.CurrentSettings, initialValue: Settings.None);
        Connect.Merge(Cancel).Select(_ => RxUnit.Default).InvokeCommand(Close);

        PickJavaPath = CreateCommandPickJavaPath();
        PickJarPath = CreateCommandPickJarPath();
    }

    private ReactiveCommand<RxUnit, string?> CreateCommandPickJavaPath()
    {
        var cmd = ReactiveCommand.CreateFromObservable(
            () => PickPathInteraction.Handle("Pick Java path")
        );

        cmd.Where(p => !string.IsNullOrWhiteSpace(p))
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(path =>
            {
                JavaPath = path;
            });

        return cmd;
    }

    private ReactiveCommand<RxUnit, string?> CreateCommandPickJarPath()
    {
        var cmd = ReactiveCommand.CreateFromObservable(
            () => PickPathInteraction.Handle("Pick jar path")
        );

        cmd.Where(p => !string.IsNullOrWhiteSpace(p))
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(path =>
            {
                JarPath = path;
            });

        return cmd;
    }

    private ReactiveCommand<RxUnit, Settings> CreateCommandConnect()
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

        return ReactiveCommand.Create(GetSettings, canConnect);
    }

    public Settings GetSettings() =>
        new()
        {
            JavaPath = UseRunningServer || string.IsNullOrEmpty(JavaPath) ? string.Empty : JavaPath,
            JarPath = UseRunningServer || string.IsNullOrEmpty(JarPath) ? string.Empty : JarPath,
            ServerUri =
                UseRunningServer && !string.IsNullOrEmpty(ServerUri) ? ServerUri : string.Empty,
            IsHosting = !UseRunningServer,
        };
}
