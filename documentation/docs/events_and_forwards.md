#  Events & Forwards

MatchZy contains an event-logging system (heavily inspired by Get5) that logs many details about what is happening in the game.

## HTTP

To receive MatchZy events on a web server, define a [URL for event logging](../configuration#matchzy_remote_log_url). MatchZy
will send all events to the URL as JSON over HTTP. You may add
a [custom HTTP header](../configuration#matchzy_remote_log_header_key) to authenticate your request.

!!! warning "Retries and deduplication"

    Events are sent one at a time in the order they happened. A failed request is retried up to 5 times with
    exponential backoff (2s, 4s, 8s, 16s) on network errors and `408`, `429` and `5xx` responses, and later events
    wait until it was sent or finally failed. Other `4xx` responses are not retried. Events are not persisted, so
    events still queued when the plugin unloads or the server stops are lost.

    Since a request can be retried after the server already processed it, the same event may be delivered more
    than once. Every event carries a unique `event_id` that stays the same across retries; use it to deduplicate.

## Events

OpenAPI documentation of the events sent by MatchZy is available [here](events.html).

### Event Categories

| Category | Events |
|---|---|
| **Series Flow** | `series_start`, `veto_started`, `map_vetoed`, `map_picked`, `side_picked`, `series_end`, `player_disconnect` |
| **Map Flow** | `going_live`, `map_result`, `demo_upload_ended` |
| **Live** | `round_end` |
| **Pause** | `match_paused`, `match_force_paused`, `match_unpaused`, `match_force_unpaused`, `timeout_started` |
| **Client Actions** | `chat_message` |

### Match Lifecycle

Events fire in this order for a typical match:

1. `series_start` — match config loaded, teams known
2. `veto_started` — veto phase begins
3. `map_vetoed` / `map_picked` / `side_picked` — one event per veto/pick action
4. `going_live` — knife or live round begins on the selected map
5. `round_end` — fires after each round result (repeats)
6. `map_result` — map ends
7. `series_end` — all maps played, winner determined

`player_disconnect`, `match_paused`, `match_unpaused`, `timeout_started`, and `chat_message` can fire at any point during steps 4–6.

### Notes on Field Types

- The `side` field uses lowercase enum values: `ct`, `t`, or `spec`.
- The `team` field uses enum values: `team1`, `team2`, or `spec` (for spectators in `chat_message`).

### KAST & Trades

The `trade_kills` and `kast` fields in the player stats of the `round_end` event follow these rules:

- Tracking only runs while the match is live. Warmup, knife round and practice are ignored.
- **Trade**: player V is traded when the enemy K who killed V is killed by one of V's teammates within [`matchzy_trade_window_seconds`](configuration.md#matchzy_trade_window_seconds) (default 5) of V's death.
    - The window is inclusive: a kill at exactly the window length still counts.
    - The window uses game time, so pauses do not count towards it.
    - If K killed several players and is then killed within the window of each of those deaths, all of them are traded. The teammate who killed K gets **one** trade kill.
    - If K dies to the world, the bomb, fall damage or a suicide, nobody is traded.
- **K**ill: at least one kill of an enemy in the round. Team kills and suicides do not count.
- **A**ssist: listed as assister on an enemy's death. Flash assists count.
- **S**urvived: still alive when the round ends. Dying after the round is decided (e.g. to the bomb, or while saving) is not survival.
- `kast` is the **number of rounds** in which at least one criterion was met, not a percentage. Divide by `rounds_played` to get KAST %.
- A round is scored when the next round starts, so that deaths in the post-round delay count. `kast` in a `round_end` event therefore covers the rounds before that one. The last round of a map is scored at map end, and it is included in the final stats written to the database and CSV.
- Only non-bot players on T or CT when the round is scored get it counted. A player who disconnects before then gets nothing for that round.
- Restoring a round backup rewinds `trade_kills` and `kast` to the restored round.
- The values are kept in memory only. A plugin reload or server crash mid-match resets them, and so does a match reset.
