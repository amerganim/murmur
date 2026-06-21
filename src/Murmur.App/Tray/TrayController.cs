using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Hardcodet.Wpf.TaskbarNotification;

namespace Murmur.App.Tray;

/// <summary>
/// Owns the system-tray icon: reflects the current <see cref="TrayState"/>, surfaces
/// balloon notifications, and exposes an Exit command. This is the user's only persistent
/// touch-point until the settings window arrives in Milestone 2.
/// </summary>
public sealed class TrayController : IDisposable
{
    private readonly TaskbarIcon _icon;
    private readonly BitmapImage _idleIcon = LoadIcon("murmur-idle.ico");
    private readonly BitmapImage _listeningIcon = LoadIcon("murmur-listening.ico");
    private readonly BitmapImage _busyIcon = LoadIcon("murmur-busy.ico");

    /// <summary>Raised when the user chooses Exit from the tray menu.</summary>
    public event EventHandler? ExitRequested;

    public TrayController()
    {
        _icon = new TaskbarIcon
        {
            IconSource = _idleIcon,
            ToolTipText = "Murmur — ready",
            ContextMenu = BuildContextMenu(),
        };

        SetState(TrayState.Idle);
    }

    /// <summary>Updates the tray icon and tooltip to reflect the given state.</summary>
    public void SetState(TrayState state)
    {
        _icon.Dispatcher.Invoke(() =>
        {
            (_icon.IconSource, _icon.ToolTipText) = state switch
            {
                TrayState.Listening => (_listeningIcon, "Murmur — listening…"),
                TrayState.Transcribing => (_busyIcon, "Murmur — transcribing…"),
                _ => (_idleIcon, "Murmur — ready"),
            };
        });
    }

    /// <summary>Shows an informational balloon (e.g. first-run model download).</summary>
    public void ShowInfo(string title, string message)
        => _icon.Dispatcher.Invoke(() => _icon.ShowBalloonTip(title, message, BalloonIcon.Info));

    /// <summary>Shows an error balloon so failures are never silent.</summary>
    public void ShowError(string title, string message)
        => _icon.Dispatcher.Invoke(() => _icon.ShowBalloonTip(title, message, BalloonIcon.Error));

    private ContextMenu BuildContextMenu()
    {
        var menu = new ContextMenu();

        var header = new MenuItem { Header = "Murmur", IsEnabled = false };
        menu.Items.Add(header);
        menu.Items.Add(new Separator());

        var exit = new MenuItem { Header = "Exit" };
        exit.Click += (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty);
        menu.Items.Add(exit);

        return menu;
    }

    private static BitmapImage LoadIcon(string fileName)
    {
        var uri = new Uri($"pack://application:,,,/Assets/{fileName}", UriKind.Absolute);
        var image = new BitmapImage();
        image.BeginInit();
        image.UriSource = uri;
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.EndInit();
        image.Freeze();
        return image;
    }

    public void Dispose() => _icon.Dispose();
}
