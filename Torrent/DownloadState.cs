namespace Torrent
{
    public class DownloadState
    {

        public DownloadState(bool isDownloded)
        {
            IsDownloded = isDownloded;
        }
        public DownloadState()
        {

        }

        public bool IsDownloded { get; set; }
        public int DownloadCount { get; set; }
        public Peer Peer { get; set; }
    }
}
