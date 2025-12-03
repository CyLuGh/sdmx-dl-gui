using System;
using System.Linq;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using Avalonia.Input;
using LanguageExt;
using ReactiveUI;
using ReactiveUI.SourceGenerators;

namespace SdmxDl.Browser.ViewModels;

public partial class RenameViewModel : BaseViewModel
{
    [Reactive]
    public partial string Original { get; set; } = string.Empty;

    [Reactive]
    public partial string Renamed { get; set; } = string.Empty;

    [ObservableAsProperty(ReadOnly = false)]
    private bool _isValid;

    public ReactiveCommand<KeyEventArgs, RxUnit> CheckKeyboardInput { get; }

    public RenameViewModel(BrowserViewModel browserViewModel)
    {
        Close = CreateCommandClose();
        ParseRename = CreateCommandParseRename(browserViewModel, Close);

        CheckKeyboardInput = ReactiveCommand.CreateRunInBackground(
            (KeyEventArgs args) =>
            {
                switch (args.Key)
                {
                    case Key.Enter:
                        Observable.Return(RxUnit.Default).InvokeCommand(ParseRename);
                        break;
                    case Key.Escape:
                        Observable.Return(RxUnit.Default).InvokeCommand(Close);
                        break;
                }
            }
        );

        this.WhenActivated(disposables =>
        {
            this.WhenAnyValue(x => x.Original)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(o => Renamed = o)
                .DisposeWith(disposables);

            _isValidHelper = this.WhenAnyValue(x => x.Renamed)
                .Select(r =>
                    !string.IsNullOrWhiteSpace(r)
                    && !browserViewModel.DataViews.Any(x =>
                        x.Title.Equals(r, StringComparison.CurrentCultureIgnoreCase)
                    )
                )
                .ToProperty(this, x => x.IsValid, scheduler: RxApp.MainThreadScheduler)
                .DisposeWith(disposables);
        });
    }

    private RxCommand CreateCommandClose()
    {
        CloseInteraction.RegisterHandler(ctx => ctx.SetOutput(RxUnit.Default));
        return ReactiveCommand.CreateFromObservable(() => CloseInteraction.Handle(RxUnit.Default));
    }

    private RxCommand CreateCommandParseRename(BrowserViewModel browserViewModel, RxCommand close)
    {
        var cmd = ReactiveCommand.Create(
            () => browserViewModel.UpdateTitle(Original, Renamed),
            this.WhenAnyValue(x => x.IsValid).Where(x => x)
        );
        cmd.InvokeCommand(close);
        return cmd;
    }

    public RxCommand ParseRename { get; }
    public RxCommand Close { get; }
    public RxInteraction CloseInteraction { get; } = new(RxApp.MainThreadScheduler);
}
