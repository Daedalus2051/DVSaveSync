using Newtonsoft.Json;
using Serilog;
using System;
using System.IO;

namespace DVSaveSync.Engine
{
    [Serializable]
    public class Configuration
    {
        /// <summary>
        /// Where the DV save game data is located.
        /// </summary>
        [JsonProperty("SaveLocation")]
        public string SaveLocation { get; set; }

        /// <summary>
        /// Where the saves will be "uploaded" to, for now this will be limited to local/network storage.
        /// </summary>
        [JsonProperty("UploadLocation")]
        public string UploadLocation { get; set; }

        /// <summary>
        /// Preference for making a backup of the savegame files.
        /// </summary>
        [JsonProperty("BackupOption")]
        public BackupPreference BackupOption { get; set; }

        /// <summary>
        /// The last time the save files were updated, to ensure overwriting does not occur.
        /// </summary>
        [JsonProperty("LastUpdated")]
        public DateTime LastUpdated { get; set; }

        /// <summary>
        /// Determines if the built-in .bak [for the current savegame file] also gets backed up (this applies to downloading remote saves too).
        /// </summary>
        [JsonProperty("IncludeBackupSaveFiles")]
        public bool IncludeBackupSaveFiles { get; set; }

        /// <summary>
        /// Determines if the additional "_backup..." savegame files should be copied as well.
        /// </summary>
        [JsonProperty("IncludeDVBackupSaveFiles")]
        public bool IncludeDVBackupSaveFiles { get; set; }

        /// <summary>
        /// Allow the program to copy a newer savegame file from the upload location to the local (overwriting the local savegame file).
        /// </summary>
        [JsonProperty("AllowDownloadSavegame")]
        public bool AllowDownloadSavegame { get; set; }

        /// <summary>
        /// Keep the program running until the user presses Enter.
        /// </summary>
        [JsonProperty("KeepAlive")]
        public bool KeepAlive { get; set; }

        /// <summary>
        /// Loads a file containing valid json representing the Configuration object.
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns>Configuration object loaded from the json data, if data cannot be read returns new Configuration object.</returns>
        public static Configuration LoadConfiguration(string filePath)
        {
            if (string.IsNullOrEmpty(filePath)) throw new ArgumentNullException(nameof(filePath));
            if (!File.Exists(filePath)) throw new FileNotFoundException($"Error: could not find file '{filePath}'");

            Configuration config = new();
            try
            {
                config = JsonConvert.DeserializeObject<Configuration>(File.ReadAllText(filePath));
                Log.Debug("Successfully loaded config from: {0}", filePath);
                Log.Debug("Local save path: {0}", config.SaveLocation);
                Log.Debug("Upload path: {0}", config.UploadLocation);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error trying to open configuration file!");
            }

            return config;
        }

        public static void SaveConfiguration(string filePath, Configuration config)
        {
            try
            {
                Log.Information("Updating configuration...");
                string jsonConfig = JsonConvert.SerializeObject(config, Formatting.Indented);
                File.WriteAllText(filePath, jsonConfig);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error trying to save configuration file!");
            }
        }
    }

    public enum BackupPreference
    {
        DoNotBackup = 0,
        AskForBackup = 1,
        OnlyInDanger = 2,
        AlwaysBackup = 3
    }
}
