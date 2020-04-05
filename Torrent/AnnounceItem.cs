namespace Torrent
{
    public class AnnounceItem
    {
        public AnnounceItem(string url)
        {
            Url = url;
        }

        public string Url { get; set; }
    }
}
