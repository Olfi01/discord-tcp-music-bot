using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace DiscordTCPMusicBot.Helpers
{
    public static class Extensions
    {
        public static List<T> Copy<T>(this IEnumerable<T> list)
        {
            List<T> result = new List<T>();
            foreach (var i in list)
            {
                result.Add(i);
            }
            return result;
        }

        public static string RemovePathForbiddenChars(this string str)
        {
            string regexSearch = new string(Path.GetInvalidFileNameChars()) + new string(Path.GetInvalidPathChars());
            Regex r = new Regex(string.Format("[{0}]", Regex.Escape(regexSearch)));
            return r.Replace(str, "");
        }
    }
}
