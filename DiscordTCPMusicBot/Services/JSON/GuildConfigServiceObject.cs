using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordTCPMusicBot.Services
{
    [JsonObject]
    public class GuildConfigServiceObject
    {
        public GuildConfigServiceObject()
        {
            Volume = 1f;
        }

        [JsonProperty(PropertyName = "volume")]
        public float Volume { get; set; }
    }
}
