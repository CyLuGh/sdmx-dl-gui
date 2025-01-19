using System;
using System.Reactive.Linq;
using Avalonia.Input;
using DynamicData;
using LanguageExt;
using ReactiveUI;
using SdmxDl.Browser.Models;
using SdmxDl.Client;

namespace SdmxDl.Browser.ViewModels;

public partial class SourceSelectorViewModel : BaseViewModel
{
    [Reactive]
    public partial bool IsSearching { get; set; }

    [Reactive]
    public partial string CurrentInput { get; set; }

    [Reactive]
    public partial SdmxWebSource? CurrentSelection { get; set; }

    [Reactive]
    public partial Option<SdmxWebSource> Selection { get; set; }

    [ObservableAsProperty]
    public partial Seq<SdmxWebSource> AllSources { get; }

    [ObservableAsProperty]
    public partial Seq<SdmxWebSource> CurrentSources { get; }

    public ReactiveCommand<RxUnit, Seq<SdmxWebSource>> RetrieveSources { get; }
    public ReactiveCommand<KeyEventArgs, RxUnit> CheckTextBoxInput { get; }
    public RxCommand ValidateSelection { get; }
    public RxCommand CancelSelection { get; }

    public SourceSelectorViewModel(ClientFactory clientFactory)
    {
        CurrentInput = string.Empty;

        RetrieveSources = CreateCommandRetrieveSources(clientFactory);
        ValidateSelection = CreateCommandValidateSelection();
        CancelSelection = ReactiveCommand.Create(() =>
        {
            IsSearching = false;
        });
        CheckTextBoxInput = CreateCommandCheckTextBoxInput();

        this.WhenAnyValue(x => x.AllSources)
            .CombineLatest(this.WhenAnyValue(x => x.CurrentInput))
            .Select(t =>
            {
                var (allSources, input) = t;
                return Filter(allSources, input);
            })
            .ToProperty(
                this,
                x => x.CurrentSources,
                out _currentSourcesHelper,
                scheduler: RxApp.MainThreadScheduler,
                initialValue: Seq<SdmxWebSource>.Empty
            );

        this.WhenAnyValue(x => x.CurrentInput)
            .CombineLatest(this.WhenAnyValue(x => x.CurrentSources).Where(s => s.Length > 0))
            .Throttle(TimeSpan.FromMilliseconds(200))
            .Select(t => t.Second[0])
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(s => CurrentSelection = s);
    }

    private RxCommand CreateCommandValidateSelection()
    {
        return ReactiveCommand.Create(
            () =>
            {
                Selection = (Option<SdmxWebSource>)CurrentSelection!;
                IsSearching = false;
            },
            this.WhenAnyValue(x => x.CurrentSelection)
                .Select(t => t is not null)
                .ObserveOn(RxApp.MainThreadScheduler)
        );
    }

    private ReactiveCommand<KeyEventArgs, RxUnit> CreateCommandCheckTextBoxInput()
    {
        return ReactiveCommand.Create(
            (KeyEventArgs args) =>
            {
                switch (args.Key)
                {
                    case Key.Return:
                        if (CurrentSelection is not null)
                            Observable.Return(RxUnit.Default).InvokeCommand(ValidateSelection);
                        break;

                    case Key.Escape:
                        Observable.Return(RxUnit.Default).InvokeCommand(CancelSelection);
                        break;

                    case Key.Up:
                        if (CurrentSources.IsEmpty)
                            return;
                        if (CurrentSelection is null)
                        {
                            CurrentSelection = CurrentSources[^1];
                        }
                        else
                        {
                            var position = CurrentSources.IndexOf((SdmxWebSource)CurrentSelection!);
                            if (position > 0)
                                CurrentSelection = CurrentSources[position - 1];
                        }

                        break;

                    case Key.Down:
                        if (CurrentSources.IsEmpty)
                            return;
                        if (CurrentSelection is null)
                        {
                            CurrentSelection = CurrentSources[0];
                        }
                        else
                        {
                            var position = CurrentSources.IndexOf((SdmxWebSource)CurrentSelection!);
                            if (position < CurrentSources.Length - 1)
                                CurrentSelection = CurrentSources[position + 1];
                        }

                        break;
                }
            }
        );
    }

    private ReactiveCommand<RxUnit, Seq<SdmxWebSource>> CreateCommandRetrieveSources(
        ClientFactory clientFactory
    )
    {
        var command = ReactiveCommand.CreateFromTask(async () =>
        {
            // var rawSources = new List<Sdmxdl.Format.Protobuf.Web.SdmxWebSource>();
            // using var response = clientFactory.GetClient().GetSources(new Empty());
            // while (await response.ResponseStream.MoveNext(CancellationToken.None))
            // {
            //     var source = response.ResponseStream.Current;
            //     rawSources.Add(source);
            // }
            //
            // return rawSources.Select(s => new SdmxWebSource(s)).ToSeq();

            return Seq.create(
                new SdmxWebSource()
                {
                    Driver = "test",
                    Endpoint = "",
                    Id = "AAA",
                    Confidentiality = Confidentiality.Public,
                },
                new SdmxWebSource()
                {
                    Driver = "test",
                    Endpoint = "",
                    Id = "CCC",
                    Confidentiality = Confidentiality.Public,
                },
                new SdmxWebSource()
                {
                    Driver = "test",
                    Endpoint = "",
                    Id = "BBB",
                    Confidentiality = Confidentiality.Public,
                }
            );
        });

        command.ToProperty(
            this,
            x => x.AllSources,
            out _allSourcesHelper,
            initialValue: Seq<SdmxWebSource>.Empty
        );

        return command;
    }

    private static Seq<SdmxWebSource> Filter(Seq<SdmxWebSource> allSources, string input)
    {
        return allSources
            .Where(s =>
                string.IsNullOrWhiteSpace(input)
                || s.Id.Contains(input, StringComparison.CurrentCultureIgnoreCase)
                || s.Aliases.Any(a => a.Contains(input, StringComparison.CurrentCultureIgnoreCase))
            )
            .OrderBy(s => s.Id)
            .ToSeq()
            .Strict();
    }
}
