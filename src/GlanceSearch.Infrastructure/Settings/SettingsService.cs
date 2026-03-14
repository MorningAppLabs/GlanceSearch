using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using GlanceSearch.Shared;
using GlanceSearch.Shared.Models;

namespace GlanceSearch.Infrastructure.Settings;

/// <summary>
/// Manages application settings as JSON file in AppData.
/// </summary>
public class SettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private AppSettings _current = new();

    /// <summary>
    /// Current settings.
    /// </summary>
    public AppSettings Current => _current;

    /// <summary>
    /// Load settings from disk. Creates default settings file if missing.
    /// </summary>
    public AppSettings Load()
    {
        try
        {
            var path = Constants.SettingsFilePath;
            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                _current = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
                _current.Translation.ApiKey = SecurityHelper.Decrypt(_current.Translation.EncryptedApiKey);
            }
            else
            {
                _current = new AppSettings();
                Save(); // Create default file
            }
        }
        catch
        {
            // If settings file is corrupted, use defaults
            _current = new AppSettings();
        }

        return _current;
    }

    /// <summary>
    /// Save current settings to disk.
    /// </summary>
    public void Save()
    {
        try
        {
            var directory = Path.GetDirectoryName(Constants.SettingsFilePath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            _current.Translation.EncryptedApiKey = SecurityHelper.Encrypt(_current.Translation.ApiKey);

            var json = JsonSerializer.Serialize(_current, JsonOptions);
            File.WriteAllText(Constants.SettingsFilePath, json);
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "Failed to save settings");
        }
    }

    /// <summary>
    /// Reset settings to defaults.
    /// </summary>
    public void Reset()
    {
        _current = new AppSettings();
        Save();
    }

    /// <summary>
    /// Get the search URL for the configured search engine.
    /// </summary>
    public string GetSearchUrl(string query)
    {
        var engine = _current.General.SearchEngine.ToLowerInvariant();
        var encodedQuery = Uri.EscapeDataString(query);

        if (engine == "custom" && !string.IsNullOrEmpty(_current.General.CustomSearchUrl))
        {
            return string.Format(_current.General.CustomSearchUrl, encodedQuery);
        }

        if (Constants.SearchEngineUrls.TryGetValue(engine, out var urlTemplate))
        {
            return string.Format(urlTemplate, encodedQuery);
        }

        // Default to Google
        return string.Format(Constants.SearchEngineUrls["google"], encodedQuery);
    }
}
