using System.Linq;
using System.Reactive.Linq;
using LanguageExt;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using SdmxDl.Browser.Models;

namespace SdmxDl.Browser.Infrastructure;

public record SeriesRequestMenuItem : ReactiveRecord
{
    public string Title { get; protected init; }
    public string CopyContent { get; }
    public ReactiveCommand<string, RxUnit> CopyCommand { get; }

    public SeriesRequestMenuItem()
    {
        Title = string.Empty;
        CopyContent = string.Empty;
        CopyCommand = ReactiveCommand.Create((string _) => RxUnit.Default);
    }

    public SeriesRequestMenuItem(
        string title,
        ReactiveCommand<string, RxUnit> copyCommand,
        string copyContent
    )
    {
        Title = string.IsNullOrWhiteSpace(copyContent) ? title : $"{title} ({copyContent})";
        CopyCommand = copyCommand;
        CopyContent = copyContent;
    }
}

public partial record SeriesRequestRootMenuItem : SeriesRequestMenuItem
{
    public SeriesRequestRootMenuItem(SeriesRequest seriesRequest)
    {
        Title = seriesRequest.Title;

        _commandsHelper = this.WhenAnyValue(x => x.CopyToClipboard)
            .Select(cmd =>
            {
                if (cmd is null)
                    return Seq<SeriesRequestMenuItem>.Empty;

                return Seq.create(
                        new SeriesRequestMenuItem("SDMX-DL URI", cmd, seriesRequest.Uri),
                        new SeriesRequestMenuItem("Source", cmd, seriesRequest.SourceId),
                        new SeriesRequestMenuItem("Flow", cmd, seriesRequest.FlowRef),
                        new SeriesRequestMenuItem("Key", cmd, seriesRequest.FullKey),
                        new SeriesRequestMenuItem("Source Flow Key", cmd, seriesRequest.Title),
                        new SeriesRequestMenuItem(
                            "Fetch data command",
                            cmd,
                            seriesRequest.FetchData
                        ),
                        new SeriesRequestMenuItem(
                            "Fetch meta command",
                            cmd,
                            seriesRequest.FetchMeta
                        ),
                        new SeriesRequestMenuItem(
                            "Fetch keys command",
                            cmd,
                            seriesRequest.FetchKeys
                        )
                    )
                    .Strict();
            })
            .ToProperty(this, x => x.Commands, scheduler: RxApp.MainThreadScheduler);
    }

    [Reactive]
    public partial ReactiveCommand<string, RxUnit>? CopyToClipboard { get; set; }

    [ObservableAsProperty]
    private Seq<SeriesRequestMenuItem> _commands;
}
