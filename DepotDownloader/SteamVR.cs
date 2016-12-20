using System;
using VersionManifestPair = System.Collections.Generic.KeyValuePair<string, ulong>;
using PlatformDepotPair = System.Collections.Generic.KeyValuePair<string, uint>;

namespace DepotDownloader
{


    [Serializable]
    public class SteamVRDepot
    {
        public static uint INVALID_DEPOTID = ContentDownloader.INVALID_DEPOT_ID;
        public SteamVRDepot()
        {
            DepotId = INVALID_DEPOTID;
        }
        public
        SteamVRDepot(uint depot)
        {
            DepotId = depot;
        }
        [NonSerialized]
        public uint DepotId;
        //public ulong GID;
        public ulong ManifestId;
    }
    [Serializable]
    public class SteamVRVersion
    {
        [NonSerialized]
        public readonly uint AppId = 250820;

        public string Version;

        public uint BuildNum;

        public SteamVRDepot Content = new SteamVRDepot(250824);
        public SteamVRDepot Win32 = new SteamVRDepot(250821);
        public SteamVRDepot OSX = new SteamVRDepot(250822);
        public SteamVRDepot Linux = new SteamVRDepot(250823);
    }

    class SteamVR
    {
        public readonly VersionManifestPair[] LegacyWindowsVersions = {
                new VersionManifestPair( "v1459383055", 2971217845583775832 ),
                new VersionManifestPair( "v1459268357", 6412586717092468451 ),
                new VersionManifestPair( "v1459194224", 8469047631146748620 ),
                new VersionManifestPair( "v1457503340", 6231830579612283653 ),
                new VersionManifestPair( "v1457155403", 4081350190006495576 ),
                new VersionManifestPair( "v1457146742", 986788611431053423 ),
                new VersionManifestPair( "v1456973056", 2616796628530172164 ),
            };
        public readonly uint AppId = 250820;
        public readonly uint Win32DepotId = 250821;
        public readonly uint ContentDepotId = 250824;
        public readonly string ContentDepotVersionPath = "bin\\version.txt";
        public readonly PlatformDepotPair[] RuntimeDepotIds = {
            new PlatformDepotPair("Win32", 250821),
            new PlatformDepotPair("OSX", 250822),
            new PlatformDepotPair("Linux", 250823)
        };

        public static SteamVRVersion PopulateVersion(string branch, string verString)
        {
            var ver = new SteamVRVersion();
            ver.Version = verString;
            ver.BuildNum = ContentDownloader.GetSteam3AppBuildNumber(ver.AppId, branch);
            PopulateDepot(branch, ver, ref ver.Content);
            PopulateDepot(branch, ver, ref ver.Win32);
            PopulateDepot(branch, ver, ref ver.OSX);
            PopulateDepot(branch, ver, ref ver.Linux);
            return ver;
        }

        public static void PopulateDepot(string branch, SteamVRVersion ver, ref SteamVRDepot depot)
        {
            ContentDownloader.Config.ManifestId = ContentDownloader.INVALID_MANIFEST_ID;
            depot.ManifestId = ContentDownloader.GetSteam3DepotManifest(depot.DepotId, ver.AppId, branch);
        }
    }
}
