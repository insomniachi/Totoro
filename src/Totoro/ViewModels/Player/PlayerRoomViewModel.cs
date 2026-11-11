using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Totoro.Core.Modules.Media;

namespace Totoro.ViewModels.Player;

/// <summary>The built-in player's watch-together room: who's in it, notices about them, and chat to send it.</summary>
public sealed partial class PlayerRoomViewModel : ObservableObject
{
	private static readonly TimeSpan s_roomNoticeDuration = TimeSpan.FromSeconds(5);

	/// <summary>Who's watching together (in a watch-together room), the user first; empty when not in a room.</summary>
	[ObservableProperty] public partial IReadOnlyList<RoomPerson> RoomPeople { get; private set; } = [];

	[ObservableProperty] public partial bool IsInRoom { get; private set; }

	/// <summary>Notices about the people in the room shown over the video, e.g. someone joining; each goes after a few seconds.</summary>
	public ObservableCollection<RoomNotice> RoomNotices { get; } = [];

	/// <inheritdoc cref="IInteractiveMediaPlayer.SetRoomPeople"/>
	public void SetRoomPeople(IReadOnlyList<RoomPerson>? people)
	{
		RoomPeople = people ?? [];
		IsInRoom = people is not null;
	}

	/// <inheritdoc cref="IInteractiveMediaPlayer.RoomChatSent"/>
	public event EventHandler<string>? RoomChatSent;

	/// <summary>What the user's typing to say to the room.</summary>
	[ObservableProperty] public partial string RoomChat { get; set; } = "";

	/// <summary>Says <see cref="RoomChat"/> to the room, and clears it.</summary>
	public void SendRoomChat()
	{
		if (RoomChat.Trim() is { Length: > 0 } text)
		{
			RoomChatSent?.Invoke(this, text);
			RoomChat = "";
		}
	}

	/// <inheritdoc cref="IInteractiveMediaPlayer.ShowRoomNotice"/>
	public async Task ShowRoomNoticeAsync(string person, string text)
	{
		var notice = new RoomNotice(person, text);
		RoomNotices.Add(notice);
		await Task.Delay(s_roomNoticeDuration);
		RoomNotices.Remove(notice);
	}
}

/// <summary>A notice about someone in the watch-together room, e.g. "joined the room".</summary>
public sealed record RoomNotice(string Person, string Text);
