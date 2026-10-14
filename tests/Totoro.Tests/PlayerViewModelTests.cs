using Microsoft.Extensions.Logging.Abstractions;
using Totoro.Core.Modules.Media;
using Totoro.ViewModels.Player;
using Xunit;

namespace Totoro.Tests;

public class PlayerViewModelTests
{
	private static PlayerViewModel CreateViewModel(PlayerAspectRatio ratio = PlayerAspectRatio.Fit)
	{
		return new PlayerViewModel(volume: 100, aspectRatio: ratio, logger: NullLogger.Instance);
	}

	[Fact]
	public void AspectRatio_DefaultsToFitOrSpecifiedInitialValue()
	{
		var defaultVm = new PlayerViewModel(100, NullLogger.Instance);
		Assert.Equal(PlayerAspectRatio.Fit, defaultVm.AspectRatio);

		var customVm = CreateViewModel(PlayerAspectRatio.Stretch);
		Assert.Equal(PlayerAspectRatio.Stretch, customVm.AspectRatio);
	}

	[Fact]
	public void CycleAspectRatio_CyclesInExpectedSequence()
	{
		var vm = CreateViewModel(PlayerAspectRatio.Fit);

		vm.CycleAspectRatioCommand.Execute(null);
		Assert.Equal(PlayerAspectRatio.Stretch, vm.AspectRatio);

		vm.CycleAspectRatioCommand.Execute(null);
		Assert.Equal(PlayerAspectRatio.Ratio16_9, vm.AspectRatio);

		vm.CycleAspectRatioCommand.Execute(null);
		Assert.Equal(PlayerAspectRatio.Ratio4_3, vm.AspectRatio);

		vm.CycleAspectRatioCommand.Execute(null);
		Assert.Equal(PlayerAspectRatio.Ratio21_9, vm.AspectRatio);

		vm.CycleAspectRatioCommand.Execute(null);
		Assert.Equal(PlayerAspectRatio.Fit, vm.AspectRatio);
	}

	[Fact]
	public void SetAspectRatio_SetsRatioAndFormatsText()
	{
		var vm = CreateViewModel(PlayerAspectRatio.Fit);

		vm.SetAspectRatio(PlayerAspectRatio.Stretch);
		Assert.Equal(PlayerAspectRatio.Stretch, vm.AspectRatio);
		Assert.Equal("Stretch to fill", PlayerViewModel.PlayerAspectRatioText(PlayerAspectRatio.Stretch));

		vm.SetAspectRatio(PlayerAspectRatio.Ratio16_9);
		Assert.Equal(PlayerAspectRatio.Ratio16_9, vm.AspectRatio);
		Assert.Equal("16:9", PlayerViewModel.PlayerAspectRatioText(PlayerAspectRatio.Ratio16_9));
	}

	[Fact]
	public void UpdateViewportSize_StoresDimensions()
	{
		var vm = CreateViewModel(PlayerAspectRatio.Fit);

		vm.UpdateViewportSize(1920, 1080);
		Assert.Equal(1920, vm.ViewportWidth);
		Assert.Equal(1080, vm.ViewportHeight);

		// Negative / zero values ignored
		vm.UpdateViewportSize(0, -100);
		Assert.Equal(1920, vm.ViewportWidth);
		Assert.Equal(1080, vm.ViewportHeight);
	}

	[Fact]
	public void SkipButton_WhenNoSegments_RemainsVisibleWith85sText()
	{
		var vm = CreateViewModel();

		// Initially chapters is empty
		Assert.False(vm.HasOpOrEd);
		Assert.True(vm.IsSkipButtonVisible);
		Assert.Equal("+85s", vm.SkipButtonText);

		// Moving position anywhere still leaves button visible as +85s
		vm.OnTime(TimeSpan.FromSeconds(30));
		Assert.True(vm.IsSkipButtonVisible);
		Assert.Equal("+85s", vm.SkipButtonText);

		vm.OnTime(TimeSpan.FromSeconds(500));
		Assert.True(vm.IsSkipButtonVisible);
		Assert.Equal("+85s", vm.SkipButtonText);
	}

