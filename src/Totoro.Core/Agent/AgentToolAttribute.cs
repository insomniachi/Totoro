namespace Totoro.Core.Agent;

/// <summary>
/// Makes a method of a tool class (registered with <c>AddAgentTools&lt;T&gt;</c>) a tool an AI agent can call, under
/// <paramref name="name"/> (snake_case, e.g. "search_anime"). Describe the method and its parameters with
/// <see cref="System.ComponentModel.DescriptionAttribute"/>: that's what the agent reads to decide when and how to
/// call it.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class AgentToolAttribute(string name) : Attribute
{
	public string Name { get; } = name;

	/// <summary>
	/// It changes something the user should agree to first (e.g. removes an anime from their list), so a chat asks them
	/// before calling it.
	/// </summary>
	public bool RequiresApproval { get; init; }
}
