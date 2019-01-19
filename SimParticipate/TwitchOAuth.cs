using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace SimParticipate
{
    class TwitchOAuth
    {
        private Log log;
        private Config cfg;
        private static readonly HttpClient client = new HttpClient();
        private string id;
        private string secret;

        private string _token;
        public string Token
        {
            get { return _token; }
        }

        public TwitchOAuth()
        {
            // register application here https://glass.twitch.tv/console/apps/create
            // https://dev.twitch.tv/docs/authentication/#registration

            // todo: figure out how to load when xml isn't available
            cfg = new Config("twitch_auth.xml", new List<Type> { typeof(DateTimeOffset) });

            var expired = false;
            try
            {
                var expiresTime = cfg.Get<DateTimeOffset>("expires");
                expired = DateTime.UtcNow > expiresTime.UtcDateTime;
            }
            catch (KeyNotFoundException)
            {
                expired = true;
            }

            if (expired)
            {
                // get new key
                // todo: figure out how to load when xml isn't available
                id = cfg.Get<string>("id");
                secret = cfg.Get<string>("secret");
                Task.Run(async () => await GetNewToken());
            }
            else
            {
                // current token is valid
                _token = cfg.Get<string>("token");
            }
        }

        // actually just go here https://dev.twitch.tv/docs/irc/
        // which suggests this https://twitchapps.com/tmi/
        private async Task<string> GetNewToken()
        {
            // once getting client id and secret, make a POST request
            // https://dev.twitch.tv/docs/authentication/getting-tokens-oauth/#oauth-client-credentials-flow

            // POST request must include space-separated list of scopes (e.g. "chat:read chat:edit whispers:read whispers:edit")
            // https://dev.twitch.tv/docs/authentication/#scopes

            // try HttpClient from System.Net.Http for POST https://stackoverflow.com/a/4015346, https://stackoverflow.com/a/27737601, https://stackoverflow.com/a/23586477

            var scopes = "channel:moderate+chat:edit+chat:read+whispers:read+whispers:edit";
            var endpoint = $"https://id.twitch.tv/oauth2/token?client_id={id}&client_secret={secret}&grant_type=client_credentials&scope={scopes}";

            var response = await client.PostAsync(endpoint, new StringContent(""));
            var responseString = await response.Content.ReadAsStringAsync();
            var responseJson = JObject.Parse(responseString);
            var expires = response.Headers.Date + TimeSpan.FromSeconds(responseJson.Value<int>("expires_in"));
            var token = responseJson.Value<string>("access_token");

            cfg.Set("expires", expires);
            cfg.Set("token", token);
            cfg.Save();

            _token = token;

            return token;

            // the POST request response should be the access token in JSON format
        }
    }
}
