using Totoro.Core.Modules.Anime;
using Totoro.ViewModels;
using Xunit;

namespace Totoro.Tests;

public class TrackingFieldTests
{
	[Fact]
	public void Changed_GivesOnlyEditedFields()
	{
		var notes = new TextField("notes", "Notes", null);
		var priority = new ChoiceField("priority", "Priority", ["Low", "Medium", "High"], "Low");
		var rewatches = new NumberField("rewatches", "Rewatches", 2);
		var tracking = new Tracking { Fields = [notes, priority, rewatches] };

		notes.Value = "";
		priority.Value = "High";
		rewatches.Value = double.NaN; // an emptied number box

		Assert.Null(tracking.Changed<TextField>("notes"));
		Assert.Equal(2, tracking.Changed<ChoiceField>("priority")?.Index);
		Assert.Null(tracking.Changed<NumberField>("rewatches"));
		Assert.Null(tracking.Clone().Fields);
	}

	[Theory]
	[InlineData(100, 1, ScoreStyle.Number, 77, 7.7)]
	[InlineData(10, 0.1, ScoreStyle.Number, 7.5, 7.5)]
	[InlineData(10, 1, ScoreStyle.Number, 8, 8)]
	[InlineData(5, 1, ScoreStyle.Stars, 4, 8)]
	[InlineData(3, 1, ScoreStyle.Smileys, 1, 3.5)]
	[InlineData(3, 1, ScoreStyle.Smileys, 2, 6)]
	[InlineData(3, 1, ScoreStyle.Smileys, 3, 8.5)]
	public void Score_RoundTripsThroughOutOfTen(double max, double step, ScoreStyle style, double shown, double outOfTen)
	{
		var form = new TrackingForm(max, step, [], style);

		Assert.Equal(outOfTen, TrackingDialogViewModel.ToTen(shown, form), 6);
		Assert.Equal(shown, TrackingDialogViewModel.FromTen(outOfTen, form), 6);
	}

	[Fact]
	public void Smileys_ReadAniListBands()
	{
		var form = new TrackingForm(3, 1, [], ScoreStyle.Smileys);

		Assert.Equal(0, TrackingDialogViewModel.FromTen(0, form));
		Assert.Equal(1, TrackingDialogViewModel.FromTen(3, form));
		Assert.Equal(2, TrackingDialogViewModel.FromTen(5, form));
		Assert.Equal(3, TrackingDialogViewModel.FromTen(10, form));
	}
}
