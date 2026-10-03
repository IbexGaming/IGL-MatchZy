using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Translations;

namespace MatchZy;

public class AdvertMessageManager
{
    private readonly MatchZy _plugin;

    public AdvertMessageManager(MatchZy plugin)
    {
        _plugin = plugin;
    }

    public void Center(
        CCSPlayerController player,
        string key,
        params object[] args)
    {
        if (!player.IsValid)
            return;

        player.PrintToCenter(
            _plugin.Localizer.ForPlayer(player, key, args)
        );
    }

    public void CenterAll(
        string key,
        params object[] args)
    {
        foreach (var player in Utilities.GetPlayers())
        {
            if (!player.IsValid || player.IsBot)
                continue;

            Center(player, key, args);
        }
    }

    public void Chat(
        CCSPlayerController player,
        string key,
        params object[] args)
    {
        if (!player.IsValid)
            return;

        player.PrintToChat(
            _plugin.Localizer.ForPlayer(player, key, args)
        );
    }

    public void ChatAll(
        string key,
        params object[] args)
    {
        foreach (var player in Utilities.GetPlayers())
        {
            if (!player.IsValid || player.IsBot)
                continue;

            Chat(player, key, args);
        }
    }
}