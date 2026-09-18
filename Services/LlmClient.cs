using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace BabelFFXIV.Services;

/// <summary>Minimal OpenAI-compatible chat-completions client.</summary>
public sealed class LlmClient : IDisposable
{
    private const string ApiKeyPlaceholder = "$API_KEY$";

    private readonly HttpClient http = new() { Timeout = TimeSpan.FromSeconds(20) };
    private readonly JsonSerializerOptions jsonOpts = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public void Configure(string baseUrl, string apiKey)
    {
        var root = baseUrl.TrimEnd('/');
        http.BaseAddress = new Uri(root + "/");
        http.DefaultRequestHeaders.Authorization = string.IsNullOrWhiteSpace(apiKey)
            ? null
            : new AuthenticationHeaderValue("Bearer", apiKey);
    }

    public static string SystemPrompt(string nativeLanguage) =>
        "You are a real-time translator inside the chat of the online game Final Fantasy XIV. " +
        "Translate the user's message to " + nativeLanguage + ". " +
        "Preserve the speaker's tone and register (casual game chat). Keep game terms, item names, " +
        "emotes inside angle brackets like <t>, and names exactly as written. " +
        "If the message is already in " + nativeLanguage + ", or is not translatable text, reply with it unchanged. " +
        "Reply with ONLY the translation - no explanations, no quotes, no notes.";

    /// <summary>Translate one message. Returns the translated text.</summary>
    public async Task<string> TranslateAsync(string model, string systemPrompt, string message)
    {
        var payload = new
        {
            model,
            temperature = 0.1,
            messages = new object[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = message },
            },
        };

        using var content = new StringContent(JsonSerializer.Serialize(payload, jsonOpts), Encoding.UTF8, "application/json");
        using var resp = await http.PostAsync("chat/completions", content).ConfigureAwait(false);
        var body = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);

        if (!resp.IsSuccessStatusCode)
            throw new HttpRequestException($"LLM API {(int)resp.StatusCode}: {Truncate(body, 300)}");

        using var doc = JsonDocument.Parse(body);
        var text = doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();

        return (text ?? string.Empty).Trim();
    }

    /// <summary>Quick connectivity/model test used by the config window.</summary>
    public async Task<string> TestAsync(string model, string apiKey)
    {
        if (apiKey == ApiKeyPlaceholder)
            throw new InvalidOperationException("Set your API key first.");
        return await TranslateAsync(model, SystemPrompt("English"), "Hello, nice to meet you!").ConfigureAwait(false);
    }

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max] + "…";

    public void Dispose() => http.Dispose();
}
