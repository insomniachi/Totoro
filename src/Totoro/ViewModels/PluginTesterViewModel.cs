using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml.Controls;
using Totoro.Core.Modules.Streaming;
using Totoro.Core.Plugins;
using Totoro.UI.Navigation;

namespace Totoro.ViewModels;

public sealed record ProviderFilterOption(Guid? Id, string Name)
{
	public override string ToString() => Name;
}

public sealed partial class StageItemViewModel : ObservableObject
{
	public StageItemViewModel(string name)
	{
		Name = name;
		Message = "Not run yet.";
	}

	public string Name { get; }

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(IsPassed))]
	[NotifyPropertyChangedFor(nameof(IsFailed))]
	[NotifyPropertyChangedFor(nameof(IsRunning))]
	[NotifyPropertyChangedFor(nameof(IsSkipped))]
	[NotifyPropertyChangedFor(nameof(IsNotStarted))]
	public partial PluginTestStageStatus Status { get; set; } = PluginTestStageStatus.NotStarted;

	public bool IsPassed => Status == PluginTestStageStatus.Passed;
	public bool IsFailed => Status == PluginTestStageStatus.Failed;
	public bool IsRunning => Status == PluginTestStageStatus.Running;
	public bool IsSkipped => Status == PluginTestStageStatus.Skipped;
	public bool IsNotStarted => Status == PluginTestStageStatus.NotStarted;

	[ObservableProperty]
	public partial string Message { get; set; }

	[ObservableProperty]
	public partial string DurationText { get; set; } = "";
}

public sealed partial class PluginTestItemViewModel : ObservableObject
{
	public PluginTestItemViewModel(IAnimeProvider provider, PluginInfo? pluginInfo)
	{
		Provider = provider;
		Id = provider.Id;
		Name = provider.Name;
		Version = pluginInfo?.Version ?? "1.0.0";
		IsDev = pluginInfo?.IsDev ?? false;
		Description = pluginInfo?.Description ?? "";

		SearchStage = new StageItemViewModel("Search");
		EpisodesStage = new StageItemViewModel("Episodes");
		ServersStage = new StageItemViewModel("Video servers");
	}

	public IAnimeProvider Provider { get; }
	public Guid Id { get; }
	public string Name { get; }
	public string Version { get; }
	public bool IsDev { get; }
	public string Description { get; }

	/// <summary>e.g. "Version 1.0.0 · Dev plugin".</summary>
	public string VersionText => IsDev ? $"Version {Version} · Dev plugin" : $"Version {Version}";

	public StageItemViewModel SearchStage { get; }
	public StageItemViewModel EpisodesStage { get; }
	public StageItemViewModel ServersStage { get; }

	public Action<PluginTestItemViewModel>? OnRunSingle { get; set; }

