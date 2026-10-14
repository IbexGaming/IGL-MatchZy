using System.Text.Json;
using System.Text.Json.Serialization;
using MatchZy.Util;

namespace MatchZy.Integrations
{
    public static class DiscordIntegration
    {
        private static readonly HttpClient _httpClient = new();
        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };

        /// <summary>
        /// Sends an admin message via a Discord webhook.
        /// </summary>
        /// <param name="player">The player who triggered the message.</param>
        /// <param name="message">The message to send.</param>
        /// <param name="webhookUrl">The URL of the Discord webhook.</param>
        /// <param name="discordAdminGroupId">The ID of the Discord role to mention.</param>
        /// <param name="ip">The public IP of the server, used for the connect link.</param>
        /// <param name="port">The port of the server, used for the connect link.</param>
        /// <returns>True if the message was sent successfully, false otherwise.</returns>
        public static async Task<bool> SendAdminMessage(
            string player,
            string message,
            string webhookUrl,
            string discordAdminGroupId,
            string ip,
            int? port
        )
        {
            if (string.IsNullOrEmpty(webhookUrl))
            {
                return false;
            }
            if (string.IsNullOrEmpty(discordAdminGroupId))
            {
                return false;
            }

            string content = $"<@&{discordAdminGroupId}> {player}: {message}";
            if (!string.IsNullOrEmpty(ip) && port != null)
            {
                content += $"\n\n[Connect to the server](steam://connect/{ip}:{port})";
            }

            var toSend = new { content };
            try
            {
                var response = await _httpClient.PostAsync(
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

                Console.WriteLine(
                    "[DiscordIntegration] Non-200 Error Code returned while sending Admin Message: "
                        + response.StatusCode
                );
                Console.WriteLine(
                    "[DiscordIntegration] Response Content: "
                        + await response.Content.ReadAsStringAsync()
                );

                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine("[DiscordIntegration] Exception: " + ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Sends an embed via a Discord webhook.
        /// </summary>
        /// <param name="title">The title of the embed.</param>
        /// <param name="description">The description of the embed.</param>
        /// <param name="color">The color of the embed.</param>
        /// <param name="webhookUrl">The URL of the Discord webhook.</param>
        /// <param name="ip">The public IP of the server, shown in the footer.</param>
        /// <param name="port">The port of the server, shown in the footer.</param>
        /// <param name="mentionRoleId">If set, the Discord role with this ID is mentioned.</param>
        /// <returns>True if the embed was sent successfully, false otherwise.</returns>
        public static async Task<bool> SendEmbed(
            string title,
            string description,
            HexColor color,
            string webhookUrl,
            string ip,
            int? port,
            string? mentionRoleId = null
        )
        {
            if (string.IsNullOrEmpty(webhookUrl))
            {
                return false;
            }

            string footerText = "MatchZy Server";
            if (!string.IsNullOrEmpty(ip) && port != null)
            {
                footerText += $" | connect {ip}:{port}";
            }

            var message = new
            {
                content = string.IsNullOrEmpty(mentionRoleId) ? null : $"<@&{mentionRoleId}>",
                embeds = new[]
                {
                    new
                    {
                        title,
                        description,
                        color = color.Value,
                        footer = new { text = footerText },
                    },
                },
            };
            try
            {
                var response = await _httpClient.PostAsync(
                    webhookUrl,
                    new StringContent(
                        JsonSerializer.Serialize(message, _jsonOptions),
                        System.Text.Encoding.UTF8,
                        "application/json"
                    )
                );

                if (response.IsSuccessStatusCode)
                {
                    return true;
                }

                Console.WriteLine(
                    "[DiscordIntegration] Non-200 Error Code returned while sending Embed: "
                        + response.StatusCode
                );
                Console.WriteLine(
                    "[DiscordIntegration] Response Content: "
                        + await response.Content.ReadAsStringAsync()
                );

                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine("[DiscordIntegration] Exception: " + ex.Message);
                return false;
            }
        }
    }
}
