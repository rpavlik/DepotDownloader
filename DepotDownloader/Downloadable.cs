namespace DepotDownloader
{
    class Downloadable
    {
        public uint AppId = ContentDownloader.INVALID_APP_ID;
        public uint DepotId = ContentDownloader.INVALID_DEPOT_ID;
        public ulong ManifestId = ContentDownloader.INVALID_MANIFEST_ID;
        public string Branch = "Public";
        public bool ForceDepot = false;
    }
}
