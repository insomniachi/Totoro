using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Totoro.Core.Services;

namespace Totoro.Views.Chat;

/// <summary>Picks how to show a <see cref="ChatEntry"/> by its kind: the user's message, a reply, a tool...</summary>
public sealed partial class ChatEntryTemplateSelector : DataTemplateSelector
{
	public DataTemplate? User { get; set; }

	public DataTemplate? Reply { get; set; }

	public DataTemplate? Tool { get; set; }

	public DataTemplate? Approval { get; set; }

	public DataTemplate? Error { get; set; }

	public DataTemplate? Anime { get; set; }

	public DataTemplate? Songs { get; set; }

	protected override DataTemplate? SelectTemplateCore(object item) => item switch
	{
		ChatUserEntry => User,
		ChatReplyEntry => Reply,
		ChatToolEntry => Tool,
		ChatApprovalEntry => Approval,
		ChatErrorEntry => Error,
		ChatAnimeEntry => Anime,
		ChatSongEntry => Songs,
		_ => null,
	};

	protected override DataTemplate? SelectTemplateCore(object item, DependencyObject container) => SelectTemplateCore(item);
}
