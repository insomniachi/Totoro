using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Totoro.Core.Agent;
using Totoro.Core.Modules;
using Totoro.Core.Modules.Anime;
using Totoro.Core.Modules.Chat;

namespace Totoro.Core.Services;

/// <summary>Something in the conversation, as the chat shows it.</summary>
public abstract partial class ChatEntry : ObservableObject;

public sealed class ChatUserEntry(string text) : ChatEntry
{
	public string Text { get; } = text;
}

/// <summary>The assistant's reply; it grows as it streams in.</summary>
public sealed partial class ChatReplyEntry : ChatEntry
{
	[ObservableProperty] public partial string Text { get; set; } = "";
}

public enum ChatToolState
{
	Running,
	Done,
	Failed,
}

/// <summary>A tool the assistant used, e.g. search_anime, and how it went.</summary>
public sealed partial class ChatToolEntry(string callId, string name, string arguments) : ChatEntry
{
	public string CallId { get; } = callId;

	public string Name { get; } = name;

	/// <summary>e.g. "Search anime".</summary>
	public string Title { get; } = ChatService.Humanize(name);

	/// <summary>What it was given, e.g. <c>{"query":"Frieren"}</c>; empty when nothing.</summary>
	public string Arguments { get; } = arguments;

	[ObservableProperty] public partial ChatToolState State { get; set; }

	/// <summary>Why it failed; empty, never null, when it didn't.</summary>
	[ObservableProperty] public partial string Error { get; set; } = "";
}

public enum ChatApprovalState
{
	Pending,
	Approved,
	Denied,
}

/// <summary>A tool the assistant wants to use that needs the user's say-so first, e.g. removing an anime from their list.</summary>
public sealed partial class ChatApprovalEntry(ToolApprovalRequestContent request, string arguments) : ChatEntry
{
	public ToolApprovalRequestContent Request { get; } = request;

	public string Title { get; } = ChatService.Humanize((request.ToolCall as FunctionCallContent)?.Name ?? "a tool");

	public string Arguments { get; } = arguments;

	[ObservableProperty] public partial ChatApprovalState State { get; set; }
}

public sealed class ChatErrorEntry(string message) : ChatEntry
{
	public string Message { get; } = message;
}

/// <summary>An anime's songs a tool returned (e.g. the one playing), as the Music tab's cards, which play them.</summary>
public sealed class ChatSongEntry(IReadOnlyList<SongItem> songs) : ChatEntry
{
	public IReadOnlyList<SongItem> Songs { get; } = songs;
}

/// <summary>Anime a tool returned (e.g. search results), as cards the user can open, add to their list or play.</summary>
public sealed class ChatAnimeEntry(IReadOnlyList<ChatAnimeItem> anime, int total) : ChatEntry
{
	public IReadOnlyList<ChatAnimeItem> Anime { get; } = anime;

	/// <summary>e.g. "5 of 30"; empty, never null, when they're all shown.</summary>
	public string More { get; } = total > anime.Count ? $"{anime.Count} of {total}" : "";
}

/// <summary>An anime card in the chat; follows changes to the user's list entry for it.</summary>
public sealed partial class ChatAnimeItem : ObservableObject
{
	public ChatAnimeItem(AnimeModel anime)
	{
		Anime = anime;
		anime.PropertyChanged += (_, e) =>
		{
			if (e.PropertyName is nameof(AnimeModel.Tracking))
			{
				OnPropertyChanged(nameof(ListStatus));
				OnPropertyChanged(nameof(ListAction));
				OnPropertyChanged(nameof(ListGlyph));
			}
		};
	}

	public AnimeModel Anime { get; }

	/// <summary>Its title, and its English one if that's different, for the tooltip.</summary>
	public string FullTitle => string.IsNullOrWhiteSpace(Anime.EngTitle) || Anime.EngTitle == Anime.Title
		? Anime.Title
		: $"{Anime.Title}\n{Anime.EngTitle}";

