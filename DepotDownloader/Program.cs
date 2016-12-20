using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using SteamKit2;
using System.ComponentModel;

using VersionManifestPair = System.Collections.Generic.KeyValuePair<string, ulong>;
using System.Xml.Serialization;
using System.Xml;
using System.Text;

namespace DepotDownloader
{
    class Program
    {
        string Username;
        string Password;
        bool Started = false;

        static void Main(string[] args)
        {
            if ( args.Length == 0 )
            {
                PrintUsage();
                return;
            }
            Program instance = new Program();

            DebugLog.Enabled = false;

            ConfigStore.LoadFromFile(Path.Combine(Environment.CurrentDirectory, "DepotDownloader.config"));

            instance.ParseOrPromptUserAndPassword(args);
            ParseGeneralContentDownloaderParams(args);
            ParseFileList(args);

            if (HasParameter(args, "-steamvrwin"))
            {
                instance.GetSteamVRWin();
                return;
            }
            if (HasParameter(args, "-steamvr"))
            {
                instance.GetSteamVR();
                return;
            }

            ContentDownloader.Config.DownloadManifestOnly = HasParameter(args, "-manifest-only");
            ContentDownloader.Config.BetaPassword = GetParameter<string>(args, "-betapassword");

            var dir = GetParameter<string>(args, "-dir");


            var dl = new Downloadable();
            dl.AppId = GetParameter<uint>(args, "-app", dl.AppId);
            dl.DepotId = GetParameter<uint>(args, "-depot", dl.DepotId);
            dl.Branch = GetBranch(args);
            dl.ManifestId = GetParameter<ulong>(args, "-manifest", dl.ManifestId);

            if (dl.AppId == ContentDownloader.INVALID_APP_ID)
            {
                Console.WriteLine( "Error: -app not specified!" );
                return;
            }

            if (dl.DepotId == ContentDownloader.INVALID_DEPOT_ID && dl.ManifestId != ContentDownloader.INVALID_MANIFEST_ID)
            {
                Console.WriteLine("Error: -manifest requires -depot to be specified");
                return;
            }

            dl.ForceDepot = HasParameter(args, "-force-depot");

            if (instance.Start())
            {
                instance.Download(dir, dl);
                instance.Shutdown();
            }
        }

        static string GetBranch(string[] args)
        {
            return GetParameter<string>(args, "-branch") ?? GetParameter<string>(args, "-beta") ?? "Public";
        }

        void ParseOrPromptUserAndPassword(string[] args)
        {
            Username = GetParameter<string>(args, "-username") ?? GetParameter<string>(args, "-user");
            Password = GetParameter<string>(args, "-password") ?? GetParameter<string>(args, "-pass");

            if (Username != null && Password == null)
            {
                Console.Write("Enter account password for \"{0}\": ", Username);
                Password = Util.ReadPassword();
                Console.WriteLine();
            }
            else if (Username == null)
            {
                Console.WriteLine("No username given. Using anonymous account with dedicated server subscription.");
            }
        }

        static void ParseGeneralContentDownloaderParams(string[] args)
        {
            ContentDownloader.Config.DownloadAllPlatforms = HasParameter(args, "-all-platforms");
            ContentDownloader.Config.VerifyAll = HasParameter(args, "-verify-all") || HasParameter(args, "-verify_all") || HasParameter(args, "-validate");
            ContentDownloader.Config.MaxServers = GetParameter<int>(args, "-max-servers", 20);
            ContentDownloader.Config.MaxDownloads = GetParameter<int>(args, "-max-downloads", 4);

            ContentDownloader.Config.MaxServers = Math.Max(ContentDownloader.Config.MaxServers, ContentDownloader.Config.MaxDownloads);

            int cellId = GetParameter<int>(args, "-cellid", -1);
            if (cellId == -1)
            {
                cellId = 0;
            }

            ContentDownloader.Config.CellID = cellId;
        }

