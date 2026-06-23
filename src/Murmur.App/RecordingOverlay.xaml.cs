using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace Murmur.App;

/// <summary>
/// A small floating status pill shown while Murmur is listening or transcribing, so the user
/// always has a visible cue that recording is active.
///
/// Critically, the overlay never takes focus and never intercepts clicks: it sets the
/// WS_EX_NOACTIVATE and WS_EX_TRANSPARENT styles so the app the user is typing into stays the
/// foreground window (otherwise injection would target the overlay instead of their app).
/// </summary>
public partial class RecordingOverlay : Window
{
    private const int GwlExStyle = -20;
    private const int WsExTransparent = 0x00000020;
    private const int WsExNoActivate = 0x08000000;
    private const int WsExToolWindow = 0x00000080;

    private readonly DispatcherTimer _transcribeTimer;
    private readonly Stopwatch _transcribeStopwatch = new();

    public RecordingOverlay()
    {
        InitializeComponent();

        // Ticks while transcribing so a slow model shows elapsed time instead of looking frozen.
        _transcribeTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _transcribeTimer.Tick += (_, _) =>
            StatusText.Text = $"Transcribing… {_transcribeStopwatch.Elapsed.TotalSeconds:0}s";
    }

    /// <summary>Shows the overlay in the "listening" state (red dot).</summary>
    public void ShowListening()
    {
        StopTranscribeTimer();
        SetState("Listening…", (Color)ColorConverter.ConvertFromString("#E53935"));
    }

    /// <summary>Shows the overlay in the "transcribing" state (amber dot) with an elapsed timer.</summary>
    public void ShowTranscribing()
    {
        SetState("Transcribing…", (Color)ColorConverter.ConvertFromString("#FBC02D"));
        _transcribeStopwatch.Restart();
        _transcribeTimer.Start();
    }

    /// <summary>Hides the overlay.</summary>
    public void HideOverlay()
    {
        StopTranscribeTimer();
        Hide();
    }

    private void StopTranscribeTimer()
    {
        _transcribeTimer.Stop();
        _transcribeStopwatch.Reset();
    }

    private void SetState(string text, Color dotColor)
    {
        StatusText.Text = text;
        StatusDot.Fill = new SolidColorBrush(dotColor);
        if (!IsVisible)
        {
            Show();
        }

        PositionBottomCenter();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        // Make the window click-through and non-activating so it can't steal foreground focus.
        var helper = new WindowInteropHelper(this);
        var style = GetWindowLong(helper.Handle, GwlExStyle);
        _ = SetWindowLong(
            helper.Handle,
            GwlExStyle,
            style | WsExTransparent | WsExNoActivate | WsExToolWindow);
    }

    private void PositionBottomCenter()
    {
        var area = SystemParameters.WorkArea;
        Left = area.Left + ((area.Width - ActualWidth) / 2);
        Top = area.Bottom - ActualHeight - 60;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
}
