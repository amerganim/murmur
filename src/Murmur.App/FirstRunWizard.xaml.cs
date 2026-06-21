using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Murmur.Core.Audio;
using Murmur.Core.Models;
using Murmur.Core.Settings;
using Murmur.Core.Stt;

namespace Murmur.App;

/// <summary>
/// First-run setup: walks the user through microphone, model, hotkey, and a live dictation test.
/// Choices are written into the shared <see cref="MurmurSettings"/> as the user advances (so the
/// running pipeline picks them up immediately), the chosen model is downloaded and warmed on the
/// test step, and the user can dictate straight into the test box to confirm it works.
/// </summary>
public partial class FirstRunWizard : Window
{
    private readonly MurmurSettings _settings;
    private readonly WhisperModelProvider _modelProvider;
    private readonly WhisperSpeechToText _speechToText;
    private readonly Action<int> _applyHotkey;

    private readonly StackPanel[] _panels;
    private int _step;
    private bool _modelPrepared;

    public FirstRunWizard(
        MurmurSettings settings,
        IReadOnlyList<AudioDeviceInfo> devices,
        WhisperModelProvider modelProvider,
        WhisperSpeechToText speechToText,
        Action<int> applyHotkey)
    {
        _settings = settings;
        _modelProvider = modelProvider;
        _speechToText = speechToText;
        _applyHotkey = applyHotkey;

        InitializeComponent();

        _panels = new[] { PanelWelcome, PanelMic, PanelModel, PanelHotkey, PanelTest };

        MicCombo.ItemsSource = devices;
        MicCombo.SelectedIndex = 0;

        ModelCombo.ItemsSource = ModelOptions.All;
        ModelCombo.SelectedIndex = 1; // base

        HotkeyCombo.ItemsSource = HotkeyOptions.All;
        HotkeyCombo.SelectedIndex = 0; // Right Ctrl

        AutoStartCheck.IsChecked = _settings.StartWithWindows;

        ShowStep(0);
    }

    private async void OnNext(object sender, RoutedEventArgs e)
    {
        // Persist the choice made on the current step before advancing, so the live pipeline
        // reflects it during the test.
        CaptureCurrentStep();

        if (_step < _panels.Length - 1)
        {
            ShowStep(_step + 1);
            if (_step == _panels.Length - 1)
            {
                await PrepareModelAndEnableTestAsync();
            }
        }
    }

    private void OnBack(object sender, RoutedEventArgs e)
    {
        if (_step > 0)
        {
            ShowStep(_step - 1);
        }
    }

    private void OnFinish(object sender, RoutedEventArgs e)
    {
        CaptureCurrentStep();
        _settings.FirstRunCompleted = true;
        DialogResult = true;
    }

    private void CaptureCurrentStep()
    {
        switch (_step)
        {
            case 1:
                _settings.MicrophoneDeviceId = (MicCombo.SelectedItem as AudioDeviceInfo)?.Id;
                break;
            case 2:
                _settings.ModelName = ((NamedOption<string>)ModelCombo.SelectedItem).Value;
                break;
            case 3:
                _settings.HotkeyVirtualKey = ((NamedOption<int>)HotkeyCombo.SelectedItem).Value;
                _settings.StartWithWindows = AutoStartCheck.IsChecked == true;
                _applyHotkey(_settings.HotkeyVirtualKey);
                break;
        }
    }

    private void ShowStep(int step)
    {
        _step = step;
        for (var i = 0; i < _panels.Length; i++)
        {
            _panels[i].Visibility = i == step ? Visibility.Visible : Visibility.Collapsed;
        }

        var isLast = step == _panels.Length - 1;
        BackButton.IsEnabled = step > 0;
        NextButton.Visibility = isLast ? Visibility.Collapsed : Visibility.Visible;
        FinishButton.Visibility = isLast ? Visibility.Visible : Visibility.Collapsed;
    }

    private async Task PrepareModelAndEnableTestAsync()
    {
        if (_modelPrepared)
        {
            return;
        }

        var model = _settings.ModelName;
        var needsDownload = !File.Exists(_modelProvider.GetModelPath(model));

        try
        {
            if (needsDownload)
            {
                DownloadBar.Visibility = Visibility.Visible;
                TestStatus.Text = $"Downloading the '{model}' model. This happens once and stays on your PC.";
            }
            else
            {
                TestStatus.Text = "Loading your model…";
            }

            ModelDownloadProgress progress = (f, r, t) => Dispatcher.Invoke(() =>
            {
                if (f is { } fraction && t is { } total)
                {
                    DownloadBar.IsIndeterminate = false;
                    DownloadBar.Value = Math.Clamp(fraction * 100, 0, 100);
                }
                else
                {
                    DownloadBar.IsIndeterminate = true;
                }
            });

            await _modelProvider.EnsureModelAsync(model, progress).ConfigureAwait(true);
            await _speechToText.WarmUpAsync().ConfigureAwait(true);

            _modelPrepared = true;
            DownloadBar.Visibility = Visibility.Collapsed;
            TestStatus.Text = "You're all set!";
            TestHint.Text = $"Click in the box below, hold {HotkeyOptions.NameFor(_settings.HotkeyVirtualKey)}, "
                + "say something, then release. Your words should appear.";
            TestHint.Visibility = Visibility.Visible;
            TestBox.IsEnabled = true;
            TestBox.Focus();
        }
        catch (Exception ex)
        {
            DownloadBar.Visibility = Visibility.Collapsed;
            TestStatus.Text = $"Couldn't prepare the model: {ex.Message}. You can finish and try again later.";
        }
    }
}
