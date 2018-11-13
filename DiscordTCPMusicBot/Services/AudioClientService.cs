using Discord;
using Discord.Audio;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordTCPMusicBot.Services
{
    public class AudioClientService
    {
        private readonly Dictionary<ulong, IAudioClient> clients = new Dictionary<ulong, IAudioClient>();

        public bool IsInChannelOf(ulong guildId)
        {
            return clients.ContainsKey(guildId);
        }

        public async Task LeaveChannelOn(ulong guildId)
        {
            await clients[guildId].StopAsync();
            clients.Remove(guildId);
        }

        public async Task<IAudioClient> Join(SocketVoiceChannel channel)
        {
            IAudioClient client = await channel.ConnectAsync();
            clients.Add(channel.Guild.Id, client);
            return client;
        }

        public async Task Stop(IAudioClient audioClient)
        {
            await audioClient.StopAsync();
            if (clients.ContainsValue(audioClient)) clients.Remove(clients.First(x => x.Value == audioClient).Key);
        }
    }
}
