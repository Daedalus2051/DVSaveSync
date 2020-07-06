using Newtonsoft.Json;
using System;
using System.IO;

namespace DVSaveSync
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
            if (string.IsNullOrEmpty(path)) throw new ArgumentNullException("path");
            if (!Directory.Exists(path)) throw new DirectoryNotFoundException();

            try
            {
                Configuration config = new Configuration
                {
                    //C:\Program Files (x86)\Steam\steamapps\common\Derail Valley
                    //C:\Program Files (x86)\Steam\steamapps\common\Derail Valley\DerailValley_Data\SaveGameData
                    SaveLocation = @"C:\Program Files (x86)\Steam\steamapps\common\Derail Valley\DerailValley_Data\SaveGameData",
                    UploadLocation = $"{Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)}\\DVSaveSync",

                    BackupOption = BackupPreference.OnlyInDanger,
                    LastUpdated = new DateTime(2020, 5, 31, 21, 17, 34),
                    IncludeBackupSaveFiles = true,
                    IncludeDVBackupSaveFiles = false,
                    AllowDownloadSavegame = true,
                    KeepAlive = true
                };

                string configJson = JsonConvert.SerializeObject(config);
                File.WriteAllText($"{path}\\config.json", configJson);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error while generating settings config!{Environment.NewLine}{ex.Message}");
            }
        }
        /*
        /// <summary>
        /// Generates the configuration for the log4net package.
        /// </summary>
        /// <param name="path">Path to the directory to save the file, NO trailing slashes.</param>
        public static void GenerateLoggingConfig(string path)
        {
            if (string.IsNullOrEmpty(path)) throw new ArgumentNullException("path");
            if (!Directory.Exists(path)) throw new DirectoryNotFoundException();

            try
            {
                using (StreamReader logDefaultStream = new StreamReader(Assembly.GetExecutingAssembly().GetManifestResourceStream("DVSaveSync.Log4NetTemplate.txt")))
                {
                    string logDefaults = logDefaultStream.ReadToEnd();
                    File.WriteAllText($"{path}\\log4net.config", logDefaults);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error while generating log config!{Environment.NewLine}{ex.Message}");
            }
        }
        */
    }
}
