# IGL-MatchZy

A CS2 match plugin for the [Ibex Gaming League](https://ibexgaming.org), built on [CounterStrikeSharp](https://github.com/roflmuffin/CounterStrikeSharp).

IGL-MatchZy is a fork of **[MatchZy](https://github.com/shobhit-pathak/MatchZy)** by [shobhit-pathak](https://github.com/shobhit-pathak). It started from upstream **0.8.15** and has been changed a lot since. Most of the changes support **IGL match-api**, the service that creates matches, provisions servers and collects stats for the IGL tournament platform.

> **Upstream documentation still applies.** Match setup, practice mode, coaching, backups, database stats and most commands work as described in the [MatchZy docs](https://shobhit-pathak.github.io/MatchZy/). This README covers only what IGL-MatchZy does differently.

## What's different from upstream

- **More events for match-api:** `veto_started`, pause/unpause, `timeout_started`, `chat_message`, plus `player_disconnect` and `demo_upload_ended`, which upstream declared but never sent. Events are delivered in order, retried on failure and carry an `event_id` for de-duplication. `player_disconnect.player` is now a SteamID64 string ⚠️ breaking.
- **Map veto follows the [IGL CS2 ruleset](https://github.com/ibexgaming/rules)** (BO2 and BO5 order changed), and `.captain` lets a player take over as veto captain.
- **Demo and backup uploads** are streamed from disk, retried, and allowed to run longer, so large demos upload reliably.
- **Server stability:** optional map change after a series ends, to avoid a server crash ([incident report](https://ibexgaming.statuspage.io/incidents/rvf7gzjmzz6s)). Backup restore sets team names correctly.
- **New cvars and IGL defaults:** see the [configuration docs](documentation/docs/configuration.md) and [cfg/MatchZy/config.cfg](cfg/MatchZy/config.cfg).

Event payloads are documented in [documentation/docs/event_schema.yml](documentation/docs/event_schema.yml). See [CHANGELOG.md](CHANGELOG.md) for the full list of changes.

---

## Installation

Requires [Metamod:Source](https://www.sourcemm.net/downloads.php?branch=dev) and [CounterStrikeSharp](https://github.com/roflmuffin/CounterStrikeSharp) on the server.

1. Download the latest release from [Releases](https://github.com/IbexGaming/IGL-MatchZy/releases). Each release has three zips:
   - `MatchZy-<version>.zip`: the plugin only
   - `MatchZy-<version>-with-cssharp-linux.zip`: the plugin and CounterStrikeSharp for Linux
   - `MatchZy-<version>-with-cssharp-windows.zip`: the plugin and CounterStrikeSharp for Windows
2. Extract it into the server's `game/csgo/` directory.
3. Adjust `cfg/MatchZy/config.cfg` as needed.

For anything else about installing and configuring, see the [upstream guide](https://shobhit-pathak.github.io/MatchZy/installation/).

## Development

```bash
dotnet tool restore          # installs CSharpier and Husky
dotnet build
dotnet csharpier format .    # CI rejects unformatted code
```

A Husky pre-commit hook is included for formatting. Add new entries to [CHANGELOG.md](CHANGELOG.md) and bump `ModuleVersion` in [MatchZy.cs](MatchZy.cs) when you release.

## Credits

All the core functionality comes from **[MatchZy](https://github.com/shobhit-pathak/MatchZy)** by [shobhit-pathak](https://github.com/shobhit-pathak) and its contributors. If you find this plugin useful, please consider [supporting the original author](https://www.buymeacoffee.com/shobhitwd).

MatchZy itself builds on [Get5](https://github.com/splewis/get5), [G5API/G5V](https://github.com/PhlexPlexico/G5API), [eBot](https://github.com/deStrO/eBot-CSGO) and [CounterStrikeSharp](https://github.com/roflmuffin/CounterStrikeSharp). See the [upstream credits](https://github.com/shobhit-pathak/MatchZy#credits-and-thanks) for the full list.

## License

[MIT](LICENSE), the same as upstream.
