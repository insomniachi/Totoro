using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Totoro.Core.Dialogs;
using Totoro.Core.Relations;
using Totoro.UI.Navigation;

namespace Totoro.ViewModels.Relations;

/// <param name="Index">Its position among the user's rules.</param>
public sealed record UserRuleItem(int Index, AnimeRelationRule Rule)
{
	public string Title => RelationRuleText.Title(Rule);
	public string Effect => RelationRuleText.Effect(Rule);
}

/// <summary>
/// Episode numbering rules, for providers that use absolute numbering across seasons: anime-relations' (kept
/// locally, updated from GitHub) and the user's own, which they add, edit and remove here.
/// </summary>
public sealed partial class AnimeRelationsViewModel : NavigatableViewModel
{
	private readonly IAnimeRelations _relations;
	private readonly IDialogService _dialogs;
	private readonly SynchronizationContext? _ui = SynchronizationContext.Current;

	public AnimeRelationsViewModel(IAnimeRelations relations, IDialogService dialogs)
	{
		_relations = relations;
		_dialogs = dialogs;
		Show();
		relations.Changed += OnChanged;
		// Downloads anime-relations if it never has been (the page then shows it, on Changed).
		_ = relations.EnsureAvailableAsync(CancellationToken.None);
	}

	/// <summary>e.g. "543 rules · changed 3 Aug 2026 · checked today".</summary>
	[ObservableProperty] public partial string UpstreamSummary { get; private set; } = "";

	[ObservableProperty] public partial IReadOnlyList<UserRuleItem> UserRules { get; private set; } = [];

	[ObservableProperty] public partial bool HasUserRules { get; private set; }

	/// <summary>How the last update went; empty (never null) before one.</summary>
	[ObservableProperty] public partial string UpdateMessage { get; private set; } = "";

	protected override Task OnNavigatedFromInternalAsync()
	{
		_relations.Changed -= OnChanged;
		return Task.CompletedTask;
	}

	[RelayCommand]
	private async Task UpdateAsync()
	{
		UpdateMessage = "Updating…";
		UpdateMessage = await _relations.UpdateAsync(CancellationToken.None)
			? "Up to date."
			: "Couldn't update it; the copy on this PC is still used.";
	}

	[RelayCommand]
	private async Task AddAsync()
	{
		if (await _dialogs.ShowAsync<RelationRuleDialogViewModel, AnimeRelationRule>(new RelationRuleRequest()) is { } rule)
		{
			_relations.AddUserRule(rule);
		}
	}

	[RelayCommand]
	private async Task EditAsync(UserRuleItem item)
	{
		if (await _dialogs.ShowAsync<RelationRuleDialogViewModel, AnimeRelationRule>(new RelationRuleRequest(item.Rule)) is { } rule)
		{
			_relations.ReplaceUserRule(item.Index, rule);
		}
	}

	[RelayCommand]
	private void Remove(UserRuleItem item) => _relations.RemoveUserRule(item.Index);

	// Updates arrive on a background thread.
	private void OnChanged(object? sender, EventArgs e)
	{
		if (_ui is null)
		{
			Show();
		}
		else
		{
			_ui.Post(_ => Show(), null);
		}
	}

	private void Show()
	{
		var info = _relations.Info;
		if (info.LastChecked is null)
		{
			UpstreamSummary = "Not downloaded yet. It's downloaded the first time it's needed.";
			UserRules = [.. _relations.UserRules.Select((x, i) => new UserRuleItem(i, x))];
			HasUserRules = UserRules.Count > 0;
			return;
		}

		List<string> parts = [$"{info.RuleCount} rules"];
		if (DateTime.TryParse(info.LastModified, out var modified))
		{
			parts.Add($"changed {modified:d MMM yyyy}");
		}

		if (info.LastChecked is { } checkedAt)
		{
			parts.Add(checkedAt.Date == DateTime.Today ? "checked today" : $"checked {checkedAt:d MMM yyyy}");
		}

		UpstreamSummary = string.Join(" · ", parts);
		UserRules = [.. _relations.UserRules.Select((x, i) => new UserRuleItem(i, x))];
		HasUserRules = UserRules.Count > 0;
	}
}