	[RelayCommand]
	private void RunTest() => OnRunSingle?.Invoke(this);

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(OverallStatusText))]
	[NotifyPropertyChangedFor(nameof(IsPassed))]
	[NotifyPropertyChangedFor(nameof(IsFailed))]
	[NotifyPropertyChangedFor(nameof(IsRunning))]
	[NotifyPropertyChangedFor(nameof(IsNotStarted))]
	public partial PluginTestStageStatus OverallStatus { get; set; } = PluginTestStageStatus.NotStarted;

	public bool IsPassed => OverallStatus == PluginTestStageStatus.Passed;
	public bool IsFailed => OverallStatus == PluginTestStageStatus.Failed;
	public bool IsRunning => OverallStatus == PluginTestStageStatus.Running;
	public bool IsNotStarted => OverallStatus == PluginTestStageStatus.NotStarted;

	public string OverallStatusText => OverallStatus switch
	{
		PluginTestStageStatus.Passed => "Passed",
		PluginTestStageStatus.Failed => "Failed",
		PluginTestStageStatus.Running => "Testing…",
		_ => "Not tested"
	};

	[ObservableProperty]
	public partial string TotalDurationText { get; set; } = "";

	/// <summary>Whether the card shows each stage's result and what it found.</summary>
	[ObservableProperty]
	public partial bool IsExpanded { get; set; }

	[ObservableProperty]
	public partial string ShowsDetails { get; set; } = "";

	[ObservableProperty]
	public partial string EpisodesDetails { get; set; } = "";

	[ObservableProperty]
	public partial string ServersDetails { get; set; } = "";

	[ObservableProperty]
	public partial string ErrorDetails { get; set; } = "";

	[ObservableProperty]
	public partial bool HasError { get; set; }

	public void Reset()
	{
		OverallStatus = PluginTestStageStatus.NotStarted;
		TotalDurationText = "";
		ShowsDetails = "";
		EpisodesDetails = "";
		ServersDetails = "";
		ErrorDetails = "";
		HasError = false;

		SearchStage.Status = PluginTestStageStatus.NotStarted;
		SearchStage.Message = "Not run yet.";
		SearchStage.DurationText = "";

		EpisodesStage.Status = PluginTestStageStatus.NotStarted;
		EpisodesStage.Message = "Not run yet.";
		EpisodesStage.DurationText = "";

		ServersStage.Status = PluginTestStageStatus.NotStarted;
		ServersStage.Message = "Not run yet.";
		ServersStage.DurationText = "";
	}

	public void ApplyResult(AnimePluginTestResult result)
	{
		OverallStatus = result.Passed ? PluginTestStageStatus.Passed : PluginTestStageStatus.Failed;
		TotalDurationText = $"{result.TotalDuration.TotalSeconds:F2}s";

		SearchStage.Status = result.SearchStage.Status;
		SearchStage.Message = result.SearchStage.Message;
		SearchStage.DurationText = $"{result.SearchStage.Duration.TotalMilliseconds:F0}ms";

		EpisodesStage.Status = result.EpisodesStage.Status;
		EpisodesStage.Message = result.EpisodesStage.Message;
		EpisodesStage.DurationText = $"{result.EpisodesStage.Duration.TotalMilliseconds:F0}ms";

		ServersStage.Status = result.ServersStage.Status;
		ServersStage.Message = result.ServersStage.Message;
		ServersStage.DurationText = $"{result.ServersStage.Duration.TotalMilliseconds:F0}ms";

		if (result.Shows is { Count: > 0 } shows)
		{
			ShowsDetails = string.Join("\n", shows.Take(10).Select(s => $"• {s.Title} (Id: {s.Id}, Score: {s.Score:F2})"));
			if (shows.Count > 10)
			{
				ShowsDetails += $"\n... and {shows.Count - 10} more shows.";
			}
		}

		if (result.Episodes is { Count: > 0 } episodes)
		{
			EpisodesDetails = string.Join("\n", episodes.Take(15).Select(e => $"• Episode {e.Number} (Id: {e.Id})"));
			if (episodes.Count > 15)
			{
				EpisodesDetails += $"\n... and {episodes.Count - 15} more episodes.";
			}
		}

		if (result.Servers is { Count: > 0 } servers)
		{
			ServersDetails = string.Join("\n", servers.Select(s => $"• {s.Name} [{s.Kind}] -> {s.Url}"));
		}

		var error = result.SearchStage.ErrorDetails
			?? result.EpisodesStage.ErrorDetails
			?? result.ServersStage.ErrorDetails;

		if (!string.IsNullOrWhiteSpace(error))
		{
			ErrorDetails = error;
			HasError = true;
		}
		else if (!result.Passed)
		{
			ErrorDetails = result.SearchStage.Status == PluginTestStageStatus.Failed ? result.SearchStage.Message
				: result.EpisodesStage.Status == PluginTestStageStatus.Failed ? result.EpisodesStage.Message
				: result.ServersStage.Message;
			HasError = true;
		}
		else
		{
			ErrorDetails = "";
			HasError = false;
		}
	}
}

public partial class PluginTesterViewModel : ObservableObject
{
	private readonly IAnimePluginTesterService _tester;
	private readonly IPluginLoader _pluginLoader;
	private CancellationTokenSource? _cancellation;

	public PluginTesterViewModel(
		IAnimePluginTesterService tester,
		IPluginLoader pluginLoader)
	{
		_tester = tester;
		_pluginLoader = pluginLoader;

		LoadProviders();
	}

	public ObservableCollection<PluginTestItemViewModel> Plugins { get; } = [];
	public ObservableCollection<ProviderFilterOption> FilterOptions { get; } = [];

	[ObservableProperty]
	public partial ProviderFilterOption? SelectedFilterOption { get; set; }

