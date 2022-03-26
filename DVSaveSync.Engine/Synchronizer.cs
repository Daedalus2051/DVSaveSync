using DVSaveSync.Common;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;

namespace DVSaveSync.Engine
{
    public class Synchronizer
    {
        public string LocalSavePath { get; set; }
        public string RemoteSavePath { get; set; }
        public Configuration SyncConfiguration { get; set; }

        public Synchronizer(Configuration config) => SyncConfiguration = config ?? throw new ArgumentNullException("SyncConfiguration");

        public Synchronizer(string localFile, string remoteFile)
        {
            LocalSavePath = localFile;
            RemoteSavePath = remoteFile;

            AmIValid("ctor(string localFile, string remoteFile)");
        }
        /// <summary>
        /// Internal aggregation of guard clauses.
        /// </summary>
        private void AmIValid(string caller, bool omitRemoteCheck = false)
        {
            // TODO: refactor this
            Exception failed = null;
            Log.Debug("Checking validity for: {0}", caller);

            if (string.IsNullOrEmpty(LocalSavePath)) 
            { 
                failed = new ArgumentNullException(nameof(LocalSavePath));
                Log.Error(failed, "Data validation check failed!");
            }
            if (string.IsNullOrEmpty(RemoteSavePath)) 
            { 
                failed = new ArgumentNullException(nameof(RemoteSavePath));
                Log.Error(failed, "Data validation check failed!");
            }
            if (!File.Exists(LocalSavePath))
            { 
                failed = new FileNotFoundException($"Local file not found: '{LocalSavePath}'");
                Log.Error(failed, "Data validation check failed!");
            }
            if (!omitRemoteCheck)
            {
                if (!File.Exists(RemoteSavePath))
                { 
                    failed = new FileNotFoundException($"Remote file not found: '{RemoteSavePath}'");
                    Log.Error(failed, "Data validation check failed!");
                }
            }
            // throw the exception that failed
            if (failed != null)
            {
                throw new Exception("Data validation check failed!", failed);
            }
        }

        public SyncCompareState GetLocalSavegameSyncState()
        {
            AmIValid("GetLocalSavegameSyncState");
            string localFile = LocalSavePath;
            string remoteFile = RemoteSavePath;

            SyncCompareState syncState;

            if (File.Exists(localFile) == false)
            {
                syncState = SyncCompareState.LocalIsMissing;
                return syncState;
            }

            DateTime localSaveDate = new FileInfo(localFile).LastWriteTime;
            DateTime remoteSaveDate = new FileInfo(remoteFile).LastWriteTime;

            Log.Debug("localSaveDate-{0}", localSaveDate.ToShortDateString());
            Log.Debug("uploadSaveDate-{0}", remoteSaveDate.ToShortTimeString());

            //https://docs.microsoft.com/en-us/dotnet/api/system.datetime.compare?view=netcore-3.1
            int result = -1;
            result = DateTime.Compare(localSaveDate, remoteSaveDate);
            if (result < 0)
            {// relationship is earlier than
             // local save is earlier than upload save = local save is older
                syncState = SyncCompareState.LocalIsOlder;
            }
            else if (result == 0)
            {// relationship is the same time as
                syncState = SyncCompareState.LocalIsSame;
            }
            else
            {// relationship is later than
             // local save is later than upload save = local save is newer
                syncState = SyncCompareState.LocalIsNewer;
            }

            return syncState;
        }

        public OperationResult SearchForGameFolder()
        {
            OperationResult result = new();

            Log.Information("Searching for Derail Valley Steam folder location...");
            // List all drive letters
            DriveInfo[] allDrives = DriveInfo.GetDrives();
            List<string> potentialLocations = new();
            string Steam_Default_Location = Path.Combine("Program Files (x86)", "Steam", "steamapps", "common");
            string Steam_Custom_Location = Path.Combine("SteamLibrary", "steamapps", "common");

            // Populate them with the most common Steam location paths
            foreach (var item in allDrives)
            {
                potentialLocations.Add(Path.Combine(item.Name, Steam_Default_Location));
                potentialLocations.Add(Path.Combine(item.Name, Steam_Custom_Location));
            }

            // Iterate through complete list and check for DV folder
            foreach (var testPath in potentialLocations)
            {
                Log.Debug("Checking: '{0}'...", testPath);
                string locationPath = Path.Combine(testPath, "Derail Valley", "DerailValley_Data", "SaveGameData");
                if (Directory.Exists(locationPath))
                {
                    Log.Information("Found Derail Valley save game folder!");
                    SyncConfiguration.SaveLocation = locationPath;
                    result.AddMessage($"Found Derail Valley save game folder at: '{locationPath}'");
                    return result;
                }
            }
            result.AddFailureMessage("Unable to find folder location.");
            return result;
        }

