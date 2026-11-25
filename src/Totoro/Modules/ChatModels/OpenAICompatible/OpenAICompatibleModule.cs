using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Totoro.Core.Extensions;
using Totoro.Core.Modules;
using Totoro.Core.Modules.Chat;
using Totoro.Core.Settings;

namespace Totoro.Modules.ChatModels.OpenAICompatible;

internal class OpenAICompatibleModule : IModule<Settings>
{
	public static Guid Id { get; } = new("8d2f6a14-c37e-4b90-a5d1-2e6b9f04c8a3");

	public Descriptor Descriptor { get; } = new()
	{
		Id = Id,
		Name = "Cloud model",
		Description = "The assistant, with a model in the cloud instead of on this PC: any service with an OpenAI-style API (OpenAI, OpenRouter, Groq...). Needs an API key from the service, and a model that can use tools.",
		DataFolder = "CloudChatModel"
	};

	public void ConfigureServices(IServiceCollection services)
	{
		services.AddTransient(_ => Descriptor);
		services.AddModuleSettings(this);
		services.AddKeyedSingleton<IChatModel, OpenAICompatibleChatModel>(Id);
	}
}

public sealed class Settings : DataContainerConfig
{
	[DisplayName("Enabled")]
	[Description("Shows the assistant (its button in the title bar), using this model.")]
	public bool Enabled { get; set; }

	[DisplayName("Address")]
	[Description("The service's API, e.g. https://api.openai.com/v1 or https://openrouter.ai/api/v1.")]
	public string Endpoint { get; set; } = OpenAICompatibleChatModel.DefaultEndpoint;

	[DisplayName("API key")]
	[Description("From the service's website. It's sent only to the address above.")]
	[Editor(DataContainerEditors.Password, "")]
	public string ApiKey { get; set; } = "";

	[DisplayName("Model")]
	[Description("The model, as the service names it, e.g. gpt-4.1-mini. It has to support tools.")]
	public string Model { get; set; } = OpenAICompatibleChatModel.DefaultModel;
}
