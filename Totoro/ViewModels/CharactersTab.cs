using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using Totoro.Core.Module.Anime;

namespace Totoro.ViewModels;

public sealed record CharacterItem(string Name, string? Image);

/// <summary>The anime's characters.</summary>
public sealed partial class CharactersTab(Func<Task<List<CharacterModel>>> fetch, ILogger logger)
	: LazyDetailsTab("No characters listed.", logger)
{
	[ObservableProperty]
	public partial IReadOnlyList<CharacterItem> Characters { get; private set; } = [];

	protected override async Task<int> LoadAsync()
	{
		Characters = [.. (await fetch()).Select(x => new CharacterItem(x.Name, x.Image?.AbsoluteUri))];
		return Characters.Count;
	}
}
