using System.Diagnostics;
using System.Reflection;
using System.Text.Json.Nodes;
using Flurl.Http;
using Splat;
using Totoro.Plugins.Helpers;

namespace Totoro.Core.Services;

public class WindowsUpdateService : ReactiveObject, IUpdateService, IEnableLogger
{
    private readonly IObservable<VersionInfo> _onUpdate;
    private readonly HttpClient _httpClient;
    private readonly IKnownFolders _knownFolders;
    private VersionInfo _current;
    private CancellationTokenSource _cts;

    public IObservable<VersionInfo> OnUpdateAvailable => _onUpdate;

    public WindowsUpdateService(HttpClient httpClient,
                                ISettings settings,
                                IKnownFolders knownFolders)
    {
        _httpClient = httpClient;
        _knownFolders = knownFolders;

        _onUpdate = Observable
            .Timer(TimeSpan.Zero, TimeSpan.FromHours(1))
            .Where(_ => settings.AutoUpdate)
            .ObserveOn(RxApp.TaskpoolScheduler)
            .SelectMany(_ => TryGetStreamAsync())
            .Where(x => !string.IsNullOrEmpty(x))
            .Select(x => JsonNode.Parse(x))
            .Select(jsonNode => new VersionInfo()
            {
                Version = new Version(jsonNode["tag_name"].ToString()),
                Url = (string)jsonNode["assets"][0]["browser_download_url"].AsValue(),
                Body = jsonNode?["body"]?.ToString()
            })
            .Where(vi =>
            {
                var current = Assembly.GetEntryAssembly().GetName().Version;
                this.Log().Debug("Current Version, {Version}", Assembly.GetEntryAssembly().GetName().Version);
                this.Log().Debug("Latest Version, {Version}", vi.Version);
                return vi.Version > current;
            })
            .Throttle(TimeSpan.FromSeconds(3));
    }

    private static async Task<string> TryGetStreamAsync()
    {
        var response = await "https://api.github.com/repos/insomniachi/totoro/releases/latest"
            .WithDefaultUserAgent()
            .AllowAnyHttpStatus()
            .GetAsync();

        if(response.StatusCode > 300)
        {
            return "";
        }

        return await response.GetStringAsync();
    }

    public async ValueTask<VersionInfo> GetCurrentVersionInfo()
    {
        if (_current is not null)
        {
            return _current;
        }

        var url = $"https://api.github.com/repositories/insomniachi/totoro/releases/tags/{Assembly.GetEntryAssembly().GetName().Version.ToString(3)}";
        var response = await _httpClient.GetAsync(url);

        if (response.IsSuccessStatusCode)
        {
            var jsonNode = JsonNode.Parse(await response.Content.ReadAsStreamAsync());
            _current = new VersionInfo()
            {
                Version = new Version(jsonNode["tag_name"].ToString()),
                Url = (string)jsonNode["assets"][0]["browser_download_url"].AsValue(),
                Body = jsonNode?["body"]?.ToString()
            };
        }
        return _current;
    }

    public async Task<VersionInfo> DownloadUpdate(VersionInfo versionInfo)
    {
        var ext = Path.GetExtension(versionInfo.Url);
        var fileName = $"Totoro_{versionInfo.Version}{ext}";
        var fullPath = Path.Combine(_knownFolders.Updates, fileName);
        versionInfo.FilePath = fullPath;

        foreach (var file in Directory.GetFiles(_knownFolders.Updates).Where(x => x != fullPath))
        {
            this.Log().Info($"Deleting file {file}");
            File.Delete(file);
        }

        if (File.Exists(fullPath)) // already download before
        {
            this.Log().Info($"File {fullPath} already Exists");
            return versionInfo;
        }

        this.Log().Info($"Current version : {Assembly.GetEntryAssembly().GetName().Version.ToString(3)}");
        this.Log().Info($"downloading update {versionInfo.Version}");

        _cts = new();
        using var client = new HttpClient();
        using var s = await client.GetStreamAsync(versionInfo.Url);
        using var fs = new FileStream(fullPath, FileMode.OpenOrCreate);

        try
        {
            await s.CopyToAsync(fs, _cts.Token);
        }
        catch (Exception ex)
        {
            this.Log().Error(ex);
            fs.Dispose();
            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
            }
        }

        this.Log().Info($"downloading update {versionInfo.Version} completed.");

        return versionInfo;
    }

    public void InstallUpdate(VersionInfo versionInfo)
    {
        this.Log().Info(@$"Executing command : msiexec /i ""{versionInfo.FilePath}""");
        Process.Start(new ProcessStartInfo
        {
            FileName = "msiexec",
            Arguments = $"/i \"{versionInfo.FilePath}\""
        });
        Process.GetCurrentProcess().CloseMainWindow();
    }

    public void ShutDown() => _cts?.Cancel();
}
