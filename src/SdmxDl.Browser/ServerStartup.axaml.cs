using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using Avalonia.ReactiveUI;
using ReactiveUI;
using SdmxDl.Browser.ViewModels;

namespace SdmxDl.Browser;

public partial class ServerStartup : ReactiveUserControl<SettingsViewModel>
{
    public ServerStartup()
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
                .BindTo(this, v => v.DataContext)
                .DisposeWith(disposables);
        });
    }

    private void PopulateFromViewModel(
        ServerStartup view,
        SettingsViewModel viewModel,
        CompositeDisposable disposables
    )
    {
        viewModel
            .PickPathInteraction.RegisterHandler(async ctx =>
            {
                var sp = GetStorageProvider();
                if (sp is null)
                {
                    ctx.SetOutput(string.Empty);
                }
                else
                {
                    var files = await sp.OpenFilePickerAsync(
                        new FilePickerOpenOptions() { Title = ctx.Input }
                    );

                    ctx.SetOutput(files.Count == 1 ? files[0].Path.AbsolutePath : string.Empty);
                }
            })
            .DisposeWith(disposables);

        viewModel
            .CloseInteraction.RegisterHandler(ctx =>
            {
                ViewModelLocator.DialogManager.DismissDialog();
                ctx.SetOutput(RxUnit.Default);
            })
            .DisposeWith(disposables);
    }

    private IStorageProvider? GetStorageProvider()
    {
        var topLevel = TopLevel.GetTopLevel(this);
        return topLevel?.StorageProvider;
    }
}
