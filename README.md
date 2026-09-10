# Cover Shuffle

A Playnite extension that lets you keep a small pool of alternate cover
artworks per game (up to 10) and rotate through them automatically, or
on demand.

## Features

- **Per-game cover pool** — up to 10 covers per game, added from:
  - a local image file,
  - [SteamGridDB](https://www.steamgriddb.com/) (requires a free API key), or
  - artwork Playnite already has for the game (its cover, background, and icon) —
    works without any API key.
- **Safe artwork ownership** — the game's original cover is captured the first
  time Cover Shuffle takes control of a game, and can always be restored.
  Removing a cover from the pool never deletes the underlying file.
- **Scheduled shuffling** — each game has its own shuffle interval (or
  inherits the global default). A due shuffle is applied once when Playnite
  starts; Playnite being closed for a while never causes a pile-up of missed
  shuffles.
- **New-game automation** — choose whether newly installed games are left
  alone, prompted about, or automatically prepared for Cover Shuffle. This
  never contacts SteamGridDB on its own.
- **Cover Shuffle Manager** — a searchable list of every game Cover Shuffle
  manages, with bulk enable/disable/interval actions and configuration
  import/export.
- **Maintenance** — scan for orphaned cover files, cover records whose file
  went missing, and cached provider images; nothing is deleted without an
  explicit, confirmed action.

## Using it

Right-click a game in Playnite → **Cover Shuffle**:

| Action | What it does |
|---|---|
| Enable / Disable Cover Shuffle | Turns scheduled shuffling on/off for this game |
| Add Cover → Local File | Copies a picture from disk into the pool |
| Add Cover → SteamGridDB | Search and browse SteamGridDB covers visually |
| Add Cover → Playnite Metadata | Pull in the game's existing cover/background/icon |
| Shuffle Now | Immediately advances to the next cover in the pool |
| Manage Covers | View and remove covers from the pool |
| Restore Original Cover | Puts the game's original cover back |

The main Playnite menu has a **Cover Shuffle Manager...** entry for managing
many games at once (search, bulk enable/disable/interval, import/export,
maintenance).

Global defaults — shuffle interval, shuffle-on-startup, new-game behavior,
and the SteamGridDB API key — are in the plugin's own settings page.

## Importing/exporting configuration

The Manager's Export button writes a `CoverShuffle.json` file plus the
referenced cover images into a folder you choose. Import reads that same
folder back in. This is meant for restoring your configuration on another
Playnite installation that shares the same library (e.g. a synced library on
a new PC) — Playnite's own game IDs need to match for the restored
configuration to line up with the right games.

## Known limitations

- Shuffling picks the next cover in the pool by add order; it is not yet the
  full randomized-cycle-with-no-immediate-repeat algorithm.
- A due shuffle is applied at Playnite startup only — a game left running for
  days past its interval shuffles at the next restart, not mid-session.
- Only the first SteamGridDB search result for a game name is used; there is
  no UI to disambiguate an ambiguous name.
- The Playnite Metadata provider only picks up artwork already downloaded to
  a local file, not a bare remote URL some library plugins store instead.

## Development

- Target framework: `net462` (matches Playnite's own runtime).
- Build: `dotnet build PluginCoverShuffle.csproj` (the build also copies the
  DLL next to `extension.yaml`, where Playnite expects it, and to
  `bin/<Configuration>/`).
- Tests: `dotnet test Tests/PluginCoverShuffle.Tests.csproj` — all tests run
  against fakes; none require a live Playnite installation, network access,
  or a SteamGridDB API key.
- See `CLAUDE.md` for the engineering ruleset this project was built against.
