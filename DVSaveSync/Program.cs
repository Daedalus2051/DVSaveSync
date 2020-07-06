using System;
using System.IO;
using System.Linq;
using System.Reflection;

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
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        static void Main(string[] args)
        {
            string version = Assembly.GetExecutingAssembly().GetName().Version.ToString();
            
            log.Info($"Starting DVSaveSwapper - {version}");
            //Console.WriteLine($"Starting DVSaveSwapper - {version}");

            string dvSaveSyncDocs = $"{Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)}\\DVSaveSync";
            if(log.IsDebugEnabled) log.Debug($"dvSaveSyncDocs is {dvSaveSyncDocs}");

            // Look for mydocs DVSaveSync folder
            if (!Directory.Exists(dvSaveSyncDocs))
            {
                Console.WriteLine($"App folder doesn't exist... creating...");
                try
                {
                    Directory.CreateDirectory(dvSaveSyncDocs);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to create app folder!{Environment.NewLine}{ex.Message}");
                    Environment.Exit((int)ErrorCodes.ErrorDuringOperations);
                }
                Console.Write("Done.");
            }

            Console.WriteLine($"Current directory: {dvSaveSyncDocs}");
            Configuration config = new Configuration();

            if (!File.Exists($"{dvSaveSyncDocs}\\config.json"))
            {
                Console.WriteLine("No config information found, using defaults...");
                Generator.CreateDefaultDVSSConfiguration(dvSaveSyncDocs);
            }
            Console.WriteLine("Loading configuration...");
            config = Configuration.LoadConfiguration($"{dvSaveSyncDocs}\\config.json");

            // Look for savegame file
            Console.Write("Inspecting game folder for save files...");

            DirectoryInfo dvDirectory = new DirectoryInfo(config.SaveLocation);
            if (!dvDirectory.Exists)
            {
                Console.WriteLine($"Error, could not find savegame directory: '{config.SaveLocation}'");
                Environment.Exit((int)ErrorCodes.CouldNotFindPath);
            }

            DirectoryInfo uploadDir = new DirectoryInfo(config.UploadLocation);
            if (!uploadDir.Exists)
            {
                Console.WriteLine($"Upload location does not exist... create? Y/N");
                string response = Console.ReadLine();
                if (response.ToUpper() == "Y")
                {
                    uploadDir.Create();
                }
                else
                {
                    Console.WriteLine($"Exiting due to upload location not existing.");
                    Environment.Exit((int)ErrorCodes.CouldNotFindPath);
                }
            }

            FileInfo[] saveGames = dvDirectory.GetFiles("savegame*");
            string localSavegame = "";

            if (saveGames.Length > 0)
            {
                localSavegame = saveGames.FirstOrDefault(s => s.Name == "savegame").FullName;
                if (string.IsNullOrEmpty(localSavegame))
                {
                    Console.WriteLine("No savegame files found! Is the save path correct?");
                    Environment.Exit((int)ErrorCodes.CouldNotFindSaveFiles);
                }
                else
                {
                    Console.WriteLine("Found savegames!");
                }
            }
            else
            {
                Console.WriteLine("No savegame files found! Is the save path correct?");
                Environment.Exit((int)ErrorCodes.CouldNotFindSaveFiles);
            }

            FileInfo localFileinfo = new FileInfo(localSavegame);
            // Check backup options
            switch (config.BackupOption)
            {
                case BackupPreference.AlwaysBackup:
                    // if always, make a backup of the local savegame file
                    if (config.BackupSaveFile() != true)
                    {
                        Console.WriteLine("There was an error, exiting!");
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
                            Console.WriteLine("There was an error, exiting!");
                            Environment.Exit((int)ErrorCodes.ErrorDuringOperations);
                        }
                    }

                    break;
                case BackupPreference.DoNotBackup:
                case BackupPreference.OnlyInDanger:
                    Console.WriteLine("Redundant backups not desired, moving on...");
                    break;
                default:
                    Console.WriteLine("Invalid backup option... skipping...");
                    break;
            }

            // Check if there is a savegame file in the upload location
            string uploadSavegame = $"{config.UploadLocation}\\savegame";
            FileInfo uploadFileinfo = new FileInfo(uploadSavegame);

            if (!File.Exists(uploadSavegame))
            {
                Console.WriteLine("No upload save found, copying local savegame to upload destination...");
                DisplayFileInfos(uploadFileinfo, localFileinfo);
                File.Copy(localSavegame, uploadSavegame);
            }
            else
            {// upload save exists, check the date...is it older than what we have?
                DateTime uploadSaveDate = new FileInfo(uploadSavegame).LastWriteTime;
                DateTime localSaveDate = new FileInfo(localSavegame).LastWriteTime;

#if DEBUG
                Console.WriteLine($"[DEBUG]: localSaveDate-{localSaveDate.ToShortDateString()} {localSaveDate.ToShortTimeString()}");
                Console.WriteLine($"[DEBUG]: uploadSaveDate-{uploadSaveDate.ToShortDateString()} {uploadSaveDate.ToShortTimeString()}");
#endif

                //https://docs.microsoft.com/en-us/dotnet/api/system.datetime.compare?view=netcore-3.1
                int result = -1;
                result = DateTime.Compare(localSaveDate, uploadSaveDate);
                if (result < 0)
                {// relationship is earlier than
                    // local save is earlier than upload save = local save is older
                    Console.WriteLine("Local save is older, copying would overwrite newer savegame...");
                    if (config.AllowDownloadSavegame)
                    {
                        if (config.BackupOption == BackupPreference.OnlyInDanger)
                        {
                            Console.WriteLine("Backing up local savegame, just in case... (this is because of your preferences)");
                            config.BackupSaveFile();
                        }
                        Console.WriteLine("Copying newer savegame from upload location...");
                        DisplayFileInfos(uploadFileinfo, localFileinfo);
                        File.Copy(uploadSavegame, localSavegame, true);
                        
                        Console.WriteLine("Savegame has been updated from [newer] Upload savegame successfully!");
                    }
                }
                else if (result == 0)
                { // relationship is the same time as
                    Console.WriteLine("Local and upload savegame files are the same! Nothing to do here.");
                }
                else
                {// relationship is later than
                    // local save is later than upload save = local save is newer
                    Console.WriteLine("Local savegame is newer than upload savegame. Copying local save to Upload location...");
                    DisplayFileInfos(uploadFileinfo, localFileinfo);
                    File.Copy(localSavegame, uploadSavegame, true);
                    Console.WriteLine("Savegame has been copied to Upload location successfully!");
                }
            }

            Console.Write("DVSaveSwapper has completed.");
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
        public static void DisplayFileInfos(FileInfo upload, FileInfo local)
        {
            // TODO: figure out why the FileInfo.Length property is throwing an exception (something to do with no file extension?)
            Console.WriteLine($"Upload file details: {upload.Name} - {upload.LastWriteTime}");
            Console.WriteLine($"Local file details: {local.Name} - {local.LastWriteTime}");
        }
    }
}
