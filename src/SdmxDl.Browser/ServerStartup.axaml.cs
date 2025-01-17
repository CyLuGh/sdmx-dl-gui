using System.Reactive.Disposables;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using ReactiveUI;
using SdmxDl.Browser.ViewModels;
using Ursa.ReactiveUIExtension;

namespace SdmxDl.Browser;

public partial class ServerStartup : ReactiveUrsaView<SettingsViewModel>
{
    public ServerStartup()
    {
        InitializeComponent();

        this.WhenActivated(disposables =>
        {
            if (DataContext is SettingsViewModel svm)
            {
                svm.PickPathInteraction.RegisterHandler(async ctx =>
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

                            ctx.SetOutput(
                                files.Count == 1 ? files[0].Path.AbsolutePath : string.Empty
                            );
                        }
                    })
                    .DisposeWith(disposables);
            }
        });
    }

    private IStorageProvider? GetStorageProvider()
    {
        var topLevel = TopLevel.GetTopLevel(this);
        return topLevel?.StorageProvider;
    }
}
