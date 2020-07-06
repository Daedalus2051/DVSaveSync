using System;
using System.IO;

namespace DVSaveSync
{
    public class Synchronizer
    {
        // Create a logger for use in this class
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        public string LocalSavePath { get; set; }
        public string RemoteSavePath { get; set; }
        public Configuration SyncConfiguration { get; set; }

        public Synchronizer(Configuration config)
        {
            if (config == null) throw new ArgumentNullException("SyncConfiguration");
            SyncConfiguration = config;
        }
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
            log.Debug($"Checking validity for: {caller}");
            if (string.IsNullOrEmpty(LocalSavePath)) throw new ArgumentNullException("LocalSavePath");
            if (string.IsNullOrEmpty(RemoteSavePath)) throw new ArgumentNullException("RemoteSavePath");
            if (!File.Exists(LocalSavePath)) throw new FileNotFoundException($"Local file not found: {LocalSavePath}");
            if (!omitRemoteCheck)
            {
                if (!File.Exists(RemoteSavePath)) throw new FileNotFoundException($"Remote file not found: {RemoteSavePath}");
            }
        }

        public SyncCompareState GetLocalSavegameSyncState()
        {
            AmIValid("GetLocalSavegameSyncState");
            string localFile = LocalSavePath;
            string remoteFile = RemoteSavePath;

            SyncCompareState syncState;

            DateTime localSaveDate = new FileInfo(localFile).LastWriteTime;
            DateTime remoteSaveDate = new FileInfo(remoteFile).LastWriteTime;

            log.Debug($"localSaveDate-{localSaveDate.ToShortDateString()} {localSaveDate.ToShortTimeString()}");
            log.Debug($"uploadSaveDate-{remoteSaveDate.ToShortDateString()} {remoteSaveDate.ToShortTimeString()}");

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
        /// <summary>
        /// Copies the local savegame to the remote directory, overwriting the remote file.
        /// </summary>
        /// <returns>Returns true if copy succeeds, false for everything else.</returns>
        public bool PushLocalToRemote()
        {
            AmIValid("PushLocalToRemote", true);
            bool success = false;

            try
            {
                DisplaySyncInfo(LocalSavePath, RemoteSavePath);
                File.Copy(LocalSavePath, RemoteSavePath, true);
                if (SyncConfiguration.IncludeBackupSaveFiles) File.Copy($"{LocalSavePath}.bak", $"{RemoteSavePath}.bak", true);
                success = true;
                SyncConfiguration.LastUpdated = DateTime.Now;
            }
            catch (Exception ex)
            {
                log.Error($"Could not push local file to remote.{Environment.NewLine}{ex.Message}");                
            }

            return success;
        }
        /// <summary>
        /// Copies the remote savegame file to the local directory, overwriting the local file.
        /// </summary>
        /// <returns>Returns true if copy succeeds, false for everything else.</returns>
        public bool DownloadRemoteToLocal()
        {
            AmIValid("DownloadRemoteToLocal");

            bool success = false;

            try
            {   
                // Verify that local save is older
                if ((GetLocalSavegameSyncState() == SyncCompareState.LocalIsOlder) == false)
                {
                    log.Warn($"Local file is NOT older than remote file... aborting download to preserve state.");
                    return false;
                }
                if (SyncConfiguration.AllowDownloadSavegame)
                {
                    if (SyncConfiguration.BackupOption == BackupPreference.OnlyInDanger)
                    {
                        log.Info("Backing up local savegame, just in case... (this is because of your preferences)");
                        SyncConfiguration.BackupSaveFile();
                    }
                    log.Info("Copying newer savegame from upload location...");
                    DisplaySyncInfo(LocalSavePath, RemoteSavePath);
                    File.Copy(RemoteSavePath, LocalSavePath, true);
                    if (SyncConfiguration.IncludeBackupSaveFiles) File.Copy($"{RemoteSavePath}.bak", $"{LocalSavePath}.bak", true);
                    //log.Info("Savegame has been updated from [newer] Upload savegame successfully!");
                    success = true;
                    SyncConfiguration.LastUpdated = DateTime.Now;
                }
                else
                {
                    log.Warn($"Remote savegame is newer, but downloading is not allowed.");
                }
            }
            catch (Exception ex)
            {
                log.Error($"Could not download remote file to local.{Environment.NewLine}{ex.Message}");
            }

            return success;
        }

        public static void DisplaySyncInfo(string localFile, string remoteFile)
        {
            FileInfo local = new FileInfo(localFile);
            FileInfo upload = new FileInfo(remoteFile);
            // TODO: figure out why the FileInfo.Length property is throwing an exception (something to do with no file extension?)
            log.Info($"Local file details: {local.Name} - {local.LastWriteTime}");
            log.Info($"Upload file details: {upload.Name} - {upload.LastWriteTime}");
        }
    }

    public enum SyncCompareState
    {
        LocalIsOlder,
        LocalIsSame,
        LocalIsNewer
    }
}