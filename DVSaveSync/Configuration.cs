using System;
using Newtonsoft.Json;
using System.IO;

namespace DVSaveSync
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
        /// Determines if the additional "_backup..." savegame files should be copied as well.
        /// </summary>
        [JsonProperty("IncludeBackupSaveFiles")]
        public bool IncludeBackupSaveFiles { get; set; }

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

        public bool BackupSaveFile()
        {
            bool result = false;
            try
            {
                File.Copy($"{SaveLocation}\\savegame", $"{SaveLocation}\\savegame-dvss_{DateTime.Now:yyyy-dd-M-HH-mm-ss}.bak");
                result = true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Configuration.BackupSaveFile]::Error trying to backup the file!{Environment.NewLine}{ex.Message}");
            }
            return result;
        }

        /// <summary>
        /// Loads a file containing valid json representing the Configuration object.
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns>Configuration object loaded from the json data, if data cannot be read returns new Configuration object.</returns>
        public static Configuration LoadConfiguration(string filePath)
        {
            if (string.IsNullOrEmpty(filePath)) throw new ArgumentNullException("filePath");
            if (!File.Exists(filePath)) throw new FileNotFoundException($"Error: could not find file '{filePath}'");

            Configuration config = new Configuration();
            try
            {
                config = JsonConvert.DeserializeObject<Configuration>(File.ReadAllText(filePath));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Configuration.LoadConfiguration]::Error trying to open configuration file!{Environment.NewLine}{ex.Message}");
            }

            return config;
        }

        /// <summary>
        /// If no config file exists, this method can be used 
        /// </summary>
        public static void GenerateDefaultConfiguration(string path)
        {
            Configuration config = new Configuration
            {
                //C:\Program Files (x86)\Steam\steamapps\common\Derail Valley
                //C:\Program Files (x86)\Steam\steamapps\common\Derail Valley\DerailValley_Data\SaveGameData
                SaveLocation = @"C:\Program Files (x86)\Steam\steamapps\common\Derail Valley\DerailValley_Data\SaveGameData",
                UploadLocation = $"{Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)}\\DVSaveSync",

                BackupOption = BackupPreference.OnlyInDanger,
                LastUpdated = new DateTime(2020, 5, 31, 21, 17, 34),
                IncludeBackupSaveFiles = false,
                AllowDownloadSavegame = true,
                KeepAlive = true
            };

            string configJson = JsonConvert.SerializeObject(config);
            File.WriteAllText($"{path}\\config.json", configJson);
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
