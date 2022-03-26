using DVSaveSync.Common;
using DVSaveSync.Engine;
using Serilog;
using System.Reflection;

// See https://aka.ms/new-console-template for more information
try
{
    Helpers.SetLogger();

    Log.Information("Program start - {0}", Assembly.GetExecutingAssembly().GetName().Version);

    string dvSaveSyncAppDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DVSaveSync");
    string configFilepath = Path.Combine(dvSaveSyncAppDataPath, "config.json");

    Log.Debug("dvSaveSyncAppDataPath is {0}", dvSaveSyncAppDataPath);

    // Check that the appdata folder exists
    if (!Directory.Exists(dvSaveSyncAppDataPath))
    {
        Log.Warning("App folder doesn't exist... attempting to create directory: {0}", dvSaveSyncAppDataPath);
        try 
        { 
            Directory.CreateDirectory(dvSaveSyncAppDataPath); 
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error creating appdata folder.");
            Helpers.Quit(ErrorCodes.ErrorDuringOperations, $"Could not create directory: {dvSaveSyncAppDataPath}");
        }
    }

    Log.Debug("Loading configuration...");
    if (!File.Exists(configFilepath))
    {
        Log.Warning("No config information found, generating default config file...");
        Generator.CreateDefaultDVSSConfiguration(dvSaveSyncAppDataPath);
    }
    Configuration config = Configuration.LoadConfiguration(configFilepath);
    Synchronizer synchronizer = new(config);

    if (args.Contains("restore"))
    {
        Log.Information("Restoring previous game saves...");
        var syncResult = synchronizer.DownloadRemoteToLocal();
        if (!syncResult.IsSuccess)
        {
            Log.Error(syncResult.ToString());
            Log.Error("Could not restore savegame files, please review logs.");
            Helpers.Quit(ErrorCodes.ErrorDuringOperations);
        }
        Log.Information("Save game files have been restored...exiting.");
        Helpers.Quit(ErrorCodes.NoErrors);
    }

    // Look for savegame file
    Log.Information("Inspecting game folder for save files...");

    var dvDirectory = new DirectoryInfo(config.SaveLocation);
    if (!dvDirectory.Exists)
    {
        Log.Information("DVSaveSync could not find the Derail Valley game folder... would you like to do a quick search? Y/N");
        
        string? response = Console.ReadLine();
        if (response != null && response.ToUpper() == "Y")
        {
            var searchResult = synchronizer.SearchForGameFolder();
            if (searchResult.IsSuccess)
            {
                dvDirectory = new DirectoryInfo(synchronizer.SyncConfiguration.SaveLocation);
                Log.Information("Success! Found the game folder at: {0}", config.SaveLocation);
            }
            else
            {
                Helpers.Quit(ErrorCodes.CouldNotFindPath, $"Error could not find savegame directory: '{config.SaveLocation}'");
            }
        }
        else
        {
            Helpers.Quit(ErrorCodes.CouldNotFindPath, $"Error cannot continue without savegame directory location.");
        }
    }

    // Check for valid upload location
    var uploadDir = new DirectoryInfo(config.UploadLocation);
    if (!uploadDir.Exists)
    {
        Log.Warning("The upload directory '{0}' does not exist.", uploadDir.FullName);
        Console.WriteLine("Upload location does not exist, create directory? Y/N");
        string? response = Console.ReadLine();
        if (response != null && response.ToUpper() == "Y")
        {
            uploadDir.Create();
            Log.Information("Created upload directory: {0}", uploadDir);
        }
        else
        {
            Helpers.Quit(ErrorCodes.CouldNotFindPath, "No upload location; cannot continue. Exiting...");
        }
    }

    // Get savegame file list
    var saveGames = dvDirectory.GetFiles("savegame*");
    string? localSavegame = "";

    if (saveGames != null && saveGames.Length > 0)
    {
#pragma warning disable CS8602 // Dereference of a possibly null reference.
        localSavegame = saveGames.FirstOrDefault(s => s.Name == "savegame").FullName;
#pragma warning restore CS8602 // Dereference of a possibly null reference.
        if (!string.IsNullOrEmpty(localSavegame))
        {
            synchronizer.LocalSavePath = localSavegame;
            Log.Information("Found savegames!");
        }
    }
    else
    {
        Console.Write("The savegame folder is empty...would you like to download your files from the remote location?");
        var usrResponse = Console.ReadLine();
        if (!string.IsNullOrEmpty(usrResponse) && usrResponse.ToUpper() == "Y")
        {
            var downloadResult = synchronizer.DownloadRemoteToLocal();
            if (downloadResult.IsSuccess == false)
            {
                Log.Error("Could not download remote savegame to local folder. {0}", downloadResult);
            }
            else 
            { 
                Log.Information("Savegame has been updated from [newer] Upload savegame successfully!"); 
            }
        }

        Log.Error("No savegame files found! Is the game directory location correct?");
        Helpers.Quit(ErrorCodes.CouldNotFindSaveFiles);
    }

    // Backup local saves if desired
    var localFileinfo = new FileInfo(synchronizer.LocalSavePath);
    switch(config.BackupOption)
    {
        case BackupPreference.AlwaysBackup:
            var backupResult = Synchronizer.BackupSaveFile(config);
            if (backupResult.IsSuccess == false)
            {
                Log.Error("There was an error backing up the files. {0}", backupResult);
                Helpers.Quit(ErrorCodes.ErrorDuringOperations);
            }
            break;
        case BackupPreference.AskForBackup:
            Console.WriteLine("Would you like to backup the current savegame? Y/N");
#pragma warning disable CS8602 // Dereference of a possibly null reference.
            string? usrResponse = Console.ReadLine().Trim();
#pragma warning restore CS8602 // Dereference of a possibly null reference.
            if (usrResponse != null && usrResponse.ToUpper() == "Y")
            {
                // Make a backup copy of the file
                var askResult = Synchronizer.BackupSaveFile(config);

                if (askResult.IsSuccess == false)
                {
                    Log.Error("There was an error backing up the files. {0}", askResult);
                    Helpers.Quit(ErrorCodes.ErrorDuringOperations);
                }
            }
            break;
        case BackupPreference.DoNotBackup:
        case BackupPreference.OnlyInDanger:
            Log.Information("Redundant backups not desired, moving on...");
            break;
        default:
            Log.Warning("Invalid backup option, skipping... consider checking the config file for corruption.");
            break;
    }

    // Check if there is a savegame file in the upload location
    var uploadSavegame = Path.Combine(config.UploadLocation, "savegame");
    var uploadFileinfo = new FileInfo(uploadSavegame);
    synchronizer.RemoteSavePath = uploadSavegame;

    if (!File.Exists(uploadSavegame))
    {
        Log.Information("No upload save found, copying local savegame to upload destination...");
        
        var pushResult = synchronizer.PushLocalToRemote();
        if (pushResult.IsSuccess == false)
        {
            Log.Error("Error copying local file to remote locaion. {0}", pushResult);
        }
    }
    else
    {
        switch (synchronizer.GetLocalSavegameSyncState())
        {
            case SyncCompareState.LocalIsNewer:
                Log.Information("Local savegame is newer than upload savegame. Copying local save to Upload location...");
                var pushResult = synchronizer.PushLocalToRemote();
                if (pushResult.IsSuccess == false)
                {
                    Log.Error("Could not copy local to remote. Please check logs for error. {0}", pushResult);
                }
                else
                {
                    Log.Information("Savegame has been copied to Upload location successfully!");
                }
                break;
            case SyncCompareState.LocalIsOlder:
                Log.Information("Local save is older, copying would overwrite newer savegame...");
                var downloadResult = synchronizer.DownloadRemoteToLocal();
                if (downloadResult.IsSuccess == false)
                {
                    Log.Error("Could not download remote savegame to local folder, check the logs for errors. {0}", downloadResult);
                }
                else
                {
                    Log.Information("Savegame has been updated from [newer] Upload savegame successfully!");
                }
                break;
            case SyncCompareState.LocalIsSame:
                Log.Information("Local and upload savegame files are the same! Nothing to do here.");
                break;
            case SyncCompareState.LocalIsMissing:
                Log.Warning("Local savegame files are missing! Please double check configs and folder locations.");
                break;
            default:
                Log.Error("I'm not sure how you got here... but it's probably not good. Check the logs for errors.");
                break;
        }
    }
    Configuration.SaveConfiguration(configFilepath, synchronizer.SyncConfiguration);

    Log.Information("DVSaveSync has completed.");
    if (config.KeepAlive)
    {
        Console.WriteLine("Press Enter to close.");
        Console.ReadLine();
    }

    Helpers.Quit(ErrorCodes.NoErrors);
}
catch (Exception ex)
{
    Log.Error(ex, "An unrecoverable error occurred while running.");
    Helpers.Quit(ErrorCodes.ErrorDuringOperations);
}