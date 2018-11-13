using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordTCPMusicBot.Helpers
{
    public class UserComparer : IEqualityComparer<SocketUser>
    {
        public bool Equals(SocketUser x, SocketUser y)
        {
            return x.Id == y.Id;
        }

        public int GetHashCode(SocketUser obj)
        {
            return obj.GetHashCode();
        }
    }
}