        /// <summary>
        /// Copies the local savegame to the remote directory, overwriting the remote file.
        /// </summary>
        /// <returns>Returns true if copy succeeds, false for everything else.</returns>
        public OperationResult PushLocalToRemote()
        {
            AmIValid("PushLocalToRemote", true);
            OperationResult success = new();

            try
            {
                DisplaySyncInfo(LocalSavePath, RemoteSavePath);
                File.Copy(LocalSavePath, RemoteSavePath, true);
                if (SyncConfiguration.IncludeBackupSaveFiles) File.Copy($"{LocalSavePath}.bak", $"{RemoteSavePath}.bak", true);
                SyncConfiguration.LastUpdated = DateTime.Now;
                success.AddMessage("Local files have been copied to the desired remote location.");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Could not push local file to remote.");
                success.AddFailureMessage("Could not push local file to remote. Check Logs for more details.");
            }

            return success;
        }
        /// <summary>
        /// Copies the remote savegame file to the local directory, overwriting the local file.
        /// </summary>
        /// <returns>Returns true if copy succeeds, false for everything else.</returns>
        public OperationResult DownloadRemoteToLocal()
        {
            AmIValid("DownloadRemoteToLocal");

            OperationResult success = new();

            try
            {
                SyncCompareState currentState = GetLocalSavegameSyncState();
                if ((currentState == SyncCompareState.LocalIsMissing) == false)
                {
                    // Verify that local save is older
                    if ((currentState == SyncCompareState.LocalIsOlder) == false)
                    {
                        Log.Warning("Local file is NOT older than remote file... aborting download to preserve state.");
                        success.AddFailureMessage("Local file is NOT older than remote file... aborting download to preserve state.");
                        return success;
                    }
                }
                if (SyncConfiguration.AllowDownloadSavegame)
                {
                    if (SyncConfiguration.BackupOption == BackupPreference.OnlyInDanger)
                    {
                        Log.Information("Backing up local savegame, just in case... (this is because of your preferences)");
                        BackupSaveFile(SyncConfiguration);
                    }
                    
                    Log.Information("Copying newer savegame from upload location...");

                    DisplaySyncInfo(LocalSavePath, RemoteSavePath);
                    File.Copy(RemoteSavePath, LocalSavePath, true);
                    if (SyncConfiguration.IncludeBackupSaveFiles) 
                        File.Copy($"{RemoteSavePath}.bak", $"{LocalSavePath}.bak", true);
                    
                    success.AddMessage("Savegame has been updated from [newer] Upload savegame successfully!");
                    success.IsSuccess = true;
                    SyncConfiguration.LastUpdated = DateTime.Now;
                }
                else
                {
                    Log.Warning("Remote savegame is newer, but downloading is not allowed.");
                    success.AddFailureMessage("Remote savegame is newer, but downloading is not allowed.");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Could not download remote file to local.");
                success.AddFailureMessage($"Could not download remote file to local. Check Logs for error details.");
            }

            return success;
        }

        public static OperationResult BackupSaveFile(Configuration config)
        {
            var result = new OperationResult();
            try
            {
                File.Copy($"{config.SaveLocation}\\savegame", $"{config.SaveLocation}\\savegame-dvss_{DateTime.Now:yyyy-dd-M-HH-mm-ss}.bak");
                if (config.IncludeBackupSaveFiles) { File.Copy($"{config.SaveLocation}\\savegame.bak", $"{config.SaveLocation}\\savegame-dvss_{DateTime.Now:yyyy-dd-M-HH-mm-ss}_bak.bak"); }
                Log.Debug("Redundant savegame backup successful.");                
            }
            catch (Exception ex)
            {
                result.AddFailureMessage("An error occurred while trying to backup the file.");
                Log.Error(ex, "Error trying to backup the file!");
            }
            return result;
        }

        public static void DisplaySyncInfo(string localFile, string remoteFile)
        {
            FileInfo local = new(localFile);
            FileInfo upload = new(remoteFile);
            // TODO: figure out why the FileInfo.Length property is throwing an exception (something to do with no file extension?)
            Log.Information("Local file details: {0} - {1}", local.Name, local.LastWriteTime);
            Log.Information("Upload file details: {0} - {1}", upload.Name, upload.LastWriteTime);
        }
    }

    public enum SyncCompareState
    {
        LocalIsOlder,
        LocalIsSame,
        LocalIsNewer,
        LocalIsMissing
    }
}