using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;

namespace MatchZy;

/// <summary>
/// Tracks trade kills and KAST (rounds with a Kill, Assist, Survival or being Traded).
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
