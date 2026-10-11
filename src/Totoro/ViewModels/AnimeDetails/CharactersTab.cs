using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using Totoro.Core.Modules.Anime;

namespace Totoro.ViewModels.AnimeDetails;

public sealed record CharacterItem(string Name, string? Image);

/// <summary>The anime's characters: those with a picture (minor ones without are left out).</summary>
public sealed partial class CharactersTab(Func<Task<List<CharacterModel>>> fetch, ILogger logger)
	: LazyDetailsTab("No characters listed.", logger)
{
	[ObservableProperty]
	public partial IReadOnlyList<CharacterItem> Characters { get; private set; } = [];

	protected override async Task<int> LoadAsync()
	{
		Characters = [.. (await fetch()).Where(x => x.Image is not null).Select(x => new CharacterItem(x.Name, x.Image!.AbsoluteUri))];
		return Characters.Count;
	}
}
