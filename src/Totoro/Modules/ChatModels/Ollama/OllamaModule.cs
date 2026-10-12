using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Totoro.Core.Extensions;
using Totoro.Core.Modules;
using Totoro.Core.Modules.Chat;
using Totoro.Core.Settings;

namespace Totoro.Modules.ChatModels.Ollama;

internal class OllamaModule : IModule<Settings>
{
	public static Guid Id { get; } = new("3b9e47c1-6a2d-4f85-b0e3-9d14c7a25f68");

	public Descriptor Descriptor { get; } = new()
	{
		Id = Id,
		Name = "Ollama",
		Description = "The assistant, with a model running on this PC through Ollama: it can do what you can in the app. Needs Ollama running, with a model that can use tools.",
		EntryPoint = "Ollama"
	};

	public void ConfigureServices(IServiceCollection services)
	{
		services.AddTransient(_ => Descriptor);
		services.AddModuleSettings(this);
		services.AddKeyedSingleton<IChatModel, OllamaChatModel>(Id);
	}
}

public sealed class Settings : DataContainerConfig
{
	[DisplayName("Model")]
	[Description("The Ollama model the assistant uses, e.g. qwen2.5:14b. It has to support tools.")]
	public string Model { get; set; } = OllamaChatModel.DefaultModel;

	[DisplayName("Address")]
	[Description("Where Ollama is running.")]
	public string Endpoint { get; set; } = OllamaChatModel.DefaultEndpoint;

	[DisplayName("Context length")]
	[Description("How many tokens the model sees at once: the tools' descriptions alone take several thousand. More needs more video memory.")]
	public int ContextLength { get; set; } = OllamaChatModel.DefaultContextLength;
}
