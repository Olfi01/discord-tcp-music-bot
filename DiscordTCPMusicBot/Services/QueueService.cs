using Discord.Audio;
using Discord.WebSocket;
using DiscordTCPMusicBot.Helpers;
using DiscordTCPMusicBot.Queue;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DiscordTCPMusicBot.Services
{
    public class QueueService
    {
        public ConfigService Config { get; set; }
        public GuildConfigManagerService GuildConfigs { get; set; }
        public GuildConfigService Guild { get; set; }

        private readonly List<List<QueueEntry>> queues = new List<List<QueueEntry>>();
        // to keep track of round robin
        private List<QueueEntry> currentList = null;
        private CancellationTokenSource cts;
        private readonly List<ulong> skipRequests = new List<ulong>();
        private QueueEntry nowPlaying;

        public QueueService(ulong guildId, ConfigService config, GuildConfigManagerService guildConfigs)
        {
            Config = config;
            GuildConfigs = guildConfigs;
            Guild = GuildConfigs.GetOrCreateService(guildId);
        }

        public void Add(QueueEntry entry)
        {
            lock (queues)
            {
                ClearQueues();

                var queue = queues.FirstOrDefault(x => x.First().OriginatorId == entry.OriginatorId);
                // if theres no queue for the originator, create one
                if (queue == null)
                {
                    queue = new List<QueueEntry>();
                    queues.Add(queue);
                }
                // either way, enqueue the new entry
                queue.Add(entry);

                // if there was no current list, make this the current queue
                if (currentList == null) currentList = queue;
            }
        }

        public QueueEntry Next()
        {
            lock (queues)
            {
                return Next(queues, ref currentList);
            }
        }

        public QueueEntry Next(List<List<QueueEntry>> queues, ref List<QueueEntry> currentList)
        {
            // if theres nothing in the queue at all, return null
            if (currentList == null && queues.Count < 1) return null;
            // if theres no current list, just take the first one
            if (currentList == null) currentList = queues.First();
            // sort out all empty lists
            while (currentList.Count < 1)
            {
                var listToRemove = currentList;
                GoToNextList(queues, ref currentList);
                queues.Remove(listToRemove);
            }
            // if it still found nothing, the queue is empty.
            if (currentList == null) return null;

            // now we'll take the first entry from that list away
            QueueEntry nextEntry = currentList.First();
            currentList.Remove(nextEntry);

            // save reference to currentList so we can delete it if necessary
            var oldList = currentList;
            // make a round robin step
            GoToNextList(queues, ref currentList);
            // remove the list if it was emptied by this
            if (oldList.Count < 1) queues.Remove(oldList);

            return nextEntry;
        }

        private void GoToNextList(List<List<QueueEntry>> queues, ref List<QueueEntry> currentList)
        {
            // if there is no next list, the next list is set to null
            if (queues.Count < 2) currentList = null;
            else
            {
                // get the next list. If current is the last, start over.
                int nextIndex = queues.IndexOf(currentList) + 1;
                if (nextIndex >= queues.Count) nextIndex = 0;
                currentList = queues[nextIndex];
            }
        }

        public QueueEntry[] GetQueue()
        {
            int offset = nowPlaying == null ? 0 : 1;
            // prepare an array for the results and list reference for the copies
            QueueEntry[] result = new QueueEntry[queues.Select(x => x.Count).Sum() + offset];
            List<List<QueueEntry>> queuesCopy;

            lock (queues)
            {
                ClearQueues();

                // copy the queues list, so we don't modify it
                queuesCopy = queues.Select(x => x.Copy()).Copy();
            }
            // now find the copied currentList
            var listToRead = queuesCopy.Find(x => x.First().OriginatorId == currentList.First().OriginatorId);

            for (int i = offset; i < result.Length; i++)
            {
                // always get the next entry using round robin
                result[i] = Next(queuesCopy, ref listToRead);
            }

            if (offset == 1) result[0] = nowPlaying;

            return result;
        }

        private void ClearQueues()
        {
            // remove all empty Lists
            queues.RemoveAll(x => x.Count < 1);
            if (currentList == null || currentList.Count < 1) currentList = queues.FirstOrDefault();
        }

        /// <summary>
        /// Tries to remove a song from the queue, returns true on success
        /// </summary>
        /// <param name="index">The index of the song to remove</param>
        /// <param name="reason">Reason why the request failed, or title of the removed song</param>
        /// <returns>true on success</returns>
        public bool TryRemove(int index, SocketUser user, out string reasonOrTitle)
        {
            if (nowPlaying != null) index++;
            var queue = GetQueue();
            if (index >= queue.Length)
            {
                reasonOrTitle = "No such index on the queue.";
                return false;
            }
            if (queue[index].OriginatorId != user.Id)
            {
                reasonOrTitle = "You haven't added this song, so you cannot remove it.";
                return false;
            }
            reasonOrTitle = queue[index].Title;
            Remove(queue[index]);
            return true;
        }

        private void Remove(QueueEntry queueEntry)
        {
            lock (queues)
            {
                queues.Find(x => x.Exists(y => y.Guid == queueEntry.Guid)).RemoveAll(y => y.Guid == queueEntry.Guid);
                ClearQueues();
            }
        }

        public bool HasEntries()
        {
            ClearQueues();

            return queues.Count > 0;
        }

        public async Task<bool> Play(IAudioClient audioClient)
        {
            lock (queues)
            {
                if (!HasEntries()) return false;
            }

            var next = Next();
            cts = new CancellationTokenSource();
            return await Play(next, audioClient, cts.Token);
        }

        public async Task<bool> Play(QueueEntry queueEntry, IAudioClient audioClient, CancellationToken ct)
        {
            skipRequests.Clear();
            nowPlaying = queueEntry;
            while (!queueEntry.IsDownloaded) { Thread.Sleep(1000); }
            var ffmpeg = CreateStream(queueEntry.FilePath);
            var output = ffmpeg.StandardOutput.BaseStream;
            var discord = audioClient.CreatePCMStream(AudioApplication.Mixed/*, bitrate: 1920*/);
            try
            {
                await output.CopyToAsync(discord, 81920, ct);
                await discord.FlushAsync(ct);
            }
            catch (OperationCanceledException)
            {
                discord.Clear();
            }
            var cont = false;
            lock (queues)
            {
                if (HasEntries()) cont = true;
            }
            if (cont)
            {
                await Task.Delay(Config.SongDelay);
                return true;
            }
            nowPlaying = null;
            return false;
        }

        /// <summary>
        /// Requests to skip the current song. Returns the number of (valid) skip requests for the song.
        /// </summary>
        /// <param name="userId">The user who requests a skip</param>
        /// <param name="channel">The voice channel the bot and user are in</param>
        /// <returns>the number of (valid) skip requests for the song</returns>
        public int RequestSkip(ulong userId, SocketVoiceChannel channel)
        {
            if (!skipRequests.Contains(userId)) skipRequests.Add(userId);
            return skipRequests.Count(x => channel.Users.Any(y => y.Id == x));
        }

        public void Skip()
        {
            skipRequests.Clear();
            cts.Cancel();
        }

        private Process CreateStream(string path)
        {
            var ffmpeg = new ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = $"-i \"{path}\" -filter:a \"volume={Guild.Volume}\" -ac 2 -f s16le -ar 48000 pipe:1",
                UseShellExecute = false,
                RedirectStandardOutput = true,
            };
            return Process.Start(ffmpeg);
        }
    }
}
