using System;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Input;
using LanguageExt;
using Polly;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using SdmxDl.Browser.Models;
using SdmxDl.Client;
using SdmxDl.Client.Models;
using Sdmxdl.Grpc;

namespace SdmxDl.Browser.ViewModels;

public class SeriesFinderViewModel : BaseViewModel
{
    [Reactive]
    public string? Query { get; set; }

    public ReactiveCommand<string?, Seq<SeriesRequest>> ParseQuery { get; }
    public RxCommand Close { get; }
    public RxInteraction CloseInteraction { get; } = new(RxApp.MainThreadScheduler);
    public ReactiveCommand<KeyEventArgs, RxUnit> CheckKeyboardInput { get; }

    public SeriesFinderViewModel(
        ClientFactory clientFactory,
        ResiliencePipeline pipeline,
        BrowserViewModel browserViewModel
    )
    {
        ParseQuery = CreateCommandParseQuery(clientFactory);
        Close = ReactiveCommand.CreateFromObservable(() => CloseInteraction.Handle(RxUnit.Default));
        CheckKeyboardInput = ReactiveCommand.CreateRunInBackground(
            (KeyEventArgs args) =>
            {
                switch (args.Key)
                {
                    case Key.Enter:
                        Observable.Return(Query).InvokeCommand(ParseQuery);
                        break;
                    case Key.Escape:
                        Observable.Return(RxUnit.Default).InvokeCommand(Close);
                        break;
                }
            }
        );

        this.WhenActivated(disposables =>
        {
            ParseQuery
                .Select(seq =>
                    !seq.IsEmpty ? Observable.Return(RxUnit.Default) : Observable.Empty<RxUnit>()
                )
                .Switch()
                .InvokeCommand(Close)
                .DisposeWith(disposables);

            ParseQuery
                .Select(seq =>
                    !seq.IsEmpty ? Observable.Return(seq) : Observable.Empty<Seq<SeriesRequest>>()
                )
                .Switch()
                .InvokeCommand(browserViewModel, x => x.SendResults)
                .DisposeWith(disposables);
        });
    }

    private ReactiveCommand<string?, Seq<SeriesRequest>> CreateCommandParseQuery(
        ClientFactory clientFactory
    )
    {
        var canParse = this.WhenAnyValue(x => x.Query)
            .Select(CheckInput)
            .ObserveOn(RxApp.MainThreadScheduler);

        var cmd = ReactiveCommand.CreateFromTask(
            (string? s) => ParseQueriesImpl(s, clientFactory),
            canParse
        );

        return cmd;
    }

    private static bool CheckInput(string? input) =>
        !string.IsNullOrWhiteSpace(input) && SplitInput(input).All(CheckInputPart);

    private static bool CheckInputPart(string? input) =>
        !string.IsNullOrWhiteSpace(input)
        && (
            (
                input.Split(' ').Length == 3
                && input.Split(' ').All(x => !string.IsNullOrWhiteSpace(x))
            )
            || (
                input.StartsWith("sdmx-dl:/")
                && input.Split('/').Length == 4
                && input.Split('/').All(x => !string.IsNullOrWhiteSpace(x))
            )
        );

    private static Seq<string> SplitInput(string input) => input.Split(';').ToSeq().Strict();

    private static Seq<string> SplitPart(string input)
    {
        return input.StartsWith("sdmx-dl:/")
            ? input.Split('/').Skip(1).ToSeq()
            : input.Split(' ').ToSeq();
    }

    internal static async Task<Seq<SeriesRequest>> ParseQueriesImpl(
        string? input,
        ClientFactory clientFactory
    )
    {
        if (!CheckInput(input))
            return Seq<SeriesRequest>.Empty;

        return (
            await Task.WhenAll(
                SplitInput(input!).Select(part => ParseQueryImpl(part, clientFactory))
            )
        )
            .ToSeq()
            .Somes();
    }

    private static async Task<Option<SeriesRequest>> ParseQueryImpl(
        string? input,
        ClientFactory clientFactory
    )
    {
        if (!CheckInputPart(input))
            return Option<SeriesRequest>.None;

        var client = clientFactory.GetClient();
        var split = SplitPart(input!);
        var source = (await client.GetSources(CancellationToken.None)).Find(s =>
            s.Id.Equals(split[0])
        );

        var flow = await source.MatchAsync(
            async s =>
                (await client.GetDataFlows(s, CancellationToken.None)).Find(f =>
                    f.Ref.Equals(split[1])
                ),
            () => Option<DataFlow>.None
        );

        return from s in source
            from f in flow
            select new SeriesRequest(s, f, split[2]);
    }
}
