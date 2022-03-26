using Serilog;

namespace DVSaveSync.Common
{
    public static class Helpers
    {
        public static void SetLogger()
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.File("logs/DVSaveSync.log", rollingInterval: RollingInterval.Day)
                .WriteTo.Console(outputTemplate: "[{Level:u3}]>{Message:0j}{NewLine}")
                .CreateLogger();
        }
        public static void Quit(ErrorCodes errorCode, string message = "")
        {
            bool isMessageEmpty = string.IsNullOrEmpty(message);

            // Handles default messages if none are provided.
            switch (errorCode)
            {
                case ErrorCodes.NoErrors:
                    if (isMessageEmpty) Log.Information("Exiting normally.");
                    break;
                case ErrorCodes.CouldNotFindPath:
                    if (isMessageEmpty) Log.Error("There was a problem finding a path.");
                    break;
                case ErrorCodes.CouldNotFindSaveFiles:
                    if (isMessageEmpty) Log.Error("There was a problem trying to find the save files for Derail Valley.");
                    break;
                case ErrorCodes.ErrorDuringOperations:
                    if (isMessageEmpty) Log.Error("There was an error while trying to process operations. Please check the logs for more details.");
                    break;
                default:
                    if (isMessageEmpty) Log.Warning("Unspecified error code, this is abnormal. Please check the logs for any additional errors.");
                    break;
            }
            if (!isMessageEmpty && errorCode == ErrorCodes.NoErrors) Log.Information(message);
            else if (!isMessageEmpty) Log.Error(message);

            Environment.Exit((int)errorCode);
        }
    }

    public enum ErrorCodes
    {
        NoErrors = 0,
        CouldNotFindSaveFiles = 1,
        CouldNotFindPath = 2,
        ErrorDuringOperations = 3
    }
}