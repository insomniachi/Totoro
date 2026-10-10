using Totoro.Core.Settings;

namespace Totoro.Core.Dialogs;

/// <param name="Title">The dialog's title.</param>
/// <param name="Container">The properties to edit; updated only if the dialog is confirmed.</param>
public sealed record DataContainerDialogRequest(string Title, IReadOnlyList<DataContainerProperty> Container);

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
		PrimaryButtonText = "Save";
		Properties = new DataContainer(_original.Select(x => x.Clone()));
	}

	/// <summary>The copies being edited.</summary>
	public DataContainer Properties { get; }

	public override Task<bool> ConfirmAsync()
	{
		for (var i = 0; i < _original.Count; i++)
		{
			_original[i].Value = Properties[i].Value;
		}

		Result = true;
		return Task.FromResult(true);
	}
}

public static class DataContainerDialogExtensions
{
	extension(IDialogService dialogs)
	{
		/// <summary>Edits <paramref name="container"/> in a dialog; returns true if the user saved.</summary>
		public async Task<bool> EditAsync(string title, IReadOnlyList<DataContainerProperty> container)
		{
			return await dialogs.ShowAsync<DataContainerDialogViewModel, bool>(new DataContainerDialogRequest(title, container));
		}

		/// <summary>Edits a module's settings in a dialog and saves them if the user saved; returns whether they did.</summary>
		public async Task<bool> EditSettingsAsync(IModuleSettings settings)
		{
			var container = settings.Config.ToDataContainer();
			if (!await dialogs.EditAsync(settings.Descriptor.Name, container))
			{
				return false;
			}

			settings.Config.UpdateValues(container);
			settings.Save();
			return true;
		}
	}
}
