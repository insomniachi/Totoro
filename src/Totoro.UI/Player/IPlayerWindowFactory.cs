using Microsoft.UI.Xaml;

namespace Totoro.UI.Player;

/// <summary>Makes the app's player window (its controls and overlays) around a <see cref="PlayerViewModel"/>.</summary>
public interface IPlayerWindowFactory
{
	Window Create(PlayerViewModel viewModel, PlayerSettings settings);
}
