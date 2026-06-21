namespace Murmur.Core.Audio;

/// <summary>
/// A selectable microphone (capture endpoint). <see cref="Id"/> is the stable WASAPI device
/// id stored in settings; <see cref="Name"/> is the friendly name shown to the user.
/// </summary>
/// <param name="Id">Stable WASAPI endpoint id, or <c>null</c> for the system default mic.</param>
/// <param name="Name">Friendly display name.</param>
public sealed record AudioDeviceInfo(string? Id, string Name);
