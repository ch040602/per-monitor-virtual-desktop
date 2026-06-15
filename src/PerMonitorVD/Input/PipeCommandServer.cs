using System.IO.Pipes;
using PerMonitorVD.Core;
using PerMonitorVD.Utilities;

namespace PerMonitorVD.Input;

public sealed class PipeCommandServer : IDisposable
{
    public const string PipeName = "PerMonitorVD.Command";

    private readonly CommandRouter _router;
    private readonly CancellationTokenSource _cts = new();
    private Task? _task;

    public PipeCommandServer(CommandRouter router)
    {
        _router = router;
    }

    public void Start()
    {
        _task = Task.Run(RunAsync);
    }

    private async Task RunAsync()
    {
        while (!_cts.IsCancellationRequested)
        {
            try
            {
                await using var server = new NamedPipeServerStream(
                    PipeName,
                    PipeDirection.InOut,
                    maxNumberOfServerInstances: 1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);

                await server.WaitForConnectionAsync(_cts.Token);

                using var reader = new StreamReader(server, leaveOpen: true);
                await using var writer = new StreamWriter(server, leaveOpen: true) { AutoFlush = true };

                var line = await reader.ReadLineAsync(_cts.Token);
                if (line is null)
                    continue;

                var command = CommandParser.Parse(line);
                var response = await _router.ExecuteAsync(command);
                await writer.WriteLineAsync(response);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Pipe command server failed; restarting listener.");
                await Task.Delay(250);
            }
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        try { _task?.Wait(500); } catch { }
        _cts.Dispose();
    }
}
