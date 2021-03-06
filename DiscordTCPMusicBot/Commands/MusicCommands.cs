﻿using Discord;
using Discord.Audio;
using Discord.Commands;
using Discord.WebSocket;
using DiscordTCPMusicBot.Helpers;
using DiscordTCPMusicBot.Music;
using DiscordTCPMusicBot.Queue;
using DiscordTCPMusicBot.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using YoutubeSearch;

namespace DiscordTCPMusicBot.Commands
{
    public class MusicCommands : ModuleBase<SocketCommandContext>
    {
        public ConfigService Config { get; set; }
        public QueueManagerService Queues { get; set; }
        public CacheService Cache { get; set; }
        public AudioClientService AudioClients { get; set; }
        public GuildConfigManagerService Guilds { get; set; }

        #region Commands
        #region !search
        [Command("search"), Summary("Searches for a song on youtube."), RequireContext(ContextType.Guild)]
        public async Task Search([Remainder, Summary("The search query")] string query)
        {
            SocketGuild guild = Context.Guild;
            QueueService queue = Queues.GetOrCreateService(guild.Id);

            List<VideoInformation> results = Search(query, Config.MaxSearchResults);

            var lines = new List<string>();
            for (int i = 0; i < results.Count; i++)
            {
                lines.Add($"{i + 1}. {results[i].Title}");
            }
            string response = string.Join("\n", lines);

            var sentMessage = await ReplyAsync(response);

            using (var waiter = new AutoResetEvent(false))
            {
                // define title reference for later use
                string title = "";

                Task handler(Cacheable<IUserMessage, ulong> message, IMessageChannel channel, SocketReaction reaction)
                {
                    if (message.Id != sentMessage.Id || reaction.UserId != Context.Message.Author.Id ||
                        (!Constants.Keycaps.Contains(reaction.Emote.Name) && Constants.EmojiX != reaction.Emote.Name))
                        return Task.CompletedTask;
                    if (Constants.EmojiX == reaction.Emote.Name)
                    {
                        title = "Nothing";
                        waiter.Set();
                        return Task.CompletedTask;
                    }
                    for (int i = 0; i < Constants.Keycaps.Length; i++)
                    {
                        if (Constants.Keycaps[i + 1] == reaction.Emote.Name)
                        {
                            Console.WriteLine("i: {0}, Title: {1}", i, results[i].Title);
                            Enqueue(results[i].Url, results[i].Title, Context.Guild.Id);
                            title = results[i].Title;
                            waiter.Set();
                            return Task.CompletedTask;
                        }
                    }
                    return Task.CompletedTask;
                }

                for (int i = 1; i <= Math.Min(results.Count, 10); i++)
                {
                    await sentMessage.AddReactionAsync(new Emoji(Constants.Keycaps[i]));
                }
                await sentMessage.AddReactionAsync(new Emoji(Constants.EmojiX));

                Context.Client.ReactionAdded += handler;
                waiter.WaitOne();
                Context.Client.ReactionAdded -= handler;

                await sentMessage.RemoveAllReactionsAsync();
                await sentMessage.ModifyAsync(msgProp => msgProp.Content = Enqueued(title));

                if (IsInVoiceChannel(guild, Context.Message.Author) && !IAmInVoiceChannel(FindVoiceChannel(guild, Context.Message.Author)))
                {
                    await JoinAndPlay(queue, FindVoiceChannel(guild, Context.Message.Author));
                }
                else if (!queue.IsPlaying)
                {
                    PlayQueue(queue, AudioClients.GetClient(guild.Id));
                }
            }
        }
        #endregion
        #region !play
        [Command("play"), Summary("Plays music from youtube"), RequireContext(ContextType.Guild)]
        public async Task Play([Remainder, Summary("The query or the URL of the youtube video.")] string urlOrQuery)
        {
            SocketGuild guild = Context.Guild;
            QueueService queue = Queues.GetOrCreateService(guild.Id);

            string youtubeLink;
            string title = null;

            // Query, select first video found, works for links too.
            // If param looks like a valid Uri, don't search for title similarities.
            var result = Search(urlOrQuery, 1, Uri.IsWellFormedUriString(urlOrQuery, UriKind.Absolute) ? (Func<VideoInformation, int>)(x => (x.Url == urlOrQuery) ? 0 : 1) : null)[0];
            youtubeLink = result.Url;
            title = result.Title;

            Enqueue(youtubeLink, title, guild.Id);

            await ReplyAsync(Enqueued(title));

            if (IsInVoiceChannel(guild, Context.Message.Author) && !IAmInVoiceChannel(FindVoiceChannel(guild, Context.Message.Author)))
            {
                await JoinAndPlay(queue, FindVoiceChannel(guild, Context.Message.Author));
            }
            else if (!queue.IsPlaying)
            {
                PlayQueue(queue, AudioClients.GetClient(guild.Id));
            }
        }
        #endregion
        #region !remove
        [Command("remove"), Summary("Removes a song from the queue."), RequireContext(ContextType.Guild)]
        public async Task Remove([Summary("The index of the song to remove.")] int index)
        {
            var queue = Queues.GetOrCreateService(Context.Guild.Id);
            if (queue.TryRemove(index - 1, Context.Message.Author, out string reasonOrTitle))
            {
                await ReplyAsync($"Removed {reasonOrTitle}.");
            }
            else
            {
                await ReplyAsync($"Failed to remove song at index {index}. Reason: {reasonOrTitle}");
            }
        }
        #endregion
        #region !queue
        [Command("queue"), Summary("Shows the current queue"), RequireContext(ContextType.Guild)]
        public async Task ShowQueue()
        {
            var queue = Queues.GetOrCreateService(Context.Guild.Id);

            var queueList = queue.GetQueue();
            List<string> responseLines = new List<string>();

            for (int i = 0; i < queueList.Length; i++)
            {
                responseLines.Add($"{i + 1}.: {queueList[i].Title} (added by " +
                    $"{Context.Guild.Users.FirstOrDefault(x => x.Id == queueList[i].OriginatorId)?.Username ?? queueList[i].OriginatorId.ToString()})");
            }

            await ReplyAsync(string.Join("\n", "Current Queue:", string.Join("\n", responseLines)));
        }
        #endregion
        #region !join
        [Command("join"), Summary("Lets the bot join the current voice channel."), RequireContext(ContextType.Guild)]
        public async Task Join()
        {
            var queue = Queues.GetOrCreateService(Context.Guild.Id);

            if (!queue.HasEntries())
            {
                await ReplyAsync("The queue is empty!");
                return;
            }
            SocketGuild guild = Context.Guild;

            if (!IsInVoiceChannel(guild, Context.Message.Author))
            {
                await ReplyAsync("You are not in a channel!");
                return;
            }
            SocketVoiceChannel channel = FindVoiceChannel(guild, Context.Message.Author);

            if (channel.Users.Any(x => x.Id == Context.Client.CurrentUser.Id))
            {
                await ReplyAsync("I'm already in your channel!");
                return;
            }

            await JoinAndPlay(queue, channel);
        }
        #endregion
        #region !leave
        [Command("leave"), Summary("Makes the bot leave the current channel."), RequireContext(ContextType.Guild)]
        public async Task Leave()
        {
            if (!AudioClients.IsInChannelOf(Context.Guild.Id))
            {
                await ReplyAsync("I'm not even in a channel!");
                return;
            }

            await AudioClients.LeaveChannelOn(Context.Guild.Id);
        }
        #endregion
        #region !skip
        [Command("skip"), Summary("Votes to skip the current song."), RequireContext(ContextType.Guild)]
        public async Task Skip()
        {
            var guild = Context.Guild;
            var user = Context.Message.Author;
            var queue = Queues.GetOrCreateService(guild.Id);

            if (!IsInVoiceChannel(Context.Guild, Context.Message.Author))
            {
                await ReplyAsync("You are not in any channel!");
                return;
            }
            SocketVoiceChannel channel = FindVoiceChannel(guild, user);
            if (!channel.Users.Any(x => x.Id == Context.Client.CurrentUser.Id))
            {
                await ReplyAsync("You are not in the same channel as me!");
                return;
            }

            int requests = queue.RequestSkip(user.Id, channel);

            int usercount = channel.Users.Count(x => !x.IsBot);
            float part = (float)requests / usercount;
            await base.ReplyAsync($"Skip requested ({requests} of {usercount}, {(int)Math.Round(part * 100)}%). " +
                $"{(int)Math.Round(Config.MinSkipQuota * 100)}% needed.");

            if (part > Config.MinSkipQuota)
            {
                queue.Skip();
                await ReplyAsync("Skipping current song.");
            }
        }
        #endregion
        #region !volume
        [Command("volume"), Summary("Gets the overall volume for the current server"), RequireContext(ContextType.Guild)]
        public async Task Volume()
        {
            var guild = Context.Guild;
            var guildConfig = Guilds.GetOrCreateService(guild.Id);

            await ReplyAsync($"Current volume: {(int)Math.Round(guildConfig.Volume * 100)}%");
        }

