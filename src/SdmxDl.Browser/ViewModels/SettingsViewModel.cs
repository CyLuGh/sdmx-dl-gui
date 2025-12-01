using System;
using System.IO;
using System.Reactive.Linq;
using Avalonia.Styling;
using Jot;
using LanguageExt;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using SdmxDl.Client.Models;
using SukiUI;

namespace SdmxDl.Browser.ViewModels;

public partial class SettingsViewModel : BaseViewModel
{
    [ObservableAsProperty(ReadOnly = false)]
    private Settings _currentSettings;

    [Reactive]
    public partial string? JavaPath { get; set; }

    [Reactive]
    public partial string? JarPath { get; set; }

    [Reactive]
    public partial string? ServerUri { get; set; }

    [Reactive]
    public partial bool UseRunningServer { get; set; }

    [Reactive]
    public partial bool IsSplitView { get; set; } = true;

    [Reactive]
    public partial bool IsLightTheme { get; set; } = true;

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

        _currentSettingsHelper = Connect
            .Merge(Cancel)
            .Merge(ReloadSettings)
            .Merge(Connect.IsExecuting.Where(x => x).Select(_ => Settings.None)) // Reset settings on new connection configuration
            .ToProperty(this, x => x.CurrentSettings, initialValue: Settings.None);
        Connect.Merge(Cancel).Select(_ => RxUnit.Default).InvokeCommand(Close);

        PickJavaPath = CreateCommandPickJavaPath();
        PickJarPath = CreateCommandPickJarPath();

        this.WhenAnyValue(x => x.IsLightTheme)
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(isLight =>
            {
                var theme = SukiTheme
                    .GetInstance()
                    .ColorThemes.Find(c => c.DisplayName.Equals("CustomTheme"))
                    .Match(t => t, () => App.AppTheme);

                SukiTheme
                    .GetInstance()
                    .ChangeBaseTheme(isLight ? ThemeVariant.Light : ThemeVariant.Dark);

                SukiTheme.GetInstance().ChangeColorTheme(theme);
            });
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
