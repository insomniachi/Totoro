namespace Totoro.Core.Agent;

/// <summary>
/// A tool couldn't do what it was asked; the message says why, in words the agent can act on (e.g. "No anime with id 5.
/// Search for it first."). Tools throw it; the agent gets the message as the tool's result.
/// </summary>
public sealed class AgentToolException(string message) : Exception(message);
