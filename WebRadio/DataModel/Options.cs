namespace WebRadio.DataModel
{
    public interface IOptions
    {
        bool ShowDownloadInfo { get; }
        bool ShowICYTags { get; }
    }

    public class Options : IOptions
    {
        public bool ShowDownloadInfo { get; set; } = true;
        public bool ShowICYTags { get; set; } = true;
    }
}
