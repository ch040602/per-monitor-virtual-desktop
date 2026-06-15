using System.IO.Pipes;

namespace pvdctl;

internal static class Program
{
    private const string PipeName = "PerMonitorVD.Command";

    private static async Task<int> Main(string[] args)
    {
        if (args.Length == 0 || args.Contains("--help") || args.Contains("-h"))
        {
            PrintHelp();
            return 0;
        }

        var commandLine = string.Join(' ', args.Select(QuoteIfNeeded));

        try
        {
            await using var pipe = new NamedPipeClientStream(".", PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await pipe.ConnectAsync(cts.Token);

            var writer = new StreamWriter(pipe, leaveOpen: true) { AutoFlush = true };
            var reader = new StreamReader(pipe, leaveOpen: true);

            await writer.WriteLineAsync(commandLine);
            var response = await reader.ReadToEndAsync(cts.Token);
            Console.WriteLine(response.TrimEnd());
            return response.StartsWith("ERR", StringComparison.OrdinalIgnoreCase) ? 2 : 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("ERR could not contact PerMonitorVD: " + ex.Message);
            return 1;
        }
    }

    private static string QuoteIfNeeded(string arg)
    {
        return arg.Contains(' ') ? "\"" + arg.Replace("\"", "\\\"") + "\"" : arg;
    }

    private static void PrintHelp()
    {
        Console.WriteLine("""
        pvdctl - PerMonitorVD command client

        Usage:
          pvdctl switch --monitor mouse --next
          pvdctl switch --monitor mouse --prev
          pvdctl switch --monitor mouse --workspace 2
          pvdctl move-window --monitor mouse --workspace 2
          pvdctl move-window --monitor mouse --next
          pvdctl refresh
          pvdctl repair
          pvdctl return-active
          pvdctl rescue-all
          pvdctl diagnostics
          pvdctl home
          pvdctl pause
          pvdctl resume
          pvdctl status
        """);
    }
}
