using CommunityToolkit.WinUI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Totoro.Core.Services;
using Totoro.ViewModels.Chat;
using Windows.System;

namespace Totoro.Views.Chat;

/// <summary>The assistant's panel, beside the pages; see <see cref="ChatViewModel"/>.</summary>
public sealed partial class ChatPanel : UserControl
{
	public ChatPanel()
	{
		InitializeComponent();
	}

	[GeneratedDependencyProperty]
	public partial ChatViewModel? ViewModel { get; set; }

	private void OnCloseClick(object sender, RoutedEventArgs e)
	{
		ViewModel?.IsOpen = false;
	}

	private void OnSuggestionClick(object sender, RoutedEventArgs e)
	{
		if ((sender as FrameworkElement)?.Tag is string text)
		{
			ViewModel?.SuggestCommand.Execute(text);
		}
	}

	private void OnAllowClick(object sender, RoutedEventArgs e) => Answer(sender, true);

	private void OnDenyClick(object sender, RoutedEventArgs e) => Answer(sender, false);

	private void Answer(object sender, bool approved)
	{
		if ((sender as FrameworkElement)?.DataContext is not ChatApprovalEntry entry || ViewModel is null)
		{
			return;
		}

		(approved ? ViewModel.AllowCommand : ViewModel.DenyCommand).Execute(entry);
	}

	private void OnInputPreviewKeyDown(object sender, KeyRoutedEventArgs e)
	{
		var shift = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift)
			.HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);
		if (e.Key != VirtualKey.Enter || shift || ViewModel is null)
		{
			return;
		}

		e.Handled = true;
		if (ViewModel.SendCommand.CanExecute(null))
		{
			ViewModel.SendCommand.Execute(null);
		}
	}

	// For the x:Bind function bindings.

	public static Visibility VisibleIfEmpty(bool isAvailable, int count) => isAvailable && count == 0 ? Visibility.Visible : Visibility.Collapsed;

	public static bool IsRunning(ChatToolState state) => state == ChatToolState.Running;

	public static Visibility VisibleIfDone(ChatToolState state) => state == ChatToolState.Done ? Visibility.Visible : Visibility.Collapsed;

	public static Visibility VisibleIfFailed(ChatToolState state) => state == ChatToolState.Failed ? Visibility.Visible : Visibility.Collapsed;

	public static Visibility VisibleIfPending(ChatApprovalState state) => state == ChatApprovalState.Pending ? Visibility.Visible : Visibility.Collapsed;

	public static Visibility VisibleIfAnswered(ChatApprovalState state) => state == ChatApprovalState.Pending ? Visibility.Collapsed : Visibility.Visible;

	public static string AnswerText(ChatApprovalState state) => state == ChatApprovalState.Approved ? "Allowed" : "Not allowed";
}
