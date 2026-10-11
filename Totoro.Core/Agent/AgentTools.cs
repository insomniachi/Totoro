using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Totoro.Core.Agent;

/// <summary>
/// Everything the app can do, as tools an AI agent can call: e.g. an in-app chat passes <see cref="Tools"/> to its chat
/// client (ChatOptions.Tools), and they could equally be served over MCP. Tools come from the tool classes registered
/// with <c>AddAgentTools&lt;T&gt;</c>: their methods marked <see cref="AgentToolAttribute"/>.
/// </summary>
public interface IAgentTools
{
	IReadOnlyList<AIFunction> Tools { get; }
}

/// <summary>A tool class, registered with <c>AddAgentTools&lt;T&gt;</c>.</summary>
public sealed record AgentToolSet(Type Type);

/// <summary>What a tool returns when it fails: why, for the agent to act on (e.g. search first, or tell the user).</summary>
public sealed record AgentToolError(string Error);

internal sealed class AgentTools(IServiceProvider services, IEnumerable<AgentToolSet> sets, IMainThread mainThread, ILogger<AgentTools> logger)
	: IAgentTools
{
	private readonly Lazy<IReadOnlyList<AIFunction>> _tools = new(() => Create(services, sets, mainThread, logger));

	/// <summary>
	/// How tools' arguments and results are read and written: camelCase, enums as their names (so the agent sees
	/// "Watching" rather than 0), nulls left out.
	/// </summary>
	public static JsonSerializerOptions SerializerOptions { get; } = CreateSerializerOptions();

	public IReadOnlyList<AIFunction> Tools => _tools.Value;

	private static List<AIFunction> Create(IServiceProvider services, IEnumerable<AgentToolSet> sets, IMainThread mainThread, ILogger logger)
	{
		List<AIFunction> tools = [];
		foreach (var set in sets.DistinctBy(x => x.Type))
		{
			var target = services.GetRequiredService(set.Type);
			foreach (var method in set.Type.GetMethods(BindingFlags.Public | BindingFlags.Instance))
			{
				if (method.GetCustomAttribute<AgentToolAttribute>() is not { } attribute)
				{
					continue;
				}

				AIFunction tool = new AgentFunction(AIFunctionFactory.Create(method, target, new AIFunctionFactoryOptions
				{
					Name = attribute.Name,
					SerializerOptions = SerializerOptions,
				}), mainThread, logger);
				tools.Add(attribute.RequiresApproval ? new ApprovalRequiredAIFunction(tool) : tool);
			}
		}

		return tools;
	}

	private static JsonSerializerOptions CreateSerializerOptions()
	{
		var options = new JsonSerializerOptions(AIJsonUtilities.DefaultOptions)
		{
			PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
			DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
		};
		options.Converters.Add(new JsonStringEnumConverter());
		options.MakeReadOnly();
		return options;
	}

	/// <summary>
	/// Runs a tool on the UI thread (tools change state the UI is bound to), and turns its failures into an
	/// <see cref="AgentToolError"/> result, so the agent learns what went wrong instead of the call just failing.
	/// </summary>
	private sealed class AgentFunction(AIFunction tool, IMainThread mainThread, ILogger logger) : DelegatingAIFunction(tool)
	{
		protected override async ValueTask<object?> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
		{
			try
			{
				return await mainThread.InvokeAsync(async () => await base.InvokeCoreAsync(arguments, cancellationToken));
			}
			catch (AgentToolException ex)
			{
				return Error(ex.Message);
			}
			catch (Exception ex) when (ex is not OperationCanceledException)
			{
				logger.LogWarning(ex, "Agent tool {Tool} failed", Name);
				return Error($"It failed: {ex.Message}");
			}
		}

		private static JsonElement Error(string message)
		{
			return JsonSerializer.SerializeToElement(new AgentToolError(message), SerializerOptions);
		}
	}
}
