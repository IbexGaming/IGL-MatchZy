#  Events & Forwards

MatchZy contains an event-logging system (heavily inspired by Get5) that logs many details about what is happening in the game.

## HTTP

To receive MatchZy events on a web server, define a [URL for event logging](../configuration#matchzy_remote_log_url). MatchZy
will send all events to the URL as JSON over HTTP. You may add
a [custom HTTP header](../configuration#matchzy_remote_log_header_key) to authenticate your request.

!!! warning "Simple HTTP"

    There is no deduplication or retry-logic for failed requests. It is assumed that a stable connection can be made
    between your game server and the URL at all times.

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
