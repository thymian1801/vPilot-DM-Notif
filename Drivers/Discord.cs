using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace vPilot_Pushover.Drivers {
    internal class Discord : INotifier {

        // Init
        private static readonly HttpClient client = new HttpClient();
        private const String apiBase = "https://discord.com/api/v10";
        private String settingDiscordBotToken = null;
        private String settingDiscordUserId = null;

        // Cached DM channel id, resolved on first send
        private String dmChannelId = null;

        /*
         *
         * Initilise the driver
         *
        */
        public void init( NotifierConfig config ) {
            this.settingDiscordBotToken = config.settingDiscordBotToken;
            this.settingDiscordUserId = config.settingDiscordUserId;
        }

        /*
         *
         * Validate the configuration
         *
        */
        public Boolean hasValidConfig() {
            if (this.settingDiscordBotToken == null || this.settingDiscordUserId == null) {
                return false;
            }
            return true;
        }

        /*
         *
         * Send Discord message via direct message to the configured user
         *
        */
        public async void sendMessage( String text, String title = "", int priority = 0 ) {

            // Resolve the DM channel for the user if we haven't already
            if (dmChannelId == null) {
                dmChannelId = await openDmChannel();
                if (dmChannelId == null) {
                    return;
                }
            }

            // Construct the message. Use bold title (Discord markdown) when present
            string discordMessage = String.IsNullOrEmpty(title) ? text : $"**{title}**\n{text}";

            string json = $"{{\"content\":{JsonEncode(discordMessage)}}}";
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var request = new HttpRequestMessage(HttpMethod.Post, $"{apiBase}/channels/{dmChannelId}/messages");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bot", settingDiscordBotToken);
            request.Content = content;

            var response = await client.SendAsync(request);
            var responseString = await response.Content.ReadAsStringAsync();
        }

        /*
         *
         * Open (or fetch) the DM channel with the configured user and return its id
         *
        */
        private async Task<String> openDmChannel() {
            string json = $"{{\"recipient_id\":\"{settingDiscordUserId}\"}}";
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var request = new HttpRequestMessage(HttpMethod.Post, $"{apiBase}/users/@me/channels");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bot", settingDiscordBotToken);
            request.Content = content;

            var response = await client.SendAsync(request);
            var responseString = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode) {
                return null;
            }

            // Extract the "id" field from the returned channel object without a JSON dependency
            return ExtractJsonString(responseString, "id");
        }

        /*
         *
         * Minimal JSON string escaping for the message content value (includes surrounding quotes)
         *
        */
        private static String JsonEncode( String value ) {
            var sb = new StringBuilder();
            sb.Append('"');
            foreach (char c in value) {
                switch (c) {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20) {
                            sb.Append("\\u");
                            sb.Append(((int)c).ToString("x4"));
                        } else {
                            sb.Append(c);
                        }
                        break;
                }
            }
            sb.Append('"');
            return sb.ToString();
        }

        /*
         *
         * Extract the value of a top-level string property from a JSON object string
         *
        */
        private static String ExtractJsonString( String json, String key ) {
            string marker = $"\"{key}\"";
            int keyIndex = json.IndexOf(marker, StringComparison.Ordinal);
            if (keyIndex < 0) return null;

            int colon = json.IndexOf(':', keyIndex + marker.Length);
            if (colon < 0) return null;

            int firstQuote = json.IndexOf('"', colon + 1);
            if (firstQuote < 0) return null;

            int secondQuote = json.IndexOf('"', firstQuote + 1);
            if (secondQuote < 0) return null;

            return json.Substring(firstQuote + 1, secondQuote - firstQuote - 1);
        }

    }
}
