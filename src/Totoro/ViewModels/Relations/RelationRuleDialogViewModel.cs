using Totoro.Core.Dialogs;
using Totoro.Core.Modules;
using Totoro.Core.Modules.Anime;
using Totoro.Core.Relations;

namespace Totoro.ViewModels.Relations;

/// <param name="Rule">The rule to edit; null to add one.</param>
/// <param name="Season">For a new rule, the season it's for, if known.</param>
public sealed record RelationRuleRequest(AnimeRelationRule? Rule = null, AnimeModel? Season = null);

/// <summary>Adds or edits one of the user's anime-relations rules. Returns the rule; saving it is up to the caller.</summary>
public sealed class RelationRuleDialogViewModel : DialogViewModel<AnimeRelationRule>
{
	public RelationRuleDialogViewModel(RelationRuleRequest request, IFactory<IAnimeMetadataProvider, Guid> databases)
	{
		var database = databases.CreateDefault();
		Editor = request.Rule is { } rule ? new RelationRuleEditorViewModel(database, rule) : new RelationRuleEditorViewModel(database, request.Season);
		Title = request.Rule is null ? "Add an absolute numbering rule" : "Edit absolute numbering rule";
		PrimaryButtonText = "Save";
		CanConfirm = Editor.IsValid;
		Editor.PropertyChanged += (_, _) => CanConfirm = Editor.IsValid;
	}

	public RelationRuleEditorViewModel Editor { get; }

	public override Task<bool> ConfirmAsync()
	{
		Result = Editor.ToRule();
		return Task.FromResult(Result is not null);
	}
}