	/// <summary>e.g. "★ 9.1 · TV · Fall 2023".</summary>
	public string Facts => string.Join(" · ", new[]
	{
		Anime.MeanScore is > 0 and var score ? $"★ {Math.Round(score / 10, 1):0.0}" : null,
		Anime.MediaFormat switch
		{
			AnimeMediaFormat.Unknown => null,
			AnimeMediaFormat.Tv or AnimeMediaFormat.Ova or AnimeMediaFormat.Ona => Anime.MediaFormat.ToString().ToUpperInvariant(),
			var format => format.ToString(),
		},
		Anime.Season is { Year: > 1900 } season ? $"{season.SeasonName} {season.Year}" : null,
	}.OfType<string>());

	/// <summary>The user's list entry: e.g. "Watching, 4 of 13"; empty, never null, when it isn't on their list.</summary>
	public string ListStatus
	{
		get
		{
			if (Anime.Tracking is not { Status: { } status } tracking)
			{
				return "";
			}

			var entry = status switch
			{
				ListItemStatus.PlanToWatch => "Plan to watch",
				ListItemStatus.OnHold => "On hold",
				_ => status.ToString(),
			};
			if (tracking.WatchedEpisodes is > 0 and var watched)
			{
				entry += $", {watched} of {Anime.TotalEpisodes?.ToString() ?? "?"}";
			}

			return entry;
		}
	}

	/// <summary>The list button's name and icon: adding it to their list, or editing its entry.</summary>
	public string ListAction => Anime.Tracking?.Status is null ? "Add to list" : "Edit list entry";

	public string ListGlyph => Anime.Tracking?.Status is null ? "" : "";
}

