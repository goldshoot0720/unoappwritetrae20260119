using Uno.UI.Hosting;
using unoappwritetrae20260119.Services;

namespace unoappwritetrae20260119;

internal class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // Kill other instances of this app to enforce single-instance
        StartupService.KillOtherInstances();

        App.InitializeLogging();

        var host = UnoPlatformHostBuilder.Create()
            .App(() => new App())
            .UseX11()
            .UseLinuxFrameBuffer()
            .UseMacOS()
            .UseWin32()
            .Build();

        host.Run();
    }
}