        [Command("volume"), Summary("Sets the overall volume for the current server"), RequireContext(ContextType.Guild)]
        public async Task Volume([Summary("Volume in percent")]int percent)
        {
            var guild = Context.Guild;
            var guildConfig = Guilds.GetOrCreateService(guild.Id);

            guildConfig.Volume = percent / 100f;

            await ReplyAsync($"Volume set to {percent}%. Change will be applied for the next song.");
        }
        #endregion
        #endregion

        #region Helper functions
        #region Voice channels
        private SocketVoiceChannel FindVoiceChannel(SocketGuild guild, SocketUser user)
        {
            return guild.VoiceChannels.First(x => x.Users.Contains(user, new UserComparer()));
        }

        private bool IsInVoiceChannel(SocketGuild guild, SocketUser user)
        {
            return guild.VoiceChannels.Any(x => x.Users.Contains(user, new UserComparer()));
        }

        private async Task<IAudioClient> JoinChannel(SocketVoiceChannel channel)
        {
            if (AudioClients.IsInChannelOf(channel.Guild.Id)) await AudioClients.LeaveChannelOn(channel.Guild.Id);

            return await AudioClients.Join(channel);
        }

        private async Task JoinAndPlay(QueueService queue, SocketVoiceChannel channel)
        {
            IAudioClient client = await JoinChannel(channel);
            PlayQueue(queue, client);
        }

