using Polly;
using ReactiveUI.Fody.Helpers;
using SdmxDl.Client;
using SdmxDl.Client.Models;

namespace SdmxDl.Browser.ViewModels;

public class DataViewModel
{
    [Reactive]
    public SdmxWebSource Source { get; set; }

    [Reactive]
    public DataFlow Flow { get; set; }

    [Reactive]
    public string Key { get; set; }

    public static string BuildTitle(SdmxWebSource source, DataFlow flow, string key) =>
        $"{source.Id} {flow.Ref} {key}";

    public DataViewModel(ClientFactory clientFactory, ResiliencePipeline pipeline) { }
}
