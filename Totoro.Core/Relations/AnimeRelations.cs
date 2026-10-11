using Microsoft.Extensions.Logging;
using Totoro.Core.Helpers;
using Totoro.Core.Modules.Anime;

namespace Totoro.Core.Relations;

/// <param name="Episode">The episode's number in the source's numbering, e.g. 16 for a 2nd season's 4th episode.</param>
/// <param name="IsUserRule">Whether it's one of the user's own rules rather than anime-relations'.</param>
public sealed record AnimeRelationMatch(AnimeRelationRule Rule, int Episode, bool IsUserRule);

/// <param name="RuleCount">How many rules anime-relations has.</param>
/// <param name="LastModified">When anime-relations was last changed (YYYY-MM-DD), as its file says.</param>
/// <param name="LastChecked">When it was last fetched from GitHub, if it has been.</param>
public sealed record AnimeRelationsInfo(int RuleCount, string? LastModified, DateTime? LastChecked);

/// <summary>
/// Where an anime's episodes are when a provider numbers them on from an earlier entry: e.g. a single SubsPlease show
/// for two seasons, whose 2nd season starts at episode 13. Uses anime-relations (https://github.com/erengy/anime-relations),
/// downloaded from GitHub when it's first needed and kept on this PC (refreshed about once a day), and the user's own
/// rules, which come first and are kept apart, in the same format, so updates never touch them.
/// </summary>
public interface IAnimeRelations
{
	/// <summary>Raised when the rules change: updated from GitHub, or the user's changed.</summary>
	event EventHandler? Changed;

	AnimeRelationsInfo Info { get; }

	/// <summary>The user's own rules, in their file's order.</summary>
	IReadOnlyList<AnimeRelationRule> UserRules { get; }

	/// <summary>
	/// Where the anime's episode is in the numbering it continues (e.g. 16 for a 2nd season's 4th episode), from the first
	/// rule that covers it; null if none does.
	/// </summary>
	AnimeRelationMatch? FindContinuedEpisode(AnimeModel anime, int episode);

	/// <summary>The rules about the anime, as either side of them; the user's first.</summary>
	IReadOnlyList<AnimeRelationMatch> GetRules(AnimeModel anime);

	void AddUserRule(AnimeRelationRule rule);

	void ReplaceUserRule(int index, AnimeRelationRule rule);

	void RemoveUserRule(int index);

	/// <summary>Fetches anime-relations from GitHub; returns whether it got a usable copy.</summary>
	Task<bool> UpdateAsync(CancellationToken ct);

	/// <summary>Fetches anime-relations in the background, if it hasn't been in the last day.</summary>
	void UpdateIfStale();

	/// <summary>
	/// Makes sure there's a copy of anime-relations: downloads it if there's none on this PC yet (waiting for it), and
	/// otherwise refreshes it in the background if it's a day old. Call before looking rules up.
	/// </summary>
	Task EnsureAvailableAsync(CancellationToken ct);
}

public sealed class AnimeRelations : IAnimeRelations
{
	private const string Url = "https://raw.githubusercontent.com/erengy/anime-relations/master/anime-relations.txt";
	private static readonly TimeSpan s_maxAge = TimeSpan.FromDays(1);
	private static readonly HttpClient s_http = new() { Timeout = TimeSpan.FromSeconds(30) };

	private readonly string _downloadedFile = FileHelper.GetPath("anime-relations.txt");
	private readonly string _userFile = FileHelper.GetPath("anime-relations.user.txt");
	private readonly ILogger<AnimeRelations> _logger;
	private readonly Lock _lock = new();

	private AnimeRelationsFile _upstream;
	private List<AnimeRelationRule> _userRules;
	private Task? _updating;

	public AnimeRelations(ILogger<AnimeRelations> logger)
	{
		_logger = logger;
		_upstream = LoadUpstream();
		_userRules = [.. Read(_userFile)?.Rules ?? []];
	}

	public event EventHandler? Changed;

	public AnimeRelationsInfo Info => new(_upstream.Rules.Count, _upstream.LastModified,
										  File.Exists(_downloadedFile) ? File.GetLastWriteTime(_downloadedFile) : null);

	public IReadOnlyList<AnimeRelationRule> UserRules
	{
		get
		{
			lock (_lock)
			{
				return [.. _userRules];
			}
		}
	}

