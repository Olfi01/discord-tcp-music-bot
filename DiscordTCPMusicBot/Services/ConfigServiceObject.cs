using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordTCPMusicBot.Services
{
    [JsonObject(MemberSerialization.OptIn)]
    public class ConfigServiceObject
    {
        public ConfigServiceObject()
        {
            MaxSearchResults = 4;
            CachePersistTime = TimeSpan.FromDays(1);
            FileCachePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Olfi01\\DCTCP\\temp\\");
            SongDelay = TimeSpan.FromSeconds(2);
            MinSkipQuota = 0.5f;
        }

        [JsonProperty(PropertyName = "max_search_results")]
        public int MaxSearchResults { get; set; }

        [JsonProperty(PropertyName = "cache_persist_time_minutes")]
        private long CachePersistTimeMinutes;
        public TimeSpan CachePersistTime { get => TimeSpan.FromMinutes(CachePersistTimeMinutes); set => CachePersistTimeMinutes = (long)Math.Floor(value.TotalMinutes); }

        [JsonProperty(PropertyName = "file_cache_path")]
        public string FileCachePath { get; set; }

        [JsonProperty(PropertyName = "song_delay_seconds")]
        private int SongDelaySeconds;
        public TimeSpan SongDelay { get => TimeSpan.FromSeconds(SongDelaySeconds); set => SongDelaySeconds = (int)Math.Floor(value.TotalSeconds); }

        [JsonProperty(PropertyName = "min_skip_quota")]
        public float MinSkipQuota { get; set; }
    }
}
