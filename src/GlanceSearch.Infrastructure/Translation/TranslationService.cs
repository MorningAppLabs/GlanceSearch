using System.Net.Http;
using System.Text.Json;
using GlanceSearch.Shared.Models;
using Serilog;

namespace GlanceSearch.Infrastructure.Translation;

/// <summary>
/// Provides text translation using Google Translate (free, unofficial endpoint) by default,
/// or premium DeepL service with user's own API key.
/// Falls back to MyMemory API (5,000 chars/day) if Google Translate fails.
/// </summary>
public class TranslationService : IDisposable
{
    private readonly HttpClient _httpClient;
    private bool _disposed;

    /// <summary>
    /// Result of a translation operation.
    /// </summary>
    public class TranslationResult
    {
        public string TranslatedText { get; set; } = string.Empty;
        public string DetectedSourceLanguage { get; set; } = "unknown";
        public string TargetLanguage { get; set; } = "en";
        public bool Success { get; set; }
        public string? Error { get; set; }
    }

    /// <summary>
    /// Supported target languages with display names.
    /// </summary>
    public static readonly Dictionary<string, string> SupportedLanguages = new()
    {
        ["auto"] = "Auto-Detect",
        ["en"] = "English",
        ["es"] = "Spanish",
        ["fr"] = "French",
        ["de"] = "German",
        ["it"] = "Italian",
        ["pt"] = "Portuguese",
        ["ru"] = "Russian",
        ["ja"] = "Japanese",
        ["ko"] = "Korean",
        ["zh"] = "Chinese (Simplified)",
        ["ar"] = "Arabic",
        ["hi"] = "Hindi",
        ["tr"] = "Turkish",
        ["nl"] = "Dutch",
        ["pl"] = "Polish",
        ["sv"] = "Swedish",
        ["da"] = "Danish",
        ["fi"] = "Finnish",
        ["el"] = "Greek",
        ["cs"] = "Czech",
        ["ro"] = "Romanian",
        ["hu"] = "Hungarian",
        ["th"] = "Thai",
        ["vi"] = "Vietnamese",
        ["id"] = "Indonesian",
        ["ms"] = "Malay",
        ["uk"] = "Ukrainian",
        ["bn"] = "Bengali",
        ["ta"] = "Tamil",
        ["te"] = "Telugu",
    };

