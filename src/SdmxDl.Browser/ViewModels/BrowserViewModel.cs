using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia.Controls;
using ReactiveUI;
using SdmxDl.Client;

namespace SdmxDl.Browser.ViewModels;

public partial class BrowserViewModel : BaseViewModel
{
    [Reactive]
    public partial bool ServerIsRunning { get; set; }

    public RxCommand LaunchServer { get; }
    public RxInteraction LaunchServerInteraction { get; } = new(RxApp.MainThreadScheduler);

    public BrowserViewModel(ClientFactory clientFactory)
    {
        LaunchServer = ReactiveCommand.CreateFromObservable(
            () => LaunchServerInteraction.Handle(RxUnit.Default)
        );

        this.WhenActivated(disposables =>
        {
            this.WhenAnyValue(x => x.ServerIsRunning)
                .Subscribe(b =>
                {
                    Console.WriteLine(b);
                })
                .DisposeWith(disposables);
        });
    }
}
