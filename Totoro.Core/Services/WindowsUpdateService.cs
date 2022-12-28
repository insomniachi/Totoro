using System.Diagnostics;
using System.Reflection;
using System.Text.Json.Nodes;
using Splat;

namespace Totoro.Core.Services;

public class WindowsUpdateService : IUpdateService, IEnableLogger
{
    private readonly IObservable<VersionInfo> _onUpdate;
    private readonly string  _updateFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Totoro", "ApplicationData", "Updates");
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
                Url = (string)jsonNode["assets"][0]["browser_download_url"].AsValue()
            })
            .Log(this, "Latest Version", x => x.Version.ToString())
            .Where(vi => vi.Version > Assembly.GetExecutingAssembly().GetName().Version)
            .Throttle(TimeSpan.FromSeconds(3));
    }

    public async Task<VersionInfo> DownloadUpdate(VersionInfo versionInfo)
    {
        Directory.CreateDirectory(_updateFolder);
        var ext = Path.GetExtension(versionInfo.Url);
        var fileName = $"Totoro_{versionInfo.Version}{ext}";
        var fullPath = Path.Combine(_updateFolder, fileName);
        versionInfo.FilePath = fullPath;

        if(File.Exists(fullPath)) // already download before
        {
            return versionInfo;
        }

        using var client = new HttpClient();
        using var s = await client.GetStreamAsync(versionInfo.Url);
        using var fs = new FileStream(fullPath, FileMode.OpenOrCreate);
        await s.CopyToAsync(fs);
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
