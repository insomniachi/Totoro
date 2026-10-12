using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Totoro.Core.Services;

namespace Totoro.ViewModels.Chat;

/// <summary>The assistant's panel, beside the pages: the conversation (<see cref="ChatService"/>) and what the user is typing.</summary>
public sealed partial class ChatViewModel : ObservableObject
{
	public ChatViewModel(ChatService chat)
	{
		Chat = chat;
		Chat.PropertyChanged += (_, e) =>
		{
			if (e.PropertyName is nameof(ChatService.IsBusy))
			{
				SendCommand.NotifyCanExecuteChanged();
				StopCommand.NotifyCanExecuteChanged();
			}
			else if (e.PropertyName is nameof(ChatService.IsAvailable))
			{
				OnPropertyChanged(nameof(IsAvailable));
			}
		};
	}

	public ChatService Chat { get; }

	/// <summary>Whether the panel is open.</summary>
	[ObservableProperty] public partial bool IsOpen { get; set; }

	/// <summary>What the user is typing; empty, never null.</summary>
	[ObservableProperty]
	[NotifyCanExecuteChangedFor(nameof(SendCommand))]
	public partial string Input { get; set; } = "";

	public bool IsAvailable => Chat.IsAvailable;

	/// <summary>Things to try, while the conversation is empty.</summary>
	public IReadOnlyList<string> Suggestions { get; } =
	[
		"What's on my Watching list?",
		"Play the next episode of the show I watched last",
		"What's airing this season that I haven't added?",
	];

	[RelayCommand(CanExecute = nameof(CanSend))]
	private async Task SendAsync()
	{
		var text = Input;
		Input = "";
		await Chat.SendAsync(text);
	}

	private bool CanSend() => !Chat.IsBusy && !string.IsNullOrWhiteSpace(Input);

	[RelayCommand]
	private async Task SuggestAsync(string text)
	{
		Input = text;
		await SendAsync();
	}

	[RelayCommand(CanExecute = nameof(CanStop))]
	private void Stop() => Chat.Stop();

	private bool CanStop() => Chat.IsBusy;

	[RelayCommand]
	private void NewChat() => Chat.NewChat();

	[RelayCommand]
	private Task AllowAsync(ChatApprovalEntry entry) => Chat.AnswerAsync(entry, true);

	[RelayCommand]
	private Task DenyAsync(ChatApprovalEntry entry) => Chat.AnswerAsync(entry, false);
}
