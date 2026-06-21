using Murmur.Core.Injection;

namespace Murmur.Core.Tests.Fakes;

/// <summary>In-memory <see cref="IClipboardAccess"/> that records every operation in order.</summary>
public sealed class FakeClipboardAccess : IClipboardAccess
{
    public string? Current { get; set; }

    /// <summary>Ordered log of operations, e.g. <c>get</c>, <c>set:hello</c>, <c>clear</c>.</summary>
    public List<string> Log { get; } = new();

    public string? GetText()
    {
        Log.Add("get");
        return Current;
    }

    public void SetText(string text)
    {
        Log.Add($"set:{text}");
        Current = text;
    }

    public void Clear()
    {
        Log.Add("clear");
        Current = null;
    }
}
