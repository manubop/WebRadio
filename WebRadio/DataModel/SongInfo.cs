namespace WebRadio.DataModel
{
    public sealed class SongInfo(string artist = "", string title = "")
    {
        public string Artist { get => artist; }

        public string Title { get => title; }

        public bool IsEmpty() => string.IsNullOrEmpty(Artist) || string.IsNullOrEmpty(Title);

        public bool Equals(SongInfo tagInfo) => Artist.Equals(tagInfo.Artist, System.StringComparison.Ordinal) && Title.Equals(tagInfo.Title, System.StringComparison.Ordinal);

        public static readonly SongInfo Empty = new();
    }
}
