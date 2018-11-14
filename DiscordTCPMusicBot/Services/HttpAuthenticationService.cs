using DiscordTCPMusicBot.Helpers;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;

namespace DiscordTCPMusicBot.Services
{
    public class HttpAuthenticationService
    {
        private readonly string authFilePath;
        private readonly Dictionary<string, string> auth = new Dictionary<string, string>();

        public HttpAuthenticationService(string authFilePath)
        {
            this.authFilePath = authFilePath;

            Helper.CreateDirectoryIfNecessary(authFilePath);
            if (File.Exists(authFilePath))
            {
                auth = JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(authFilePath));
            }

            WriteFile();
        }

        private void WriteFile()
        {
            File.WriteAllText(authFilePath, JsonConvert.SerializeObject(auth));
        }

        private void Add(string username, string token)
        {
            auth.Add(username, token);

            WriteFile();
        }

        public bool Authenticate(string username, string token)
        {
            return auth.ContainsKey(username) && auth[username] == token;
        }

        public string CreateOrGetToken(string username)
        {
            if (auth.ContainsKey(username)) return auth[username];
            Guid token = Guid.NewGuid();
            Add(username, token.ToString());
            return token.ToString();
        }

        public bool Invalidate(string username)
        {
            if (!auth.ContainsKey(username)) return false;
            auth.Remove(username);
            return true;
        }
    }
}
