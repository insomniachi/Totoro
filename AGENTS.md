# Totoro

A WinUI 3 app (.NET 10, Windows App SDK) for tracking and watching anime.

## Projects and folders

The projects are in `src/`; the solution (`Totoro.slnx`) and the build settings they share (`Directory.Build.props`, `Directory.Packages.props` for package versions) are at the root. Paths below are within `src/`.

| Project | Holds |
|---|---|
| `Totoro.Core` | What doesn't depend on WinUI: module contracts, settings, services, agent tool infrastructure. |
| `Totoro.UI` | Reusable WinUI pieces: controls, dialogs, navigation, theming, threading. |
| `Totoro` | The app: pages and view models, modules, playback, agent tools. |

- `Totoro.Core/Modules/<Area>`: what modules implement and use, e.g. `IAnimeTracker` (Anime), `IMediaPlayer` (Media), `IAnimeProvider` and `ITorrentStreamer` (Streaming).
- `Totoro.Core/Services`: shared logic the UI and agent tools both call, e.g. `AnimeTrackingService`.
- `Totoro.Core/Agent`: the agent tool infrastructure (`IAgentTools`, `[AgentTool]`).
- `Totoro.UI/AnimatedVisuals`: our own animated icons (in `Icons`), as Lottie files and the code LottieGen generates from them: change a `.json` and run `Generate.ps1` there.
- `Totoro/Modules/<Kind>/<Name>`: a module, e.g. `AnimeDatabase/Anilist`, `TorrentStreamers/Rqbit`. Code generated from a schema goes in a subfolder with its own namespace (`Anilist/GraphQl`). A module's window or pages go with the others in `Views` and `ViewModels`, e.g. the built-in player's (`MediaPlayers/Vlc`) in `Player/`.
- `Totoro/Agent`: the app's agent tools.
- `Totoro/ViewModels` and `Totoro/Views`: pages and dialogs, side by side by name (`AnimeSettingsDialogViewModel` and `AnimeSettingsDialog`). A page's parts go in a subfolder (`AnimeDetails/`).
- `Totoro/Playback`: watching an episode (`WatchService`), and downloading one (`DownloadService`, into folders named by `DownloadNames` with the anime's AniList and MAL ids).

## Code conventions

Enforced at build time by `.editorconfig` (warnings):

- Microsoft's C# naming conventions: `_camelCase` fields, `s_camelCase` static fields, `Async` suffix, and so on.
- Namespaces match folders, and are file-scoped.

Also:

- **UI layout:** follow the Windows spacing guidelines: 8 between buttons and from a header to its control, 12 between content areas, 16 for a surface's padding. Line sections up with each other.
- **Toolkit choice:** prefer CommunityToolkit controls over DevWinUI's when both have one.
- **x:Bind:** it doesn't call a function while something along its argument's path is null (`ViewModel` not set yet), leaving the old value; but it does call it with an argument that is null itself. So strings passed to x:Bind functions are never null (use `""`), and functions taking an object that can be null take it as nullable and handle null.
- **Processes the app starts** (media players, rqbit...) end with it: pass them to `ChildProcesses.Add`. Without the app, nothing tracks what's watched.

## Everything the app does is available to AI agents

The app has an assistant: a chat panel (the title bar's Assistant button) whose model, a local one through Ollama for now, calls the agent tools (`ChatService`, with models as `IChatModel` modules under `Totoro/Modules/ChatModels`). Agents, in the app or later over MCP, must be able to do whatever the user can do in the UI. So every feature has three parts:

1. **Logic in a service, not a view model or dialog.** Views and agent tools both call it, e.g. `AnimeTrackingService`, `WatchService`, `IAnimeSettingsStore`.
2. **An agent tool.** It's a method marked `[AgentTool("snake_case_name")]` in a tool class under `Totoro/Agent`, or a module's own class, registered with `services.AddAgentTools<T>()`.
   - **Descriptions:** describe the method and each parameter with `[Description]`; that's all the agent knows about it.
   - **Ids, not UI objects:** take anime ids (in the selected database), names and enums.
   - **Results:** return records (`Totoro/Agent/AgentResults.cs`) whose values say what they mean, e.g. scores out of 10. Return anime as `AnimeSummary` and songs as `AnimeSongList` (made with `From`): the chat shows them as the app's own cards, which open, add and play them. Any result can do that by implementing `IAgentAnime` or `IAgentSongs`, whose source objects stay out of its JSON (`AgentToolResult`).
   - **Failures:** throw `AgentToolException` with a message the agent can act on ("Search for it to get its id."). Other exceptions become a generic error.
   - **No dialogs:** where the UI asks the user (e.g. which show on a provider), return the choices and offer a tool to make the choice.
   - **Approval:** mark anything destructive `RequiresApproval = true` (removing from the list, signing out) so a chat asks the user first.
   - **Threading:** tools run on the UI thread, so they can change state the UI is bound to.
3. **The UI follows changes made elsewhere.** Changes go out as messages (`AnimeTrackingChangedMessage`, `PlaybackProgressChangedMessage`...) that views apply, so a change made by an agent shows straight away.

Try the tools in the app on the **Agent tools** page. It lists every tool with the schema the agent sees, and calls a tool with JSON arguments.

## Finding your way around: graphify

The code is mapped as a knowledge graph in `graphify-out/` (rebuilt by a git hook after each commit). Follow `.agents/rules/graphify.md`: for questions about the code, ask the graph first, with the `graphify` MCP server's tools (`query_graph`, `get_node`, `shortest_path`..., set up in `.mcp.json`). The CLI is `py -3.13 -m graphify` (its `graphify.exe` isn't on the PATH), e.g. `py -3.13 -m graphify query "..."` or `py -3.13 -m graphify update .` after changing code.
