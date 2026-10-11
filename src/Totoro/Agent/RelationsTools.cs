using System.ComponentModel;
using Totoro.Core.Agent;
using Totoro.Core.Modules.Anime;
using Totoro.Core.Relations;
using Totoro.ViewModels.Relations;

namespace Totoro.Agent;

[Description("An absolute numbering rule: where a season's episodes are when its provider uses absolute numbering, carrying on across seasons.")]
public sealed record NumberingRule(
	[property: Description("Which anime it's about, e.g. \"Seihantai na Kimi to Boku → 2nd Season\".")]
	string Title,
	[property: Description("What it does, e.g. \"Episodes 1-13 are 13-25 in absolute numbering\".")]
	string Effect,
	[property: Description("Whether it's one of the user's own rules (which can be removed by index) rather than anime-relations'.")]
	bool IsUserRule,
	[property: Description("Its index among the user's rules, for remove_numbering_rule; null for anime-relations' rules.")]
	int? Index,
	[property: Description("The rule in anime-relations syntax.")]
	string Line);

[Description("A season of a series.")]
public sealed record SeriesSeason(
	[property: Description("Its place in the series, from 1.")]
	int Number,
	long AnimeId,
	string Title,
	int? Episodes,
	[property: Description("The absolute number of its first episode, counting from the first season; null if an earlier season's length isn't known.")]
	int? AbsoluteFirstEpisode);

[Description("anime-relations, kept on this PC, and the user's own numbering rules.")]
public sealed record NumberingRules(int AnimeRelationsRules, string? AnimeRelationsLastChanged, IReadOnlyList<NumberingRule> UserRules);

/// <summary>
/// Agent tools for episode numbering: anime-relations and the user's own rules, for providers that number a season on
/// from an earlier one (see <see cref="IAnimeRelations"/>). An anime's own numbering setting is in update_anime_settings.
/// </summary>
public sealed class RelationsTools(AnimeLookup lookup, IAnimeRelations relations)
{
	[AgentTool("get_numbering_rules")]
	[Description("The absolute numbering rules about an anime (where its episodes are when its provider uses absolute numbering), " +
				 "or with no anime, anime-relations' state and all of the user's own rules.")]
	public async Task<object> GetNumberingRulesAsync(long? animeId = null)
	{
		await relations.EnsureAvailableAsync(CancellationToken.None);
		var userRules = relations.UserRules;
		NumberingRule Describe(AnimeRelationRule rule, bool isUserRule)
		{
			var index = isUserRule ? userRules.ToList().IndexOf(rule) : -1;
			return new NumberingRule(RelationRuleText.Title(rule), RelationRuleText.Effect(rule), isUserRule, index >= 0 ? index : null, rule.ToLine());
		}

		if (animeId is not { } id)
		{
			var info = relations.Info;
			return new NumberingRules(info.RuleCount, info.LastModified, [.. userRules.Select(x => Describe(x, true))]);
		}

		var anime = await lookup.GetAsync(id);
		return relations.GetRules(anime).Select(x => Describe(x.Rule, x.IsUserRule)).ToList();
	}

