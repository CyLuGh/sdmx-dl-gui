using System;
using System.Diagnostics.Contracts;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Input;
using DynamicData;
using LanguageExt;
using Polly;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using SdmxDl.Client;

namespace SdmxDl.Browser.ViewModels;

public abstract partial class SelectorViewModel<TData, TInput> : CancellableBaseViewModel
{
    [Pure]
    protected abstract Seq<TData> Filter(Seq<TData> all, string? input);

    [Pure]
    protected abstract Task<Seq<TData>> RetrieveDataImpl(TInput input, ClientFactory clientFactory);

    [Reactive]
    public partial bool IsSearching { get; set; }

    [Reactive]
    public partial string? CurrentInput { get; set; }

    [Reactive]
    public partial TData? CurrentSelection { get; set; }

    [Reactive]
    public partial Option<TData> Selection { get; set; }

    [ObservableAsProperty(ReadOnly = false, UseProtected = true)]
    private Seq<TData> _allData;

    [ObservableAsProperty(ReadOnly = false, UseProtected = true)]
    private Seq<TData> _currentSources;

    public ReactiveCommand<TInput, Seq<TData>> RetrieveData { get; }
    public ReactiveCommand<KeyEventArgs, RxUnit> CheckTextBoxInput { get; }
    public RxCommand ValidateSelection { get; }
    public RxCommand CancelSelection { get; }

    protected SelectorViewModel(ClientFactory clientFactory, ResiliencePipeline pipeline)
    {
        RetrieveData = CreateCommandRetrieveData(clientFactory, pipeline);

        ValidateSelection = CreateCommandValidateSelection();
        CancelSelection = ReactiveCommand.Create(() =>
        {
            IsSearching = false;
        });
        CheckTextBoxInput = CreateCommandCheckTextBoxInput();

        this.WhenAnyValue(x => x.AllData)
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(_ =>
            {
                Selection = Option<TData>.None;
            });

        _currentSourcesHelper = this.WhenAnyValue(x => x.AllData)
            .CombineLatest(this.WhenAnyValue(x => x.CurrentInput))
            .Select(t =>
            {
                var (allSources, input) = t;
                return Filter(allSources, input);
            })
            .ToProperty(
                this,
                x => x.CurrentSources,
                scheduler: RxApp.MainThreadScheduler,
                initialValue: Seq<TData>.Empty
            );

        this.WhenAnyValue(x => x.CurrentInput)
            .CombineLatest(this.WhenAnyValue(x => x.CurrentSources).Where(s => s.Length > 0))
            .Throttle(TimeSpan.FromMilliseconds(200))
            .Select(t => t.Second[0])
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(s => CurrentSelection = s);
    }

    private ReactiveCommand<TInput, Seq<TData>> CreateCommandRetrieveData(
        ClientFactory clientFactory,
        ResiliencePipeline pipeline
    )
    {
        var command = ReactiveCommand.CreateFromTask<TInput, Seq<TData>>(async input =>
        {
            return await pipeline.ExecuteAsync<Seq<TData>>(
                async token => await RetrieveDataImpl(input, clientFactory),
                CancellationToken.None
            );
        });
        _allDataHelper = command.ToProperty(
            this,
            x => x.AllData,
            scheduler: RxApp.MainThreadScheduler,
            initialValue: Seq<TData>.Empty
        );
        return command;
    }

    private RxCommand CreateCommandValidateSelection()
    {
        return ReactiveCommand.Create(
            () =>
            {
                Selection = (Option<TData>)CurrentSelection!;
                IsSearching = false;
            },
            this.WhenAnyValue(x => x.CurrentSelection)
                .Select(t => t is not null)
                .ObserveOn(RxApp.MainThreadScheduler)
        );
    }

    private void TryValidate()
    {
        if (CurrentSelection is not null)
            Observable.Return(RxUnit.Default).InvokeCommand(ValidateSelection);
    }

    private void TryUpwardSelection()
    {
        if (CurrentSources.IsEmpty)
            return;
        if (CurrentSelection is null)
        {
            CurrentSelection = CurrentSources[^1];
        }
        else
        {
            var position = CurrentSources.IndexOf(CurrentSelection!);
            if (position > 0)
                CurrentSelection = CurrentSources[position - 1];
        }
    }

    private void TryDownwardSelection()
    {
        if (CurrentSources.IsEmpty)
            return;
        if (CurrentSelection is null)
        {
            CurrentSelection = CurrentSources[0];
        }
        else
        {
            var position = CurrentSources.IndexOf(CurrentSelection!);
            if (position < CurrentSources.Length - 1)
                CurrentSelection = CurrentSources[position + 1];
        }
    }

    private ReactiveCommand<KeyEventArgs, RxUnit> CreateCommandCheckTextBoxInput()
    {
        return ReactiveCommand.Create(
            (KeyEventArgs args) =>
            {
                switch (args.Key)
                {
                    case Key.Return:
                        TryValidate();
                        break;

                    case Key.Escape:
                        Observable.Return(RxUnit.Default).InvokeCommand(CancelSelection);
                        break;

                    case Key.Up:
                        TryUpwardSelection();
                        break;

                    case Key.Down:
                        TryDownwardSelection();
                        break;
                }
            }
        );
    }
}
