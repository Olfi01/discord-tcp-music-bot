using Newtonsoft.Json;
using System;
using System.IO;

namespace DiscordTCPMusicBot.Services
{
    public class ConfigService
    {
        private readonly string filePath;
        private readonly ConfigServiceObject config;

        public ConfigService(string filePath)
        {
            this.filePath = filePath;

            if (!File.Exists(filePath))
            {
                config = new ConfigServiceObject();
            }
            else
            {
                config = JsonConvert.DeserializeObject<ConfigServiceObject>(File.ReadAllText(filePath));
            }

            WriteFile();
        }

        private void WriteFile()
        {
            File.WriteAllText(filePath, JsonConvert.SerializeObject(config, Formatting.Indented));
        }

        public int MaxSearchResults { get => config.MaxSearchResults; set { config.MaxSearchResults = value; WriteFile(); } }
        public TimeSpan CachePersistTime { get => config.CachePersistTime; set { config.CachePersistTime = value; WriteFile(); } }
        public string FileCachePath { get => config.FileCachePath; set { config.FileCachePath = value; WriteFile(); } }
        public TimeSpan SongDelay { get => config.SongDelay; set { config.SongDelay = value; WriteFile(); } }
        public string BotToken { get => File.ReadAllText("token.txt"); }
        public float MinSkipQuota { get => config.MinSkipQuota; set { config.MinSkipQuota = value; WriteFile(); } }
    }
}
