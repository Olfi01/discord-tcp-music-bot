using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordTCPMusicBot.Services
{
    public class QueueManagerService
    {
        private readonly Dictionary<ulong, QueueService> services = new Dictionary<ulong, QueueService>();
        private readonly ConfigService config;
        private readonly GuildConfigManagerService guildConfigs;

        public QueueManagerService(ConfigService config, GuildConfigManagerService guildConfigs)
        {
            this.config = config;
            this.guildConfigs = guildConfigs;
        }

        public QueueService GetOrCreateService(ulong guildId)
        {
            if (!services.ContainsKey(guildId)) services.Add(guildId, new QueueService(guildId, config, guildConfigs));
            return services[guildId];
        }
    }
}
