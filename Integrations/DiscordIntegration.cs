using System.Text.Json;
using CounterStrikeSharp.API.Modules.Cvars;
using MatchZy.Util;

namespace MatchZy.Integrations
{
    public static class DiscordIntegration
    {
        private static readonly HttpClient _httpClient = new();

        /// <summary>
        /// Sends an admin message via a Discord webhook.
        /// </summary>
        /// <param name="player">The player who triggered the message.</param>
        /// <param name="message">The message to send.</param>
        /// <param name="webhookUrl">The URL of the Discord webhook.</param>
        /// <returns>True if the message was sent successfully, false otherwise.</returns>
        public async static Task<bool> SendAdminMessage(
            string player,
            string message,
            string webhookUrl,
            string discordAdminGroupId
        )
        {
            string? ip = ConVar.Find("ip")?.StringValue;
            if (string.IsNullOrEmpty(ip))
            {
                return false;
            }
            int? port = ConVar.Find("hostport")?.GetPrimitiveValue<int>();
            if (port == null)
            {
                return false;
            }
            if (string.IsNullOrEmpty(discordAdminGroupId))
            {
                return false;
            }

            var toSend = new
            {
                content = $"<@&{discordAdminGroupId}> {player}: {message}\n\n[Connect to the server](steam://connect/{ip}:{port})",
            };
            var response = await _httpClient
                .PostAsync(
                    webhookUrl,
                    new StringContent(
                        JsonSerializer.Serialize(toSend),
                        System.Text.Encoding.UTF8,
                        "application/json"
                    )
                );

            if (response.IsSuccessStatusCode)
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Sends an embed via a Discord webhook.
        /// </summary>
        /// <param name="title">The title of the embed.</param>
        /// <param name="description">The description of the embed.</param>
        /// <param name="color">The color of the embed.</param>
        /// <param name="webhookUrl">The URL of the Discord webhook.</param>
        /// <returns>True if the embed was sent successfully, false otherwise.</returns>
        public async static Task<bool> SendEmbed(
            string title,
            string description,
            HexColor color,
            string webhookUrl
        )
        {
            string? ip = ConVar.Find("ip")?.StringValue;
            int? port = ConVar.Find("hostport")?.GetPrimitiveValue<int>();

            string footerText = "MatchZy Server";
            if (!string.IsNullOrEmpty(ip) && port != null)
            {
                footerText += $" | connect {ip}:{port}";
            }

            var message = new
            {
                embeds = new[]
                {
                    new
                    {
                        title,
                        description,
                        color = color.Hex,
                        footer = new { text = footerText },
                    },
                },
            };
            var response = await _httpClient
                .PostAsync(
                    webhookUrl,
                    new StringContent(
                        JsonSerializer.Serialize(message),
                        System.Text.Encoding.UTF8,
                        "application/json"
                    )
                );

            if (response.IsSuccessStatusCode)
            {
                return true;
            }

            return false;
        }
    }
}
