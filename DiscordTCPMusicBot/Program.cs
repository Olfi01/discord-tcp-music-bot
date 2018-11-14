using Discord;
using Discord.Audio;
using Discord.Commands;
using Discord.Net.Providers.WS4Net;
using Discord.WebSocket;
using DiscordTCPMusicBot.Helpers;
using DiscordTCPMusicBot.Music;
using DiscordTCPMusicBot.Queue;
using DiscordTCPMusicBot.Services;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading.Tasks;
using YoutubeSearch;

namespace DiscordTCPMusicBot
{
    class Program
    {
        private readonly DiscordSocketClient _client;

        private readonly IServiceCollection _map = new ServiceCollection();
        private readonly CommandService _commands = new CommandService(new CommandServiceConfig { DefaultRunMode = RunMode.Async });

        private QueueManagerService Queues;
        private AudioClientService AudioClients;
        private CacheService Cache;
        private ConfigService Config;

        private string token;

        static void Main(string[] args)
        {
#if DEBUG
            var logSeverity = LogSeverity.Debug;
#else
            var logSeverity = LogSeverity.Info;
#endif

            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "-s":
                    case "-S":
                    case "--severity":
                    case "--Severity":
                        if (args.Length >= i + 1) continue;
                        logSeverity = GetSeverity(args[i + 1]);
                        i++;
                        continue;
                }
            }
            new Program(logSeverity).MainAsync().GetAwaiter().GetResult();
        }

        private static LogSeverity GetSeverity(string v)
        {
            switch (v.ToLower())
            {
                case "0":
                case "critical":
                    return LogSeverity.Critical;
                case "1":
                case "error":
                    return LogSeverity.Error;
                case "2":
                case "warning":
                    return LogSeverity.Warning;
                case "3":
                case "info":
                    return LogSeverity.Info;
                case "4":
                case "verbose":
                    return LogSeverity.Verbose;
                case "5":
                case "debug":
                    return LogSeverity.Debug;
                default:
                    return LogSeverity.Info;
            }
        }

        private Program(LogSeverity logSeverity)
        {
            _client = new DiscordSocketClient(new DiscordSocketConfig
            {
                LogLevel = logSeverity,
                MessageCacheSize = 50,
                WebSocketProvider = WS4NetProvider.Instance,
            });
        }

        private static Task Logger(LogMessage message)
        {
            var cc = Console.ForegroundColor;
            switch (message.Severity)
            {
                case LogSeverity.Critical:
                case LogSeverity.Error:
                    Console.ForegroundColor = ConsoleColor.Red;
                    break;
                case LogSeverity.Warning:
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    break;
                case LogSeverity.Info:
                    Console.ForegroundColor = ConsoleColor.White;
                    break;
                case LogSeverity.Verbose:
                case LogSeverity.Debug:
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    break;
            }
            Console.WriteLine($"{DateTime.Now,-19} [{message.Severity,8}] {message.Source}: {message.Message}");
            Console.ForegroundColor = cc;

            return Task.CompletedTask;
        }

        private async Task MainAsync()
        {
            _client.Log += Logger;

            var httpAuth = new HttpAuthenticationService(Helper.GetAppDataPath("auth.json"));

            await Init(httpAuth);

            await _client.LoginAsync(TokenType.Bot, token);
            await _client.StartAsync();

            HeyListen(httpAuth);

            // Wait infinitely so your bot actually stays connected.
            await Task.Delay(-1);
        }

        private void HeyListen(HttpAuthenticationService httpAuth)
        {
            using (HttpListener listener = new HttpListener())
            {
                listener.Prefixes.Add(Config.HttpPrefix);
                listener.AuthenticationSchemes = AuthenticationSchemes.Basic;
                listener.Start();
                while (true)
                {
                    HttpListenerContext context = listener.GetContext();
                    HttpListenerBasicIdentity identity = (HttpListenerBasicIdentity)context.User.Identity;
                    if (identity == null || !httpAuth.Authenticate(identity.Name, identity.Password))
                    {
                        HttpListenerResponse authResponse = context.Response;
                        authResponse.AddHeader("WWW-Authenticate", "Basic");
                        authResponse.StatusCode = (int)HttpStatusCode.Unauthorized;
                        using (StreamWriter writer = new StreamWriter(authResponse.OutputStream))
                        {
                            writer.Write("NO!");
                            writer.Flush();
                        }
                        continue;
                    }
                    var userId = Helper.GetUserId(identity.Name);
                    var guildId = Helper.GetGuildId(identity.Name);
                    HttpListenerRequest request = context.Request;
                    string command;
                    using (StreamReader reader = new StreamReader(request.InputStream))
                    {
                        command = reader.ReadToEnd();
                    }
                    HttpListenerResponse response = context.Response;
                    string responseString;
                    try
                    {
                        responseString = HandleHttpCommand(command, userId, guildId);
                    }
                    catch (Exception ex)
                    {
                        responseString = ex.ToString();
                        response.StatusCode = (int)HttpStatusCode.InternalServerError;
                    }
                    using (StreamWriter writer = new StreamWriter(response.OutputStream))
                    {
                        writer.Write(responseString);
                        writer.Flush();
                    }
                }
            }
        }

        private string HandleHttpCommand(string command, ulong userId, ulong guildId)
        {
            string action = command;
            string args = null;
            if (command.Contains(" "))
            {
                action = command.Remove(command.IndexOf(" "));
                args = command.Substring(command.IndexOf(" ") + 1);
            }

            #region Commands
            #region play
            if (action == "play")
            {
                if (string.IsNullOrEmpty(args)) return "You must specify either a query or a URL";

                SocketGuild guild = _client.GetGuild(guildId);
                QueueService queue = Queues.GetOrCreateService(guildId);

                string youtubeLink;
                string title = null;

                // Query, select first video found, works for links too.
                // If param looks like a valid Uri, don't search for title similarities.
                var result = Search(args, 1, Uri.IsWellFormedUriString(args, UriKind.Absolute) ? (Func<VideoInformation, int>)(x => (x.Url == args) ? 0 : 1) : null)[0];
                youtubeLink = result.Url;
                title = result.Title;

                Enqueue(youtubeLink, title, guildId, userId);

                if (IsInVoiceChannel(guild, userId) && !IAmInVoiceChannel(FindVoiceChannel(guild, userId)))
                {
                    JoinAndPlay(queue, FindVoiceChannel(guild, userId)).Wait();
                }
                else if (!queue.IsPlaying)
                {
                    PlayQueue(queue, AudioClients.GetClient(guildId));
                }
                return Enqueued(title);
            }
            #endregion
            #region queue
            if (action == "queue")
            {
                SocketGuild guild = _client.GetGuild(guildId);
                var queue = Queues.GetOrCreateService(guildId);

                var queueList = queue.GetQueue();
                List<string> responseLines = new List<string>();

                for (int i = 0; i < queueList.Length; i++)
                {
                    responseLines.Add($"{i + 1}.: {queueList[i].Title} (added by " +
                        $"{guild.Users.FirstOrDefault(x => x.Id == queueList[i].OriginatorId)?.Username ?? queueList[i].OriginatorId.ToString()})");
                }

                return string.Join("\n", "Current Queue:", string.Join("\n", responseLines));
            }
            #endregion
            #region skip
            if (action == "skip")
            {
                var guild = _client.GetGuild(guildId);
                var user = guild.GetUser(userId);
                var queue = Queues.GetOrCreateService(guild.Id);

                if (!IsInVoiceChannel(guild, userId))
                {
                    return "You are not in any channel!";
                }
                SocketVoiceChannel channel = FindVoiceChannel(guild, userId);
                if (!channel.Users.Any(x => x.Id == _client.CurrentUser.Id))
                {
                    return "You are not in the same channel as me!";
                }

                int requests = queue.RequestSkip(user.Id, channel);

                int usercount = channel.Users.Count(x => !x.IsBot);
                float part = (float)requests / usercount;
                var response = $"Skip requested ({requests} of {usercount}, {(int)Math.Round(part * 100)}%). " +
                    $"{(int)Math.Round(Config.MinSkipQuota * 100)}% needed.";

                if (part > Config.MinSkipQuota)
                {
                    queue.Skip();
                    return $"{response}\nSkipping current song.";
                }

                return response;
            }
            #endregion
            #region search
            if (action == "search")
            {
                SocketGuild guild = _client.GetGuild(guildId);
                QueueService queue = Queues.GetOrCreateService(guildId);

                List<VideoInformation> results = Search(args, Config.MaxSearchResults);

                var lines = new List<string>();
                for (int i = 0; i < results.Count; i++)
                {
                    lines.Add($"{i + 1}. {results[i].Title} ({results[i].Url})");
                }
                string response = string.Join("\n", lines);

                return response;
            }
            #endregion
            #region remove
            if (action == "remove")
            {
                if (!int.TryParse(args, out int index))
                {
                    return "Not a valid index.";
                }
                var queue = Queues.GetOrCreateService(guildId);
                if (queue.TryRemove(index - 1, userId, out string reasonOrTitle))
                {
                    return $"Removed {reasonOrTitle}.";
                }
                else
                {
                    return $"Failed to remove song at index {index}. Reason: {reasonOrTitle}";
                }
            }
            #endregion
            #endregion

            return "Unrecognized command.";
        }

        private IServiceProvider _services;

        private async Task Init(HttpAuthenticationService httpAuth)
        {
            Cache = new CacheService();
            _map.AddSingleton(Cache);
            AudioClients = new AudioClientService();
            _map.AddSingleton(AudioClients);
            var gcmService = new GuildConfigManagerService();
            _map.AddSingleton(gcmService);
            _map.AddSingleton(httpAuth);

            Config = new ConfigService(Helper.GetAppDataPath("config.json"));
            Cleanup(Config.FileCachePath);
            token = Config.BotToken;
            _map.AddSingleton(Config);
            Queues = new QueueManagerService(Config, gcmService);
            _map.AddSingleton(Queues);

            _services = _map.BuildServiceProvider();

            await _commands.AddModulesAsync(Assembly.GetEntryAssembly());

            _client.MessageReceived += HandleCommandAsync;
        }

        private void Cleanup(string fileCachePath)
        {
            foreach (var file in Directory.CreateDirectory(fileCachePath).EnumerateFiles())
            {
                file.Delete();
            }
        }

        private async Task HandleCommandAsync(SocketMessage arg)
        {
            // Bail out if it's a System Message.
            if (!(arg is SocketUserMessage msg)) return;

            // Create a number to track where the prefix ends and the command begins
            int pos = 0;
            if (msg.HasCharPrefix(Constants.Prefix, ref pos))
            {
                var context = new SocketCommandContext(_client, msg);

                await _commands.ExecuteAsync(context, pos, _services);
            }
        }

        #region Helper functions
        #region Voice channels
        private SocketVoiceChannel FindVoiceChannel(SocketGuild guild, ulong userId)
        {
            return guild.VoiceChannels.First(x => x.Users.Any(y => y.Id == userId));
        }

        private bool IsInVoiceChannel(SocketGuild guild, ulong userId)
        {
            return guild.VoiceChannels.Any(x => x.Users.Any(y => y.Id == userId));
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
            return channel.Users.Any(x => x.Id == _client.CurrentUser.Id);
        }
        #endregion
        #region Queue
        private void Enqueue(string youtubeLink, string title, ulong guildId, ulong userId)
        {
            QueueEntry entry = null;
            if (Cache.TryGetCachedFile(youtubeLink, out MusicFile musicFile))
            {
                entry = QueueEntry.FromMusicFile(musicFile, userId);
            }
            else
            {
                entry = new QueueEntry(youtubeLink, userId, title, filePath: Path.Combine(Config.FileCachePath, title.RemovePathForbiddenChars()),
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
