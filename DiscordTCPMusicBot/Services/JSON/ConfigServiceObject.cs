﻿using DiscordTCPMusicBot.Helpers;
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
            FileCachePath = Helper.GetAppDataPath("temp\\");
            SongDelay = TimeSpan.FromSeconds(2);
            MinSkipQuota = 0.5f;
            HttpPrefix = "http://localhost:420/";
            RemainInChannel = false;
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

        [JsonProperty(PropertyName = "http_prefix")]
        public string HttpPrefix { get; set; }

        [JsonProperty(PropertyName = "remain_in_channel")]
        public bool RemainInChannel { get; set; }
    }
}
