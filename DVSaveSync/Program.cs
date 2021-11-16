using System;
using System.IO;
using System.Linq;
using System.Reflection;
using log4net;

// Configure log4net using the .config file
[assembly: log4net.Config.XmlConfigurator(Watch = true)]
// This will cause log4net to look for a configuration file
// called DVSaveSync.exe.config in the application base
// directory (i.e. the directory containing DVSaveSync.exe)
namespace DVSaveSync
{
    class Program
    {
        // Create a logger for use in this class
        private static readonly ILog log = LogManager.GetLogger(typeof(Program));

        static void Main(string[] args)
        {
            string version = Assembly.GetExecutingAssembly().GetName().Version.ToString();
            string dvSaveSyncDocs = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "DVSaveSync");

            log.Info($"Starting DVSaveSync - {version}");

            string configFilepath = $"{dvSaveSyncDocs}\\config.json";

            if (log.IsDebugEnabled) log.Debug($"dvSaveSyncDocs is {dvSaveSyncDocs}");

            // Look for mydocs DVSaveSync folder
            if (!Directory.Exists(dvSaveSyncDocs))
            {
                log.Info($"App folder doesn't exist... creating directory: {dvSaveSyncDocs}");
                try
                {
                    Directory.CreateDirectory(dvSaveSyncDocs);
                }
                catch (Exception ex)
                {
                    Quit(ErrorCodes.ErrorDuringOperations, $"Failed to create app folder ({dvSaveSyncDocs})!{Environment.NewLine}{ex.Message}");
                }
            }

            if (!File.Exists(configFilepath))
            {
                log.Info("No config information found, using defaults...");
                Generator.CreateDefaultDVSSConfiguration(dvSaveSyncDocs);
            }
            log.Info($"Loading configuration...{configFilepath}");
            Configuration config = Configuration.LoadConfiguration(configFilepath);
            Synchronizer synchro = new Synchronizer(config);

            if (args.Contains("restore"))
            {
                log.Info("Restoring previous game saves...");
                if(!synchro.DownloadRemoteToLocal())
                {
                    log.Error("Could not restore savegame files, please review logs.");
                    Environment.Exit((int)ErrorCodes.ErrorDuringOperations);
                }
                log.Info("Save game files have been restored...exiting.");
                Environment.Exit((int)ErrorCodes.NoErrors);
            }

            // Look for savegame file
            log.Info("Inspecting game folder for save files...");

            DirectoryInfo dvDirectory = new DirectoryInfo(config.SaveLocation);
            if (!dvDirectory.Exists)
            {
                Console.WriteLine("DVSaveSync could not find the Derail Valley game folder... would you like to do a quick search? Y/N");
                string response = Console.ReadLine();
                if (response.ToUpper() == "Y")
                {
                    if (synchro.SearchForGameFolder())
                    {
                        dvDirectory = new DirectoryInfo(synchro.SyncConfiguration.SaveLocation);
                        log.Info($"Success! Found the game folder at: {config.SaveLocation}");
                    }
                    else
                    {
                        Quit(ErrorCodes.CouldNotFindPath, $"Error, could not find savegame directory: '{config.SaveLocation}'");
                    }
                }
                else
                {
                    Quit(ErrorCodes.CouldNotFindPath, $"Error, could not find savegame directory: '{config.SaveLocation}'");
                }
            }

            // Check for valid upload location
            DirectoryInfo uploadDir = new DirectoryInfo(config.UploadLocation);
            if (!uploadDir.Exists)
            {
                log.Warn($"The upload directory '{uploadDir.FullName}' does not exist.");
                Console.WriteLine($"Upload location does not exist... create? Y/N");
                string response = Console.ReadLine();
                if (response.ToUpper() == "Y")
                {
                    uploadDir.Create();
                    log.Info($"Created upload directory: {uploadDir}");
                }
                else
                {
                    Quit(ErrorCodes.CouldNotFindPath, $"Exiting due to upload location not existing.");
                }
            }

            // Get savegame file list
            FileInfo[] saveGames = dvDirectory.GetFiles("savegame*");
            string localSavegame = "";

            if (saveGames.Length > 0)
            {
                localSavegame = saveGames.FirstOrDefault(s => s.Name == "savegame").FullName;
                if (string.IsNullOrEmpty(localSavegame))
                {
                    log.Warn("No savegame files found! Is the save path correct?");
                    Environment.Exit((int)ErrorCodes.CouldNotFindSaveFiles);
                }
                else
                {
                    synchro.LocalSavePath = localSavegame;
                    log.Info("Found savegames!");
                }
            }
            else
            {
                log.Error("No savegame files found! Is the save path correct?");
                Environment.Exit((int)ErrorCodes.CouldNotFindSaveFiles);
            }

