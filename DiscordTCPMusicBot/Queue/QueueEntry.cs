using Discord.WebSocket;
using System;
using System.Linq;
using System.Threading.Tasks;
using DiscordTCPMusicBot.Music;

namespace DiscordTCPMusicBot.Queue
{
    public class QueueEntry : MusicFile
    {
        public SocketUser Originator { get; set; }
        public Guid Guid { get; set; }
        
        public QueueEntry(string youtubeUrl, SocketUser originator, string title, string filePath = null, bool alreadyDownloaded = false, Action<Task> onDownloadFinished = null) 
            : base(youtubeUrl, filePath, title)
        {
            Originator = originator;
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

        public static QueueEntry FromMusicFile(MusicFile musicFile, SocketUser originator)
        {
            return new QueueEntry(musicFile.YoutubeUrl, originator, musicFile.Title, musicFile.FilePath, true);
        }
    }
}
