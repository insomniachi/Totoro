using Microsoft.Extensions.AI;

namespace Totoro.Core.Modules.Chat;

/// <summary>
/// A language model the assistant chats with, e.g. Claude. Modules register implementations keyed by their module id;
/// the assistant (<see cref="Services.ChatService"/>) gives it the app's agent tools and runs their calls.
/// </summary>
public interface IChatModel
{
	/// <summary>The module's id.</summary>
	Guid Id { get; }

	string Name { get; }

	/// <summary>Whether it's set up (e.g. it has an API key), so <see cref="CreateClient"/> works.</summary>
	bool IsAvailable();

	/// <summary>A client for it, with its settings as they are now.</summary>
	IChatClient CreateClient();
}
