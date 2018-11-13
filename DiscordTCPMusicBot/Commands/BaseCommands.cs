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
        public AudioClientService AudioClients { get; set; }

        [Command("ping")]
        public async Task Ping()
        {
            await ReplyAsync("Pong!");
        }

#if DEBUG
        [Command("test")]
        public async Task Test()
        {
            await ReplyAsync("Nothing to test right now.");
        }
#endif
    }
}
