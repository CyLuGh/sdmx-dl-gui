using CliWrap;
using CliWrap.Buffered;
using Grpc.Net.Client;
using SdmxDl.Client.Models;
using Sdmxdl.Grpc;

namespace SdmxDl.Client;

public class ClientFactory
{
    public SdmxWebManager.SdmxWebManagerClient GetClient()
    {
        var channel = GrpcChannel.ForAddress(
            string.IsNullOrEmpty(Settings.ServerUri) ? "http://localhost:4567" : Settings.ServerUri
        );
        return new(channel);
    }

    public Settings Settings { get; set; }

    /*public async Task StartServer(string javaPath, string jarPath, CancellationToken token)
    {
        var cmd = Cli.Wrap(javaPath).WithArguments(["-jar", jarPath]);
        var cmd = Cli.Wrap(javaPath)
            .WithArguments(
                new []
                {
                    $"-Dquarkus.grpc.server.port={port}",
                    "-Dquarkus.http.host-enabled=false"
                }
            )
            .WithWorkingDirectory(...);
        
        var commandResults = await cmd.ExecuteBufferedAsync(token);

        if (!string.IsNullOrEmpty(commandResults.StandardError))
            throw new SdmxDlServerException(
                $"SDMXDL server has encountered an exception:{System.Environment.NewLine}{commandResults.StandardError}"
            );
    }*/
}
