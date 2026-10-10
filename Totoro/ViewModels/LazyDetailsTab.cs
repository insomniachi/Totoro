using Microsoft.Extensions.Logging;

namespace Totoro.ViewModels;

/// <summary>A details tab that fetches its own data, the first time it's shown.</summary>
public abstract class LazyDetailsTab : DetailsTab
{
	private readonly string _emptyText;
	private readonly ILogger _logger;
	private Task? _loading;

	protected LazyDetailsTab(string emptyText, ILogger logger)
	{
		_emptyText = emptyText;
		_logger = logger;
		IsLoaded = false;
	}

	/// <summary>Loads the tab's data, once; later calls return the same task.</summary>
	public override Task EnsureLoadedAsync() => _loading ??= LoadOnceAsync();

	private async Task LoadOnceAsync()
	{
		try
		{
			var count = await LoadAsync();
			Message = count == 0 ? _emptyText : null;
		}
		catch (Exception ex)
		{
			_logger.LogWarning(ex, "Failed to load the {Tab} tab", GetType().Name);
			Message = "Couldn't load this right now.";
		}
		finally
		{
			IsLoaded = true;
		}
	}

	/// <summary>Loads and publishes the content; returns how many items there are.</summary>
	protected abstract Task<int> LoadAsync();
}
