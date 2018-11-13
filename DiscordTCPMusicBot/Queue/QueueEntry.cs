using Discord.WebSocket;
using System;
using System.Linq;
using System.Threading.Tasks;
using DiscordTCPMusicBot.Music;

namespace DiscordTCPMusicBot.Queue
{
    public class QueueEntry : MusicFile
    {
        public ulong OriginatorId { get; set; }
        public Guid Guid { get; set; }
        
        public QueueEntry(string youtubeUrl, ulong originatorId, string title, string filePath = null, bool alreadyDownloaded = false, Action<Task> onDownloadFinished = null) 
            : base(youtubeUrl, filePath, title)
        {
            OriginatorId = originatorId;
            if (filePath != null && !alreadyDownloaded)
            {
                FilePath = null;
                DownloadAsync(filePath).ContinueWith(task =>
                {
                    if (onDownloadFinished != null) onDownloadFinished.Invoke(task);
                    //FilePath = filePath;
                });
            }
            Guid = new Guid();
        }

        public static QueueEntry FromMusicFile(MusicFile musicFile, ulong originatorId)
        {
            return new QueueEntry(musicFile.YoutubeUrl, originatorId, musicFile.Title, musicFile.FilePath, true);
        }
    }
}