        static void ParseFileList(string[] args)
        {

            string fileList = GetParameter<string>(args, "-filelist");
            string[] files = null;

            if ( fileList != null )
            {
                try
                {
                    string fileListData = File.ReadAllText( fileList );
                    files = fileListData.Split( new char[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries );

                    ContentDownloader.Config.UsingFileList = true;
                    ContentDownloader.Config.FilesToDownload = new List<string>();
                    ContentDownloader.Config.FilesToDownloadRegex = new List<Regex>();

                    foreach (var fileEntry in files)
                    {
                        try
                        {
                            Regex rgx = new Regex(fileEntry, RegexOptions.Compiled | RegexOptions.IgnoreCase);
                            ContentDownloader.Config.FilesToDownloadRegex.Add(rgx);
                            Console.WriteLine("Treated as regex: '{0}'", fileEntry);
                        }
                        catch
                        {
                            ContentDownloader.Config.FilesToDownload.Add(fileEntry);
                            Console.WriteLine("Treated as literal file: '{0}'", fileEntry);
                            continue;
                        }
                    }

                    Console.WriteLine( "Using filelist: '{0}'.", fileList );
                }
                catch ( Exception ex )
                {
                    Console.WriteLine( "Warning: Unable to load filelist: {0}", ex.ToString() );
                }
            }

        }

        bool Start()
        {
            if (!Started)
            {
                Started = ContentDownloader.InitializeSteam3(Username, Password);
                if (!Started)
                {
                    Console.WriteLine( "Warning: Unable to initialize Steam!");
                }
            }
            return Started;
        }

        void Download(string dir, Downloadable dl)
        {
            if (!Started)
            {
                if (!Start())
                {
                    return;
                }
            }
            ContentDownloader.Config.InstallDirectory = dir;
            ContentDownloader.Config.ManifestId = dl.ManifestId;
            ContentDownloader.DownloadApp(dl.AppId, dl.DepotId, dl.Branch, dl.ForceDepot);
        }
        void Shutdown()
        {
            if (Started)
            {
                ContentDownloader.ShutdownSteam3();
                Started = false;
            }
        }

        ~Program()
        {
            Shutdown();

        }

        void GetSteamVRWin()
        {
            var steamvr = new SteamVR();
            var dl = new Downloadable();
            dl.AppId = steamvr.AppId;
            dl.DepotId = steamvr.Win32DepotId;

            foreach (var pair in steamvr.LegacyWindowsVersions)
            {
                Console.WriteLine("Working on {0}", pair.Key);
                var dir = string.Format("steamvr-win32-{0}", pair.Key);
                dl.ManifestId = pair.Value;
                Download(dir, dl);
            }
            Console.WriteLine("Shutting down...");
            Shutdown();
        }
        static bool IsExceptionAlreadyExists(IOException e)
        {
            return e.HResult == -2147024816; /* HRESULT 0x80070050*/
        }
        void DoGetSteamVRBranch(string branch = "Public")
        {

            var steamvr = new SteamVR();
            SteamVRVersion ver;
            {
                /// Get the current version
                Console.WriteLine("Getting current release 'version number'");
                var dl = new Downloadable();
                dl.AppId = steamvr.AppId;
                dl.DepotId = steamvr.ContentDepotId;
                dl.Branch = branch;
                ContentDownloader.Config = new DownloadConfig();
                ContentDownloader.Config.FilesToDownload = new List<string>();
                ContentDownloader.Config.FilesToDownload.Add(steamvr.ContentDepotVersionPath);
                ContentDownloader.Config.UsingFileList = true;
                var dir = "steamvr-work";
                Download(dir, dl);
                var verString = File.ReadAllText(string.Format("{0}\\{1}", dir, steamvr.ContentDepotVersionPath)).Trim();
                Console.WriteLine("Current {1} SteamVR version is known as v{0}", verString, branch);

                ContentDownloader.Config.UsingFileList = false;
                ContentDownloader.Config.FilesToDownload.Clear();
                Console.WriteLine("Retrieving other metadata...");
                ver = SteamVR.PopulateVersion(branch, verString);
            }

            {
                var fn = string.Format("steamvr-{0}.xml", ver.Version);
                Console.WriteLine("Writing metadata to {0}...", fn);
                // Write out metadata file
                try
                {
                    FileStream stream = new FileStream(fn, FileMode.CreateNew);
                    XmlSerializer ser = new XmlSerializer(ver.GetType());
                    XmlTextWriter text = new XmlTextWriter(stream, Encoding.UTF8);
                    ser.Serialize(text, ver);
                    stream.Close();
                }
                catch (IOException e)
                {
                    if (IsExceptionAlreadyExists(e))
                    {
                        Console.WriteLine("Metadata file {0} already exists, continuing...", fn);
                    }
                    else
                    {
                        Console.WriteLine("Warning: Metadata file could not be opened: {0}", e.ToString());
                    }
                }
                var branchFn = string.Format("steamvr-{0}.{1}.xml", ver.Version, branch);

                try
                {
                    File.Copy(fn, branchFn);
                }
                catch (IOException e)
                {

                    if (IsExceptionAlreadyExists(e))
                    {
                        Console.WriteLine("B ranch-specific filename copy ({0} to {1}) already exists, continuing...", fn, branchFn);
                    }
                    else
                    {
                        Console.WriteLine("Warning: Could not copy metadata file to branch-specific filename ({0} to {1}): {2}", fn, branchFn, e.ToString());
                    }
                }
            }

            ContentDownloader.Config.DownloadAllPlatforms = true;
            DownloadSteamVRRuntime(ver, "Win32", ver.Win32);
            DownloadSteamVRRuntime(ver, "OSX", ver.OSX);
            DownloadSteamVRRuntime(ver, "Linux", ver.Linux);
        }
        void GetSteamVR()
        {
            DoGetSteamVRBranch();
            DoGetSteamVRBranch("beta");
            Console.WriteLine("Shutting down...");
            Shutdown();
        }

        void DownloadSteamVRRuntime(SteamVRVersion ver, string name, SteamVRDepot depot)
        {
            var dir = string.Format("steamvr-{0}\\{1}", ver.Version, name);

            Console.WriteLine("Working on {0}", dir);
            var dl = new Downloadable();
            dl.AppId = ver.AppId;
            dl.DepotId = depot.DepotId;
            dl.ManifestId = depot.ManifestId;
            Download(dir, dl);
        }

        static int IndexOfParam( string[] args, string param )
        {
            for ( int x = 0 ; x < args.Length ; ++x )
            {
                if ( args[ x ].Equals( param, StringComparison.OrdinalIgnoreCase ) )
                    return x;
            }
            return -1;
        }
        static bool HasParameter( string[] args, string param )
        {
            return IndexOfParam( args, param ) > -1;
        }

        static T GetParameter<T>(string[] args, string param, T defaultValue = default(T))
        {
            int index = IndexOfParam(args, param);

            if (index == -1 || index == (args.Length - 1))
                return defaultValue;

            string strParam = args[index + 1];

            var converter = TypeDescriptor.GetConverter(typeof(T));
            if( converter != null )
            {
                return (T)converter.ConvertFromString(strParam);
            }
            
            return default(T);
        }

        static void PrintUsage()
        {
            Console.WriteLine( "\nUsage: depotdownloader <parameters> [optional parameters]\n" );

            Console.WriteLine( "Parameters:" );
            Console.WriteLine("\t-app <#>\t\t\t\t- the AppID to download.");            
            Console.WriteLine("\t-steamvrwin\t\t\t\t- custom steamvr windows runtime behavior");
            Console.WriteLine("\t-steamvr\t\t\t\t- custom steamvr runtime behavior");
            Console.WriteLine();

            Console.WriteLine( "Optional Parameters:" );
            Console.WriteLine( "\t-depot <#>\t\t\t- the DepotID to download." );
            Console.WriteLine( "\t-cellid <#>\t\t\t- the overridden CellID of the content server to download from." );
            Console.WriteLine( "\t-username <user>\t\t\t- the username of the account to login to for restricted content." );
            Console.WriteLine( "\t-password <pass>\t\t\t- the password of the account to login to for restricted content." );
            Console.WriteLine( "\t-dir <installdir>\t\t\t- the directory in which to place downloaded files." );
            Console.WriteLine( "\t-filelist <filename.txt>\t\t- a list of files to download (from the manifest). Can optionally use regex to download only certain files." );
            Console.WriteLine( "\t-all-platforms\t\t\t- downloads all platform-specific depots when -app is used." );
            Console.WriteLine( "\t-manifest-only\t\t\t- downloads a human readable manifest for any depots that would be downloaded." );
            Console.WriteLine( "\t-beta <branchname>\t\t\t\t- download from specified branch if available (default: Public)." );
            Console.WriteLine( "\t-betapassword <pass>\t\t\t- branch password if applicable." );
            Console.WriteLine( "\t-manifest <id>\t\t\t- manifest id of content to download (requires -depot, default: current for branch)." );
            Console.WriteLine( "\t-max-servers <#>\t\t\t- maximum number of content servers to use. (default: 8)." );
            Console.WriteLine( "\t-max-downloads <#>\t\t\t- maximum number of chunks to download concurrently. (default: 4)." );
        }
    }
}