            // Backup local saves if desired
            FileInfo localFileinfo = new FileInfo(synchro.LocalSavePath);
            switch (config.BackupOption)
            {
                case BackupPreference.AlwaysBackup:
                    // if always, make a backup of the local savegame file
                    if (config.BackupSaveFile() != true)
                    {
                        log.Error("There was an error, exiting!");
                        Environment.Exit((int)ErrorCodes.ErrorDuringOperations);
                    }
                    break;
                case BackupPreference.AskForBackup:
                    Console.WriteLine("Would you like to backup the current savegame? Y/N");
                    string usrResponse = Console.ReadLine();
                    if (usrResponse.ToUpper() == "Y")
                    {
                        // Make a backup copy of the file
                        if (config.BackupSaveFile() != true)
                        {
                            log.Error("There was an error, exiting!");
                            Environment.Exit((int)ErrorCodes.ErrorDuringOperations);
                        }
                    }

                    break;
                case BackupPreference.DoNotBackup:
                case BackupPreference.OnlyInDanger:
                    log.Info("Redundant backups not desired, moving on...");
                    break;
                default:
                    log.Warn("Invalid backup option... skipping...");
                    break;
            }

            // Check if there is a savegame file in the upload location
            string uploadSavegame = $"{config.UploadLocation}\\savegame";
            FileInfo uploadFileinfo = new FileInfo(uploadSavegame);

            if (!File.Exists(uploadSavegame))
            {
                log.Info("No upload save found, copying local savegame to upload destination...");
                synchro.RemoteSavePath = $"{config.UploadLocation}\\savegame";
                if (synchro.PushLocalToRemote() != true)
                {
                    log.Error($"Error copying local file to remote location, check logs for more details.");
                }
            }
            else
            {
                synchro.RemoteSavePath = uploadSavegame;
                switch(synchro.GetLocalSavegameSyncState())
                {
                    case SyncCompareState.LocalIsNewer:
                        log.Info("Local savegame is newer than upload savegame. Copying local save to Upload location...");
                        if (synchro.PushLocalToRemote() == false)
                        {
                            log.Error("Could not copy local to remote. Please check logs for error.");
                        }
                        else { log.Info("Savegame has been copied to Upload location successfully!"); }
                        break;
                    case SyncCompareState.LocalIsOlder:
                        log.Info("Local save is older, copying would overwrite newer savegame...");
                        if (synchro.DownloadRemoteToLocal() == false)
                        {
                            log.Error("Could not download remote savegame to local folder, check the logs for errors.");
                        }
                        else { log.Info("Savegame has been updated from [newer] Upload savegame successfully!"); }
                        break;
                    case SyncCompareState.LocalIsSame:
                        log.Info("Local and upload savegame files are the same! Nothing to do here.");
                        break;
                    default:
                        log.Error("I'm not sure how you got here... but it's probably not good. Check the logs for errors.");
                        break;
                }
            }
            Configuration.SaveConfiguration(configFilepath, synchro.SyncConfiguration);

            log.Info("DVSaveSwapper has completed.");
            if (config.KeepAlive)
            {
                Console.WriteLine(" Press Enter to close.");
                Console.ReadLine();
            }
            Environment.Exit(0);
        }

        public enum ErrorCodes
        {
            NoErrors = 0,
            CouldNotFindSaveFiles = 1,
            CouldNotFindPath = 2,
            ErrorDuringOperations = 3
        }
        public static void Quit(ErrorCodes errorCode, string message="")
        {
            // TODO: Implement the rest of this, or refactor because...why again?

            // store the inverse of the result from isnullorempty... i.e. If isnullorempty = true, then we don't log the empty string.
            bool logMessage = !string.IsNullOrEmpty(message);

            switch (errorCode)
            {
                case ErrorCodes.NoErrors:
                    if (logMessage) log.Info(message);
                    break;
                case ErrorCodes.CouldNotFindPath:
                    if (logMessage) log.Error(message);
                    break;
                case ErrorCodes.CouldNotFindSaveFiles:
                    if (logMessage) log.Error(message);
                    break;
                case ErrorCodes.ErrorDuringOperations:
                    if (logMessage) log.Error(message);
                    break;
                default:
                    if (logMessage) log.Info(message);
                    log.Warn("Unspecified error code, this is abnormal. Check the logs for errors.");
                    break;
            }

            Environment.Exit((int)errorCode);
        }
    }
}
