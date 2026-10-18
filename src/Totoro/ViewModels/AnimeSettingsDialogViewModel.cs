using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Totoro.Core.Dialogs;
using Totoro.Core.Modules;
using Totoro.Core.Modules.Anime;
using Totoro.Core.Modules.Streaming;
using Totoro.Core.Relations;
using Totoro.Core.Settings;
using Totoro.ViewModels.Relations;

namespace Totoro.ViewModels;

// ToString is what screen readers announce for each ComboBox item, so it returns the label rather than the record dump.

/// <param name="Id">The provider's (module's) id; null for whichever is selected in Settings.</param>
/// <param name="Icon">The provider's module's icon; "" when it has none.</param>
public sealed record ProviderChoice(Guid? Id, string Label, string Icon)
{
	public override string ToString() => Label;
}

/// <param name="Value">Null to work it out from the provider's episode numbers (automatic).</param>
public sealed record NumberingChoice(string Label, EpisodeNumbering? Value)
{
	public override string ToString() => Label;
}

/// <param name="Value">Null to follow the app-wide setting.</param>
public sealed record SkipModeChoice(string Label, SkipMode? Value)
{
	public override string ToString() => Label;
}

/// <summary>
/// Edits an anime's own settings for watching it (see <see cref="AnimeSettings"/>): the anime provider to watch it from,
/// that provider's settings, the show it is on that provider, and how to skip its openings and endings. Saves them
/// when confirmed; returns whether it did.
/// </summary>
public sealed partial class AnimeSettingsDialogViewModel : DialogViewModel<bool>
{
	private readonly AnimeModel _anime;
	private readonly AnimeSettings _settings;
	private readonly IAnimeSettingsStore _store;
	private readonly List<IModuleSettings> _moduleSettings;
	private readonly IAnimeProvider? _defaultProvider;
	private readonly IAnimeRelations _relations;

	// Each provider's own settings as edited, by provider id; kept while switching between providers, so switching back
	// doesn't lose the edits. Only what's been shown is here.
	private readonly Dictionary<Guid, ProviderSettingsDraft> _drafts = [];
	private readonly HashSet<Guid> _forgottenShows = [];

	public AnimeSettingsDialogViewModel(
		AnimeModel anime,
		IAnimeSettingsStore store,
		IFactory<IAnimeProvider, Guid> providers,
		IFactory<IAnimeMetadataProvider, Guid> databases,
		IAnimeRelations relations,
		IEnumerable<IModuleSettings> moduleSettings,
		IEnumerable<Descriptor> modules,
		IPreferences preferences)
	{
		_anime = anime;
		_store = store;
		_settings = store.Get(anime);
		_moduleSettings = [.. moduleSettings];
		_defaultProvider = providers.CreateDefault();
		_relations = relations;

		Title = anime.Title;
		PrimaryButtonText = "Save";

		string IconOf(Guid? id) => modules.FirstOrDefault(m => m.Id == id)?.Image ?? "";

		Providers =
		[
			new(null, _defaultProvider is null ? "Default" : $"Default ({_defaultProvider.Name})", IconOf(_defaultProvider?.Id)),
			.. providers.CreateAll().Select(x => new ProviderChoice(x.Id, x.Name, IconOf(x.Id))),
		];
		OpeningSkipModes = CreateSkipModes(preferences.OpeningSkipMode);
		EndingSkipModes = CreateSkipModes(preferences.EndingSkipMode);

		SelectedProvider = Providers.FirstOrDefault(x => x.Id == _settings.ProviderId) ?? Providers[0];
		OpeningSkip = OpeningSkipModes.First(x => x.Value == _settings.OpeningSkipMode);
		EndingSkip = EndingSkipModes.First(x => x.Value == _settings.EndingSkipMode);

		Numbering = NumberingChoices.First(x => x.Value == _settings.EpisodeNumbering);
		HideFromDiscord = _settings.HideFromDiscord;
		NewRule = new RelationRuleEditorViewModel(databases.CreateDefault(), anime);
		_ = ShowRuleAsync(anime);
	}

