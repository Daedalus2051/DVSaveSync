using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Updater
{
    internal class Manifest
    {
        public string? Version { get; set; }
        public List<string>? Files { get; set; }
        public string? DestinationFolder { get; set; }

        public static Manifest Load(string path)
        {
            if (string.IsNullOrEmpty(path)) throw new ArgumentNullException(nameof(path));
            if (!File.Exists(path)) throw new FileNotFoundException(nameof(path));

            var jsonData = File.ReadAllText(path);
            Manifest? loadedManifest = JsonConvert.DeserializeObject<Manifest>(jsonData);

            return loadedManifest;
        }

        public static void Save(Manifest manifest, string path)
        {
            if (manifest == null) throw new ArgumentNullException(nameof(manifest));
            if (string.IsNullOrEmpty(path)) throw new ArgumentException("Cannot be null or empty.", nameof(path));            

            var jsonData = JsonConvert.SerializeObject(manifest, Formatting.Indented);
            File.WriteAllText(path, jsonData);
        }
    }
}
