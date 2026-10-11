# Totoro

A WinUI 3 app (.NET 10, Windows App SDK) for tracking and watching anime.

## Projects and folders

| Project | Holds |
|---|---|
| `Totoro.Core` | What doesn't depend on WinUI: module contracts, settings, services, agent tool infrastructure. |
| `Totoro.UI` | Reusable WinUI pieces: controls, dialogs, navigation, theming, threading. |
| `Totoro` | The app: pages and view models, modules, playback, agent tools. |

- `Totoro.Core/Modules/<Area>`: what modules implement and use, e.g. `IAnimeTracker` (Anime), `IMediaPlayer` (Media), `IAnimeProvider` and `ITorrentStreamer` (Streaming).
- `Totoro.Core/Services`: shared logic the UI and agent tools both call, e.g. `AnimeTrackingService`.
- `Totoro.Core/Agent`: the agent tool infrastructure (`IAgentTools`, `[AgentTool]`).
- `Totoro/Modules/<Kind>/<Name>`: a module, e.g. `AnimeDatabase/Anilist`, `TorrentStreamers/Rqbit`. Code generated from a schema goes in a subfolder with its own namespace (`Anilist/GraphQl`).
- `Totoro/Agent`: the app's agent tools.
- `Totoro/ViewModels` and `Totoro/Views`: pages and dialogs, side by side by name (`AnimeSettingsDialogViewModel` and `AnimeSettingsDialog`). A page's parts go in a subfolder (`AnimeDetails/`).
- `Totoro/Playback`: watching an episode (`WatchService`).

## Code conventions

Enforced at build time by `.editorconfig` (warnings):

- Microsoft's C# naming conventions: `_camelCase` fields, `s_camelCase` static fields, `Async` suffix, and so on.
- Namespaces match folders, and are file-scoped.

Also:

- **UI layout:** follow the Windows spacing guidelines: 8 between buttons and from a header to its control, 12 between content areas, 16 for a surface's padding. Line sections up with each other.
- **Toolkit choice:** prefer CommunityToolkit controls over DevWinUI's when both have one.
- **x:Bind:** it doesn't call a function whose argument is null. Strings passed to x:Bind functions are never null (use `""`).
- **Processes the app starts** (media players, rqbit...) end with it: pass them to `ChildProcesses.Add`. Without the app, nothing tracks what's watched.

## Everything the app does is available to AI agents

An AI chat may be added later, and agents (in-app, or over MCP) must be able to do whatever the user can do in the UI. So every feature has three parts:

1. **Logic in a service, not a view model or dialog.** Views and agent tools both call it, e.g. `AnimeTrackingService`, `WatchService`, `IAnimeSettingsStore`.
2. **An agent tool.** It's a method marked `[AgentTool("snake_case_name")]` in a tool class under `Totoro/Agent`, or a module's own class, registered with `services.AddAgentTools<T>()`.
   - **Descriptions:** describe the method and each parameter with `[Description]`; that's all the agent knows about it.
   - **Ids, not UI objects:** take anime ids (in the selected database), names and enums.
   - **Results:** return records (`Totoro/Agent/AgentResults.cs`) whose values say what they mean, e.g. scores out of 10.
   - **Failures:** throw `AgentToolException` with a message the agent can act on ("Search for it to get its id."). Other exceptions become a generic error.
   - **No dialogs:** where the UI asks the user (e.g. which show on a provider), return the choices and offer a tool to make the choice.
   - **Approval:** mark anything destructive `RequiresApproval = true` (removing from the list, signing out) so a chat asks the user first.
   - **Threading:** tools run on the UI thread, so they can change state the UI is bound to.
3. **The UI follows changes made elsewhere.** Changes go out as messages (`AnimeTrackingChangedMessage`, `PlaybackProgressChangedMessage`...) that views apply, so a change made by an agent shows straight away.

Try the tools in the app on **Test → Agent tools**. It lists every tool with the schema the agent sees, and calls a tool with JSON arguments.
