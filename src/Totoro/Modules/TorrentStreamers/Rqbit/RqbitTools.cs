using System.ComponentModel;
using Totoro.Core.Agent;

namespace Totoro.Modules.TorrentStreamers.Rqbit;

[Description("The rqbit Totoro downloaded.")]
internal sealed record RqbitInstallInfo(
	[property: Description("The rqbit release installed, e.g. v9.0.1.")]
	string Version,
	[property: Description("The folder with rqbit.exe.")]
	string Folder);

/// <summary>Agent tools for the rqbit module (see <see cref="RqbitInstaller"/>).</summary>
internal sealed class RqbitTools(RqbitInstaller installer)
{
	[AgentTool("download_rqbit", RequiresApproval = true)]
	[Description("Downloads rqbit, which streams and downloads torrents; when it's already there, updates it to the latest release. It's also downloaded by itself the first time a torrent is played.")]
	public async Task<RqbitInstallInfo> DownloadRqbitAsync()
	{
		await installer.InstallAsync();
		return installer.Error is { Length: > 0 } error
			? throw new AgentToolException(error)
			: new RqbitInstallInfo(RqbitInstaller.InstalledVersion, RqbitInstaller.Folder);
	}
}
