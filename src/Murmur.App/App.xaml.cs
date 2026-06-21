using System.Threading;
using System.Windows;

namespace Murmur.App;

/// <summary>
/// Application entry point. Murmur runs as a tray-only app: on startup it builds the
/// composition root (which wires services and shows the tray icon) and otherwise stays
/// out of the way until the hotkey is pressed.
/// </summary>
public partial class App : Application
{
    private const string SingleInstanceMutexName = "Global\\Murmur.SingleInstance";

    private Mutex? _singleInstanceMutex;
    private CompositionRoot? _root;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Murmur is a tray app, so an old instance can linger after a terminal closes.
        // Refuse to start a second one — two instances would fight over the model download
        // and the global hotkey.
        _singleInstanceMutex = new Mutex(initiallyOwned: true, SingleInstanceMutexName, out var isNew);
        if (!isNew)
        {
            MessageBox.Show(
                "Murmur is already running. Look for its icon in the system tray.",
                "Murmur",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            Shutdown();
            return;
        }

        _root = new CompositionRoot();
        _root.ExitRequested += (_, _) => Shutdown();
        _root.Start();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _root?.Dispose();
        _singleInstanceMutex?.Dispose();
        base.OnExit(e);
    }
}
