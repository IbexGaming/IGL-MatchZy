using System.Text.Json;
using System.Text.Json.Serialization;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Translations;
using CounterStrikeSharp.API.Modules.Cvars;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Utils;

namespace MatchZy.Advertisement;

public class AdvertisementManager
{
    private readonly BasePlugin _plugin;
    private readonly List<CounterStrikeSharp.API.Modules.Timers.Timer> _timers = new();

    public AdvertisementConfig Config { get; private set; } = new();

    public AdvertisementManager(BasePlugin plugin)
    {
        _plugin = plugin;
    }

    public void Load()
    {
        Config = LoadConfig();

        StopTimers();
        StartTimers();
    }

    private void StartTimers()
    {
        foreach (var advertisement in Config.Ads)
        {
            var timer = _plugin.AddTimer(
                advertisement.Interval,
                () => ShowAdvertisement(advertisement),
                TimerFlags.REPEAT
            );

            _timers.Add(timer);
        }
    }

    private void StopTimers()
    {
        foreach (var timer in _timers)
            timer.Kill();

        _timers.Clear();
    }

    private void ShowAdvertisement(Advertisement advertisement)
    {
        var messages = advertisement.NextMessages;

        if (messages.TryGetValue("Chat", out var chat))
            PrintChat(chat);

        if (messages.TryGetValue("Center", out var center))
            PrintCenter(center);
    }

    private void PrintChat(string message)
    {
        message = ProcessMessage(message);

        foreach (var player in Utilities.GetPlayers())
        {
            if (!player.IsValid || player.IsBot)
                continue;

            player.PrintToChat($" {message}");
        }
    }

    private void PrintCenter(string message)
    {
        message = ProcessMessage(message);

        foreach (var player in Utilities.GetPlayers())
        {
            if (!player.IsValid || player.IsBot)
                continue;

            player.PrintToCenter(message);
        }
    }

    private string ProcessMessage(string message)
    {
        var mapName = NativeAPI.GetMapName();

        message = message
            .Replace("{MAP}", mapName)
            .Replace("{TIME}", DateTime.Now.ToString("HH:mm:ss"))
            .Replace("{DATE}", DateTime.Now.ToString("dd.MM.yyyy"))
            .Replace(
                "{SERVERNAME}",
                ConVar.Find("hostname")?.StringValue ?? ""
            )
            .Replace(
                "{IP}",
                ConVar.Find("ip")?.StringValue ?? ""
            )
            .Replace(
                "{PORT}",
                ConVar.Find("hostport")?
                    .GetPrimitiveValue<int>()
                    .ToString() ?? ""
            )
            .Replace("{MAXPLAYERS}", Server.MaxPlayers.ToString())
            .Replace(
                "{PLAYERS}",
                Utilities.GetPlayers()
                    .Count(p =>
                        p.PlayerPawn.Value != null &&
                        p.PlayerPawn.Value.IsValid)
                    .ToString()
            )
            .Replace("\n", "\u2029");

        return message.ReplaceColorTags();
    }

    private AdvertisementConfig LoadConfig()
    {
        var directory = Path.Combine(
            Application.RootDirectory,
            "cfg/MatchZy"
        );

        Directory.CreateDirectory(directory);

        var path = Path.Combine(
            directory,
            "advertisements.json"
        );

        if (!File.Exists(path))
        {
            var config = new AdvertisementConfig();

            File.WriteAllText(
                path,
                JsonSerializer.Serialize(
                    config,
                    new JsonSerializerOptions
                    {
                        WriteIndented = true
                    }
                )
            );

            return config;
        }

        return JsonSerializer.Deserialize<AdvertisementConfig>(
            File.ReadAllText(path),
            new JsonSerializerOptions
            {
                ReadCommentHandling = JsonCommentHandling.Skip
            }
        ) ?? new AdvertisementConfig();
    }
}