	[ObservableProperty]
	public partial string ShowTitle { get; set; } = "Frieren";

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(CanRun))]
	public partial bool IsBusy { get; set; }

	public bool CanRun => !IsBusy;

	[ObservableProperty]
	public partial double TimeoutSeconds { get; set; } = AnimePluginTesterService.DefaultTimeout.TotalSeconds;

	[ObservableProperty]
	public partial bool HasProviders { get; set; }

	[ObservableProperty]
	public partial string StatusMessage { get; set; } = "";

	[ObservableProperty]
	public partial bool InfoBarIsOpen { get; set; }

	[ObservableProperty]
	public partial string InfoBarTitle { get; set; } = "";

	[ObservableProperty]
	public partial string InfoBarMessage { get; set; } = "";

	[ObservableProperty]
	public partial InfoBarSeverity InfoBarSeverity { get; set; } = InfoBarSeverity.Informational;

	[RelayCommand]
	public void LoadProviders()
	{
		Plugins.Clear();
		FilterOptions.Clear();

		var providers = _tester.GetAvailableProviders();
		HasProviders = providers.Count > 0;

		FilterOptions.Add(new ProviderFilterOption(null, "All anime providers"));

		foreach (var provider in providers)
		{
			var pluginInfo = _pluginLoader.Plugins.FirstOrDefault(p => p.Id == provider.Id);
			var item = new PluginTestItemViewModel(provider, pluginInfo)
			{
				OnRunSingle = target => _ = RunSingleTestAsync(target)
			};
			Plugins.Add(item);
			FilterOptions.Add(new ProviderFilterOption(provider.Id, provider.Name));
		}

		SelectedFilterOption = FilterOptions.FirstOrDefault();

		// With none, the page's empty state says so (and leads to the Plugins page).
		InfoBarIsOpen = false;
	}

	[RelayCommand]
	public async Task RunTestsAsync()
	{
		if (IsBusy)
		{
			return;
		}

		var query = ShowTitle.Trim();
		if (string.IsNullOrWhiteSpace(query))
		{
			InfoBarIsOpen = true;
			InfoBarSeverity = InfoBarSeverity.Warning;
			InfoBarTitle = "Missing show title";
			InfoBarMessage = "Please enter the title of a show to test (e.g. 'Frieren', 'One Piece').";
			return;
		}

		if (Plugins.Count == 0)
		{
			InfoBarIsOpen = true;
			InfoBarSeverity = InfoBarSeverity.Warning;
			InfoBarTitle = "No plugins to test";
			InfoBarMessage = "Install anime provider plugins before running tests.";
			return;
		}

		IsBusy = true;
		InfoBarIsOpen = false;
		_cancellation?.Cancel();
		_cancellation?.Dispose();
		_cancellation = new CancellationTokenSource();
		var ct = _cancellation.Token;

		var targetItems = SelectedFilterOption?.Id is { } targetId
			? Plugins.Where(p => p.Id == targetId).ToList()
			: Plugins.ToList();

		foreach (var item in targetItems)
		{
			item.Reset();
		}

		int passedCount = 0;
		int failedCount = 0;

		try
		{
			for (int i = 0; i < targetItems.Count; i++)
			{
				if (ct.IsCancellationRequested)
				{
					break;
				}

				var item = targetItems[i];
				item.OverallStatus = PluginTestStageStatus.Running;
				StatusMessage = $"Testing {item.Name} ({i + 1} of {targetItems.Count})...";

				var progressHandler = new Progress<PluginTestProgress>(p =>
				{
					if (p.ProviderId == item.Id)
					{
						if (p.StageName == "Search")
						{
							item.SearchStage.Status = p.Status;
							item.SearchStage.Message = p.Message;
						}
						else if (p.StageName == "GetEpisodes")
						{
							item.EpisodesStage.Status = p.Status;
							item.EpisodesStage.Message = p.Message;
						}
						else if (p.StageName == "GetServers")
						{
							item.ServersStage.Status = p.Status;
							item.ServersStage.Message = p.Message;
						}
					}
				});

				var result = await _tester.TestProviderAsync(item.Provider, query, progressHandler, TimeSpan.FromSeconds(TimeoutSeconds), ct);
				item.ApplyResult(result);

				if (result.Passed)
				{
					passedCount++;
				}
				else
				{
					failedCount++;
				}
			}

			if (ct.IsCancellationRequested)
			{
				InfoBarIsOpen = true;
				InfoBarSeverity = InfoBarSeverity.Warning;
				InfoBarTitle = "Testing cancelled";
				InfoBarMessage = "The test run was cancelled.";
				StatusMessage = "Cancelled.";
			}
			else
			{
				InfoBarIsOpen = true;
				if (failedCount == 0)
				{
					InfoBarSeverity = InfoBarSeverity.Success;
					InfoBarTitle = "All tests passed!";
					InfoBarMessage = $"All {passedCount} provider(s) passed Search, GetEpisodes, and GetServers for \"{query}\".";
					StatusMessage = $"Completed: {passedCount} passed, 0 failed.";
				}
				else if (passedCount == 0)
				{
					InfoBarSeverity = InfoBarSeverity.Error;
					InfoBarTitle = "All tests failed";
					InfoBarMessage = $"{failedCount} provider(s) failed during testing. Check failure details on each card.";
					StatusMessage = $"Completed: 0 passed, {failedCount} failed.";
				}
				else
				{
					InfoBarSeverity = InfoBarSeverity.Warning;
					InfoBarTitle = "Testing completed with failures";
					InfoBarMessage = $"{passedCount} passed, {failedCount} failed. Review the details below.";
					StatusMessage = $"Completed: {passedCount} passed, {failedCount} failed.";
				}
			}
		}
		catch (OperationCanceledException)
		{
			InfoBarIsOpen = true;
			InfoBarSeverity = InfoBarSeverity.Warning;
			InfoBarTitle = "Testing cancelled";
			InfoBarMessage = "The test run was cancelled.";
			StatusMessage = "Cancelled.";
		}
		catch (Exception ex)
		{
			InfoBarIsOpen = true;
			InfoBarSeverity = InfoBarSeverity.Error;
			InfoBarTitle = "Error during testing";
			InfoBarMessage = ex.Message;
			StatusMessage = $"Error: {ex.Message}";
		}
		finally
		{
			IsBusy = false;
		}
	}

	[RelayCommand]
	public async Task RunSingleTestAsync(PluginTestItemViewModel? item)
	{
		if (item is null || IsBusy)
		{
			return;
		}

		var query = ShowTitle.Trim();
		if (string.IsNullOrWhiteSpace(query))
		{
			InfoBarIsOpen = true;
			InfoBarSeverity = InfoBarSeverity.Warning;
			InfoBarTitle = "Missing show title";
			InfoBarMessage = "Please enter the title of a show to test.";
			return;
		}

		IsBusy = true;
		InfoBarIsOpen = false;
		item.Reset();
		item.OverallStatus = PluginTestStageStatus.Running;
		StatusMessage = $"Testing {item.Name}...";

		_cancellation?.Cancel();
		_cancellation?.Dispose();
		_cancellation = new CancellationTokenSource();
		var ct = _cancellation.Token;

		try
		{
			var progressHandler = new Progress<PluginTestProgress>(p =>
			{
				if (p.ProviderId == item.Id)
				{
					if (p.StageName == "Search")
					{
						item.SearchStage.Status = p.Status;
						item.SearchStage.Message = p.Message;
					}
					else if (p.StageName == "GetEpisodes")
					{
						item.EpisodesStage.Status = p.Status;
						item.EpisodesStage.Message = p.Message;
					}
					else if (p.StageName == "GetServers")
					{
						item.ServersStage.Status = p.Status;
						item.ServersStage.Message = p.Message;
					}
				}
			});

			var result = await _tester.TestProviderAsync(item.Provider, query, progressHandler, TimeSpan.FromSeconds(TimeoutSeconds), ct);
			item.ApplyResult(result);

			InfoBarIsOpen = true;
			if (result.Passed)
			{
				InfoBarSeverity = InfoBarSeverity.Success;
				InfoBarTitle = $"{item.Name} passed!";
				InfoBarMessage = $"{item.Name} passed Search, GetEpisodes, and GetServers in {result.TotalDuration.TotalSeconds:F2}s.";
				StatusMessage = $"{item.Name} passed.";
			}
			else
			{
				InfoBarSeverity = InfoBarSeverity.Error;
				InfoBarTitle = $"{item.Name} failed";
				// The failing stage's message; its card has the whole error (with the stack trace).
				InfoBarMessage = new[] { item.SearchStage, item.EpisodesStage, item.ServersStage }.FirstOrDefault(x => x.IsFailed)?.Message
					?? "Its card has the details.";
				StatusMessage = $"{item.Name} failed.";
			}
		}
		catch (OperationCanceledException)
		{
			item.OverallStatus = PluginTestStageStatus.Failed;
			StatusMessage = "Cancelled.";
		}
		finally
		{
			IsBusy = false;
		}
	}

	[RelayCommand]
	public void CancelTests()
	{
		_cancellation?.Cancel();
	}

	[RelayCommand]
	public async Task GoToPluginsAsync()
	{
		if (Navigators.TryGetNavigator("main", out var main))
		{
			await main.NavigateToViewModelAsync(typeof(PluginsViewModel));
		}
	}
}