	[AgentTool("add_numbering_rule")]
	[Description("Adds an absolute numbering rule of the user's own, for when a provider uses absolute numbering " +
				 "(e.g. one show for both seasons, the 2nd season from 13) and anime-relations has no rule for it.")]
	public async Task<NumberingRule> AddNumberingRuleAsync(
		[Description("The season the rule places, e.g. the 2nd season.")] long seasonAnimeId,
		[Description("Where absolute numbering starts; leave it out for the series' first season (see get_series_seasons).")]
		long? earlierAnimeId = null,
		[Description("The absolute number of the season's first episode, e.g. 13; leave it out to count it from the seasons in between.")]
		int? earlierFirstEpisode = null,
		[Description("The season's first episode the rule covers.")] int seasonFirstEpisode = 1,
		[Description("The season's last episode the rule covers; leave it out while the season airs.")] int? seasonLastEpisode = null)
	{
		if (earlierFirstEpisode < 1 || seasonFirstEpisode < 1 || seasonLastEpisode < seasonFirstEpisode)
		{
			throw new AgentToolException("Episode numbers start at 1, and the last episode can't come before the first.");
		}

		var season = await lookup.GetAsync(seasonAnimeId);
		AnimeModel earlier;
		if (earlierAnimeId is { } earlierId && earlierFirstEpisode is { } given)
		{
			earlier = await lookup.GetAsync(earlierId);
		}
		else
		{
			// Filled in from the series' seasons: absolute numbering from the first, over the seasons in between.
			var seasons = await SeasonMap.GetSeasonsAsync(lookup.Database, seasonAnimeId, CancellationToken.None);
			var index = seasons.ToList().FindIndex(x => x.Id == seasonAnimeId);
			var from = earlierAnimeId is { } id ? seasons.ToList().FindIndex(x => x.Id == id) : 0;
			if (index <= 0 || from < 0 || from >= index)
			{
				throw new AgentToolException(index <= 0
					? "It's the first season of its series: absolute numbering starts with it, so it needs no rule."
					: "That entry isn't one of the seasons before it; see get_series_seasons.");
			}

			earlier = seasons[from];
			given = earlierFirstEpisode ?? (SeasonMap.AbsoluteFirstEpisode(seasons, from, index) + seasonFirstEpisode - 1)
				?? throw new AgentToolException("A season in between hasn't a known episode count; give earlierFirstEpisode.");
		}

		var last = seasonLastEpisode ?? (season.TotalEpisodes is > 0 and var total ? total : null);
		var rule = new AnimeRelationRule(
			RelationIds.Of(earlier),
			new EpisodeRange(given, last is { } l ? given + l - seasonFirstEpisode : null),
			RelationIds.Of(season),
			new EpisodeRange(seasonFirstEpisode, last),
			AlsoCountsOnItself: true)
		{
			Title = $"{earlier.Title} -> {season.Title}",
		};

		relations.AddUserRule(rule);
		return new NumberingRule(RelationRuleText.Title(rule), RelationRuleText.Effect(rule), true, relations.UserRules.Count - 1, rule.ToLine());
	}

	[AgentTool("get_series_seasons")]
	[Description("The seasons of the series an anime belongs to, first to last (TV and ONA entries, found through prequels and sequels), " +
				 "with their episode counts and absolute numbers: what a provider using absolute numbering counts through.")]
	public async Task<IReadOnlyList<SeriesSeason>> GetSeriesSeasonsAsync(long animeId)
	{
		var seasons = await SeasonMap.GetSeasonsAsync(lookup.Database, animeId, CancellationToken.None);
		return
		[
			.. seasons.Select((x, i) => new SeriesSeason(i + 1, x.Id, x.Title, x.TotalEpisodes is > 0 ? x.TotalEpisodes : null,
														SeasonMap.AbsoluteFirstEpisode(seasons, 0, i))),
		];
	}

	[AgentTool("remove_numbering_rule", RequiresApproval = true)]
	[Description("Removes one of the user's own numbering rules, by its index (from get_numbering_rules).")]
	public string RemoveNumberingRule(int index)
	{
		var rules = relations.UserRules;
		if (index < 0 || index >= rules.Count)
		{
			throw new AgentToolException($"There's no rule {index}; the user has {rules.Count}.");
		}

		relations.RemoveUserRule(index);
		return $"Removed the rule for {RelationRuleText.Title(rules[index])}.";
	}

	[AgentTool("update_anime_relations")]
	[Description("Fetches the latest anime-relations from GitHub (it's also updated by itself about once a day).")]
	public async Task<string> UpdateAnimeRelationsAsync()
	{
		var updated = await relations.UpdateAsync(CancellationToken.None);
		var info = relations.Info;
		return updated
			? $"Updated: {info.RuleCount} rules, last changed {info.LastModified}."
			: $"Couldn't update it; still using the copy on this PC ({info.RuleCount} rules, last changed {info.LastModified}).";
	}
}
