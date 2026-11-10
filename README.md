# IGL-MatchZy

A CS2 match plugin for the [Ibex Gaming League](https://ibexgaming.org), built on [CounterStrikeSharp](https://github.com/roflmuffin/CounterStrikeSharp).

IGL-MatchZy is a fork of **[MatchZy](https://github.com/shobhit-pathak/MatchZy)** by [shobhit-pathak](https://github.com/shobhit-pathak). It started from upstream **0.8.15** and has been changed a lot since. Most of the changes support **IGL match-api**, the service that creates matches, provisions servers and collects stats for the IGL tournament platform.

> **Upstream documentation still applies.** Match setup, practice mode, coaching, backups, database stats and most commands work as described in the [MatchZy docs](https://shobhit-pathak.github.io/MatchZy/). This README covers only what IGL-MatchZy does differently.

| | |
|---|---|
| Current version | `0.11.0` (see [CHANGELOG.md](CHANGELOG.md)) |
| Based on | MatchZy `0.8.15` |
| Runtime | .NET 10, CounterStrikeSharp `1.0.375` |
| Plugin name | `IGL - MatchZy` (chat prefix `[IGL-MatchZy]`) |

---

## What's different from upstream

### More events for match-api

| Change | Details |
|---|---|
| **New events** | `veto_started`, `match_paused`, `match_force_paused`, `match_unpaused`, `match_force_unpaused`, `timeout_started`, `chat_message` |
| **`player_disconnect` is now sent** | Upstream declared this event but never sent it |
| **SteamIDs instead of entity IDs** ⚠️ breaking | `player_disconnect.player` is the player's SteamID64 as a **string**. Upstream used the CounterStrikeSharp user ID (an int) |
| **Normalised enum values** | `team` is always `team1` / `team2` / `spec`; `side` is always `ct` / `t` / `spec` (lowercase) |
| **`demo_upload_ended` is now sent** | Sent after every demo upload, with whether the upload worked |
| **Ordered delivery** | Events are sent one at a time, in the order they happened |
| **Retries** | Failed events are retried with exponential backoff |
| **`event_id` on every event** | A UUID that stays the same across retries, so receivers can drop duplicates |

The full reference is in [Events for match-api](#events-for-match-api) below.

### Map veto follows the IGL ruleset

The veto order follows the [IGL CS2 ruleset](https://github.com/ibexgaming/rules):

| Format | Upstream MatchZy (7-map pool) | IGL-MatchZy (7-map pool) |
|---|---|---|
| **BO2** | ban, ban, pick, pick | **ban, ban, ban, ban, pick, pick** |
| **BO5** | pick ×4, ban ×2, then decider | **ban ×2, pick ×4, then decider** |

BO1 and BO3 are unchanged. Teams alternate on every step, starting with the team that won the knife round or was chosen to start the veto.

**Captain override:** players can type `.captain` (or `!captain`) before or during the veto to become their team's veto captain. Upstream always used whichever team member happened to be found first among connected players. The new captain takes over the current veto step straight away.

### Demo and backup uploads

- **Streamed from disk.** Uploads are no longer loaded into memory first, which also fixes uploads of files larger than 2 GB.
- **Retried.** A failed upload is retried up to 5 times with exponential backoff (2s, 4s, 8s, 16s) after network errors, timeouts and `408`, `429` or `5xx` responses. Other `4xx` responses are not retried.
- **Longer timeout.** Each upload attempt can take up to 30 minutes. The old limit was the default 100 seconds, which cut off large demos.
- **Optional cleanup.** With `matchzy_demo_delete_after_upload true`, the local demo is deleted after a successful upload. It is kept if the upload fails.

### Server stability

- **`matchzy_default_map_on_series_end`** changes to a fixed map after a series ends. This works around a server segfault that happened when the next match's veto picked the map the server was already on ([incident report](https://ibexgaming.statuspage.io/incidents/rvf7gzjmzz6s)). Use a map outside your server's map group, for example `de_vertigo` or a workshop ID.
- **Backup restore** sets team names correctly based on each team's starting side.

### New and changed settings

New cvars:

| Cvar | Default | Description |
|---|---|---|
| `matchzy_default_map_on_series_end` | `""` (off) | Map or workshop ID to change to after a series ends. |
| `matchzy_demo_delete_after_upload` | `false` | Deletes the local demo file after a successful upload to `matchzy_demo_upload_url`. |
| `matchzy_player_connect_message` | `""` (off) | Private message shown to each player when they connect. Supports `$$$` line breaks, colour tags and `{TIME}`, `{MATCH_ID}`, `{MAP}`, `{MAPNUMBER}`, `{TEAM1}`, `{TEAM2}`, `{TEAM1_SCORE}`, `{TEAM2_SCORE}`. |
| `match_enable_score_report` | `false` | Shows the score in chat after each round, like the damage report. Note: this cvar is named `match_`, not `matchzy_`. |

Defaults changed in [cfg/MatchZy/config.cfg](cfg/MatchZy/config.cfg):

| Cvar | Upstream | IGL |
|---|---|---|
| `matchzy_kick_when_no_match_loaded` | `false` | `true` |
| `matchzy_stop_command_available` | `false` | `true` |
| `matchzy_stop_command_no_damage` | `false` | `true` |
| `matchzy_tech_pause_duration` | `300` | `-1` (no limit) |
| `matchzy_max_tech_pauses_allowed` | `2` | `10` |
| `matchzy_enable_damage_report` | `true` | `false` |
| `matchzy_show_credits_on_match_start` | `true` | `false` |
| `matchzy_hostname_format` | `MatchZy \| {TEAM1} vs {TEAM2}` | `IGL \| {TEAM1} vs {TEAM2}` |
| `matchzy_chat_prefix` | `[MatchZy]` | `[IGL-MatchZy]` |

---

## Events for match-api

IGL-MatchZy sends every event as a JSON `POST` to the remote log URL. match-api sets the URL and auth header over RCON when it assigns a match to a server, so you normally don't configure these yourself. To point a server somewhere else by hand:

```cfg
matchzy_remote_log_url "https://example.com/webhooks/matchzy"
matchzy_remote_log_header_key "Authorization"
matchzy_remote_log_header_value "Bearer <token>"
```

### Delivery, retries and duplicates

- **In order.** Events go through a single queue and are sent one at a time, in the order they happened. While one event is being retried, later events wait behind it, so a slow retry never lets a later event overtake an earlier one.
- **Retries.** A failed request is retried up to 5 times with exponential backoff (2s, 4s, 8s, 16s) after network errors, timeouts and `408`, `429` or `5xx` responses. Other `4xx` responses are not retried. Each attempt times out after 10 seconds, so one event can hold up the queue for about 80 seconds at most.
- **Duplicates are possible.** If a request reaches match-api but the response is lost, the event is sent again. Every event has an `event_id` (UUID) that stays the same across retries, so use it to drop duplicates.
- **Nothing is saved to disk.** Events still in the queue when the plugin unloads or the server stops are lost.
- **Captured when queued.** Each event is sent to the URL and auth header that were set when it happened, even if the cvars change before it goes out.

### All events

★ = added or changed in IGL-MatchZy.

| Category | Event | When it fires |
|---|---|---|
| Series | `series_start` | Match config is loaded and teams are known |
| Series | ★ `veto_started` | Veto phase begins |
| Series | `map_vetoed` / `map_picked` / `side_picked` | Once per veto action |
| Series | `series_end` | All maps have been played |
| Series | ★ `player_disconnect` | A player leaves after the match has started |
| Map | `going_live` | Knife round or live play begins on a map |
| Map | `round_end` | After every round |
| Map | `map_result` | A map ends |
| Map | ★ `demo_upload_ended` | A GOTV demo upload finishes, whether it worked or not. Only sent when `matchzy_demo_upload_url` is set |
| Pause | ★ `match_paused` | A team pauses (`.pause` / `.tech`) |
| Pause | ★ `match_unpaused` | Both teams have typed `.unpause` |
| Pause | ★ `match_force_paused` | An admin force-pauses (`.forcepause` / `.fp`) |
| Pause | ★ `match_force_unpaused` | An admin force-unpauses (`.forceunpause` / `.fup`) |
| Pause | ★ `timeout_started` | A team calls a tactical timeout (`.tac`) |
| Chat | ★ `chat_message` | Any chat message while the match is live |

A typical match sends them in this order:

```
series_start → veto_started → map_vetoed / map_picked / side_picked (repeated)
  → going_live → round_end (repeated) → map_result → demo_upload_ended
  → (next map: going_live …) → series_end
```

Pause, timeout, chat and disconnect events can fire at any time while a map is being played.

### Shared fields

| Field | Type | Notes |
|---|---|---|
| `event` | string | Event name |
| `event_id` | string (UUID) | Unique for each event and unchanged across retries. Present on every event |
| `matchid` | integer | Match ID from the loaded match config |
| `map_number` | integer | Index of the current map in the series, **starting at 0** |
| `team` | string | `team1`, `team2`, or `spec` (spec only appears in `chat_message`) |
| `side` | string | `ct`, `t`, or `spec` |

`team` refers to the team in the match config, so it stays the same when sides swap at half-time. `side` is the side the team is playing at that moment.

### Payloads for the new events

Every payload also includes `event_id`. It is shown in the first example and left out of the rest to keep them short.

#### `veto_started`

```json
{ "event": "veto_started", "event_id": "3f2b8c1e-7a4d-4e9b-9c61-2d5f0a8e7b13", "matchid": 1234 }
```

#### `match_paused`

A team paused the match.

```json
{ "event": "match_paused", "matchid": 1234, "map_number": 0, "team": "team1", "side": "ct" }
```

#### `match_unpaused`

The match resumed after both teams typed `.unpause`. `team`/`side` is the team whose `.unpause` completed it. `pause_duration` is in seconds.

```json
{ "event": "match_unpaused", "matchid": 1234, "map_number": 0, "team": "team2", "side": "t", "pause_duration": 95 }
```

#### `match_force_paused` / `match_force_unpaused`

An admin paused or unpaused the match. No team is attached.

```json
{ "event": "match_force_paused", "matchid": 1234, "map_number": 0 }
{ "event": "match_force_unpaused", "matchid": 1234, "map_number": 0, "pause_duration": 300 }
```

#### `timeout_started`

A team started a tactical timeout. It is only sent if the team still has timeouts left.

```json
{ "event": "timeout_started", "matchid": 1234, "map_number": 1, "team": "team2", "side": "ct" }
```

#### `chat_message`

Sent for every chat message while the match is live, including chat commands such as `.pause`. `teamonly` is `true` for team chat.

```json
{
  "event": "chat_message",
  "matchid": 1234,
  "map_number": 0,
  "steamid": "76561198000000000",
  "player_name": "player",
  "team": "team1",
  "side": "t",
  "message": "gl hf",
  "teamonly": false
}
```

#### `player_disconnect`

`player` is the SteamID64 as a string. Only sent once the match has started.

```json
{ "event": "player_disconnect", "matchid": 1234, "player": "76561198000000000" }
```

#### `demo_upload_ended`

Sent after the upload has finished, retries included. `success` says whether the demo was stored.

```json
{ "event": "demo_upload_ended", "matchid": 1234, "map_number": 0, "filename": "2026-11-10_20-15-00_1234_de_mirage_Ibex_vs_Rivals.dem", "success": true }
```

The other events (`series_start`, `map_picked`, `round_end`, `map_result`, and so on) use the same payloads as upstream. The full OpenAPI schema for every event is in [documentation/docs/event_schema.yml](documentation/docs/event_schema.yml), and the C# definitions are in [Events.cs](Events.cs).

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