	public AnimeRelationMatch? FindContinuedEpisode(AnimeModel anime, int episode)
	{
		var ids = RelationIds.Of(anime);
		foreach (var (rule, isUserRule) in AllRules())
		{
			// The rule itself, and with "!", the destination counting on itself: the same numbers either way.
			if (rule.Destination.Matches(ids) && rule.ToSourceEpisode(episode) is { } continued)
			{
				return new AnimeRelationMatch(rule, continued, isUserRule);
			}
		}

		return null;
	}

	public IReadOnlyList<AnimeRelationMatch> GetRules(AnimeModel anime)
	{
		var ids = RelationIds.Of(anime);
		return
		[
			.. AllRules().Where(x => x.Rule.Destination.Matches(ids) || x.Rule.Source.Matches(ids))
						 .Select(x => new AnimeRelationMatch(x.Rule, x.Rule.SourceEpisodes.First, x.IsUserRule))
		];
	}

	public void AddUserRule(AnimeRelationRule rule) => ChangeUserRules(rules => rules.Add(rule));

	public void ReplaceUserRule(int index, AnimeRelationRule rule) => ChangeUserRules(rules => rules[index] = rule);

	public void RemoveUserRule(int index) => ChangeUserRules(rules => rules.RemoveAt(index));

	public async Task<bool> UpdateAsync(CancellationToken ct)
	{
		try
		{
			var text = await s_http.GetStringAsync(Url, ct);
			var file = AnimeRelationsFile.Parse(text);
			if (file.Rules.Count == 0)
			{
				_logger.LogWarning("anime-relations from GitHub has no rules; keeping the local copy");
				return false;
			}

			Directory.CreateDirectory(Path.GetDirectoryName(_downloadedFile)!);
			await File.WriteAllTextAsync(_downloadedFile, text, ct);
			lock (_lock)
			{
				_upstream = LoadUpstream();
			}

			Changed?.Invoke(this, EventArgs.Empty);
			return true;
		}
		catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException)
		{
			_logger.LogWarning(ex, "Couldn't update anime-relations");
			return false;
		}
	}

	public void UpdateIfStale()
	{
		if (File.Exists(_downloadedFile) && DateTime.Now - File.GetLastWriteTime(_downloadedFile) < s_maxAge)
		{
			return;
		}

		lock (_lock)
		{
			if (_updating is { IsCompleted: false })
			{
				return;
			}

			_updating = Task.Run(() => UpdateAsync(CancellationToken.None));
		}
	}

	public async Task EnsureAvailableAsync(CancellationToken ct)
	{
		if (File.Exists(_downloadedFile))
		{
			UpdateIfStale();
			return;
		}

		Task updating;
		lock (_lock)
		{
			// One download, however many ask for it at once.
			updating = _updating is { IsCompleted: false } running ? running : _updating = UpdateAsync(CancellationToken.None);
		}

		await updating.WaitAsync(ct);
	}

	/// <summary>Every rule, the user's first.</summary>
	private List<(AnimeRelationRule Rule, bool IsUserRule)> AllRules()
	{
		lock (_lock)
		{
			return [.. _userRules.Select(x => (x, true)), .. _upstream.Rules.Select(x => (x, false))];
		}
	}

	private void ChangeUserRules(Action<List<AnimeRelationRule>> change)
	{
		lock (_lock)
		{
			change(_userRules);
			Directory.CreateDirectory(Path.GetDirectoryName(_userFile)!);
			File.WriteAllText(_userFile, new AnimeRelationsFile(_userRules, null).Format(
				"""
				# Your own anime-relations rules, which Totoro uses before anime-relations' own
				# (https://github.com/erengy/anime-relations). Same syntax:
				#
				#   <MyAnimeList id>|<Kitsu id>|<AniList id>:<episodes> -> <MyAnimeList id>|<Kitsu id>|<AniList id>:<episodes>
				#
				# "?" for an unknown id or an open-ended range, "~" to repeat the source's id, and "!" at the end for the
				# destination numbering on itself too. Edit them in Totoro (Settings > Episode numbering) or here.
				"""));
		}

		Changed?.Invoke(this, EventArgs.Empty);
	}

	/// <summary>The copy downloaded from GitHub; empty until there is one.</summary>
	private AnimeRelationsFile LoadUpstream() => Read(_downloadedFile) ?? new AnimeRelationsFile([], null);

	private AnimeRelationsFile? Read(string file)
	{
		try
		{
			return File.Exists(file) ? AnimeRelationsFile.Parse(File.ReadAllText(file)) : null;
		}
		catch (IOException ex)
		{
			_logger.LogWarning(ex, "Couldn't read {File}", file);
			return null;
		}
	}
}
