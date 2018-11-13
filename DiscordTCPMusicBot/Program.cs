using Discord;
using Discord.Commands;
using Discord.Net.Providers.WS4Net;
using Discord.WebSocket;
using DiscordTCPMusicBot.Commands;
using DiscordTCPMusicBot.Helpers;
using DiscordTCPMusicBot.Services;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.IO;
using System.Net;
using System.Reflection;
using System.Threading.Tasks;

namespace DiscordTCPMusicBot
{
    class Program
    {
        private readonly DiscordSocketClient _client;

        private readonly IServiceCollection _map = new ServiceCollection();
        private readonly CommandService _commands = new CommandService(new CommandServiceConfig { DefaultRunMode = RunMode.Async });

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

            await Init();

            await _client.LoginAsync(TokenType.Bot, token);
            await _client.StartAsync();

            HeyListen();

            // Wait infinitely so your bot actually stays connected.
            await Task.Delay(-1);
        }

        private void HeyListen()
        {
            using (HttpListener listener = new HttpListener())
            {
                listener.Prefixes.Add("http://localhost:420/");
                listener.Start();
                while (true)
                {
                    HttpListenerContext context = listener.GetContext();
                    HttpListenerRequest request = context.Request;
                    string command;
                    using (StreamReader reader = new StreamReader(request.InputStream))
                    {
                        command = reader.ReadToEnd();
                    }
                    HttpListenerResponse response = context.Response;
                    string responseString = HandleHttpCommand(command);
                    using (StreamWriter writer = new StreamWriter(response.OutputStream))
                    {
                        writer.Write(responseString);
                        writer.Flush();
                    }
                }
            }
        }

        private string HandleHttpCommand(string command)
        {
            throw new NotImplementedException();
        }

        private IServiceProvider _services;

        private async Task Init()
        {
            _map.AddSingleton(new CacheService());
            _map.AddSingleton(new AudioClientService());
            var gcmService = new GuildConfigManagerService();
            _map.AddSingleton(gcmService);

            ConfigService config = new ConfigService(Helper.GetAppDataPath("config.json"));
            Cleanup(config.FileCachePath);
            token = config.BotToken;
            _map.AddSingleton(config);
            _map.AddSingleton(new QueueManagerService(config, gcmService));

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
    }

}
