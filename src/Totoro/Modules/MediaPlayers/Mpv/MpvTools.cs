using System.ComponentModel;
using Totoro.Core.Agent;

namespace Totoro.Modules.MediaPlayers.Mpv;

[Description("The mpv Totoro downloaded.")]
internal sealed record MpvInstallInfo(
	[property: Description("The mpv build installed, e.g. 20261020 (the date it was built).")]
	string Version,
	[property: Description("The folder with mpv.exe, ffmpeg.exe and yt-dlp.exe.")]
	string Folder);

/// <summary>Agent tools for the mpv module (see <see cref="MpvInstaller"/>).</summary>
internal sealed class MpvTools(MpvInstaller installer)
{
	[AgentTool("download_mpv", RequiresApproval = true)]
	[Description("Downloads mpv, with FFmpeg and yt-dlp, for the MPV media player; when they're already there, updates them to the latest builds. Also adds Anime4K's upscaling to mpv's config, or takes it out, as the module's InstallAnime4K and Anime4KGpu settings say (in mpv, Ctrl+1 to 6 pick a mode, Ctrl+0 turns it off).")]
	public async Task<MpvInstallInfo> DownloadMpvAsync()
	{
		await installer.InstallAsync();
		return installer.Error is { Length: > 0 } error
			? throw new AgentToolException(error)
			: new MpvInstallInfo(MpvInstaller.InstalledVersion, MpvInstaller.Folder);
	}
}
