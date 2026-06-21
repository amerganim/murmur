using System.Windows;

namespace Murmur.App;

/// <summary>
/// Modeless progress window shown while a Whisper model downloads (first run or after the user
/// picks a different model). Reports percentage and size; falls back to an indeterminate bar
/// when the server does not send a content length.
/// </summary>
public partial class ModelDownloadWindow : Window
{
    public ModelDownloadWindow(string modelName)
    {
        InitializeComponent();
        Title = $"Murmur — downloading {modelName}";
    }

    /// <summary>
    /// Updates the bar. Safe to call from any thread — marshals to the UI thread. Matches the
    /// <see cref="Murmur.Core.Models.ModelDownloadProgress"/> signature.
    /// </summary>
    public void Report(double? fraction, long received, long? total)
    {
        Dispatcher.Invoke(() =>
        {
            if (fraction is { } f && total is { } t)
            {
                Bar.IsIndeterminate = false;
                Bar.Value = Math.Clamp(f * 100, 0, 100);
                PercentText.Text = $"{f * 100:0}%  ({Mb(received)} / {Mb(t)} MB)";
            }
            else
            {
                Bar.IsIndeterminate = true;
                PercentText.Text = $"{Mb(received)} MB downloaded…";
            }
        });
    }

    private static long Mb(long bytes) => bytes / (1024 * 1024);
}
