using MediaToolkit;
using MediaToolkit.Model;
using System;
using System.IO;
using System.Threading.Tasks;
using VideoLibrary;

namespace DiscordTCPMusicBot.Music
{
    public class MusicFile
    {
        private const string fileExtension = ".mp3";

        public string YoutubeUrl { get; set; }
        public string FilePath { get; set; }
        public string Title { get; set; }

        public bool IsDownloaded { get => FilePath != null; }

        public MusicFile(string youtubeUrl, string filePath, string title)
        {
            YoutubeUrl = youtubeUrl;
            Title = title;
            FilePath = filePath;
        }

        public async Task DownloadAsync(string filePath)
        {
            var dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            try
            {
                if (FilePath != null) throw new InvalidOperationException("File has already been downloaded.");

                var youtube = YouTube.Default;
                var vid = await youtube.GetVideoAsync(YoutubeUrl);
                var bytes = await vid.GetBytesAsync();
                string inputFilePath = filePath + vid.FullName;
                File.WriteAllBytes(inputFilePath, bytes);

                var inputFile = new MediaFile { Filename = inputFilePath };
                string outputFilePath = filePath + fileExtension;
                var outputFile = new MediaFile { Filename = outputFilePath };

                using (var engine = new Engine())
                {
                    engine.GetMetadata(inputFile);

                    engine.Convert(inputFile, outputFile);
                }

                FilePath = outputFilePath;
                File.Delete(inputFilePath);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }
    }
}
