using Discord.Commands;
using DiscordTCPMusicBot.Helpers;
using DiscordTCPMusicBot.Services;
using System.Threading.Tasks;

namespace DiscordTCPMusicBot.Commands
{
    public class TcpCommands : ModuleBase<SocketCommandContext>
    {
        public HttpAuthenticationService Auth { get; set; }

        [Command("token"), Summary("Requests a token and username for http authorization"), RequireContext(ContextType.Guild)]
        public async Task Token()
        {
            var username = Helper.CreateHttpUsername(Context.Message.Author.Id, Context.Guild.Id);
            var token = Auth.CreateOrGetToken(username);

            var dm = await Context.Message.Author.GetOrCreateDMChannelAsync();
            await dm.SendMessageAsync($"Your username for {Context.Guild.Name}: {username}\nYour Token: {token}\nDO NOT GIVE THIS TOKEN TO ANYONE!");

            await ReplyAsync("I have sent you your token in DM.");
        }

        [Command("invalidate"), Summary("Invalidates your token for http authorization"), RequireContext(ContextType.Guild)]
        public async Task Invalidate()
        {
            var username = Helper.CreateHttpUsername(Context.Message.Author.Id, Context.Guild.Id);
            if (Auth.Invalidate(username))
            {
                await ReplyAsync("Your token was successfully invalidated!");
            }
            else
            {
                await ReplyAsync("You didn't have any token for this guild.");
            }
        }
    }
}
