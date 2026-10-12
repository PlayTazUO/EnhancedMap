using System;
using System.IO;
using System.Net;
using EnhancedMapServerNetCore.Configuration;
using EnhancedMapServerNetCore.Internals;
using EnhancedMapServerNetCore.Logging;

namespace EnhancedMapServerNetCore.Managers
{
    /// <summary>
    ///     Prompts for the information required by Server.xml, Rooms.xml and Accounts.xml
    ///     when one or more of those files are missing, so the server can be started for the
    ///     first time without having to create the configuration by hand.
    /// </summary>
    public static class SetupManager
    {
        public static void Setup()
        {
            string folderPath = Path.Combine(Core.RootPath, "Data");

            bool serverMissing = !File.Exists(Path.Combine(folderPath, "Server.xml"));
            bool roomsMissing = !File.Exists(Path.Combine(folderPath, "Rooms.xml"));
            bool accountsMissing = !File.Exists(Path.Combine(folderPath, "Accounts.xml"));

            if (!serverMissing && !roomsMissing && !accountsMissing)
                return;

            if (Core.HeadLess || Console.IsInputRedirected)
            {
                Log.Message(LogTypes.Warning, "Missing configuration file(s) and no interactive console available. Using defaults.");

                if (serverMissing)
                    SettingsManager.Save();
                if (roomsMissing)
                {
                    if (RoomManager.Get("General") == null)
                        RoomManager.Add("General", "general");
                    RoomManager.Save();
                }
                if (accountsMissing)
                {
                    Log.Message(LogTypes.Warning, "No accounts configured. Use the console '/adduser' command to create one.");
                    AccountManager.Save();
                }

                return;
            }

            Log.Message(LogTypes.None, "");
            Log.Message(LogTypes.None, "Some configuration files are missing. Please answer the following questions.");
            Log.Message(LogTypes.None, "Press Enter to accept the default shown between [ ].");
            Log.Message(LogTypes.None, "");

            if (serverMissing)
                SetupServer();

            string defaultRoom = RoomManager.Get("General") != null ? "General" : null;
            if (roomsMissing)
                defaultRoom = SetupRooms(defaultRoom);

            if (accountsMissing)
                SetupAccount(defaultRoom);

            if (serverMissing)
                SettingsManager.Save();
            if (roomsMissing)
                RoomManager.Save();
            if (accountsMissing)
                AccountManager.Save();

            Log.Message(LogTypes.None, "");
            Log.Message(LogTypes.None, "Configuration saved. Starting server...");
            Log.Message(LogTypes.None, "");
        }

        private static void SetupServer()
        {
            Config config = SettingsManager.Configuration;

            Log.Message(LogTypes.None, "-- Server settings --");

            config.Port = ReadUShort("Server port", config.Port, IPEndPoint.MinPort, IPEndPoint.MaxPort);
            config.KickTimer = ReadUInt("Kick timer in seconds", config.KickTimer, 1);
            config.CredentialsSystem = (CREDENTIAL_SYSTEM) ReadUShort(
                "Credentials system [0 = username + password, 1 = password only]",
                (ushort) config.CredentialsSystem, 0, 1);
            config.MaxSimultaneConnections = ReadUShort(
                "Max simultaneous connections per user", config.MaxSimultaneConnections, 1, ushort.MaxValue);
            config.MaxActiveConnections = ReadUShort(
                "Max active connections", config.MaxActiveConnections, 1, ushort.MaxValue);

            Log.Message(LogTypes.None, "");
        }

        private static string SetupRooms(string defaultRoom)
        {
            Log.Message(LogTypes.None, "-- Room settings --");

            string name = ReadString("Room name", defaultRoom ?? "General");

            Room existing = RoomManager.Get(name);
            string password = existing != null ? existing.Password : string.Empty;

            if (SettingsManager.Configuration.CredentialsSystem == CREDENTIAL_SYSTEM.ONLY_PASSWORD)
                password = ReadString("Room password", password);

            if (existing == null)
                RoomManager.Add(name, password);
            else
                existing.Password = password;

            Log.Message(LogTypes.None, "");
            return name;
        }

        private static void SetupAccount(string defaultRoom)
        {
            Log.Message(LogTypes.None, "-- Administrator account --");
            Log.Message(LogTypes.None, "At least one account is required to connect to the server.");

            string username = ReadUsername();
            string password = ReadPassword();
            ushort level = ReadUShort(
                "Account level [0 = Normal, 1 = RoomAdmin, 2 = ServerAdmin]", 2, 0, 2);
            string room = ReadString("Room", defaultRoom ?? "General");

            if (RoomManager.Get(room) == null)
            {
                Log.Message(LogTypes.Warning, $"Room '{room}' not found. Using '{defaultRoom ?? "General"}'.");
                room = defaultRoom ?? "General";
            }

            AccountManager.Add(username, password, room, (ACCOUNT_LEVEL) level);

            Log.Message(LogTypes.None, "");
        }

        private static string ReadUsername()
        {
            while (true)
            {
                string input = ReadString("Username", string.Empty);

                if (string.IsNullOrEmpty(input))
                    Console.WriteLine("Username is required.");
                else if (input.Length > 16)
                    Console.WriteLine("Max username length is 16 chars.");
                else if (!Utility.IsStringAllowed(input))
                    Console.WriteLine("Some characters are not allowed in the username.");
                else
                    return input;
            }
        }

        private static string ReadPassword()
        {
            while (true)
            {
                string input = ReadString("Password", string.Empty);

                if (string.IsNullOrEmpty(input))
                    Console.WriteLine("Password is required.");
                else if (input.Length > 16)
                    Console.WriteLine("Max password length is 16 chars.");
                else if (!Utility.IsStringAllowed(input))
                    Console.WriteLine("Some characters are not allowed in the password.");
                else
                    return input;
            }
        }

        private static string ReadString(string label, string defaultValue)
        {
            Console.Write($"{label} [{defaultValue}]: ");
            string input = Console.ReadLine();

            return string.IsNullOrWhiteSpace(input) ? defaultValue : input.Trim();
        }

        private static ushort ReadUShort(string label, ushort defaultValue, int min, int max)
        {
            while (true)
            {
                Console.Write($"{label} [{defaultValue}]: ");
                string input = Console.ReadLine();

                if (string.IsNullOrWhiteSpace(input))
                    return defaultValue;

                if (ushort.TryParse(input.Trim(), out ushort value) && value >= min && value <= max)
                    return value;

                Console.WriteLine($"Please enter a number between {min} and {max}.");
            }
        }

        private static uint ReadUInt(string label, uint defaultValue, uint min)
        {
            while (true)
            {
                Console.Write($"{label} [{defaultValue}]: ");
                string input = Console.ReadLine();

                if (string.IsNullOrWhiteSpace(input))
                    return defaultValue;

                if (uint.TryParse(input.Trim(), out uint value) && value >= min)
                    return value;

                Console.WriteLine($"Please enter a number greater than or equal to {min}.");
            }
        }
    }
}
