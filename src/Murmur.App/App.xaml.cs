using System.Windows;

namespace Murmur.App;

/// <summary>
/// Application entry point. Murmur runs as a tray-only app: on startup it builds the
/// composition root (which wires services and shows the tray icon) and otherwise stays
/// out of the way until the hotkey is pressed.
/// </summary>
public partial class App : Application
{
    private CompositionRoot? _root;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _root = new CompositionRoot();
        _root.ExitRequested += (_, _) => Shutdown();
        _root.Start();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _root?.Dispose();
        base.OnExit(e);
    }
}
