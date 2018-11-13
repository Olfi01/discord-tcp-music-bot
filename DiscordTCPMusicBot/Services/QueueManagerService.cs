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

        public QueueService GetOrCreateService(ulong guildId)
        {
            if (!services.ContainsKey(guildId)) services.Add(guildId, new QueueService());
            return services[guildId];
        }
    }
}
