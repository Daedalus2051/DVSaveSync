using Newtonsoft.Json;
using Serilog;
using System;
using System.IO;

namespace DVSaveSync.Engine
{
    /// <summary>
    /// Handles all of the code to generate potentially missing configuration files.
    /// </summary>
    public static class Generator
    {
        /// <summary>
        /// Generates the configuration for the DV Save Sync settings.
        /// </summary>
        /// <param name="path">Path to the directory to save the file, NO trailing slashes.</param>
        public static void CreateDefaultDVSSConfiguration(string path)
        {
            if (string.IsNullOrEmpty(path)) throw new ArgumentNullException(nameof(path));
            if (!Directory.Exists(path)) throw new DirectoryNotFoundException();

            try
            {
                Configuration config = new()
                {
                    SaveLocation = @"C:\Program Files (x86)\Steam\steamapps\common\Derail Valley\DerailValley_Data\SaveGameData",
                    UploadLocation = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), 
                        "DVSaveSync", "Saves"
                    ),

                    BackupOption = BackupPreference.OnlyInDanger,
                    LastUpdated = new DateTime(2020, 5, 31, 21, 17, 34),
                    IncludeBackupSaveFiles = true,
                    IncludeDVBackupSaveFiles = false,
                    AllowDownloadSavegame = true,
                    KeepAlive = true
                };

                string configJson = JsonConvert.SerializeObject(config, Formatting.Indented);
                File.WriteAllText(Path.Combine(path, "config.json"), configJson);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error while generating settings config!");
            }
        }
    }
}
