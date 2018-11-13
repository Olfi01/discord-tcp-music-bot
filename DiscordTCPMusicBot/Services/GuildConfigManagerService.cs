using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordTCPMusicBot.Services
{
    public class GuildConfigManagerService
    {
        private readonly Dictionary<ulong, GuildConfigService> services = new Dictionary<ulong, GuildConfigService>(); 

        public GuildConfigService GetOrCreateService(ulong guildId)
        {
            if (!services.ContainsKey(guildId)) services.Add(guildId, new GuildConfigService(guildId));
            return services[guildId];
        }
    }
}
