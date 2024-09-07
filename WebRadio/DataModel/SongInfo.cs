using Un4seen.Bass.AddOn.Tags;

namespace WebRadio.DataModel
{
    public class SongInfo
    {
        public string Artist { get; set; } = string.Empty;

        public string Title { get; set; } = string.Empty;

        public bool IsEmpty() => string.IsNullOrEmpty(Artist) || string.IsNullOrEmpty(Title);

        public bool Equals(TAG_INFO tagInfo) => Artist.Equals(tagInfo.artist, System.StringComparison.Ordinal) && Title.Equals(tagInfo.title, System.StringComparison.Ordinal);

        public static readonly SongInfo Empty = new();
    }
}
