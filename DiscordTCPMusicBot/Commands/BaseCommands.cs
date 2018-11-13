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

        [Command("test")]
        public async Task Test()
        {
            SocketVoiceChannel channel = Context.Guild.VoiceChannels.First(x => x.Users.Contains(Context.Message.Author, new UserComparer()));
            var audioClient = await AudioClients.Join(channel);
            var path = "C:\\Users\\flmeyer\\Downloads\\Carly Rae Jepsen - Call Me Maybe.mp3";
            var ffmpegInfo = new ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = $"-i \"{path}\" -ac 2 -f s16le -ar 48000 pipe:1",
                UseShellExecute = false,
                RedirectStandardOutput = true,
            };
            var args = ffmpegInfo.Arguments;
            var ffmpeg = Process.Start(ffmpegInfo);
            var output = ffmpeg.StandardOutput.BaseStream;
            var discord = audioClient.CreatePCMStream(AudioApplication.Mixed, bitrate: 1920);
            await output.CopyToAsync(discord);
            await discord.FlushAsync();
        }
    }
}
