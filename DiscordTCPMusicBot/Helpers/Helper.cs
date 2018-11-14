using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordTCPMusicBot.Helpers
{
    public static class Helper
    {
        public static void CreateDirectoryIfNecessary(string path)
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        }

        public static string GetAppDataPath(string subpath)
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), Constants.AppDataSubPath, subpath);
        }

        public static string CreateHttpUsername(ulong userId, ulong guildId)
        {
            return $"{userId}@{guildId}";
        }

        private static Tuple<ulong, ulong> GetUserAndGuildId(string httpUsername)
        {
            var split = httpUsername.Split('@');
            return new Tuple<ulong, ulong>(ulong.Parse(split[0]), ulong.Parse(split[1]));
        }

        public static ulong GetUserId(string httpUsername) => GetUserAndGuildId(httpUsername).Item1;

        public static ulong GetGuildId(string httpUsername) => GetUserAndGuildId(httpUsername).Item2;
    }
}
