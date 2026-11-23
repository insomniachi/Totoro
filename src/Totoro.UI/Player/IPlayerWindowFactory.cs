namespace Totoro.UI.Player;

/// <summary>Opens the app's player (its controls and overlays) around a <see cref="PlayerViewModel"/>.</summary>
public interface IPlayerWindowFactory
{
	IPlayerHost Create(PlayerViewModel viewModel, PlayerSettings settings);
}

/// <summary>
/// Where the player plays: the main window, or a window of its own. Closing it (the user, or
/// <see cref="Close"/>) stops playback.
/// </summary>
public interface IPlayerHost
{
	event EventHandler? Closed;

	/// <summary>Opens it, or brings it to the front.</summary>
	void Activate();

	void Close();
}
