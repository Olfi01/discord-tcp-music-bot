using Discord;
using Discord.Audio;
using Discord.Commands;
using Discord.WebSocket;
using DiscordTCPMusicBot.Helpers;
using DiscordTCPMusicBot.Services;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace DiscordTCPMusicBot.Commands
{
    public class BaseCommands : ModuleBase<SocketCommandContext>
    {
        public CommandService CommandService { get; set; }

        [Command("ping"), Summary("Pings the bot to see whether it reacts")]
        public async Task Ping()
        {
            await ReplyAsync("Pong!");
        }

        #region !help
        [Command("Help"), Summary("Prints a help message")]
        public async Task Help()
        {
            char prefix = Constants.Prefix;
            var builder = new EmbedBuilder()
            {
                Color = new Color(114, 137, 218),
                Description = "These are the commands you can use"
            };
            
            foreach (var module in CommandService.Modules)
            {
                string description = null;
                foreach (var cmd in module.Commands)
                {
                    var result = await cmd.CheckPreconditionsAsync(Context);
                    if (result.IsSuccess)
                        description += $"{prefix}{cmd.Aliases.First()} {string.Join(" ", cmd.Parameters.Select(x => $"<{x.Name}>"))}\n";
                }
                
                if (!string.IsNullOrWhiteSpace(description))
                {
                    builder.AddField(x =>
                    {
                        x.Name = module.Name;
                        x.Value = description;
                        x.IsInline = false;
                    });
                }
            }

            await ReplyAsync("", false, builder.Build());
        }

        [Command("help")]
        public async Task HelpAsync(string command)
        {
            var result = CommandService.Search(Context, command);

            if (!result.IsSuccess)
            {
                await ReplyAsync($"Sorry, I couldn't find a command like **{command}**.");
                return;
            }
            
            var builder = new EmbedBuilder()
            {
                Color = new Color(114, 137, 218),
                Description = $"Here are some commands like **{command}**"
            };

            foreach (var match in result.Commands)
            {
                var cmd = match.Command;

                builder.AddField(x =>
                {
                    x.Name = string.Join(", ", cmd.Aliases);
                    x.Value = $"Parameters: {string.Join(", ", cmd.Parameters.Select(p => p.Name))}\n" + 
                              $"Summary: {cmd.Summary}";
                    x.IsInline = false;
                });
            }

            await ReplyAsync("", false, builder.Build());
        }
        #endregion

#if DEBUG
        [Command("test")]
        public async Task Test()
        {
            await ReplyAsync("Nothing to test right now.");
        }
#endif
    }
}
