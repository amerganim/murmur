using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using Murmur.Core.Audio;
using Murmur.Core.Settings;

namespace Murmur.App;

/// <summary>
/// Settings dialog: hotkey, activation mode, microphone, model, language, and start-with-Windows.
/// Edits the supplied <see cref="MurmurSettings"/> in place only when the user clicks Save
/// (Cancel leaves it untouched). The caller is responsible for persisting and applying changes.
/// </summary>
public partial class SettingsWindow : Window
{
    private readonly MurmurSettings _settings;
    private readonly string _originalModel;

    public SettingsWindow(MurmurSettings settings, IReadOnlyList<AudioDeviceInfo> devices)
    {
        _settings = settings;
        _originalModel = settings.ModelName;

        InitializeComponent();

        HotkeyCombo.ItemsSource = HotkeyOptions.All;
        HotkeyCombo.SelectedItem = FindOption(HotkeyOptions.All, settings.HotkeyVirtualKey)
            ?? HotkeyOptions.All[0];

        ModeCombo.ItemsSource = new[]
        {
            new NamedOption<HotkeyMode>("Push to talk (hold)", HotkeyMode.PushToTalk),
            new NamedOption<HotkeyMode>("Toggle (press to start/stop)", HotkeyMode.Toggle),
        };
        ModeCombo.SelectedIndex = settings.HotkeyMode == HotkeyMode.Toggle ? 1 : 0;

        MicCombo.ItemsSource = devices;
        MicCombo.SelectedItem = SelectDevice(devices, settings.MicrophoneDeviceId);

        ModelCombo.ItemsSource = ModelOptions.All;
        ModelCombo.SelectedItem = FindOption(ModelOptions.All, settings.ModelName)
            ?? ModelOptions.All[1];

        LanguageCombo.ItemsSource = LanguageOptions.All;
        LanguageCombo.SelectedItem = FindOption(LanguageOptions.All, settings.Language)
            ?? LanguageOptions.All[0];

        TrimSilenceCheck.IsChecked = settings.TrimSilence;
        TerminalPunctuationCheck.IsChecked = settings.TerminalStripTrailingPunctuation;
        CustomWordsBox.Text = settings.CustomVocabulary;
        AutoStartCheck.IsChecked = settings.StartWithWindows;

        CommandModeCheck.IsChecked = settings.CommandModeEnabled;
        CommandHotkeyCombo.ItemsSource = HotkeyOptions.All;
        CommandHotkeyCombo.SelectedItem = FindOption(HotkeyOptions.All, settings.CommandModeHotkeyVirtualKey)
            ?? HotkeyOptions.All[2]; // Right Alt
        OllamaModelBox.Text = settings.OllamaModel;

        ModelCombo.SelectionChanged += OnModelSelectionChanged;
    }

    /// <summary>True when the saved model differs from the one in effect when the dialog opened.</summary>
    public bool ModelChanged { get; private set; }

    private static readonly HashSet<string> HeavyModels = new(StringComparer.OrdinalIgnoreCase)
    {
        "ggml-medium", "ggml-large-v3", "ggml-large-v3-turbo", "ggml-large-v3-turbo-q5_0",
    };

    private void OnModelSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var selected = (ModelCombo.SelectedItem as NamedOption<string>)?.Value;
        var changing = selected is not null && selected != _originalModel;
        var heavy = selected is not null && HeavyModels.Contains(selected);

        var lines = new List<string>();
        if (changing)
        {
            lines.Add("Murmur will download the new model the next time you dictate.");
        }

        if (heavy)
        {
            lines.Add("This model is very accurate but can take 30+ seconds per phrase on a "
                + "PC without a dedicated GPU. If dictation feels stuck, try Small.");
        }

        ModelHint.Text = string.Join(" ", lines);
        ModelHint.Visibility = lines.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        _settings.HotkeyVirtualKey = ((NamedOption<int>)HotkeyCombo.SelectedItem).Value;
        _settings.HotkeyMode = ((NamedOption<HotkeyMode>)ModeCombo.SelectedItem).Value;
        _settings.MicrophoneDeviceId = (MicCombo.SelectedItem as AudioDeviceInfo)?.Id;

        var chosenModel = ((NamedOption<string>)ModelCombo.SelectedItem).Value;
        ModelChanged = chosenModel != _originalModel;
        _settings.ModelName = chosenModel;

        _settings.Language = ((NamedOption<string>)LanguageCombo.SelectedItem).Value;
        _settings.TrimSilence = TrimSilenceCheck.IsChecked == true;
        _settings.TerminalStripTrailingPunctuation = TerminalPunctuationCheck.IsChecked == true;
        _settings.CustomVocabulary = CustomWordsBox.Text.Trim();
        _settings.StartWithWindows = AutoStartCheck.IsChecked == true;

        _settings.CommandModeEnabled = CommandModeCheck.IsChecked == true;
        _settings.CommandModeHotkeyVirtualKey = ((NamedOption<int>)CommandHotkeyCombo.SelectedItem).Value;
        var model = OllamaModelBox.Text.Trim();
        if (!string.IsNullOrEmpty(model))
        {
            _settings.OllamaModel = model;
        }

        DialogResult = true;
    }

    private static NamedOption<T>? FindOption<T>(IReadOnlyList<NamedOption<T>> options, T value)
    {
        foreach (var option in options)
        {
            if (EqualityComparer<T>.Default.Equals(option.Value, value))
            {
                return option;
            }
        }

        return null;
    }

    private static AudioDeviceInfo? SelectDevice(IReadOnlyList<AudioDeviceInfo> devices, string? id)
    {
        foreach (var device in devices)
        {
            if (device.Id == id)
            {
                return device;
            }
        }

        return devices.Count > 0 ? devices[0] : null;
    }
}
