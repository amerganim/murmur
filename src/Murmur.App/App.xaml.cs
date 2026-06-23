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
        // and the global hotkey. When relaunching elevated we pass --wait-mutex so the new
        // instance briefly waits for the old one to exit and release the lock.
        var waitForMutex = e.Args.Contains("--wait-mutex");
        _singleInstanceMutex = new Mutex(initiallyOwned: false, SingleInstanceMutexName);

        var acquired = _singleInstanceMutex.WaitOne(TimeSpan.Zero);
        if (!acquired && waitForMutex)
        {
            acquired = _singleInstanceMutex.WaitOne(TimeSpan.FromSeconds(8));
        }

        if (!acquired)
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
        _root.RestartAsAdminRequested += OnRestartAsAdminRequested;
        _root.Start();
    }

    private void OnRestartAsAdminRequested(object? sender, EventArgs e)
    {
        if (ElevationHelper.TryRestartElevated())
        {
            // Release the single-instance lock so the elevated instance can take over, then exit.
            Shutdown();
        }
        else
        {
            MessageBox.Show(
                "Murmur could not restart as administrator (the prompt may have been cancelled).",
                "Murmur",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _root?.Dispose();
        if (_singleInstanceMutex is not null)
        {
            try
            {
                _singleInstanceMutex.ReleaseMutex();
            }
            catch (ApplicationException)
            {
                // Not the owner (never acquired) — nothing to release.
            }

            _singleInstanceMutex.Dispose();
        }

        base.OnExit(e);
    }
}
