using DiscordTCPMusicBot.Helpers;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordTCPMusicBot.Services
{
    public class GuildConfigService
    {
        private readonly ulong guildId;
        private readonly string configFilePath;
        private readonly GuildConfigServiceObject config;

        public GuildConfigService(ulong guildId)
        {
            this.guildId = guildId;
            configFilePath = Helper.GetAppDataPath($"servers\\{guildId}.json");

            Helper.CreateDirectoryIfNecessary(configFilePath);
            
            if (File.Exists(configFilePath))
            {
                config = JsonConvert.DeserializeObject<GuildConfigServiceObject>(File.ReadAllText(configFilePath));
            }
            else
            {
                config = new GuildConfigServiceObject();
            }

            WriteFile();
        }

        private void WriteFile()
        {
            File.WriteAllText(configFilePath, JsonConvert.SerializeObject(config, Formatting.Indented));
        }

        public float Volume { get => config.Volume; set { config.Volume = value; WriteFile(); } }
    }
}
