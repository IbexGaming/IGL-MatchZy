using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;

namespace MatchZy;

/// <summary>
/// Tracks trade kills and KAST (rounds with a Kill, Assist, Survival or being Traded).
///
/// Rules:
/// <list type="bullet">
/// <item>Only tracked while the match is live (not in warmup, knife round or practice).</item>
/// <item><b>Trade</b>: player V is traded when the enemy K who killed V is killed by one of V's
/// teammates within <c>matchzy_trade_window_seconds</c> of V's death. The window is inclusive and is
/// measured in game time (<c>Server.CurrentTime</c>), so pauses do not count towards it. A negative
/// window is treated as 0.</item>
/// <item>If K killed several players (V1, V2, ...) and is then killed within the window of each death,
/// every one of them is traded, but the teammate who killed K is credited with a single trade kill.</item>
/// <item>If K dies to the world, the bomb, fall damage or a suicide, nobody is traded.</item>
/// <item><b>K</b>: at least one kill of an enemy in the round. Team kills and suicides do not count.</item>
/// <item><b>A</b>: listed as assister (including flash assists) on the death of an enemy.</item>
/// <item><b>S</b>: still alive when the round ends. Dying after the round is decided (e.g. to the bomb
/// or while saving) is not survival.</item>
/// <item>KAST is the number of rounds in which at least one criterion was met, not a percentage.
/// Consumers divide by <c>RoundsPlayed</c> themselves.</item>
/// <item>Only non-bot players on T or CT at round end get the round counted. Players who disconnect
/// mid-round get nothing for that round.</item>
/// <item>Results are stored per round index (<c>TotalRoundsPlayed</c> at round start). Every round start
/// drops results for that index and later ones, so a round backup restore rewinds them automatically.</item>
/// <item>State lives in memory only: a plugin reload or server crash loses it, and it is reset with the match.</item>
/// </list>
/// </summary>
public partial class MatchZy
{
    private int currentKastRound = 0;

    // Deaths of this round that can still be traded: who died, on which team, by whom, when.
    private readonly List<(
        ulong VictimSteamId,
        int VictimTeam,
        int KillerUserId,
        float Time
    )> kastRecentDeaths = new();
    private readonly HashSet<ulong> kastKilledThisRound = new();
    private readonly HashSet<ulong> kastAssistedThisRound = new();
    private readonly HashSet<ulong> kastTradedThisRound = new();
    private readonly HashSet<ulong> kastDiedThisRound = new();

    // steamid -> round indices with KAST
    private readonly Dictionary<ulong, HashSet<int>> kastRounds = new();

    // steamid -> round index -> trade kills in that round
    private readonly Dictionary<ulong, Dictionary<int, int>> tradeKillsByRound = new();

    private void OnKastRoundStart()
    {
        // Scored here rather than at round end so deaths during the post-round delay count.
        // A score for state left over from before the match went live is dropped by the rewind below.
        if (isMatchLive)
            ScoreKastRound();

        currentKastRound = GetGameRules().TotalRoundsPlayed;
        kastRecentDeaths.Clear();
        kastKilledThisRound.Clear();
        kastAssistedThisRound.Clear();
        kastTradedThisRound.Clear();
        kastDiedThisRound.Clear();

        foreach (var rounds in kastRounds.Values)
            rounds.RemoveWhere(round => round >= currentKastRound);
        foreach (var rounds in tradeKillsByRound.Values)
        {
            foreach (int round in rounds.Keys.Where(round => round >= currentKastRound).ToList())
                rounds.Remove(round);
        }
    }

    private void OnKastPlayerDeath(EventPlayerDeath @event)
    {
        if (!isMatchLive)
            return;

        CCSPlayerController? victim = @event.Userid;
        if (!IsPlayerValid(victim) || !victim!.UserId.HasValue)
            return;

        kastDiedThisRound.Add(victim.SteamID);

        CCSPlayerController? attacker = @event.Attacker;
        if (
            !IsPlayerValid(attacker)
            || !attacker!.UserId.HasValue
            || attacker == victim
            || attacker.TeamNum == victim.TeamNum
        )
            return;

        float now = Server.CurrentTime;
        float tradeWindow = Math.Max(0f, tradeWindowSeconds.Value);

        kastKilledThisRound.Add(attacker.SteamID);

        // The victim's own recent kills of the attacker's teammates are now traded.
        bool isTradeKill = false;
        foreach (var death in kastRecentDeaths)
        {
            if (
                death.KillerUserId == victim.UserId.Value
                && death.VictimTeam == attacker.TeamNum
                && now - death.Time <= tradeWindow
            )
            {
                kastTradedThisRound.Add(death.VictimSteamId);
                isTradeKill = true;
            }
        }
        if (isTradeKill && !attacker.IsBot)
        {
            if (!tradeKillsByRound.TryGetValue(attacker.SteamID, out var rounds))
                tradeKillsByRound[attacker.SteamID] = rounds = new();
            rounds[currentKastRound] = rounds.GetValueOrDefault(currentKastRound) + 1;
        }

        CCSPlayerController? assister = @event.Assister;
        if (IsPlayerValid(assister) && assister!.TeamNum != victim.TeamNum)
            kastAssistedThisRound.Add(assister.SteamID);

        kastRecentDeaths.Add((victim.SteamID, victim.TeamNum, attacker.UserId.Value, now));
    }

    private void ScoreKastRound()
    {
        foreach (var player in playerData.Values)
        {
            if (!player.IsValid || player.IsBot || (player.TeamNum != 2 && player.TeamNum != 3))
                continue;

            ulong steamId = player.SteamID;
            bool metKast =
                kastKilledThisRound.Contains(steamId)
                || kastAssistedThisRound.Contains(steamId)
                || !kastDiedThisRound.Contains(steamId)
                || kastTradedThisRound.Contains(steamId);
            if (!metKast)
                continue;

            if (!kastRounds.TryGetValue(steamId, out var rounds))
                kastRounds[steamId] = rounds = new();
            rounds.Add(currentKastRound);
        }
    }

    private int GetKastRounds(ulong steamId) =>
        kastRounds.TryGetValue(steamId, out var rounds) ? rounds.Count : 0;

    private int GetTradeKills(ulong steamId) =>
        tradeKillsByRound.TryGetValue(steamId, out var rounds) ? rounds.Values.Sum() : 0;

    private void ResetKast()
    {
        currentKastRound = 0;
        kastRecentDeaths.Clear();
        kastKilledThisRound.Clear();
        kastAssistedThisRound.Clear();
        kastTradedThisRound.Clear();
        kastDiedThisRound.Clear();
        kastRounds.Clear();
        tradeKillsByRound.Clear();
    }
}