/// <summary>
/// The assistant: a conversation with the selected chat model (<see cref="IChatModel"/>) that can do anything the app
/// can, through its agent tools (<see cref="IAgentTools"/>). Replies stream into <see cref="Entries"/>; the tools'
/// calls run as they come, except those that need approval (<see cref="ApprovalRequiredAIFunction"/>), which wait for
/// the user (<see cref="AnswerAsync"/>). Used on the UI thread.
/// </summary>
public sealed partial class ChatService(
	IFactory<IChatModel, Guid> models,
	IAgentTools tools,
	AnimeSongService songs,
	ISongPlayer songPlayer,
	ILogger<ChatService> logger) : ObservableObject
{
	// The most cards shown for one tool's result, the best scored (a whole list would bury the conversation).
	private const int MaxShownAnime = 5;

	private static string Instructions => $"""
		You're the assistant in Totoro, a Windows app for tracking and watching anime. You can do what the user can in the
		app, through your tools: look anime up, change their list (on AniList or MyAnimeList), play episodes, change
		settings, and so on. What you change shows in the app straight away. Today is {DateTime.Now:dddd d MMMM yyyy}.

		Anime ids belong to the user's anime database: find them with search_anime, discover_anime or get_user_list before
		using one; don't guess them. Every anime those return has the user's listEntry, or none if it isn't on their list.
		If a tool fails, its error says why; act on it, or tell the user. Search for titles as the user wrote them, typos and all: the search
		forgives them. Only if it finds nothing, search again with the title you think they meant.

		The anime and songs your tools return are shown to the user as cards, with buttons to open, add or play each. They're
		the answer: don't describe them in text. Ask the tools for just what's relevant, with their filters and limit.

		Always reply in the language the user writes in. Keep replies short, in plain text without Markdown (the chat shows
		it as is): the user is in the middle of using the app.
		""";

	private readonly List<ChatMessage> _history = [];
	// What the tools returned, by call, from when they run to when their results are shown.
	private readonly ConcurrentDictionary<string, object?> _results = new();
	private IChatClient? _client;
	private CancellationTokenSource? _running;

	// Where this turn's entries start; the cards of its latest tool result, shown when the turn ends if nothing came after
	// them (otherwise they were a step, e.g. a search for an id); and whether its text is hidden: cards are the answer, so
	// the model's text around them isn't shown, unless it goes on to use another tool.
	private int _turnStart;
	private ChatEntry? _pendingCards;
	private bool _quiet;

	/// <summary>The conversation so far, as shown.</summary>
	public ObservableCollection<ChatEntry> Entries { get; } = [];

	/// <summary>Whether it's replying (or using tools); the user can stop it meanwhile.</summary>
	[ObservableProperty] public partial bool IsBusy { get; private set; }

	/// <summary>Whether a chat model is set up, so there's someone to chat with.</summary>
	public bool IsAvailable => models.CreateDefault()?.IsAvailable() == true;

	/// <summary>The model's name, e.g. "Ollama (qwen2.5:14b)"; empty when there's none.</summary>
	public string ModelName => models.CreateDefault()?.Name ?? "";

	/// <summary>Sends the user's message and streams the reply, using tools as the model asks.</summary>
	public async Task SendAsync(string text)
	{
		if (IsBusy || string.IsNullOrWhiteSpace(text))
		{
			return;
		}

		// An unanswered approval can't be left behind: the model waits on it.
		foreach (var pending in Entries.OfType<ChatApprovalEntry>().Where(x => x.State == ChatApprovalState.Pending).ToList())
		{
			pending.State = ChatApprovalState.Denied;
			_history.Add(new ChatMessage(ChatRole.User, [pending.Request.CreateResponse(false, "The user moved on without answering.")]));
		}

		Entries.Add(new ChatUserEntry(text.Trim()));
		_history.Add(new ChatMessage(ChatRole.User, text.Trim()));
		await RunAsync();
	}

	/// <summary>Allows or denies a tool call waiting for approval; once all are answered, the conversation carries on.</summary>
	public async Task AnswerAsync(ChatApprovalEntry entry, bool approved)
	{
		if (entry.State != ChatApprovalState.Pending || IsBusy)
		{
			return;
		}

		entry.State = approved ? ChatApprovalState.Approved : ChatApprovalState.Denied;
		_history.Add(new ChatMessage(ChatRole.User, [entry.Request.CreateResponse(approved, approved ? null : "The user declined.")]));
		if (!Entries.OfType<ChatApprovalEntry>().Any(x => x.State == ChatApprovalState.Pending))
		{
			await RunAsync();
		}
	}

	/// <summary>Stops the reply (and any tools after the one running).</summary>
	public void Stop() => _running?.Cancel();

	/// <summary>Starts over, with the selected model as it's set up now.</summary>
	public void NewChat()
	{
		Stop();
		_history.Clear();
		Entries.Clear();
		Refresh();
	}

	/// <summary>Picks up a change to the model's setup (e.g. signing in), keeping the conversation.</summary>
	public void Refresh()
	{
		_client = null;
		OnPropertyChanged(nameof(IsAvailable));
		OnPropertyChanged(nameof(ModelName));
	}

	/// <summary>e.g. "search_anime" as "Search anime".</summary>
	public static string Humanize(string toolName)
	{
		var words = toolName.Replace('_', ' ');
		return words.Length == 0 ? words : char.ToUpperInvariant(words[0]) + words[1..];
	}

	/// <summary>
	/// Runs one turn: streams the model's reply, with its tool calls run in between (by the function-invoking client),
	/// until it's done or a tool waits for approval. A turn that fails or is stopped leaves the history as it was before
	/// it, so the next one starts from a consistent conversation.
	/// </summary>
	private async Task RunAsync()
	{
		if (models.CreateDefault() is not { } model || !model.IsAvailable())
		{
			Entries.Add(new ChatErrorEntry("There's no assistant model set up yet: set one up in Settings, under Modules."));
			return;
		}

		_client ??= new ChatClientBuilder(model.CreateClient())
					.UseFunctionInvocation(configure: x => x.FunctionInvoker = InvokeToolAsync)
					.Build();
		var options = new ChatOptions { Instructions = Instructions, Tools = [.. tools.Tools] };
		var historyBefore = _history.Count;
		_turnStart = Entries.Count;
		_pendingCards = null;
		_quiet = false;
		List<ChatResponseUpdate> updates = [];
		ChatReplyEntry? reply = null;

		using var running = _running = new CancellationTokenSource();
		IsBusy = true;
		try
		{
			await foreach (var update in _client.GetStreamingResponseAsync(_history, options, running.Token))
			{
				updates.Add(update);
				foreach (var content in update.Contents)
				{
					reply = Show(content, reply);
				}
			}

			if (_pendingCards is not null)
			{
				Entries.Add(_pendingCards);
			}

			_history.AddMessages(updates);
		}
		catch (OperationCanceledException) when (running.IsCancellationRequested)
		{
			Rollback(historyBefore);
			Entries.Add(new ChatErrorEntry("Stopped."));
		}
		catch (HttpRequestException ex) when (ex.StatusCode is null)
		{
			// Nothing answered, e.g. Ollama isn't running.
			logger.LogWarning(ex, "Couldn't reach the assistant's model");
			Rollback(historyBefore);
			Entries.Add(new ChatErrorEntry($"Couldn't reach {model.Name}. Check it's running. ({ex.Message})"));
		}
		catch (Exception ex)
		{
			logger.LogWarning(ex, "The assistant failed");
			Rollback(historyBefore);
			Entries.Add(new ChatErrorEntry($"Something went wrong: {ex.Message}"));
		}
		finally
		{
			_running = null;
			IsBusy = false;
			// Tools still marked running didn't finish.
			foreach (var tool in Entries.OfType<ChatToolEntry>().Where(x => x.State == ChatToolState.Running))
			{
				tool.State = ChatToolState.Failed;
			}
		}
	}

	/// <summary>
	/// Runs a tool for the model, keeping what it returned (<see cref="AgentToolResult"/>) for <see cref="Show"/> to show
	/// as cards. A result shown that way goes back with a note saying so: the model then doesn't describe it all again
	/// (in the result, it heeds that better than in its instructions).
	/// </summary>
	private async ValueTask<object?> InvokeToolAsync(FunctionInvocationContext context, CancellationToken cancellationToken)
	{
		var result = await context.Function.InvokeAsync(context.Arguments, cancellationToken);
		var raw = AgentToolResult.Of(context.Arguments);
		_results[context.CallContent.CallId] = raw;
		return ShownAs(raw) is { } shown && result is JsonElement json
			? JsonSerializer.SerializeToElement(new ShownResult(
				$"The user now sees {shown}. Don't describe it in text (the user won't see that). If this was a step towards what the user asked for (e.g. you searched to get an anime's id), carry on and do it; if not, you're done.",
				json), AgentTools.SerializerOptions)
			: result;
	}

	/// <summary>A tool's result that the user sees as cards, with a note for the model saying so.</summary>
	private sealed record ShownResult(string Note, JsonElement Result);

	/// <summary>How the user sees a tool's result (see <see cref="EntryFor"/>), for the model; null if not as cards.</summary>
	private static string? ShownAs(object? raw)
	{
		if (raw is IAgentSongs { Anime: not null, Songs.Count: > 0 } songs)
		{
			return songs.Songs.Count == 1 ? "this song as a card, with its player" : "these songs as cards, each with its player";
		}

		var anime = TotalOf(raw);
		return anime switch
		{
			0 => null,
			1 => "this anime as a card, with its picture and buttons to open, add or play it",
			_ => anime > MaxShownAnime ? $"the first {MaxShownAnime} of these anime as cards (and that there are {anime - MaxShownAnime} more)" : $"these {anime} anime as cards, with pictures and buttons to open, add or play each",
		};
	}

	/// <summary>A tool's result as cards: an anime's songs with their player, or anime (search results, the user's list...).</summary>
	private ChatEntry? EntryFor(object? raw)
	{
		if (raw is IAgentSongs { Anime: { } owner, Songs.Count: > 0 } songList)
		{
			return new ChatSongEntry([.. songList.Songs.Select(x => new SongItem(owner, x, songs, songPlayer))]);
		}

		var anime = AnimeIn(raw);
		return anime.Count == 0
			? null
			: new ChatAnimeEntry([.. anime.Take(MaxShownAnime).Select(x => new ChatAnimeItem(x))], TotalOf(raw));
	}
	/// <summary>Shows a piece of the reply; returns the reply text entry to append to next.</summary>
	private ChatReplyEntry? Show(AIContent content, ChatReplyEntry? reply)
	{
		switch (content)
		{
			case TextContent { Text.Length: > 0 } text:
				if (_quiet)
				{
					return null;
				}

				if (reply is null)
				{
					reply = new ChatReplyEntry();
					Entries.Add(reply);
				}

				reply.Text += text.Text;
				return reply;
			case FunctionCallContent call:
				// Another step: the cards so far were steps towards it.
				_pendingCards = null;
				_quiet = false;
				Entries.Add(new ChatToolEntry(call.CallId, call.Name, Format(call.Arguments)));
				return null;
			case FunctionResultContent result when Entries.OfType<ChatToolEntry>().LastOrDefault(x => x.CallId == result.CallId) is { } tool:
				var error = ErrorOf(result.Result);
				tool.State = error is null ? ChatToolState.Done : ChatToolState.Failed;
				tool.Error = error ?? "";
				// What it found or did, as cards: search results, the user's list, the song playing...
				_results.TryRemove(result.CallId, out var raw);
				// Shown when the turn ends, if it's the final result. One that isn't cards (e.g. counts) means the answer is the
				// text that follows instead.
				_pendingCards = EntryFor(raw);
				if (_pendingCards is not null)
				{
					HideTurnText();
					return null;
				}

				_quiet = false;
				return reply;
			case ToolApprovalRequestContent approval:
				_pendingCards = null;
				_quiet = false;
				Entries.Add(new ChatApprovalEntry(approval, Format((approval.ToolCall as FunctionCallContent)?.Arguments)));
				return null;
			default:
				return reply;
		}
	}

	/// <summary>Takes this turn's text away (e.g. "Let me look that up"), and hides what follows: the cards say it.</summary>
	private void HideTurnText()
	{
		foreach (var text in Entries.Skip(_turnStart).OfType<ChatReplyEntry>().ToList())
		{
			Entries.Remove(text);
		}

		_quiet = true;
	}

	private void Rollback(int count)
	{
		_history.RemoveRange(count, _history.Count - count);
		// The user's message stays in the history, so the next turn answers it in context; anything after it goes.
	}

	// On one line, for the chat (the agent tools' options indent).
	private static readonly JsonSerializerOptions s_compact = new(AgentTools.SerializerOptions) { WriteIndented = false };

	/// <summary>A tool's arguments, as compact JSON; empty when none.</summary>
	private static string Format(IDictionary<string, object?>? arguments)
	{
		return arguments is { Count: > 0 } ? JsonSerializer.Serialize(arguments, s_compact) : "";
	}

	/// <summary>
	/// The anime a tool returned, in the order to show them: one (e.g. get_anime's), a list in the order it ranked them
	/// (e.g. by episodes), or a list best scored first. Anime within those (e.g. related ones) aren't.
	/// </summary>
	private static List<AnimeModel> AnimeIn(object? raw) => raw switch
	{
		IAgentAnime { Source: { } anime } => [anime],
		IAgentAnimeList list => [.. SourcesOf(list.Ranked)],
		IEnumerable<IAgentAnime> many => [.. SourcesOf(many).OrderByDescending(x => x.MeanScore ?? 0)],
		_ => [],
	};

	/// <summary>How many anime the tool found: all of a ranked list (it returns the first), or those it returned.</summary>
	private static int TotalOf(object? raw) => raw is IAgentAnimeList list ? Math.Max(list.Total, list.Ranked.Count) : AnimeIn(raw).Count;

	private static IEnumerable<AnimeModel> SourcesOf(IEnumerable<IAgentAnime> anime) =>
		anime.Select(x => x.Source).OfType<AnimeModel>().DistinctBy(x => x.Id);

	/// <summary>The message of an <see cref="AgentToolError"/> result, if that's what the tool returned.</summary>
	private static string? ErrorOf(object? result)
	{
		var element = result switch
		{
			JsonElement json => json,
			AgentToolError error => JsonSerializer.SerializeToElement(error, AgentTools.SerializerOptions),
			_ => (JsonElement?)null,
		};
		return element is { ValueKind: JsonValueKind.Object } value && value.TryGetProperty("error", out var message) &&
			   message.ValueKind == JsonValueKind.String
			? message.GetString()
			: null;
	}
}
