using DiscordTCPMusicBot.Music;
using System;
using System.IO;
using System.Runtime.Caching;
using System.Threading.Tasks;

namespace DiscordTCPMusicBot.Services
{
    public class CacheService
    {
        private readonly MemoryCache cache;

        public CacheService()
        {
            cache = new MemoryCache("QueueCache");
        }

        public bool TryGetCachedFile(string youtubeUrl, out MusicFile musicFile)
        {
            if (!cache.Contains(youtubeUrl))
            {
                musicFile = null;
                return false;
            }
            musicFile = (MusicFile)cache.Get(youtubeUrl);
            return true;
        }

        public void AddToCache(string youtubeUrl, MusicFile musicFile, TimeSpan cachePersistTime)
        {
            cache.Add(new CacheItem(youtubeUrl, musicFile), new CacheItemPolicy() { AbsoluteExpiration = DateTime.Now + cachePersistTime });
            cache.CreateCacheEntryChangeMonitor(new string[] { youtubeUrl }).NotifyOnChanged(state => { if (!cache.Contains(youtubeUrl)) ScheduleDelete(musicFile.FilePath); });
        }

        private void ScheduleDelete(string filePath)
        {
            Task.Run(() =>
            {
                bool again = true;
                while (again)
                {
                    again = false;
                    try { File.Delete(filePath); } catch (IOException) { again = true; }
                }
            });
        }
    }
}
