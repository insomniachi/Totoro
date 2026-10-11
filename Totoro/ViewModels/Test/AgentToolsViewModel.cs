using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.AI;
using Totoro.Core.Agent;

namespace Totoro.ViewModels.Test;

/// <summary>
/// A playground for the agent tools: every tool an AI agent can call (IAgentTools), with what the agent sees of it
/// (its description and parameters' schema). Calls one with JSON arguments, to see what the agent would get back.
/// </summary>
public partial class AgentToolsViewModel : ObservableObject
{
	public AgentToolsViewModel(IAgentTools agentTools)
	{
		Tools = [.. agentTools.Tools.OrderBy(x => x.Name).Select(x => new AgentToolItem(x))];
		SelectedTool = Tools.FirstOrDefault();
	}

	public IReadOnlyList<AgentToolItem> Tools { get; }

	[ObservableProperty] public partial AgentToolItem? SelectedTool { get; set; }

	/// <summary>The arguments to call the selected tool with, as JSON.</summary>
	[ObservableProperty] public partial string ToolArguments { get; set; } = "{}";

	/// <summary>What the tool returned, as the agent would get it.</summary>
	[ObservableProperty] public partial string ToolResult { get; private set; } = "";

	partial void OnSelectedToolChanged(AgentToolItem? value)
	{
		ToolArguments = value?.ArgumentsTemplate() ?? "{}";
		ToolResult = "";
	}

	[RelayCommand]
	private async Task RunToolAsync()
	{
		if (SelectedTool is not { } tool)
		{
			return;
		}

		try
		{
			var arguments = JsonSerializer.Deserialize<Dictionary<string, object?>>(string.IsNullOrWhiteSpace(ToolArguments) ? "{}" : ToolArguments) ?? [];
			ToolResult = "Running…";
			var result = await tool.Tool.InvokeAsync(new AIFunctionArguments(arguments));
			ToolResult = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
		}
		catch (JsonException ex)
		{
			ToolResult = $"The arguments aren't valid JSON: {ex.Message}";
		}
	}
}

/// <summary>An agent tool in the playground.</summary>
public sealed class AgentToolItem(AIFunction tool)
{
	public AIFunction Tool { get; } = tool;

	public string Name => Tool.Name;

	public string Description => Tool.Description;

	/// <summary>Its parameters' JSON schema, which is what an agent reads.</summary>
	public string Schema { get; } = JsonSerializer.Serialize(tool.JsonSchema, new JsonSerializerOptions { WriteIndented = true });

	/// <summary>A chat asks the user before calling it.</summary>
	public string ApprovalText => Tool.GetService<ApprovalRequiredAIFunction>() is not null ? "Asks the user first" : "";

	/// <summary>Arguments to start from: the required ones, with placeholder values.</summary>
	public string ArgumentsTemplate()
	{
		var schema = Tool.JsonSchema;
		var arguments = new JsonObject();
		if (schema.TryGetProperty("required", out var required) && schema.TryGetProperty("properties", out var properties))
		{
			foreach (var name in required.EnumerateArray().Select(x => x.GetString()!))
			{
				var type = properties.TryGetProperty(name, out var property) && property.TryGetProperty("type", out var t) ? t.ToString() : "";
				arguments[name] = type switch
				{
					_ when type.Contains("integer") || type.Contains("number") => 0,
					_ when type.Contains("boolean") => false,
					_ when type.Contains("array") => new JsonArray(),
					_ => "",
				};
			}
		}

		return arguments.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
	}
}
