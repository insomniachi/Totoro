using System.ClientModel;
using Microsoft.Extensions.AI;
using OpenAI;
using OpenAI.Chat;
using Totoro.Core.Modules.Chat;
using Totoro.Core.Settings;

namespace Totoro.Modules.ChatModels.OpenAICompatible;

/// <summary>
/// A model in the cloud, through any service with an OpenAI-style chat completions API (OpenAI, OpenRouter, Groq,
/// Gemini's and Anthropic's compatibility endpoints...), so the assistant works without a model on this PC.
/// </summary>
internal sealed class OpenAICompatibleChatModel(IModuleSettings<Settings> settings) : IChatModel
{
	public const string DefaultEndpoint = "https://api.openai.com/v1";

	// A guess at a cheap model that's good with tools; the user can name any their service has.
	public const string DefaultModel = "gpt-4.1-mini";

	public Guid Id => OpenAICompatibleModule.Id;

	public string Name => $"Cloud ({ModelName})";

	// Set up once it has a key: whether the key and model work shows when a message is sent.
	public bool IsAvailable() => settings.Value.Enabled && !string.IsNullOrWhiteSpace(settings.Value.ApiKey);

	public IChatClient CreateClient()
	{
		var endpoint = string.IsNullOrWhiteSpace(settings.Value.Endpoint) ? DefaultEndpoint : settings.Value.Endpoint.Trim();
		var client = new ChatClient(ModelName, new ApiKeyCredential(settings.Value.ApiKey.Trim()), new OpenAIClientOptions { Endpoint = new Uri(endpoint) });
		return new ChatClientBuilder(client.AsIChatClient())
			   .ConfigureOptions(options => options.Temperature ??= 0.3f)
			   .Build();
	}

	private string ModelName => string.IsNullOrWhiteSpace(settings.Value.Model) ? DefaultModel : settings.Value.Model.Trim();
}
