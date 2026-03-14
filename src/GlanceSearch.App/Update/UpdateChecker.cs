using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using GlanceSearch.App.Theme;
using GlanceSearch.Infrastructure.Settings;
using GlanceSearch.Shared;
using Serilog;

namespace GlanceSearch.App.Update;

public class GitHubRelease
{
    [JsonPropertyName("tag_name")]
    public string TagName { get; set; } = "";

    [JsonPropertyName("html_url")]
    public string HtmlUrl { get; set; } = "";

    [JsonPropertyName("assets")]
    public List<GitHubAsset> Assets { get; set; } = [];
}

public class GitHubAsset
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("browser_download_url")]
    public string DownloadUrl { get; set; } = "";

    [JsonPropertyName("size")]
    public long Size { get; set; }
}

public static class UpdateChecker
{
    private static readonly HttpClient _httpClient = new();
    private static bool _downloading = false;

    static UpdateChecker()
    {
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "GlanceSearch-App");
        _httpClient.Timeout = TimeSpan.FromMinutes(10);
    }

    public static async Task CheckForUpdatesAsync(SettingsService settings, bool manualCheck = false)
    {
        if (!manualCheck && !settings.Current.General.CheckForUpdates)
            return;

        try
        {
            var url = "https://api.github.com/repos/MorningAppLabs/GlanceSearch/releases/latest";
            var release = await _httpClient.GetFromJsonAsync<GitHubRelease>(url);

            if (release == null || string.IsNullOrEmpty(release.TagName))
            {
                if (manualCheck)
                    ToastService.Show("Up to Date", $"You are running the latest version (v{Constants.AppVersion}).", ToastType.Success);
                return;
            }

            var latestVersionStr = release.TagName.TrimStart('v', 'V');
            if (!Version.TryParse(latestVersionStr, out var latestVersion) ||
                !Version.TryParse(Constants.AppVersion, out var currentVersion))
            {
                if (manualCheck)
                    ToastService.Show("Update Check", $"Latest: v{latestVersionStr}", ToastType.Info);
                return;
            }

            if (latestVersion > currentVersion)
            {
                // Try to find a .exe asset for direct download
                var exeAsset = release.Assets.FirstOrDefault(a =>
                    a.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase));

                if (exeAsset != null)
                {
                    var sizeMb = exeAsset.Size / (1024.0 * 1024.0);
                    ToastService.Show(
                        $"Update Available — v{latestVersion}",
                        $"Click to download and install ({sizeMb:F0} MB). The app will restart automatically.",
                        ToastType.Info,
                        () => _ = DownloadAndInstallAsync(release, exeAsset));
                }
                else
                {
                    // No .exe asset — direct user to GitHub releases page
                    var releaseUrl = string.IsNullOrEmpty(release.HtmlUrl)
                        ? "https://github.com/MorningAppLabs/GlanceSearch/releases"
                        : release.HtmlUrl;

                    ToastService.Show(
                        $"Update Available — v{latestVersion}",
                        "Click to open the GitHub release page and download manually.",
                        ToastType.Info,
                        () =>
                        {
                            try { Process.Start(new ProcessStartInfo(releaseUrl) { UseShellExecute = true }); }
                            catch { }
                        });
                }
            }
            else if (manualCheck)
            {
                ToastService.Show("Up to Date", $"You are running the latest version (v{Constants.AppVersion}).", ToastType.Success);
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to check for updates");
            if (manualCheck)
                ToastService.Show("Update Check Failed", "Could not reach GitHub. Check your internet connection.", ToastType.Error);
        }
    }

    /// <summary>
    /// Downloads the update .exe then launches a helper PowerShell script that replaces
    /// the running .exe while the app is closed and relaunches it.
    /// </summary>
    private static async Task DownloadAndInstallAsync(GitHubRelease release, GitHubAsset asset)
    {
        if (_downloading)
        {
            ToastService.Show("Already Downloading", "Please wait for the current download to finish.", ToastType.Info);
            return;
        }

        _downloading = true;

        try
        {
            var tempDir = Path.GetTempPath();
            var tempExePath = Path.Combine(tempDir, $"GlanceSearch-update-{release.TagName}.exe");
            var currentExePath = Process.GetCurrentProcess().MainModule?.FileName;

            if (string.IsNullOrEmpty(currentExePath))
            {
                ToastService.Show("Update Error", "Cannot locate current executable path.", ToastType.Error);
                return;
            }

            ToastService.Show("Downloading Update", $"Downloading GlanceSearch {release.TagName}… please wait.", ToastType.Info);
            Log.Information("Downloading update {Version} from {Url}", release.TagName, asset.DownloadUrl);

            using var response = await _httpClient.GetAsync(asset.DownloadUrl, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            await using var fileStream = File.Create(tempExePath);
            await using var downloadStream = await response.Content.ReadAsStreamAsync();

            var buffer = new byte[81920];
            long totalRead = 0;
            int read;
            while ((read = await downloadStream.ReadAsync(buffer)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, read));
                totalRead += read;
            }

            fileStream.Close();
            Log.Information("Update downloaded successfully: {Bytes} bytes", totalRead);

            // Detect whether the asset is a Setup installer (NSIS/Inno) or the raw app exe.
            // NSIS supports silent install with /S; raw exe replacement uses the PS copy approach.
            bool isInstaller = asset.Name.IndexOf("Setup", StringComparison.OrdinalIgnoreCase) >= 0
                            || asset.Name.IndexOf("Installer", StringComparison.OrdinalIgnoreCase) >= 0;

            var scriptPath = Path.Combine(tempDir, "glancesearch-updater.ps1");
            string scriptContent;

            if (isInstaller)
            {
                // Run the NSIS installer silently — it handles replacing the installed files
                scriptContent =
                    $"Start-Sleep -Seconds 2\r\n" +
                    $"try {{\r\n" +
                    $"    Start-Process '{tempExePath}' -ArgumentList '/S' -Wait -ErrorAction Stop\r\n" +
                    $"    # Re-launch the freshly installed app\r\n" +
                    $"    $installed = '{currentExePath}'\r\n" +
                    $"    if (Test-Path $installed) {{ Start-Process $installed }}\r\n" +
                    $"}} catch {{\r\n" +
                    $"    Start-Process 'https://github.com/MorningAppLabs/GlanceSearch/releases'\r\n" +
                    $"}} finally {{\r\n" +
                    $"    Remove-Item '{tempExePath}' -Force -ErrorAction SilentlyContinue\r\n" +
                    $"    Remove-Item $MyInvocation.MyCommand.Path -Force -ErrorAction SilentlyContinue\r\n" +
                    $"}}\r\n";
            }
            else
            {
                // Raw single-file exe: copy over current exe then relaunch
                scriptContent =
                    $"Start-Sleep -Seconds 2\r\n" +
                    $"try {{\r\n" +
                    $"    Copy-Item -Path '{tempExePath}' -Destination '{currentExePath}' -Force -ErrorAction Stop\r\n" +
                    $"    Start-Process '{currentExePath}'\r\n" +
                    $"}} catch {{\r\n" +
                    $"    Start-Process 'https://github.com/MorningAppLabs/GlanceSearch/releases'\r\n" +
                    $"}} finally {{\r\n" +
                    $"    Remove-Item '{tempExePath}' -Force -ErrorAction SilentlyContinue\r\n" +
                    $"    Remove-Item $MyInvocation.MyCommand.Path -Force -ErrorAction SilentlyContinue\r\n" +
                    $"}}\r\n";
            }

            File.WriteAllText(scriptPath, scriptContent, System.Text.Encoding.UTF8);

            ToastService.Show(
                $"✅ GlanceSearch {release.TagName} Ready",
                "Click here to restart and install the update now.",
                ToastType.Success,
                () => LaunchUpdaterAndExit(scriptPath));
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to download update");
            ToastService.Show("Download Failed", $"Could not download update: {ex.Message}", ToastType.Error);
        }
        finally
        {
            _downloading = false;
        }
    }

    /// <summary>
    /// Launches the PowerShell updater script as a hidden background process, then shuts down the app.
    /// </summary>
    private static void LaunchUpdaterAndExit(string scriptPath)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NonInteractive -WindowStyle Hidden -ExecutionPolicy Bypass -File \"{scriptPath}\"",
                UseShellExecute = false,
                CreateNoWindow = true
            };
            Process.Start(psi);
            Log.Information("Updater script launched. App shutting down for update installation.");
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
                System.Windows.Application.Current.Shutdown());
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to launch updater script");
            ToastService.Show("Update Error", $"Could not launch updater: {ex.Message}\nPlease restart manually.", ToastType.Error);
        }
    }
}
