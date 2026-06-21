namespace Murmur.Core.Injection;

/// <summary>
/// Outcome of an attempt to inject text into the foreground application.
/// </summary>
public sealed record InjectionResult(bool Success, string? FailureReason = null)
{
    /// <summary>A successful injection.</summary>
    public static InjectionResult Ok { get; } = new(true);

    /// <summary>A failed injection with an explanatory reason.</summary>
    public static InjectionResult Fail(string reason) => new(false, reason);
}
