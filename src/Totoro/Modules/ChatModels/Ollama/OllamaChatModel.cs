using Microsoft.Extensions.AI;
using OllamaSharp;
using OllamaSharp.Models;
using Totoro.Core.Modules.Chat;
using Totoro.Core.Settings;

namespace Totoro.Modules.ChatModels.Ollama;

/// <summary>A model running locally in Ollama, through OllamaSharp's Microsoft.Extensions.AI client.</summary>
internal sealed class OllamaChatModel(IModuleSettings<Settings> settings) : IChatModel
{
	public const string DefaultModel = "qwen2.5:14b";
	public const string DefaultEndpoint = "http://localhost:11434";

	// Ollama's own default (2048) is less than the tools' definitions take.
	public const int DefaultContextLength = 16384;

	public Guid Id => OllamaModule.Id;

	public string Name => $"Ollama ({ModelName})";

	// Nothing to sign in to: whether Ollama is running shows when a message is sent.
	public bool IsAvailable() => true;

	public IChatClient CreateClient()
	{
		var endpoint = string.IsNullOrWhiteSpace(settings.Value.Endpoint) ? DefaultEndpoint : settings.Value.Endpoint.Trim();
		var contextLength = settings.Value.ContextLength > 0 ? settings.Value.ContextLength : DefaultContextLength;
		return new ChatClientBuilder(new OllamaApiClient(new Uri(endpoint), ModelName))
			   .ConfigureOptions(options =>
			   {
				   options.AddOllamaOption(OllamaOption.NumCtx, contextLength);
				   // Steadier than its default (0.8) at picking tools, and less given to drifting into other languages.
				   options.Temperature ??= 0.3f;
			   })
			   .Build();
	}

	private string ModelName => string.IsNullOrWhiteSpace(settings.Value.Model) ? DefaultModel : settings.Value.Model.Trim();
}