	public IReadOnlyList<ProviderChoice> Providers { get; }
	public IReadOnlyList<SkipModeChoice> OpeningSkipModes { get; }
	public IReadOnlyList<SkipModeChoice> EndingSkipModes { get; }

	[ObservableProperty] public partial ProviderChoice SelectedProvider { get; set; }
	[ObservableProperty] public partial SkipModeChoice OpeningSkip { get; set; }
	[ObservableProperty] public partial SkipModeChoice EndingSkip { get; set; }

	public IReadOnlyList<NumberingChoice> NumberingChoices { get; } =
	[
		new("Automatic", null),
		new("By season", EpisodeNumbering.Season),
		new("Absolute", EpisodeNumbering.Absolute),
	];

	/// <summary>How its provider numbers its episodes (see <see cref="AnimeSettings.EpisodeNumbering"/>).</summary>
	[ObservableProperty] public partial NumberingChoice Numbering { get; set; }

	/// <summary>Whether watching it is kept off Discord (see <see cref="AnimeSettings.HideFromDiscord"/>).</summary>
	[ObservableProperty] public partial bool HideFromDiscord { get; set; }

	/// <summary>The rule that says where its episodes are in absolute numbering, or that there's none.</summary>
	[ObservableProperty] public partial string RuleSummary { get; private set; } = "Looking for a rule…";

	/// <summary>A rule of the user's own for it, added when saving if it's filled in.</summary>
	public RelationRuleEditorViewModel NewRule { get; }

	// About the provider it'll be watched from (the selected one, or the default). Strings are never null: they're passed
	// to x:Bind functions.

	/// <summary>Whether there's a provider to watch it from at all.</summary>
	[ObservableProperty] public partial bool HasProvider { get; private set; }

	/// <summary>E.g. "Show on SubsPlease".</summary>
	[ObservableProperty] public partial string ShowHeader { get; private set; } = "";

	/// <summary>The show it was matched with on the provider, or how it will be.</summary>
	[ObservableProperty] public partial string ShowDescription { get; private set; } = "";

	/// <summary>Whether it's been matched with a show (which can then be forgotten, to find it again).</summary>
	[ObservableProperty] public partial bool HasShow { get; private set; }

	/// <summary>Whether the provider has settings, which the anime can have its own of.</summary>
	[ObservableProperty] public partial bool HasProviderSettings { get; private set; }

	/// <summary>E.g. "Use its own SubsPlease settings".</summary>
	[ObservableProperty] public partial string OwnSettingsHeader { get; private set; } = "";

	[ObservableProperty] public partial bool UseOwnProviderSettings { get; set; }

	/// <summary>The anime's own settings for the provider, being edited; null if the provider has none.</summary>
	[ObservableProperty] public partial DataContainer? ProviderSettings { get; private set; }

	partial void OnSelectedProviderChanged(ProviderChoice value)
	{
		var id = value.Id ?? _defaultProvider?.Id;
		var name = value.Id is null ? _defaultProvider?.Name ?? "" : value.Label;

		HasProvider = id is not null;
		ShowHeader = $"Show on {name}";
		OwnSettingsHeader = $"Use its own {name} settings";
		UpdateShow(id);

		var draft = id is { } providerId ? GetDraft(providerId) : null;
		HasProviderSettings = draft is not null;
		ProviderSettings = draft?.Container;
		UseOwnProviderSettings = draft?.UseOwn ?? false;
	}

	partial void OnUseOwnProviderSettingsChanged(bool value)
	{
		if (CurrentProviderId is { } id && _drafts.TryGetValue(id, out var draft))
		{
			draft.UseOwn = value;
		}
	}

	private Guid? CurrentProviderId => SelectedProvider.Id ?? _defaultProvider?.Id;

