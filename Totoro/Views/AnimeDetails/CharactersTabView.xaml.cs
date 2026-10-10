using CommunityToolkit.WinUI;
using Microsoft.UI.Xaml.Controls;
using Totoro.ViewModels;

namespace Totoro.Views.AnimeDetails;

/// <summary>The Characters tab.</summary>
public sealed partial class CharactersTabView : UserControl
{
	public CharactersTabView()
	{
		InitializeComponent();
	}

	[GeneratedDependencyProperty]
	public partial CharactersTab? ViewModel { get; set; }
}