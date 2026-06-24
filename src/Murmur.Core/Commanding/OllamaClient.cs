using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace Murmur.Core.Commanding;

/// <summary>
/// <see cref="IOllamaClient"/> backed by the local Ollama HTTP API (<c>/api/generate</c>).
/// Endpoint and model are resolved per call from settings.
/// </summary>
public sealed class OllamaClient : IOllamaClient
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMinutes(5) };

    private readonly Func<string> _endpointProvider;
    private readonly Func<string> _modelProvider;

    public OllamaClient(Func<string> endpointProvider, Func<string> modelProvider)
    {
        _endpointProvider = endpointProvider ?? throw new ArgumentNullException(nameof(endpointProvider));
        _modelProvider = modelProvider ?? throw new ArgumentNullException(nameof(modelProvider));
    }

    /// <inheritdoc />
    public async Task<string> RewriteAsync(
        string instruction, string selectedText, CancellationToken cancellationToken = default)
    {
        var endpoint = _endpointProvider().TrimEnd('/');
        var model = _modelProvider();
        var url = $"{endpoint}/api/generate";

        var request = new GenerateRequest(model, BuildPrompt(instruction, selectedText), false);

        HttpResponseMessage response;
        try
        {
            response = await Http.PostAsJsonAsync(url, request, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new OllamaUnavailableException(
                "Couldn't reach Ollama. Make sure it's installed and running (`ollama serve`).", ex);
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new OllamaUnavailableException(
                $"Ollama returned {(int)response.StatusCode}. Is the model '{model}' installed (`ollama pull {model}`)?");
        }

        var result = await response.Content
            .ReadFromJsonAsync<GenerateResponse>(cancellationToken)
            .ConfigureAwait(false);

        return (result?.Response ?? string.Empty).Trim();
    }

    /// <summary>
    /// Builds a prompt that instructs the model to return ONLY the edited text, so the result can
    /// be pasted straight back over the selection.
    /// </summary>
    internal static string BuildPrompt(string instruction, string selectedText)
        => "You are a writing assistant inside a dictation app. Apply the user's instruction to "
            + "the text and reply with ONLY the resulting text — no preamble, no explanations, no "
            + "quotation marks.\n\n"
            + $"Instruction: {instruction}\n\n"
            + $"Text:\n{selectedText}\n\n"
            + "Result:";

    private sealed record GenerateRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("prompt")] string Prompt,
        [property: JsonPropertyName("stream")] bool Stream);

    private sealed record GenerateResponse(
        [property: JsonPropertyName("response")] string? Response);
}
