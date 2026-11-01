using System;
using System.ComponentModel;
using System.IO;
using System.Windows.Media;
using RetroAmp.Core;

namespace RetroAmp.Media
{
    public enum ItemKind { LocalAudio, LocalVideo, OnlineStream, DirectUrl }

    public sealed class PlaylistItem : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        void On(string p) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));

        public string Location { get; }
        public ItemKind Kind { get; private set; }
        public string CachedFile { get; set; }
        public bool PlayAsVideo { get; set; }
        public bool TagsLoaded { get; set; }
        public int Bitrate { get; set; }
        public int SampleRate { get; set; }
        public int Channels { get; set; }

        string title, artist; TimeSpan duration; int index; bool isCurrent; bool failed;

        public PlaylistItem(string location, string title = null, TimeSpan? duration = null)
        {
            Location = location;
            this.title = title;
            if (duration.HasValue) this.duration = duration.Value;
            Kind = Classify(location);
        }

        public static ItemKind Classify(string loc)
        {
            if (Util.IsUrl(loc))
            {
                if (OnlineSources.IsKnownSite(loc)) return ItemKind.OnlineStream;
                string ext = Util.SafeExt(loc);
                if (Util.VideoExts.Contains(ext)) return ItemKind.DirectUrl;
                if (Util.AudioExts.Contains(ext)) return ItemKind.DirectUrl;
                return ItemKind.DirectUrl; // radio streams etc.; yt-dlp is tried as fallback
            }
            return Util.VideoExts.Contains(Util.SafeExt(loc)) ? ItemKind.LocalVideo : ItemKind.LocalAudio;
        }

        public bool IsVideo => Kind == ItemKind.LocalVideo || PlayAsVideo ||
                               (Kind == ItemKind.DirectUrl && Util.VideoExts.Contains(Util.SafeExt(Location)));
        public bool IsOnline => Kind == ItemKind.OnlineStream || Kind == ItemKind.DirectUrl;

        public string SourceTag
        {
            get
            {
                if (Kind == ItemKind.OnlineStream) return OnlineSources.ShortName(Location);
                if (Kind == ItemKind.DirectUrl) return "URL";
                if (Kind == ItemKind.LocalVideo) return "VID";
                return "";
            }
        }

        public string Title { get => title; set { title = value; On(nameof(Title)); On(nameof(Display)); } }
        public string Artist { get => artist; set { artist = value; On(nameof(Artist)); On(nameof(Display)); } }
        public TimeSpan Duration { get => duration; set { duration = value; On(nameof(Duration)); On(nameof(DurationText)); } }
        public int Index { get => index; set { if (index != value) { index = value; On(nameof(Index)); On(nameof(Display)); } } }
        public bool Failed { get => failed; set { failed = value; On(nameof(ForeBrush)); } }
        public bool IsCurrent { get => isCurrent; set { if (isCurrent != value) { isCurrent = value; On(nameof(IsCurrent)); On(nameof(ForeBrush)); } } }

        public string DisplayTitle
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(title))
                    return string.IsNullOrWhiteSpace(artist) ? title : artist + " - " + title;
                if (Util.IsUrl(Location)) return Location;
                try { return Path.GetFileNameWithoutExtension(Location); } catch { return Location; }
            }
        }

        public string Display
        {
            get
            {
                string tag = SourceTag;
                return index + ". " + (tag.Length > 0 ? "[" + tag + "] " : "") + DisplayTitle;
            }
        }

        public string DurationText => duration > TimeSpan.Zero ? Util.FormatTime(duration) : "";

        public Brush ForeBrush => isCurrent ? UI.Theme.PlaylistCurrent : (failed ? UI.Theme.PlaylistFailed : UI.Theme.PlaylistText);

        public void RefreshDisplay() { On(nameof(Display)); On(nameof(DurationText)); }
    }
}
