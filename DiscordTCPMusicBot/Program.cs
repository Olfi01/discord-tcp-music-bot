using Discord;
using Discord.Commands;
using Discord.Net.Providers.WS4Net;
using Discord.WebSocket;
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
            new Program().MainAsync().GetAwaiter().GetResult();
        }

        private Program()
        {
            _client = new DiscordSocketClient(new DiscordSocketConfig
            {
#if DEBUG
                LogLevel = LogSeverity.Debug,
#else
                LogLevel = LogSeverity.Info,
#endif
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
            _map.AddSingleton(new QueueManagerService());
            _map.AddSingleton(new AudioClientService());

            ConfigService config = new ConfigService("config.json");
            Cleanup(config.FileCachePath);
            token = config.BotToken;
            _map.AddSingleton(config);

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
            if (msg.HasCharPrefix('!', ref pos))
            {
                var context = new SocketCommandContext(_client, msg);

                await _commands.ExecuteAsync(context, pos, _services);
            }
        }
    }

}
