using System;
using System.Net;

namespace EnhancedMap.Core.Network
{
    /// <summary>
    ///     Lightweight, cross-platform update check. The original also downloaded and swapped the
    ///     executable via WinForms dialogs; UI/install concerns now belong to the Avalonia layer.
    /// </summary>
    public static class Update
    {
        public static void CheckUpdates()
        {
            try
            {
                using (var client = new WebClient {Proxy = null})
                {
                    string result = client.DownloadString("http://razorenhanced.org/download/Version-EM.txt");
                    var checkedVersion = new Version(result);

                    if (checkedVersion > MainCore.MapVersion)
                    {
                        Console.WriteLine("New version available: {0}", checkedVersion);
                        Diagnostic.Logger.Warn($"New version {checkedVersion} is available.");
                    }
                    else
                    {
                        Console.WriteLine("EnhancedMap is already running latest version.");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Failed to check for updates: " + ex.Message);
            }
        }
    }
}
