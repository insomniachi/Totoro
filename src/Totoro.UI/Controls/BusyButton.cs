using CommunityToolkit.WinUI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;

namespace Totoro.UI.Controls;

/// <summary>
/// A button that shows a spinner in place of its content while <see cref="IsBusy"/>, looking otherwise as it does:
/// same size, and not greyed out while its command, running, can't execute.
/// </summary>
public sealed partial class BusyButton : Button
{
	private readonly ContentPresenter _hiddenContent = new() { Opacity = 0 }; // keeps the button's size
	private readonly ProgressRing _ring = new() { Width = 16, Height = 16, IsActive = false };
	private readonly Grid _busyContent = new();

	public BusyButton()
	{
		_busyContent.Children.Add(_hiddenContent);
		_busyContent.Children.Add(_ring);
		// The content's color, e.g. white on an accent button, where the ring's own accent color wouldn't show
		_ring.SetBinding(ForegroundProperty, new Binding { Source = this, Path = new PropertyPath(nameof(Foreground)) });
		IsEnabledChanged += (_, _) => KeepNormalLook();
	}

	[GeneratedDependencyProperty]
	public partial bool IsBusy { get; set; }

	partial void OnIsBusyChanged(bool newValue)
	{
		_ring.IsActive = newValue;
		if (newValue)
		{
			Wrap(Content);
			KeepNormalLook();
		}
		else if (Content == _busyContent)
		{
			var content = _hiddenContent.Content;
			_hiddenContent.Content = null; // let the content go back to the button
			Content = content;
			if (!IsEnabled) VisualStateManager.GoToState(this, "Disabled", false);
		}
	}

	protected override void OnContentChanged(object oldContent, object newContent)
	{
		base.OnContentChanged(oldContent, newContent);
		// Content set while busy (or after IsBusy in XAML) goes behind the spinner
		if (IsBusy) Wrap(newContent);
	}

	private void Wrap(object content)
	{
		if (content == _busyContent) return;
		_hiddenContent.Content = content;
		Content = _busyContent;
	}

	/// <summary>Undoes the Disabled state the button goes to while its command runs; it still takes no clicks.</summary>
	private void KeepNormalLook()
	{
		// After the button's own state change, which follows IsEnabled changing
		DispatcherQueue.TryEnqueue(() =>
		{
			if (IsBusy && !IsEnabled) VisualStateManager.GoToState(this, "Normal", false);
		});
	}
}
