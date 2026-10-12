using System.ComponentModel;
using Totoro.Core.Agent;
using Totoro.Core.Services;

namespace Totoro.Agent;

[Description("Totoro's version, and the latest one on GitHub.")]
public sealed record UpdateInfo(
	[property: Description("The version running.")]
	string CurrentVersion,
	[property: Description("A newer version, if there is one.")]
	string? NewVersion,
	[property: Description("What's new in it, from its release notes.")]
	string? ReleaseNotes,
	[property: Description("Whether this copy can update itself (it was installed with the installer).")]
	bool CanInstall);

/// <summary>Agent tools for updating Totoro from its GitHub releases (see <see cref="UpdateService"/>).</summary>
public sealed class UpdateTools(UpdateService updates)
{
	[AgentTool("check_for_updates")]
	[Description("Checks whether there's a newer version of Totoro.")]
	public async Task<UpdateInfo> CheckForUpdatesAsync()
	{
		var release = await updates.CheckAsync();
		if (updates.State == UpdateState.Failed)
		{
			throw new AgentToolException(updates.Error);
		}

		return new UpdateInfo(updates.CurrentVersion.ToString(), release?.Version.ToString(), release?.Notes, updates.CanInstall);
	}

	[AgentTool("install_update", RequiresApproval = true)]
	[Description("Downloads and installs the newer version of Totoro. Totoro closes for it, and starts again once it's installed.")]
	public async Task<string> InstallUpdateAsync()
	{
		if (await updates.CheckAsync() is null)
		{
			throw new AgentToolException(updates.State == UpdateState.Failed ? updates.Error : "Totoro is up to date.");
		}

		await updates.InstallAsync();
		return updates.State == UpdateState.Failed
			? throw new AgentToolException(updates.Error)
			: $"Installing Totoro {updates.Available?.Version}; it's closing and will start again.";
	}
}
