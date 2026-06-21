using NAudio.CoreAudioApi;

namespace Murmur.Core.Audio;

/// <summary>
/// Enumerates active capture endpoints via WASAPI (NAudio), matching the device ids used by
/// <see cref="NAudioWasapiCapture"/> so a selection round-trips correctly.
/// </summary>
public sealed class NAudioDeviceEnumerator : IAudioDeviceEnumerator
{
    /// <inheritdoc />
    public IReadOnlyList<AudioDeviceInfo> GetCaptureDevices()
    {
        var devices = new List<AudioDeviceInfo>
        {
            new(null, "System default microphone"),
        };

        try
        {
            using var enumerator = new MMDeviceEnumerator();
            foreach (var device in enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active))
            {
                devices.Add(new AudioDeviceInfo(device.ID, device.FriendlyName));
                device.Dispose();
            }
        }
        catch
        {
            // If enumeration fails we still return the default-mic option so the UI works.
        }

        return devices;
    }
}
