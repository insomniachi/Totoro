using Totoro.Core.Settings;

namespace Totoro.Core.Dialogs;

/// <param name="Title">The dialog's title.</param>
/// <param name="Container">The properties to edit; updated only if the dialog is confirmed.</param>
/// <param name="PrimaryButtonText">What confirming does, e.g. "Join".</param>
public sealed record DataContainerDialogRequest(string Title, IReadOnlyList<DataContainerProperty> Container, string PrimaryButtonText = "Save");

/// <summary>
/// Edits a <see cref="DataContainer"/> in a dialog. The dialog edits copies of the properties and writes them back
/// when confirmed, so cancelling leaves the container as it was. Show it with
/// <see cref="DataContainerDialogExtensions"/>.
/// </summary>
public sealed class DataContainerDialogViewModel : DialogViewModel<bool>
{
	private readonly IReadOnlyList<DataContainerProperty> _original;

	public DataContainerDialogViewModel(DataContainerDialogRequest request)
	{
		_original = request.Container;
		Title = request.Title;
		PrimaryButtonText = request.PrimaryButtonText;
		Properties = new DataContainer(_original.Select(x => x.Clone()));
	}

	/// <summary>The copies being edited.</summary>
	public DataContainer Properties { get; }

	public override Task<bool> ConfirmAsync()
	{
		// Only what changed (an action's can't be written), and all of it checked before any is written, so a value that
		// isn't valid leaves every original as it was and keeps the dialog open.
		var changed = _original.Zip(Properties).Where(x => !Equals(x.First.Value, x.Second.Value)).ToList();
		if (changed.Any(x => !x.First.Clone().TrySetValue(x.Second.Value, out _)))
		{
			return Task.FromResult(false);
		}

		foreach (var (original, copy) in changed)
		{
			original.TrySetValue(copy.Value, out _);
		}

		Result = true;
		return Task.FromResult(true);
	}
}

public static class DataContainerDialogExtensions
{
	extension(IDialogService dialogs)
	{
		/// <summary>Edits <paramref name="container"/> in a dialog; returns true if the user confirmed (with <paramref name="primaryButtonText"/>).</summary>
		public async Task<bool> EditAsync(string title, IReadOnlyList<DataContainerProperty> container, string primaryButtonText = "Save")
		{
			return await dialogs.ShowAsync<DataContainerDialogViewModel, bool>(new DataContainerDialogRequest(title, container, primaryButtonText));
		}

		/// <summary>Edits a module's settings in a dialog and saves them if the user saved; returns whether they did.</summary>
		public async Task<bool> EditSettingsAsync(IModuleSettings settings)
		{
			// Bound to the settings: confirming writes the edited values into them.
			if (!await dialogs.EditAsync(settings.Descriptor.Name, settings.Config.ToDataContainer()))
			{
				return false;
			}

			settings.Save();
			return true;
		}
	}
}
