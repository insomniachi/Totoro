using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Totoro.Core.Dialogs;
using Totoro.Core.Module.Streaming;

namespace Totoro.Playback;

/// <param name="Title">The anime's title, to search for and show.</param>
/// <param name="Results">What searching for the anime's titles found, best first.</param>
public sealed record ShowPickerRequest(IAnimeProvider Provider, string Title, IReadOnlyList<ProviderShow> Results);

/// <summary>Picks the provider's show for an anime, when its title didn't match exactly one; the user can search too.</summary>
public sealed partial class ShowPickerDialogViewModel : DialogViewModel<ProviderShow>
{
	private readonly IAnimeProvider _provider;

	public ShowPickerDialogViewModel(ShowPickerRequest request)
	{
		_provider = request.Provider;
		AnimeTitle = request.Title;
		Query = request.Title;
		Results = request.Results;
		SelectedShow = Results.FirstOrDefault();
		Title = $"Find it on {request.Provider.Name}";
		PrimaryButtonText = "Watch";
		CanConfirm = SelectedShow is not null;
	}

	public string AnimeTitle { get; }

	public string Prompt => $"Which {_provider.Name} show is {AnimeTitle}?";

	[ObservableProperty] public partial string Query { get; set; }
	[ObservableProperty] public partial IReadOnlyList<ProviderShow> Results { get; private set; }
	[ObservableProperty] public partial ProviderShow? SelectedShow { get; set; }

	/// <summary>Why the list is empty; empty, never null, when it isn't (it's passed to x:Bind functions).</summary>
	[ObservableProperty] public partial string Message { get; private set; } = "";

	partial void OnResultsChanged(IReadOnlyList<ProviderShow> value)
	{
		Message = value.Count > 0 ? "" : $"No shows on {_provider.Name} match. Try another title.";
	}

	partial void OnSelectedShowChanged(ProviderShow? value) => CanConfirm = value is not null;

	[RelayCommand]
	private async Task SearchAsync()
	{
		Results = string.IsNullOrWhiteSpace(Query) ? [] : await _provider.SearchAsync(Query, CancellationToken.None);
		SelectedShow = Results.FirstOrDefault();
	}

	public override Task<bool> ConfirmAsync()
	{
		Result = SelectedShow;
		return Task.FromResult(SelectedShow is not null);
	}
}