	/// <summary>Forgets the show it was matched with, so it's looked for again the next time it's watched.</summary>
	[RelayCommand]
	private void ForgetShow()
	{
		if (CurrentProviderId is { } id)
		{
			_forgottenShows.Add(id);
			UpdateShow(id);
		}
	}

	private void UpdateShow(Guid? providerId)
	{
		var show = providerId is { } id && !_forgottenShows.Contains(id) ? _settings.Shows.GetValueOrDefault(id) : null;
		HasShow = show is not null;
		ShowDescription = show?.Title ?? "Not found yet. It's looked for the first time you watch it.";
	}

	/// <summary>The provider's settings to edit for the anime: its own, or a copy of the module's to start from.</summary>
	private ProviderSettingsDraft? GetDraft(Guid providerId)
	{
		if (_drafts.TryGetValue(providerId, out var draft))
		{
			return draft;
		}

		if (_moduleSettings.FirstOrDefault(x => x.Descriptor.Id == providerId) is not { } module)
		{
			return null;
		}

		var own = _settings.ProviderSettings.GetValueOrDefault(providerId);
		var config = module.CreateCopy(own);
		var container = config.ToDataContainer();
		if (container.Count == 0)
		{
			return null;
		}

		return _drafts[providerId] = new ProviderSettingsDraft(module, config, container) { UseOwn = own is not null };
	}

	public override Task<bool> ConfirmAsync()
	{
		_settings.ProviderId = SelectedProvider.Id;
		_settings.OpeningSkipMode = OpeningSkip.Value;
		_settings.EndingSkipMode = EndingSkip.Value;
		_settings.EpisodeNumbering = Numbering.Value;
		_settings.HideFromDiscord = HideFromDiscord;
		if (NewRule.ToRule() is { } rule)
		{
			_relations.AddUserRule(rule);
		}

		foreach (var (id, draft) in _drafts)
		{
			if (draft.UseOwn)
			{
				draft.Config.UpdateValues(draft.Container);
				_settings.ProviderSettings[id] = draft.Module.ToJson(draft.Config);
			}
			else
			{
				_settings.ProviderSettings.Remove(id);
			}
		}

		foreach (var id in _forgottenShows)
		{
			_settings.Shows.Remove(id);
		}

		_store.Save(_anime, _settings);
		Result = true;
		return Task.FromResult(true);
	}

	/// <summary>Shows its rule, once anime-relations is here (it's downloaded the first time it's needed).</summary>
	private async Task ShowRuleAsync(AnimeModel anime)
	{
		try
		{
			await _relations.EnsureAvailableAsync(CancellationToken.None);
		}
		catch (Exception)
		{
			// Shown from whatever rules there are.
		}

		RuleSummary = DescribeRule(anime, _relations);
	}

	private static string DescribeRule(AnimeModel anime, IAnimeRelations relations)
	{
		var ids = RelationIds.Of(anime);
		if (relations.GetRules(anime).FirstOrDefault(x => x.Rule.Destination.Matches(ids)) is not { } match)
		{
			return "Absolute numbering carries on across seasons. There's no rule for it yet: add one below if its provider uses it.";
		}

		var source = match.IsUserRule ? "your rule" : "anime-relations";
		return $"Absolute numbering carries on across seasons: its episodes {match.Rule.DestinationEpisodes} are {match.Rule.SourceEpisodes} ({source}).";
	}

	private static List<SkipModeChoice> CreateSkipModes(SkipMode appWide)
	{
		List<SkipModeChoice> modes =
		[
			new("Don't skip", SkipMode.Off),
			new("Offer to skip", SkipMode.Ask),
			new("Skip automatically", SkipMode.Automatic),
		];
		return [new($"Default ({modes.First(x => x.Value == appWide).Label})", null), .. modes];
	}

	private sealed class ProviderSettingsDraft(IModuleSettings module, DataContainerConfig config, DataContainer container)
	{
		public IModuleSettings Module { get; } = module;
		public DataContainerConfig Config { get; } = config;
		public DataContainer Container { get; } = container;
		public bool UseOwn { get; set; }
	}
}