        private bool IAmInVoiceChannel(SocketVoiceChannel channel)
        {
            return channel.Users.Any(x => x.Id == Context.Client.CurrentUser.Id);
        }
        #endregion
        #region Queue
        private void Enqueue(string youtubeLink, string title, ulong guildId)
        {
            QueueEntry entry = null;
            if (Cache.TryGetCachedFile(youtubeLink, out MusicFile musicFile))
            {
                entry = QueueEntry.FromMusicFile(musicFile, Context.Message.Author.Id);
            }
            else
            {
                entry = new QueueEntry(youtubeLink, Context.Message.Author.Id, title, filePath: Path.Combine(Config.FileCachePath, title.RemovePathForbiddenChars()),
                    alreadyDownloaded: false, onDownloadFinished: x =>
                    {
                        Cache.AddToCache(youtubeLink, entry, Config.CachePersistTime);
                    });
            }
            Queues.GetOrCreateService(guildId).Add(entry);
        }

        private static string Enqueued(string title)
        {
            return $"Enqueued song: {title}";
        }

        private void PlayQueue(QueueService queue, IAudioClient audioClient)
        {
            // if the task ends with false, don't continue, the queue is empty.
            queue.Play(audioClient).ContinueWith(x =>
            {
                if (x.Result) PlayQueue(queue, audioClient);
                else if (!Config.RemainInChannel) AudioClients.Stop(audioClient).Wait();
            });
        }
        #endregion
        #region Search
        private List<VideoInformation> Search(string query, int maxResults, Func<VideoInformation, int> comparer = null)
        {
            var items = new VideoSearch();

            // ordering by similarity to title by default, I should look into how that algorithm works sometime, seems interesting
            var results = items.SearchQuery(query, 1).OrderBy(comparer ?? (x => LevenshteinDistance.Compute(x.Title, query))).ToList();

            if (results.Count > maxResults)
                results.RemoveRange(maxResults, results.Count - maxResults);
            return results;
        }
        #endregion
        #endregion
    }
}
