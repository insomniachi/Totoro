using CommunityToolkit.WinUI;
using Microsoft.UI.Xaml.Controls;
using Totoro.ViewModels.Relations;

namespace Totoro.Views.Relations;

public sealed partial class RelationRuleDialog : UserControl
{
	public RelationRuleDialog()
	{
		InitializeComponent();
	}

	[GeneratedDependencyProperty]
	public partial RelationRuleDialogViewModel? ViewModel { get; set; }
}
