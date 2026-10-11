using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.AI;
using Totoro.Core.Agent;
using Totoro.Core.Dialogs;
using Totoro.Core.Settings;

namespace Totoro.ViewModels;

/// <summary>
/// Playgrounds: the HTML renderer (<c>Totoro.UI.Text.HtmlText</c>), the settings editor, and the agent tools (call any
/// tool with JSON arguments, as an AI agent would).
/// </summary>
public partial class TestViewModel : ObservableObject
{
	private readonly IDialogService _dialogs;

	public TestViewModel(IDialogService dialogs, IAgentTools agentTools)
	{
		_dialogs = dialogs;
		Tools = [.. agentTools.Tools.OrderBy(x => x.Name).Select(x => new AgentToolItem(x))];
		SelectedTool = Tools.FirstOrDefault();
		Html = Sample;

		Settings = new EditorDemoSettings().ToDataContainer();
		foreach (var property in Settings)
		{
			property.PropertyChanged += (_, _) => UpdateValues();
		}

		UpdateValues();
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

	/// <summary>The settings editor demo's properties, shown by an inline editor and editable in a dialog.</summary>
	public DataContainer Settings { get; }

	/// <summary>The properties' current values and types, to show that edits keep each value's type.</summary>
	[ObservableProperty]
	public partial string Values { get; private set; } = "";

	[ObservableProperty]
	public partial string DialogResult { get; private set; } = "";

	[RelayCommand]
	private async Task EditInDialogAsync()
	{
		var saved = await _dialogs.EditAsync("Settings editor demo", Settings);
		DialogResult = saved
			? "Saved: the inline editor and the values show the changes."
			: "Cancelled: nothing changed.";
	}

	private void UpdateValues()
	{
		Values = string.Join(Environment.NewLine,
							 Settings.Select(x => $"{x.Name} = {(x.Value is string s ? $"\"{s}\"" : x.Value)}  ({x.Value?.GetType().Name ?? "null"})"));
	}

	[ObservableProperty]
	public partial string Html { get; set; }

	/// <summary>Everything the renderer supports, written the way AniList sends it (description(asHtml: true)).</summary>
	public const string Sample = """
		<h1>HTML renderer showcase</h1>
		<p>Everything here is HTML, as AniList returns descriptions with <code>asHtml: true</code>. Edit it on the left to try things out.</p>

		<h2>Text styles</h2>
		<p><strong>Bold</strong> (<code>__bold__</code>), <b>also bold</b>, <em>italic</em> (<code>_italic_</code>), <i>also italic</i>,
		<del>strikethrough</del> (<code>~~strike~~</code>), <u>underline</u> and <code>inline code</code>.
		Styles <strong>nest: <em>bold italic, <del>and struck</del></em></strong>.</p>

		<h2>Line breaks and paragraphs</h2>
		<p>A line,<br>a line break,<br><br>and a blank line. Raw newlines
		like this one are just spaces, as in HTML; extra breaks<br><br><br><br>are capped at one blank line.</p>

		<h2>Links and spoilers</h2>
		<p>A <a href="https://anilist.co/anime/150672">link to [Oshi no Ko] on AniList</a>. Only http and https links are followed.</p>
		<p>A spoiler, from <code>~!spoiler!~</code>: <span class='markdown_spoiler'><span>Hidden until clicked, and it can hold <strong>styles</strong> and <a href="https://anilist.co">links</a>.</span></span></p>

		<h2>Headings</h2>
		<h3>Heading 3</h3>
		<h4>Heading 4</h4>
		<h5>Heading 5</h5>

		<h2>Alignment and blocks</h2>
		<center><p>Centered, from <code>~~~centered~~~</code></p></center>
		<blockquote><p>A quote, from <code>&gt; quote</code>.</p></blockquote>
		<ul>
		  <li>A bullet list</li>
		  <li>With a <strong>styled</strong> item</li>
		  <li>And a nested list
		    <ol><li>First</li><li>Second</li></ol>
		  </li>
		</ul>
		<ol><li>A numbered list</li><li>Counts by itself</li></ol>
		<hr />
		<pre><code>A code block keeps its
		    indentation and
		line breaks.</code></pre>

		<h2>Media</h2>
		<p>An image, from <code>img220(url)</code>:</p>
		<p><img width="120" src="https://s4.anilist.co/file/anilistcdn/media/anime/cover/small/bx150672-WqmmwZ4nMzAy.png"></p>
		<p>A video, from <code>youtube(id)</code>: <span class='youtube' id='gKWEUJ4r5do'></span></p>

		<h2>Everything else</h2>
		<p>Entities are decoded: &amp; &lt;tag&gt; &quot;quotes&quot; &#9733;.
		Unknown tags like <marquee>this one</marquee> are dropped but keep their text, and comments<!-- like this --> are hidden.</p>
		""";
}

/// <summary>Settings for the editor demo: one property per kind of editor, configured the way module settings are.</summary>
public sealed class EditorDemoSettings : DataContainerConfig
{
	[DisplayName("Display name")]
	[Description("A string gets a text box.")]
	public string DisplayName { get; set; } = "Totoro";

	[DisplayName("Autoplay")]
	[Description("A bool gets a toggle switch.")]
	[Icon("Play")]
	public bool Autoplay { get; set; } = true;

	[DisplayName("Episodes per page")]
	[Description("An int gets a number box, and stays a whole number.")]
	public int PageSize { get; set; } = 24;

	[DisplayName("Playback speed")]
	[Description("A double gets a number box too.")]
	[Icon("Clock")]
	public double Speed { get; set; } = 1.25;

	[DisplayName("Quality")]
	[Description("An enum gets a dropdown of its values, labelled by their [Description].")]
	public DemoQuality Quality { get; set; } = DemoQuality.FullHd;

	[DisplayName("Server")]
	[Description("[AllowedValues] gets a dropdown of those values.")]
	[AllowedValues("Auto", "Tokyo", "Frankfurt", "Virginia")]
	public string Server { get; set; } = "Auto";

	[DisplayName("Media player")]
	[Description("[Editor(DataContainerEditors.FileBrowser, \"\")] gets a file picker.")]
	[Editor(DataContainerEditors.FileBrowser, "")]
	public string PlayerPath { get; set; } = "";
}

public enum DemoQuality
{
	[Description("Best available")] Best,
	[Description("1080p")] FullHd,
	[Description("720p")] Hd,
	[Description("Data saver (480p)")] DataSaver,
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
