namespace SdmxDl.Engine;

public interface IClient
{
    Task StartServer(string javaPath, string jarPath, CancellationToken token);

    Settings Settings { get; set; }
}
