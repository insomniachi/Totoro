using System.Diagnostics;
using System.Reflection;
using System.Text.Json.Nodes;
using Splat;

namespace Totoro.Core.Services;

public class WindowsUpdateService : ReactiveObject, IUpdateService, IEnableLogger
{
    private readonly IObservable<VersionInfo> _onUpdate;
    private readonly string _updateFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Totoro", "ApplicationData", "Updates");
    private readonly HttpClient _httpClient;
    private VersionInfo _current;

    public IObservable<VersionInfo> OnUpdateAvailable => _onUpdate;

    public WindowsUpdateService(HttpClient httpClient)
    {
        _onUpdate = Observable
            .Timer(TimeSpan.Zero, TimeSpan.FromHours(1))
            .ObserveOn(RxApp.TaskpoolScheduler)
            .SelectMany(_ => httpClient.GetStreamAsync("https://api.github.com/repos/athulrajts/AnimDL.GUI/releases/latest"))
            .Select(x => JsonNode.Parse(x))
            .Select(jsonNode => new VersionInfo()
            {
                Version = new Version(jsonNode["tag_name"].ToString()),
                Url = (string)jsonNode["assets"][0]["browser_download_url"].AsValue(),
                Body = jsonNode?["body"]?.ToString()
            })
            .Where(vi => vi.Version > Assembly.GetEntryAssembly().GetName().Version)
            .Log(this, "New Version", vi => vi.Version.ToString())
            .Throttle(TimeSpan.FromSeconds(3));
        _httpClient = httpClient;
    }

    public async ValueTask<VersionInfo> GetCurrentVersionInfo()
    {
        if(_current is null)
        {
            var url = $"https://api.github.com/repositories/522584084/releases/tags/{Assembly.GetEntryAssembly().GetName().Version}";
            var response = await _httpClient.GetAsync(url);

            if(response.IsSuccessStatusCode)
            {
                var jsonNode = JsonNode.Parse(await response.Content.ReadAsStreamAsync());
                _current = new VersionInfo()
                {
                    Version = new Version(jsonNode["tag_name"].ToString()),
                    Url = (string)jsonNode["assets"][0]["browser_download_url"].AsValue(),
                    Body = jsonNode?["body"]?.ToString()
                };
            }
        }
        return _current;
    }

    public async Task<VersionInfo> DownloadUpdate(VersionInfo versionInfo)
    {
        Directory.CreateDirectory(_updateFolder);
        var ext = Path.GetExtension(versionInfo.Url);
        var fileName = $"Totoro_{versionInfo.Version}{ext}";
        var fullPath = Path.Combine(_updateFolder, fileName);
        versionInfo.FilePath = fullPath;

        foreach (var file in Directory.GetFiles(_updateFolder).Where(x => x != fullPath))
        {
            this.Log().Info($"Deleting file {file}");
            File.Delete(file);
        }

        if (File.Exists(fullPath)) // already download before
        {
            this.Log().Info($"File {fullPath} already Exists");
            return versionInfo;
        }

        this.Log().Info($"Current version : {Assembly.GetEntryAssembly().GetName().Version}");
        this.Log().Info($"downloading update {versionInfo.Version}");

        using var client = new HttpClient();
        using var s = await client.GetStreamAsync(versionInfo.Url);
        using var fs = new FileStream(fullPath, FileMode.OpenOrCreate);
        await s.CopyToAsync(fs);

        this.Log().Info($"downloading update {versionInfo.Version} completed.");

        return versionInfo;
    }

    public void InstallUpdate(VersionInfo versionInfo)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "msiexec",
            Arguments = $"/i \"{versionInfo.FilePath}\""
        });
        Process.GetCurrentProcess().CloseMainWindow();
    }
}
