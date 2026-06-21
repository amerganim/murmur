namespace Murmur.Core.Audio;

/// <summary>
/// Lists the microphones available for capture, for the settings window and first-run wizard.
/// </summary>
public interface IAudioDeviceEnumerator
{
    /// <summary>
    /// Returns the active capture devices. The first entry is always the "system default"
    /// option (with a <c>null</c> id) so the user can defer to Windows' default mic.
    /// </summary>
    IReadOnlyList<AudioDeviceInfo> GetCaptureDevices();
}
