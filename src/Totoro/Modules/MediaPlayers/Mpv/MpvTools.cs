using System.ComponentModel;
using Totoro.Core.Agent;

namespace Totoro.Modules.MediaPlayers.Mpv;

[Description("The mpv Totoro downloaded.")]
internal sealed record MpvInstallInfo(
	[property: Description("The mpv build installed, e.g. 20261020 (the date it was built).")]
	string Version,
	[property: Description("The folder with mpv.exe, ffmpeg.exe and yt-dlp.exe.")]
	string Folder);

[Description("Anime4K's upscaling modes for the video playing in mpv in Totoro's window.")]
internal sealed record Anime4KModes(
	[property: Description("The modes, by index: 0 is Off, the rest are Anime4K's (e.g. \"From 1080p\", Anime4K's Mode A).")]
	IReadOnlyList<string> Modes,
	[property: Description("The index of the mode in use; -1 if it's none of them (shaders of the user's own).")]
	int Current);

/// <summary>Agent tools for the mpv module (see <see cref="MpvInstaller"/>).</summary>
internal sealed class MpvTools(MpvInstaller installer, MpvWindowMediaPlayer player)
{
	[AgentTool("get_anime4k_modes")]
	[Description("Anime4K's upscaling modes for the video playing in mpv in Totoro's window, and the one in use, as the player's Anime4K button offers them.")]
	public Anime4KModes GetAnime4KModes()
	{
		if (!player.IsOpen)
		{
			throw new AgentToolException("Nothing is playing in mpv in Totoro's window: play an episode with the MPV player, its PlayInApp setting on.");
		}

		return player.ShaderModes is { Count: > 0 } modes
			? new Anime4KModes(modes, player.GetShaderMode())
			: throw new AgentToolException("mpv's config has no Anime4K modes: turn on the MPV module's InstallAnime4K setting, run download_mpv, then play again.");
	}

	[AgentTool("set_anime4k_mode")]
	[Description("Switches the video playing in mpv in Totoro's window to an Anime4K upscaling mode from get_anime4k_modes, or turns it off. Returns the modes as they are now.")]
	public Anime4KModes SetAnime4KMode([Description("The mode's index from get_anime4k_modes; 0 turns Anime4K off.")] int index)
	{
		var modes = GetAnime4KModes().Modes;
		if (index < 0 || index >= modes.Count)
		{
			throw new AgentToolException($"There's no mode {index}: pick one from 0 to {modes.Count - 1}.");
		}

		player.SelectShaderMode(index);
		return GetAnime4KModes();
	}

	[AgentTool("download_mpv", RequiresApproval = true)]
	[Description("Downloads mpv, with FFmpeg and yt-dlp, for the MPV media player; when they're already there, updates them to the latest builds. Also adds Anime4K's upscaling to mpv's config, or takes it out, as the module's InstallAnime4K and Anime4KGpu settings say (Anime4KGpu Auto picks from the graphics card's memory, and skips Anime4K under 4 GB; in mpv, Ctrl+1 to 6 pick a mode, Ctrl+0 turns it off).")]
	public async Task<MpvInstallInfo> DownloadMpvAsync()
	{
		await installer.InstallAsync();
		return installer.Error is { Length: > 0 } error
			? throw new AgentToolException(error)
			: new MpvInstallInfo(MpvInstaller.InstalledVersion, MpvInstaller.Folder);
	}
}
