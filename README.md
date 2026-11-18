# Totoro

A Windows app for tracking and watching anime, and tracking and reading manga, built with WinUI 3 and .NET 11.

![Discover: a spotlight of what's trending, then rows of trending anime and the sequels you missed](docs/screenshots/discover.jpg)

- **Your lists, from AniList or MyAnimeList.** Browse and search anime and manga, and track what you watch and read. When you're signed in to both, changes are made on both; signed out, your AniList list is kept on your PC.
- **Discover.** What's trending, what airs today, what starts next season, and the sequels, prequels, side stories and spin-offs of what you've watched that aren't on your list.
- **Search by genre, tag or theme.** Pick the genres, tags or themes an anime or manga must have, or must not, along with its year and score.
- **Plugins.** Where episodes and chapters come from is up to plugins, which anyone can write; install them from the Plugins page. Plugins can also add anime databases, debrid services and more.
- **Watch episodes.** They play in a built-in player (with libvlc or Windows' own media engine) or in mpv, in its own window or in Totoro's (with mpv's config, so Anime4K upscaling works either way; in Totoro's, its mode is picked from the controls), streamed or downloaded, with torrents streamed through rqbit. It can download the next episode while you watch, and tracks episodes you watch in your browser too.
- **Skips openings and endings.** Using Aniskip's times, or found by their sound in downloaded episodes, it offers to skip them or skips them for you. It offers the next episode, and marks episodes watched when you finish them.
- **Read manga.** Chapters open in the in-app reader, which tracks your progress.
- **Schedule.** A calendar of when the episodes of what you're watching air.
- **Episode numbering.** For sources that number episodes across seasons, it uses anime-relations, plus rules of your own.
- **Music.** Each anime's openings and endings, from AnimeThemes, to listen to or watch, with their lyrics (from LRCLIB) lit up as they're sung, and Japanese shown with furigana and romaji. Hear one playing somewhere else on your PC? The listen button in the search box names it, and the anime it's from.
- **Find an anime from a screenshot.** trace.moe finds which anime a picture is from.
- **Discord.** It can show the episode you're watching as your Discord status.
- **An assistant.** Ask it about your list or for anything the app does ("what's airing that I haven't added?", "play the opening of Frieren"); it answers with the app's own cards. It runs on your PC through [Ollama](https://ollama.com), with `qwen2.5:14b` by default (its settings are on the Plugins page).
- **AI-agent friendly.** Everything the app does is also an agent tool (see [AGENTS.md](AGENTS.md)).
- **Stays out of the way.** It can start with Windows, sit in the notification area, tell you when a new episode of something you're watching is out on its plugin, and update itself from GitHub releases.

## Screenshots

![The built-in player during an episode's opening, offering to skip it, with the opening and ending marked on the seek bar](docs/screenshots/player.jpg)

**Watching**: the built-in player offers to skip the opening, and marks it and the ending on the seek bar

| | |
|---|---|
| ![Library: the anime being watched, with countdowns to their next episodes and progress](docs/screenshots/library.jpg) | ![An anime's details: its facts, synopsis and videos, with tabs for episodes, characters, music and related anime](docs/screenshots/details.jpg) |
| **Library**: your list, with countdowns to the next episodes | **Details**: an anime's episodes, characters, music and related anime |
| ![Sequels and spin-offs missed: a grid of related anime not on the list, with filters and sorting](docs/screenshots/missed.jpg) | ![Release schedule: a month's calendar of when watched anime air](docs/screenshots/schedule.jpg) |
| **Missed sequels and spin-offs**, filtered and sorted | **Schedule**: when what you're watching airs |
| ![Manga Discover: trending and all-time popular manga](docs/screenshots/manga-discover.jpg) | ![The manga reader showing the first page of a chapter](docs/screenshots/manga-reader.jpg) |
| **Manga**: discover, track and read | **Reader**: chapters open in the app |
| ![An anime's openings and endings, one playing with its Japanese lyrics shown with furigana and romaji](docs/screenshots/music.jpg) | ![The Plugins page, filtered to the built-in plugins](docs/screenshots/plugins.jpg) |
| **Music**: openings and endings, with lyrics as they're sung | **Plugins**: install sources, players and services, and change their settings |

## Building

You need:

- Windows 10 (1809) or later
- The [.NET 11 SDK](https://dotnet.microsoft.com/download)
- The Windows App SDK's runtime, or Visual Studio 2026 with the *WinUI application development* workload

Then:

```bash
dotnet build src/Totoro/Totoro.csproj -c Release -p:Platform=x64
```

Or open `Totoro.slnx` in Visual Studio and run the `Totoro` project. libvlc comes from NuGet. rqbit and mpv (libmpv too) aren't part of the build: the app downloads them into its data folder.

### The installer

```bash
./installer/build.ps1 -Version 2.1.0
```

publishes the app self-contained (with .NET and the Windows App SDK's runtime) and builds `installer/output/Totoro-2.1.0-setup-x64.exe` with [Inno Setup 6](https://jrsoftware.org/isinfo.php). It installs for the current user, without admin rights.

GitHub Actions builds the installer only when run by hand (Actions > Installer > Run workflow). It takes a version, or uses the last release's with the patch number incremented, sets the project to that version, and publishes a GitHub release tagged with it (such as `v2.1.0`) with the installer attached.

Installed copies update themselves from those releases: Totoro looks for a newer one when it starts (Settings > About), offers it, and installs it on a click, checking the download against GitHub's SHA-256. It closes for the installer, which starts it again.

## Repository layout

```
├── src/
│   ├── Totoro/          The app: pages, view models, modules, playback, agent tools
│   ├── Totoro.Core/     What doesn't depend on WinUI: contracts, settings, services
│   └── Totoro.UI/       Reusable WinUI pieces: controls, dialogs, navigation, theming
├── tests/Totoro.Tests/  Tests
├── installer/           The installer's script (Inno Setup) and build script
├── Totoro.slnx          The solution
├── Directory.Build.props, Directory.Packages.props   Build settings and package versions
└── AGENTS.md            How the code is organised, and its conventions
```

## License

Totoro is MIT licensed; see [LICENSE](LICENSE). What it includes, and their licenses, are in
[THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md). libvlc and NMeCab, which it ships, are LGPL-2.1; their license, and where to get
their source, ship with the app.

## Thanks

- [rqbit](https://github.com/ikatson/rqbit) (Apache-2.0) streams torrents.
- [libvlc](https://www.videolan.org/vlc/libvlc.html) and [LibVLCSharp](https://code.videolan.org/videolan/LibVLCSharp) (LGPL-2.1) play video.
- [NAudio](https://github.com/naudio/NAudio) plays songs' audio, and hears what the PC plays for [Shazam](https://www.shazam.com) to name, as [shazam-for-real](https://github.com/AlekseyMartynov/shazam-for-real) (ISC) does.
- Anime data comes from [AniList](https://anilist.co), [MyAnimeList](https://myanimelist.net) (its search, genres and schedule through [tenrai](https://tenrai.org), which serves the [Jikan](https://jikan.moe) API).
- Episode details and id mappings come from [ani.zip](https://ani.zip) and [tenrai](https://tenrai.org).
- Skip times come from [Aniskip](https://aniskip.com), episode numbering from [anime-relations](https://github.com/erengy/anime-relations), and songs from [AnimeThemes](https://animethemes.moe).
- Lyrics come from [LRCLIB](https://lrclib.net), with Japanese read by [NMeCab](https://github.com/komutan/NMeCab) (LGPL-2.1).
- [trace.moe](https://trace.moe) finds anime from screenshots.
