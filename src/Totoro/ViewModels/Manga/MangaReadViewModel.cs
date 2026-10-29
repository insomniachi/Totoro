using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Totoro.Commands;
using Totoro.Core.Dialogs;
using Totoro.Core.Modules;
using Totoro.Core.Modules.Manga;
using Totoro.Core.Services;
using Totoro.Core.Settings;
using Totoro.UI.Navigation;

namespace Totoro.ViewModels.Manga;

/// <summary>
/// Manga mode's Read, as anime's Watch (<see cref="ProviderWatchViewModel"/>): search a manga source directly, pick a
/// manga (cards), then beside them a chapter, and read it. For manga that aren't in the database (or without one);
/// reading here doesn't change the user's list.
/// </summary>
public partial class MangaReadViewModel : NavigatableViewModel
{
	// What the page showed when left (e.g. to read a chapter), shown again on coming back: each visit has a new view model.
	private static (Guid Source, string Query, string? Selected)? s_last;

	private readonly Dictionary<Guid, IMangaProvider> _sources;
	private readonly IDialogService _dialogs;
	private readonly List<IModuleSettings> _moduleSettings;
	private CancellationTokenSource? _search;

	public MangaReadViewModel(MangaReadingService reading, IDialogService dialogs, IEnumerable<IModuleSettings> moduleSettings, IEnumerable<Descriptor> modules)
	{
		_dialogs = dialogs;
		_moduleSettings = [.. moduleSettings];
		_sources = reading.Sources.ToDictionary(x => x.Id);
		Sources = [.. _sources.Values.Select(x => new ProviderChoice(x.Id, x.Name, modules.FirstOrDefault(m => m.Id == x.Id)?.Image ?? ""))];
		var selected = s_last?.Source ?? reading.Source?.Id;
		SelectedSource = Sources.FirstOrDefault(x => x.Id == selected) ?? Sources.FirstOrDefault();
	}

	protected override async Task OnNavigatedToInternalAsync()
	{
		if (s_last is not { } last)
		{
			return;
		}

		Query = last.Query;
		await SearchAsync();
		Selected = Results.FirstOrDefault(x => x.Id == last.Selected);
	}

	protected override Task OnNavigatedFromInternalAsync()
	{
		s_last = SelectedSource?.Id is { } id ? (id, Query, Selected?.Id) : null;
		return Task.CompletedTask;
	}

	public IReadOnlyList<ProviderChoice> Sources { get; }

	public bool HasNoSources => Sources.Count == 0;

	[ObservableProperty]
	[NotifyCanExecuteChangedFor(nameof(EditSourceSettingsCommand))]
	public partial ProviderChoice? SelectedSource { get; set; }

	private IMangaProvider? Source => SelectedSource?.Id is { } id ? _sources.GetValueOrDefault(id) : null;

	[ObservableProperty]
	public partial string Query { get; set; } = "";

	[ObservableProperty]
	public partial List<ProviderManga> Results { get; set; } = [];

	/// <summary>The manga picked; its chapters show beside the cards.</summary>
	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(HasManga))]
	public partial ProviderManga? Selected { get; set; }

	public bool HasManga => Selected is not null;

	[ObservableProperty]
	public partial List<ProviderChapter> Chapters { get; set; } = [];

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(HasChapter))]
	[NotifyCanExecuteChangedFor(nameof(ReadCommand))]
	public partial ProviderChapter? SelectedChapter { get; set; }

	/// <summary>Whether a chapter is picked: its Read button shows then.</summary>
	public bool HasChapter => SelectedChapter is not null;

	/// <summary>What's going on, or what went wrong; "" when there's nothing to say.</summary>
	[ObservableProperty]
	public partial string Message { get; set; } = "";

	partial void OnSelectedSourceChanged(ProviderChoice? value)
	{
		Results = [];
		Selected = null;
		_ = SearchAsync();
	}

	private IModuleSettings? SourceSettings => _moduleSettings.FirstOrDefault(x => x.Descriptor.Id == SelectedSource?.Id);

	private bool CanEditSourceSettings() => SourceSettings is not null;

	/// <summary>Edits the source's own settings (as in Settings), then gets the chapters again with them.</summary>
	[RelayCommand(CanExecute = nameof(CanEditSourceSettings))]
	private async Task EditSourceSettingsAsync()
	{
		if (SourceSettings is { } settings && await _dialogs.EditSettingsAsync(settings) && Selected is { } manga)
		{
			await LoadChaptersAsync(manga);
		}
	}

	[RelayCommand]
	private async Task SearchAsync()
	{
		_search?.Cancel();
		var search = _search = new CancellationTokenSource();
		if (Source is not { } source || Query.Trim().Length == 0)
		{
			return;
		}

		Message = "";
		using (BeginBusy())
		{
			try
			{
				var results = await source.SearchAsync(Query.Trim(), search.Token);
				if (!search.IsCancellationRequested)
				{
					Results = results;
					Message = results.Count == 0 ? $"Nothing on {source.Name} matches \"{Query.Trim()}\"." : "";
				}
			}
			catch (Exception ex) when (!search.IsCancellationRequested)
			{
				Message = $"Couldn't search {source.Name}: {ex.Message}";
			}
		}
	}

	partial void OnSelectedChanged(ProviderManga? value) => _ = LoadChaptersAsync(value);

	private async Task LoadChaptersAsync(ProviderManga? manga)
	{
		Chapters = [];
		SelectedChapter = null;
		if (manga is null || Source is not { } source)
		{
			return;
		}

		using (BeginBusy())
		{
			try
			{
				var chapters = await MangaReadingService.GetChaptersAsync(source, manga.Id, CancellationToken.None);
				if (Selected == manga)
				{
					Chapters = chapters;
					Message = chapters.Count == 0 ? $"{source.Name} has no chapters of {manga.Title}." : "";
				}
			}
			catch (Exception ex) when (Selected == manga)
			{
				Message = $"Couldn't load its chapters: {ex.Message}";
			}
			catch (Exception)
			{
				// For a manga that's since changed: nobody's waiting for it.
			}
		}
	}

	[RelayCommand(CanExecute = nameof(HasChapter))]
	private Task ReadAsync()
	{
		return Source is { } source && SelectedChapter is { } chapter
			? MangaCommands.OpenReaderAsync(new ReaderRequest(null, source.Id, Chapters, Chapters.IndexOf(chapter)))
			: Task.CompletedTask;
	}
}
