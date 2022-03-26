// See https://aka.ms/new-console-template for more information
using Serilog;
using System.Reflection;
using Updater;

try
{
    Log.Logger = new LoggerConfiguration()
        .MinimumLevel.Debug()
        .WriteTo.File("DVSaveSync-Updater.log", rollingInterval: RollingInterval.Day)
        .WriteTo.Console(outputTemplate: "[{Level:u3}]>{Message:0j}{NewLine}")
        .CreateLogger();

    Log.Information("Updater - {0}", Assembly.GetExecutingAssembly().GetName().Version);
    
    // Need an option for creating manifests...

    // Check/load local manifest?
    Log.Information("Checking for manifest...");

    Manifest currentManifest;

    if (File.Exists("manifest.json"))
    {
        currentManifest = Manifest.Load("manifest.json");
        if (currentManifest == null) currentManifest = new();
    }

    // Check version information (look for an updated version)

    // if there IS a new version
    // Get/download updated manifest

}
catch (Exception ex)
{
    Log.Error(ex, "An error occurred while trying to update.");
}