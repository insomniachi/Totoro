using System.ComponentModel;
using Totoro.Core.Agent;
using Totoro.Core.Relations;
using Totoro.ViewModels.Relations;

namespace Totoro.Agent;

[Description("An episode numbering rule: where a season's episodes are when its provider numbers it on from an earlier entry.")]
public sealed record NumberingRule(
	[property: Description("Which anime it's about, e.g. \"Seihantai na Kimi to Boku → 2nd Season\".")]
	string Title,
	[property: Description("What it does, e.g. \"Episodes 1-13 are numbered 13-25\".")]
	string Effect,
	[property: Description("Whether it's one of the user's own rules (which can be removed by index) rather than anime-relations'.")]
	bool IsUserRule,
	[property: Description("Its index among the user's rules, for remove_numbering_rule; null for anime-relations' rules.")]
	int? Index,
	[property: Description("The rule in anime-relations syntax.")]
	string Line);

[Description("anime-relations, kept on this PC, and the user's own numbering rules.")]
public sealed record NumberingRules(int AnimeRelationsRules, string? AnimeRelationsLastChanged, IReadOnlyList<NumberingRule> UserRules);

/// <summary>
/// Agent tools for episode numbering: anime-relations and the user's own rules, for providers that number a season on
/// from an earlier one (see <see cref="IAnimeRelations"/>). An anime's own numbering setting is in update_anime_settings.
/// </summary>
public sealed class RelationsTools(AnimeLookup lookup, IAnimeRelations relations)
{
	[AgentTool("get_numbering_rules")]
	[Description("The episode numbering rules about an anime (where its episodes are when its provider numbers it on from an earlier season), " +
				 "or with no anime, anime-relations' state and all of the user's own rules.")]
	public async Task<object> GetNumberingRulesAsync(long? animeId = null)
	{
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
	[Description("Adds a numbering rule of the user's own, for when a provider numbers a season on from an earlier entry " +
				 "(e.g. one show for both seasons, the 2nd season's first episode numbered 13) and anime-relations has no rule for it.")]
	public async Task<NumberingRule> AddNumberingRuleAsync(
		[Description("The season the rule places, e.g. the 2nd season.")] long seasonAnimeId,
		[Description("The entry whose numbering the provider continues, e.g. the first season.")] long earlierAnimeId,
		[Description("The number the provider gives the season's first episode, e.g. 13.")] int earlierFirstEpisode,
		[Description("The season's first episode the rule covers.")] int seasonFirstEpisode = 1,
		[Description("The season's last episode the rule covers; leave it out while the season airs.")] int? seasonLastEpisode = null,
		[Description("Also when the provider names the show as the season itself but still numbers it on.")] bool alsoWhenNamedAsSeason = true)
	{
		if (earlierFirstEpisode < 1 || seasonFirstEpisode < 1 || seasonLastEpisode < seasonFirstEpisode)
		{
			throw new AgentToolException("Episode numbers start at 1, and the last episode can't come before the first.");
		}

		var season = await lookup.GetAsync(seasonAnimeId);
		var earlier = await lookup.GetAsync(earlierAnimeId);
		var last = seasonLastEpisode ?? (season.TotalEpisodes is > 0 and var total ? total : null);
		var rule = new AnimeRelationRule(
			RelationIds.Of(earlier),
			new EpisodeRange(earlierFirstEpisode, last is { } l ? earlierFirstEpisode + l - seasonFirstEpisode : null),
			RelationIds.Of(season),
			new EpisodeRange(seasonFirstEpisode, last),
			alsoWhenNamedAsSeason)
		{
			Title = $"{earlier.Title} -> {season.Title}",
		};

		relations.AddUserRule(rule);
		return new NumberingRule(RelationRuleText.Title(rule), RelationRuleText.Effect(rule), true, relations.UserRules.Count - 1, rule.ToLine());
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
