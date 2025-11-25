using System;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Microsoft.Extensions.Configuration;
using ReactiveUI;
using ReactiveUI.Avalonia;
using SdmxDl.Browser.ViewModels;
using Splat;
using SukiUI.Dialogs;
using SukiUI.Toasts;
using Velopack;

namespace SdmxDl.Browser;

public partial class Browser : ReactiveUserControl<BrowserViewModel>
{
    public Browser()
    {
        InitializeComponent();

        this.WhenActivated(disposables =>
        {
            this.WhenAnyValue(x => x.ViewModel)
                .WhereNotNull()
                .Do(vm => PopulateFromViewModel(this, vm, disposables))
                .Subscribe()
                .DisposeWith(disposables);

            this.WhenAnyValue(x => x.ViewModel)
                .BindTo(this, x => x.DataContext)
                .DisposeWith(disposables);
        });
    }

    private static void PopulateFromViewModel(
        Browser view,
        BrowserViewModel viewModel,
        CompositeDisposable disposables
    ) { }
}
