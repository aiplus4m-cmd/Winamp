using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace RetroAmp.Media
{
    /// <summary>Reads tags (title/artist/duration/bitrate) for local files in the background.</summary>
    public static class TagLoader
    {
        static readonly BlockingCollection<PlaylistItem> Queue = new BlockingCollection<PlaylistItem>();
        static Dispatcher dispatcher;

        public static void Init(Dispatcher d)
        {
            dispatcher = d;
            var t = new Thread(Worker) { IsBackground = true, Name = "TagLoader", Priority = ThreadPriority.BelowNormal };
            t.Start();
        }

        public static void Enqueue(PlaylistItem item)
        {
            if (item.TagsLoaded || item.IsOnline) return;
            Queue.Add(item);
        }

        static void Worker()
        {
            foreach (var item in Queue.GetConsumingEnumerable())
            {
                if (item.TagsLoaded) continue;
                try
                {
                    using (var f = TagLib.File.Create(item.Location))
                    {
                        string title = f.Tag?.Title;
                        string artist = f.Tag?.FirstPerformer ?? f.Tag?.FirstAlbumArtist;
                        var dur = f.Properties?.Duration ?? TimeSpan.Zero;
                        int br = f.Properties?.AudioBitrate ?? 0;
                        int sr = f.Properties?.AudioSampleRate ?? 0;
                        int ch = f.Properties?.AudioChannels ?? 0;
                        dispatcher?.BeginInvoke(new Action(() =>
                        {
                            if (!string.IsNullOrWhiteSpace(title)) item.Title = title.Trim();
                            if (!string.IsNullOrWhiteSpace(artist)) item.Artist = artist.Trim();
                            if (dur > TimeSpan.Zero) item.Duration = dur;
                            item.Bitrate = br; item.SampleRate = sr; item.Channels = ch;
                        }), DispatcherPriority.Background);
                    }
                }
                catch { }
                item.TagsLoaded = true;
            }
        }
    }
}
