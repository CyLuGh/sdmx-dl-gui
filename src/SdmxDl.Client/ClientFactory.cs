﻿using CliWrap;
using CliWrap.Buffered;
using Grpc.Net.Client;
using SdmxDl.Client.Models;
using SdmxDl.Engine;
using Sdmxdl.Grpc;

namespace SdmxDl.Client;

public class ClientFactory
{
    private CancellationTokenSource HostProcessCancellationTokenSource { get; } = new();

    public SdmxWebManager.SdmxWebManagerClient GetClient()
    {
        var channel = GrpcChannel.ForAddress(
            string.IsNullOrEmpty(Settings.ServerUri) ? "http://localhost:4557" : Settings.ServerUri
        );
        return new(channel);
    }

    public Settings Settings { get; set; }

    public async Task StartServer(string javaPath, string jarPath)
    {
        var cmd = Cli.Wrap(javaPath).WithArguments(["-jar", jarPath]);

        var commandResults = await cmd.ExecuteBufferedAsync(
            HostProcessCancellationTokenSource.Token
        );

        if (!string.IsNullOrEmpty(commandResults.StandardError))
        {
            throw new SdmxDlServerException(
                $"SDMXDL server has encountered an exception:{System.Environment.NewLine}{commandResults.StandardError}"
            );
        }
    }

    public void StopServer()
    {
        HostProcessCancellationTokenSource.Cancel();
    }
}