	[Fact]
	public async Task SkipButton_WhenSegmentsExist_AdaptsToCurrentPlaybackSegment()
	{
		var vm = CreateViewModel();

		// Set duration to 24 minutes (1440 seconds)
		// Opening: 10s -> 100s
		// Ending: 1350s -> 1440s
		await vm.SetChaptersAsync(
		[
			new MediaChapter("Episode", TimeSpan.Zero),
			new MediaChapter(nameof(MediaSegmentType.Opening), TimeSpan.FromSeconds(10)),
			new MediaChapter("Episode", TimeSpan.FromSeconds(100)),
			new MediaChapter(nameof(MediaSegmentType.Ending), TimeSpan.FromSeconds(1350)),
		]);

		Assert.True(vm.HasOpOrEd);

		// 1. At 5s (before OP): button should be collapsed
		vm.OnTime(TimeSpan.FromSeconds(5));
		Assert.False(vm.IsSkipButtonVisible);

		// 2. At 10s (OP start): button visible as "Skip OP"
		vm.OnTime(TimeSpan.FromSeconds(10));
		Assert.True(vm.IsSkipButtonVisible);
		Assert.Equal("Skip OP", vm.SkipButtonText);
		Assert.Equal("Skip opening", vm.SkipButtonToolTip);

		// 3. At 50s (mid OP): button visible as "Skip OP"
		vm.OnTime(TimeSpan.FromSeconds(50));
		Assert.True(vm.IsSkipButtonVisible);
		Assert.Equal("Skip OP", vm.SkipButtonText);

		// 4. At 100s (Episode body): button collapsed
		vm.OnTime(TimeSpan.FromSeconds(100));
		Assert.False(vm.IsSkipButtonVisible);

		// 5. At 500s (mid Episode body): button collapsed
		vm.OnTime(TimeSpan.FromSeconds(500));
		Assert.False(vm.IsSkipButtonVisible);

		// 6. At 1360s (during ED): button visible as "Skip ED"
		vm.OnTime(TimeSpan.FromSeconds(1360));
		Assert.True(vm.IsSkipButtonVisible);
		Assert.Equal("Skip ED", vm.SkipButtonText);
		Assert.Equal("Skip ending", vm.SkipButtonToolTip);
	}

	[Fact]
	public async Task SkipAheadCommand_WhenInOpening_SkipsToEndOfOpening()
	{
		var vm = CreateViewModel();

		await vm.SetChaptersAsync(
		[
			new MediaChapter("Episode", TimeSpan.Zero),
			new MediaChapter(nameof(MediaSegmentType.Opening), TimeSpan.FromSeconds(10)),
			new MediaChapter("Episode", TimeSpan.FromSeconds(100)),
			new MediaChapter(nameof(MediaSegmentType.Ending), TimeSpan.FromSeconds(1350)),
		]);

		// Seek to 15s (during OP)
		vm.OnTime(TimeSpan.FromSeconds(15));
		Assert.True(vm.IsSkipButtonVisible);
		Assert.Equal("Skip OP", vm.SkipButtonText);

		// Execute SkipAhead
		await vm.SkipAheadCommand.ExecuteAsync(null);

		// Position should now be at the end of the OP (100s)
		Assert.Equal(100, vm.Position);
		// And skip button should now be collapsed (we're in normal episode)
		Assert.False(vm.IsSkipButtonVisible);
	}

	[Fact]
	public async Task SkipAheadCommand_WhenInEnding_SkipsToEndOfEnding()
	{
		var vm = CreateViewModel();

		await vm.SetChaptersAsync(
		[
			new MediaChapter("Episode", TimeSpan.Zero),
			new MediaChapter(nameof(MediaSegmentType.Opening), TimeSpan.FromSeconds(10)),
			new MediaChapter("Episode", TimeSpan.FromSeconds(100)),
			new MediaChapter(nameof(MediaSegmentType.Ending), TimeSpan.FromSeconds(1350)),
			new MediaChapter("Episode", TimeSpan.FromSeconds(1440)),
		]);

		// Seek to 1360s (during ED)
		vm.OnTime(TimeSpan.FromSeconds(1360));
		Assert.True(vm.IsSkipButtonVisible);
		Assert.Equal("Skip ED", vm.SkipButtonText);

		// Execute SkipAhead
		await vm.SkipAheadCommand.ExecuteAsync(null);

		// Position should now be at 1440s
		Assert.Equal(1440, vm.Position);
		Assert.False(vm.IsSkipButtonVisible);
	}

	[Fact]
	public async Task SkipAheadCommand_WhenNoSegments_Jumps85Seconds()
	{
		var vm = CreateViewModel();

		vm.OnTime(TimeSpan.FromSeconds(15));
		Assert.Equal("+85s", vm.SkipButtonText);

		await vm.SkipAheadCommand.ExecuteAsync(null);

		Assert.Equal(15 + 85, vm.Position);
		Assert.True(vm.IsSkipButtonVisible);
		Assert.Equal("+85s", vm.SkipButtonText);
	}
}
