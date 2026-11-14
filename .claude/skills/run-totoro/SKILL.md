---
name: run-totoro
description: Build, launch and drive the Totoro app with the winapp CLI (UI Automation), without desktop control. Use to run the app, play an episode, check a change in the real app, call an agent tool in it, or screenshot it.
---

# Running and driving Totoro

Everything here goes through `winapp ui` (UI Automation): no mouse, no focus needed, works from VS Code.

## Launch

```bash
winapp run src/Totoro/Totoro.csproj --detach --devtools off
```

It builds (about 60s), launches, and prints the app's **PID** as its last line. Give that PID to every later
command (`-a <pid>`): `-a Totoro` also matches other windows with "Totoro" in the title (VS Code with this repo open),
and does so silently when the app isn't running. Wait about 10s after launch before the first command.

- Rebuilding: stop the app first (`taskkill //PID <pid> //F`): it locks its output.
- `NETSDK1047 ... doesn't have a target for ... win-x64` means a plain `dotnet build` restored without the runtime:
  `dotnet restore src/Totoro/Totoro.csproj -r win-x64`, then run again.
- `--devtools on` (the default) adds an in-app XAML toolbar; you need it only for `winapp devtools`.

### In Windows Sandbox (a fresh install)

```bash
pwsh .claude/skills/run-totoro/sandbox.ps1
```

Installs the ReAnime plugin and makes images load (WinINet's revocation check hangs in the guest), then launches and
prints the PID. Add `--on sandbox` to every `winapp ui` command. In-app restart doesn't work in the sandbox.

## Drive

| To | Command |
|---|---|
| Go to a page | `winapp ui invoke "Library" --type ListItem -a <pid>` (Discover, Library, Search, Schedule, Watch, Downloads, Plugins, Agent tools, Settings) |
| Anime or manga | `winapp ui invoke "Manga" --type ListItem -a <pid>` |
| A list's status tab | `winapp ui invoke "Completed" --type ListItem -a <pid>` |
| Open an anime from a card | `winapp ui invoke "<title>" --type Button -a <pid>` (cards are buttons named after their title) |
| A card's actions (its hover buttons and right-click menu) | `winapp ui invoke "<title>" --type Button --action expand -a <pid>`, then `winapp ui invoke "Play" --type MenuItem -a <pid>` (Play, Binge, Edit tracking, Download, Settings; `--action collapse` closes it) |
| Play the next episode | `winapp ui invoke WatchButton -a <pid>` (named "Play episode N" or "Resume episode N"; read it with `get-property WatchButton`) |
| Search | `winapp ui set-value TextBox "<text>" -a <pid>` (the mode's kind; start with `a:` `m:` `c:` `v:` or `s:` for anime, manga, characters, voice actors or songs) |
| Find anything | `winapp ui search "<text>" -a <pid>`; `winapp ui inspect -a <pid> -i` lists what can be acted on |
| See it | `winapp ui screenshot -a <pid> -o <scratchpad>/shot.png`, then Read the png |

After an action that changes the page, wait a couple of seconds and search again: slugs (`btn-…-1a2b`) change when
elements are recreated; labels and AutomationIds don't.

## Call an agent tool (any feature, no navigating)

Every feature is an agent tool (see AGENTS.md), and the Agent tools page calls them:

```bash
winapp ui invoke "Agent tools" --type ListItem -a <pid>
winapp ui invoke "get_anime" --type ListItem -a <pid>          # the tool, by its name
winapp ui set-value ToolArguments '{"animeId": 12345}' -a <pid>      # its schema: get-value ToolSchema
winapp ui invoke RunTool -a <pid>
winapp ui get-value ToolResult -a <pid>                         # the JSON result
```

## Keeping the app drivable

When adding UI, check it with `winapp ui inspect -a <pid> -i`. Anything you can act on must show up there with a name:

- A button whose content isn't plain text (an icon, a StackPanel) needs `AutomationProperties.Name`.
- A control that handles `Tapped` itself needs an automation peer that implements Invoke, e.g. `MediaCard`.
- Use `controls:SelectableSegmented` / `SelectableSegmentedItem`, not the toolkit's `Segmented`: its segments can't be selected through UI Automation.
- An item in a list is named by its `ToString()` unless the container sets a name.
- Use `controls:SelectableTokenView`, not the Labs `TokenView`: reading the plain one hangs and leaves the app's whole tree unreadable.
