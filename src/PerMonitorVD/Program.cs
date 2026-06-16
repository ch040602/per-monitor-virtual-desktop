using PerMonitorVD.App;
using PerMonitorVD.Utilities;

namespace PerMonitorVD;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        if (SynchronizationContext.Current is null)
            SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());

        using var singleInstance = new SingleInstanceGuard("Local\\PerMonitorVD.App");
        if (!singleInstance.IsOwner)
        {
            return;
        }

        using var context = new PerMonitorVdApplicationContext(args);
        Application.Run(context);
    }
}
