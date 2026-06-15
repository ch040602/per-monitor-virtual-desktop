using PerMonitorVD.Core;

namespace PerMonitorVD.Input;

public static class CommandParser
{
    public static WorkspaceCommand Parse(string commandLine)
    {
        var args = SplitArgs(commandLine).ToArray();
        if (args.Length == 0)
            return new WorkspaceCommand { Type = WorkspaceCommandType.Status };

        return Parse(args);
    }

    public static WorkspaceCommand Parse(string[] args)
    {
        if (args.Length == 0)
            return new WorkspaceCommand { Type = WorkspaceCommandType.Status };

        var verb = args[0].Trim().ToLowerInvariant();
        var monitor = ReadOption(args, "--monitor") ?? "mouse";

        return verb switch
        {
            "switch" => ParseSwitch(args, monitor),
            "move-window" => ParseMoveWindow(args, monitor),
            "repair" => new WorkspaceCommand { Type = WorkspaceCommandType.Repair },
            "refresh" => new WorkspaceCommand { Type = WorkspaceCommandType.Refresh },
            "return-active" => new WorkspaceCommand { Type = WorkspaceCommandType.ReturnActive },
            "rescue" => new WorkspaceCommand { Type = WorkspaceCommandType.RescueAll },
            "rescue-all" => new WorkspaceCommand { Type = WorkspaceCommandType.RescueAll },
            "diagnostics" => new WorkspaceCommand { Type = WorkspaceCommandType.Diagnostics },
            "home" => new WorkspaceCommand { Type = WorkspaceCommandType.ShowHome },
            "pause" => new WorkspaceCommand { Type = WorkspaceCommandType.Pause },
            "resume" => new WorkspaceCommand { Type = WorkspaceCommandType.Resume },
            "status" => new WorkspaceCommand { Type = WorkspaceCommandType.Status },
            _ => new WorkspaceCommand { Type = WorkspaceCommandType.Status }
        };
    }

    private static WorkspaceCommand ParseSwitch(string[] args, string monitor)
    {
        if (args.Contains("--next", StringComparer.OrdinalIgnoreCase))
            return new WorkspaceCommand { Type = WorkspaceCommandType.SwitchNext, MonitorTarget = monitor };

        if (args.Contains("--prev", StringComparer.OrdinalIgnoreCase))
            return new WorkspaceCommand { Type = WorkspaceCommandType.SwitchPrev, MonitorTarget = monitor };

        var workspace = ReadIntOption(args, "--workspace");
        if (workspace is not null)
            return new WorkspaceCommand { Type = WorkspaceCommandType.SwitchToIndex, WorkspaceIndex = workspace, MonitorTarget = monitor };

        return new WorkspaceCommand { Type = WorkspaceCommandType.Status };
    }

    private static WorkspaceCommand ParseMoveWindow(string[] args, string monitor)
    {
        if (args.Contains("--next", StringComparer.OrdinalIgnoreCase))
            return new WorkspaceCommand { Type = WorkspaceCommandType.MoveFocusedWindowNext, MonitorTarget = monitor };

        if (args.Contains("--prev", StringComparer.OrdinalIgnoreCase))
            return new WorkspaceCommand { Type = WorkspaceCommandType.MoveFocusedWindowPrev, MonitorTarget = monitor };

        var workspace = ReadIntOption(args, "--workspace");
        if (workspace is not null)
            return new WorkspaceCommand { Type = WorkspaceCommandType.MoveFocusedWindowToIndex, WorkspaceIndex = workspace, MonitorTarget = monitor };

        return new WorkspaceCommand { Type = WorkspaceCommandType.Status };
    }

    private static string? ReadOption(string[] args, string name)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                return args[i + 1];
        }

        return null;
    }

    private static int? ReadIntOption(string[] args, string name)
    {
        var value = ReadOption(args, name);
        return int.TryParse(value, out var parsed) ? parsed : null;
    }

    private static IEnumerable<string> SplitArgs(string commandLine)
    {
        var current = new System.Text.StringBuilder();
        var inQuotes = false;

        foreach (var ch in commandLine)
        {
            if (ch == '"')
            {
                inQuotes = !inQuotes;
                continue;
            }

            if (char.IsWhiteSpace(ch) && !inQuotes)
            {
                if (current.Length > 0)
                {
                    yield return current.ToString();
                    current.Clear();
                }
            }
            else
            {
                current.Append(ch);
            }
        }

        if (current.Length > 0)
            yield return current.ToString();
    }
}