    public TranslationService()
    {
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(10)
        };
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "GlanceSearch/1.0");
    }

    /// <summary>
    /// Translate text using the configured service.
    /// </summary>
    public async Task<TranslationResult> TranslateAsync(
        string text,
        string targetLanguage,
        string sourceLanguage = "auto",
        TranslationSettings? settings = null)
    {
        if (string.IsNullOrWhiteSpace(text))
            return new TranslationResult { Success = false, Error = "No text to translate" };

        // Trim very long texts to avoid API limits
        var trimmedText = text.Length > 4500 ? text[..4500] : text;

        var service = settings?.Service ?? "free";

        try
        {
            return service switch
            {
                "deepl" when !string.IsNullOrEmpty(settings?.ApiKey)
                    => await TranslateWithDeepL(trimmedText, targetLanguage, sourceLanguage, settings!.ApiKey),
                _ => await TranslateWithGoogle(trimmedText, targetLanguage, sourceLanguage),
            };
        }
        catch (HttpRequestException ex)
        {
            Log.Warning(ex, "Translation network error");
            return new TranslationResult
            {
                Success = false,
                Error = "Network error — check your internet connection"
            };
        }
        catch (TaskCanceledException)
        {
            return new TranslationResult { Success = false, Error = "Translation timed out" };
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Translation failed");
            return new TranslationResult { Success = false, Error = $"Translation error: {ex.Message}" };
        }
    }

    /// <summary>
    /// Free translation via Google Translate (unofficial endpoint, no API key needed).
    /// Falls back to MyMemory if Google is unavailable.
    /// </summary>
    private async Task<TranslationResult> TranslateWithGoogle(
        string text, string targetLanguage, string sourceLanguage)
    {
        try
        {
            var sl = sourceLanguage == "auto" ? "auto" : sourceLanguage;
            var encodedText = Uri.EscapeDataString(text);
            var url = $"https://translate.googleapis.com/translate_a/single?client=gtx&sl={sl}&tl={targetLanguage}&dt=t&q={encodedText}";

            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            // Google returns: [[["translated","original",...],...],...]
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            var root = doc.RootElement;

            var sb = new System.Text.StringBuilder();
            if (root.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                var outerArray = root[0];
                foreach (var segment in outerArray.EnumerateArray())
                {
                    if (segment.ValueKind == System.Text.Json.JsonValueKind.Array &&
                        segment.GetArrayLength() > 0)
                    {
                        var part = segment[0].GetString();
                        if (!string.IsNullOrEmpty(part))
                            sb.Append(part);
                    }
                }
            }

            var translatedText = sb.ToString();
            if (string.IsNullOrWhiteSpace(translatedText))
                throw new Exception("Empty response from Google Translate");

            // Detected source language is in root[2] if available
            var detectedLang = sourceLanguage;
            if (root.ValueKind == System.Text.Json.JsonValueKind.Array &&
                root.GetArrayLength() > 2 &&
                root[2].ValueKind == System.Text.Json.JsonValueKind.String)
            {
                detectedLang = root[2].GetString() ?? sourceLanguage;
            }

            return new TranslationResult
            {
                TranslatedText = translatedText,
                DetectedSourceLanguage = detectedLang,
                TargetLanguage = targetLanguage,
                Success = true
            };
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Google Translate failed, falling back to MyMemory");
            return await TranslateWithMyMemory(text, targetLanguage, sourceLanguage);
        }
    }

    /// <summary>
    /// Fallback: MyMemory API (no API key needed, 5000 chars/day).
    /// https://mymemory.translated.net/doc/spec.php
    /// </summary>
    private async Task<TranslationResult> TranslateWithMyMemory(
        string text, string targetLanguage, string sourceLanguage)
    {
        var langPair = sourceLanguage == "auto"
            ? $"autodetect|{targetLanguage}"
            : $"{sourceLanguage}|{targetLanguage}";

        var encodedText = Uri.EscapeDataString(text);
        var url = $"https://api.mymemory.translated.net/get?q={encodedText}&langpair={langPair}";

        var response = await _httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var responseData = root.GetProperty("responseData");
        var translatedText = responseData.GetProperty("translatedText").GetString() ?? "";
        var match = responseData.GetProperty("match").GetDouble();

        // Detect source language from the response
        var detectedLang = sourceLanguage;
        if (root.TryGetProperty("matches", out var matches) && matches.GetArrayLength() > 0)
        {
            var firstMatch = matches[0];
            if (firstMatch.TryGetProperty("source", out var src))
                detectedLang = src.GetString() ?? sourceLanguage;
        }

        // MyMemory returns "NO QUERY SPECIFIED" or similar on errors
        if (translatedText.Contains("NO QUERY SPECIFIED", StringComparison.OrdinalIgnoreCase) ||
            translatedText.Contains("PLEASE SELECT TWO LANGUAGES", StringComparison.OrdinalIgnoreCase))
        {
            return new TranslationResult { Success = false, Error = "Translation service error" };
        }

        return new TranslationResult
        {
            TranslatedText = translatedText,
            DetectedSourceLanguage = detectedLang,
            TargetLanguage = targetLanguage,
            Success = true
        };
    }

    /// <summary>
    /// Premium translation via DeepL API (requires user's API key).
    /// </summary>
    private async Task<TranslationResult> TranslateWithDeepL(
        string text, string targetLanguage, string sourceLanguage, string apiKey)
    {
        // DeepL uses uppercase language codes
        var targetLang = targetLanguage.ToUpperInvariant();
        if (targetLang == "EN") targetLang = "EN-US";
        if (targetLang == "PT") targetLang = "PT-BR";

        var isFreePlan = apiKey.EndsWith(":fx");
        var baseUrl = isFreePlan
            ? "https://api-free.deepl.com/v2/translate"
            : "https://api.deepl.com/v2/translate";

        var request = new HttpRequestMessage(HttpMethod.Post, baseUrl);
        request.Headers.Add("Authorization", $"DeepL-Auth-Key {apiKey}");

        var formContent = new List<KeyValuePair<string, string>>
        {
            new("text", text),
            new("target_lang", targetLang),
        };

        if (sourceLanguage != "auto")
            formContent.Add(new("source_lang", sourceLanguage.ToUpperInvariant()));

        request.Content = new FormUrlEncodedContent(formContent);

        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);

        var translations = doc.RootElement.GetProperty("translations");
        var first = translations[0];
        var translatedText = first.GetProperty("text").GetString() ?? "";
        var detectedLang = first.TryGetProperty("detected_source_language", out var dsl)
            ? dsl.GetString()?.ToLowerInvariant() ?? "auto"
            : "auto";

        return new TranslationResult
        {
            TranslatedText = translatedText,
            DetectedSourceLanguage = detectedLang,
            TargetLanguage = targetLanguage,
            Success = true
        };
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _httpClient.Dispose();
    }
}
