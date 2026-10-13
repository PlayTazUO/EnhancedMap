using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using EnhancedMap.Core;
using EnhancedMap.Core.MapObjects;
using EnhancedMap.Core.Network;
using EnhancedMap.Diagnostic;

namespace EnhancedMap.Desktop.Windows
{
    public partial class SettingsWindow : Window
    {
        private const string SETTINGS = "config.xml";
        private bool _loaded;

        public SettingsWindow()
        {
            InitializeComponent();

            AboutVersion.Text = $"Enhanced Map {MainCore.MapVersion}";

            SocketClient.Connected += (s, e) => Dispatcher.UIThread.Post(() => ConnectButton.Content = "Disconnect");
            SocketClient.Disconnected += (s, e) => Dispatcher.UIThread.Post(() => ConnectButton.Content = "Connect");
            SocketClient.Waiting += (s, e) => Dispatcher.UIThread.Post(() => ConnectButton.Content = "Cancel");

            Logger.MessageWrited += OnLogMessage;

            BuildLabelsTab();
            BuildServerCommTab();

            LoadSettings();

            Closing += (s, e) =>
            {
                e.Cancel = true;
                Hide();
            };
        }

        public void ApplyFps() { }

        /// <summary>
        ///     Copies the current control values into <see cref="Global.SettingsCollection" /> without
        ///     writing to disk. Used so connecting from the main menu honors unsaved edits.
        /// </summary>
        public void ApplySettings()
        {
            ApplyToCollection();
        }

        /// <summary>
        ///     Brings the settings window to the front on the Network tab and shows a
        ///     connection error message.
        /// </summary>
        public void ShowConnectionError(Window owner, string message)
        {
            ClearConnectionError();
            ConnectionErrorText.Text = message;
            ConnectionErrorText.IsVisible = true;
            Tabs.SelectedItem = NetworkTab;

            if (!IsVisible)
                Show(owner);

            Activate();
        }

        public void ClearConnectionError()
        {
            ConnectionErrorText.IsVisible = false;
        }

        // ---------------------------------------------------------------- load

        public void LoadSettings()
        {
            _loaded = false;

            XmlElement root = null;
            if (File.Exists(SETTINGS))
            {
                try
                {
                    var doc = new XmlDocument();
                    doc.Load(SETTINGS);
                    root = doc["data"];
                }
                catch
                {
                    root = null;
                }
            }

            if (root != null)
            {
                XmlElement network = root["network"];
                if (network != null)
                {
                    Set(TextName, network, "username");
                    Set(TextPassword, network, "password");
                    CheckRememberCredentials.IsChecked = GetBool(network, "remembercredentials");
                    Set(TextIp, network, "ip");
                    Set(TextPort, network, "port");
                    CheckRememberServer.IsChecked = GetBool(network, "rememberserver");
                    CheckAutoLogin.IsChecked = GetBool(network, "autologin");
                }

                XmlElement general = root["general"];
                if (general != null)
                {
                    CheckShowHits.IsChecked = GetBool(general, "showhits");
                    CheckShowStamina.IsChecked = GetBool(general, "showstamina");
                    CheckShowMana.IsChecked = GetBool(general, "showmana");
                    CheckTrackDeathPoint.IsChecked = GetBool(general, "trackdeathpoint");
                    CheckShowHiddenIcon.IsChecked = GetBool(general, "showhiddenicon");
                    CheckShowDeathIcon.IsChecked = GetBool(general, "showdeathicon");
                    CheckAbbreviateNames.IsChecked = GetBool(general, "abbreviatenames");
                    CheckPanicSounds.IsChecked = GetBool(general, "panicsounds");
                    CheckAlertSounds.IsChecked = GetBool(general, "alertsounds");
                    CheckSmartNamePosition.IsChecked = GetBool(general, "smartnamesposition");
                    CheckHideLessImportantMarkers.IsChecked = GetBool(general, "hidelessimportantplaces");
                    CheckShowTownsNames.IsChecked = GetBool(general, "showtownsnames");
                    CheckShowGuardlines.IsChecked = GetBool(general, "showguardlines");
                    CheckShowCoordinates.IsChecked = GetBool(general, "showcoordinates");
                    CheckShowHouses.IsChecked = GetBool(general, "showhouses");
                    CheckDontRenderUsersDiffFacet.IsChecked = GetBool(general, "dontrenderusersindifferentfacets");
                    Global.SettingsCollection["centeruser"] = general.ToText("centeruser", Global.SettingsCollection["centeruser"].ToString());
                }

                XmlElement application = root["application"];
                if (application != null)
                {
                    CheckOpenChatAtStart.IsChecked = GetBool(application, "openchatatstart");
                    CheckShowFps.IsChecked = GetBool(application, "showfps");
                    decimal fps = GetInt(application, "fps");
                    NumFps.Value = fps < 1 ? 30 : fps;
                }

                XmlElement diagnostic = root["diagnostic"];
                if (diagnostic != null)
                    CheckAutoScroll.IsChecked = GetBool(diagnostic, "autoscroll");
            }
            else
            {
                LoadDefaults();
            }

            ApplyToCollection();

            RefreshCenterUsers();

            _loaded = true;
        }

        // ---------------------------------------------------------------- center on user

        private const string CenterNone = "(none)";
        private bool _loadingCenter;

        /// <summary>
        ///     Rebuilds the center-on-user list from currently connected users. Keeps the
        ///     remembered selection even if that user is offline so it auto-follows on reconnect.
        /// </summary>
        public void RefreshCenterUsers()
        {
            _loadingCenter = true;

            string current = Global.SettingsCollection["centeruser"]?.ToString() ?? string.Empty;

            ComboCenterUser.Items.Clear();
            ComboCenterUser.Items.Add(CenterNone);

            var names = RenderObjectsManager.Get<UserObject>().Where(u => u != null && !u.IsDisposing).Select(u => u.Name).Distinct().ToList();

            if (!string.IsNullOrEmpty(current) && !names.Contains(current))
                names.Add(current);

            foreach (string name in names)
                ComboCenterUser.Items.Add(name);

            ComboCenterUser.SelectedItem = string.IsNullOrEmpty(current) ? CenterNone : current;

            _loadingCenter = false;
        }

        private void OnCenterUserChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_loadingCenter || !(ComboCenterUser.SelectedItem is string name))
                return;

            if (name == CenterNone)
            {
                Global.CenterOn(null);
                return;
            }

            UserObject user = RenderObjectsManager.GetUser(name);
            if (user != null)
                Global.CenterOn(user);
            else
                Global.SettingsCollection["centeruser"] = name;
        }

        private void LoadDefaults()
        {
            var s = Global.SettingsCollection;
            TextName.Text = s["username"].ToString();
            TextPassword.Text = s["password"].ToString();
            CheckRememberCredentials.IsChecked = s["remembercredentials"].ToBool();
            TextIp.Text = s["ip"].ToString();
            TextPort.Text = s["port"].ToString();
            CheckRememberServer.IsChecked = s["rememberserver"].ToBool();
            CheckAutoLogin.IsChecked = s["autologin"].ToBool();

            CheckShowHits.IsChecked = s["showhits"].ToBool();
            CheckShowStamina.IsChecked = s["showstamina"].ToBool();
            CheckShowMana.IsChecked = s["showmana"].ToBool();
            CheckTrackDeathPoint.IsChecked = s["trackdeathpoint"].ToBool();
            CheckShowHiddenIcon.IsChecked = s["showhiddenicon"].ToBool();
            CheckShowDeathIcon.IsChecked = s["showdeathicon"].ToBool();
            CheckAbbreviateNames.IsChecked = s["abbreviatenames"].ToBool();
            CheckPanicSounds.IsChecked = s["panicsounds"].ToBool();
            CheckAlertSounds.IsChecked = s["alertsounds"].ToBool();
            CheckSmartNamePosition.IsChecked = s["smartnamesposition"].ToBool();
            CheckHideLessImportantMarkers.IsChecked = s["hidelessimportantplaces"].ToBool();
            CheckShowTownsNames.IsChecked = s["showtownsnames"].ToBool();
            CheckShowGuardlines.IsChecked = s["showguardlines"].ToBool();
            CheckShowCoordinates.IsChecked = s["showcoordinates"].ToBool();
            CheckShowHouses.IsChecked = s["showhouses"].ToBool();
            CheckDontRenderUsersDiffFacet.IsChecked = s["dontrenderusersindifferentfacets"].ToBool();

            CheckOpenChatAtStart.IsChecked = s["openchatatstart"].ToBool();
            CheckShowFps.IsChecked = s["showfps"].ToBool();
            NumFps.Value = s["fps"].ToInt();
            CheckAutoScroll.IsChecked = s["autoscroll"].ToBool();
        }

        // ---------------------------------------------------------------- save

        public void SaveSettings()
        {
            ApplyToCollection();

            var settings = new XmlWriterSettings {Indent = true, IndentChars = "\t"};
            using (XmlWriter xml = XmlWriter.Create(SETTINGS, settings))
            {
                xml.WriteStartDocument(true);
                xml.WriteStartElement("data");

                xml.WriteStartElement("network");
                xml.WriteAttributeString("username", TextName.Text ?? string.Empty);
                xml.WriteAttributeString("password", CheckRememberCredentials.IsChecked == true ? TextPassword.Text ?? string.Empty : string.Empty);
                xml.WriteAttributeString("remembercredentials", (CheckRememberCredentials.IsChecked == true).ToString());
                xml.WriteAttributeString("ip", CheckRememberServer.IsChecked == true ? TextIp.Text ?? string.Empty : string.Empty);
                xml.WriteAttributeString("port", TextPort.Text ?? string.Empty);
                xml.WriteAttributeString("rememberserver", (CheckRememberServer.IsChecked == true).ToString());
                xml.WriteAttributeString("autologin", (CheckAutoLogin.IsChecked == true).ToString());
                xml.WriteEndElement();

                xml.WriteStartElement("general");
                WriteCheck(xml, "showhits", CheckShowHits);
                WriteCheck(xml, "showstamina", CheckShowStamina);
                WriteCheck(xml, "showmana", CheckShowMana);
                xml.WriteAttributeString("centeruser", Global.SettingsCollection["centeruser"]?.ToString() ?? string.Empty);
                WriteCheck(xml, "trackdeathpoint", CheckTrackDeathPoint);
                WriteCheck(xml, "showhiddenicon", CheckShowHiddenIcon);
                WriteCheck(xml, "showdeathicon", CheckShowDeathIcon);
                WriteCheck(xml, "abbreviatenames", CheckAbbreviateNames);
                WriteCheck(xml, "panicsounds", CheckPanicSounds);
                WriteCheck(xml, "alertsounds", CheckAlertSounds);
                WriteCheck(xml, "smartnamesposition", CheckSmartNamePosition);
                WriteCheck(xml, "hidelessimportantplaces", CheckHideLessImportantMarkers);
                WriteCheck(xml, "showtownsnames", CheckShowTownsNames);
                WriteCheck(xml, "showguardlines", CheckShowGuardlines);
                WriteCheck(xml, "showcoordinates", CheckShowCoordinates);
                WriteCheck(xml, "showhouses", CheckShowHouses);
                WriteCheck(xml, "dontrenderusersindifferentfacets", CheckDontRenderUsersDiffFacet);
                xml.WriteEndElement();

                xml.WriteStartElement("application");
                WriteCheck(xml, "openchatatstart", CheckOpenChatAtStart);
                WriteCheck(xml, "showfps", CheckShowFps);
                xml.WriteAttributeString("fps", ((int) (NumFps.Value ?? 30)).ToString());
                xml.WriteEndElement();

                xml.WriteStartElement("diagnostic");
                WriteCheck(xml, "autoscroll", CheckAutoScroll);
                xml.WriteEndElement();

                xml.WriteEndElement();
            }

            Logger.Log("Settings saved.");
        }

        private void ApplyToCollection()
        {
            var s = Global.SettingsCollection;

            s["username"] = TextName.Text ?? "You";
            s["password"] = TextPassword.Text ?? string.Empty;
            s["remembercredentials"] = CheckRememberCredentials.IsChecked == true;
            s["ip"] = TextIp.Text ?? string.Empty;
            s["port"] = TextPort.Text ?? "8887";
            s["rememberserver"] = CheckRememberServer.IsChecked == true;
            s["autologin"] = CheckAutoLogin.IsChecked == true;

            s["showhits"] = CheckShowHits.IsChecked == true;
            s["showstamina"] = CheckShowStamina.IsChecked == true;
            s["showmana"] = CheckShowMana.IsChecked == true;
            s["trackdeathpoint"] = CheckTrackDeathPoint.IsChecked == true;
            s["showhiddenicon"] = CheckShowHiddenIcon.IsChecked == true;
            s["showdeathicon"] = CheckShowDeathIcon.IsChecked == true;
            s["abbreviatenames"] = CheckAbbreviateNames.IsChecked == true;
            s["panicsounds"] = CheckPanicSounds.IsChecked == true;
            s["alertsounds"] = CheckAlertSounds.IsChecked == true;
            s["smartnamesposition"] = CheckSmartNamePosition.IsChecked == true;
            s["hidelessimportantplaces"] = CheckHideLessImportantMarkers.IsChecked == true;
            s["showtownsnames"] = CheckShowTownsNames.IsChecked == true;
            s["showguardlines"] = CheckShowGuardlines.IsChecked == true;
            s["showcoordinates"] = CheckShowCoordinates.IsChecked == true;
            s["showhouses"] = CheckShowHouses.IsChecked == true;
            s["dontrenderusersindifferentfacets"] = CheckDontRenderUsersDiffFacet.IsChecked == true;

            s["openchatatstart"] = CheckOpenChatAtStart.IsChecked == true;
            s["showfps"] = CheckShowFps.IsChecked == true;
            s["fps"] = (int) (NumFps.Value ?? 30);
            s["autoscroll"] = CheckAutoScroll.IsChecked == true;
        }

        private static void WriteCheck(XmlWriter xml, string name, CheckBox box)
        {
            xml.WriteAttributeString(name, (box.IsChecked == true).ToString());
        }

        // ---------------------------------------------------------------- events

        private void OnOkClick(object sender, RoutedEventArgs e)
        {
            SaveSettings();
            Hide();
        }

        private void OnConnectClick(object sender, RoutedEventArgs e)
        {
            ClearConnectionError();

            if (NetworkManager.SocketClient.IsConnected || NetworkManager.SocketClient.Status == ConnectionStatus.Waiting)
            {
                NetworkManager.Disconnect(false);
            }
            else
            {
                ApplyToCollection();
                if (!NetworkManager.Connect())
                {
                    ConnectionErrorText.Text = "Failed to connect. Check the server address, port and credentials.";
                    ConnectionErrorText.IsVisible = true;
                }
            }
        }

        private void OnSaveLogClick(object sender, RoutedEventArgs e)
        {
            if (LogPanel.Children.Count == 0)
                return;

            File.AppendAllText("enhancedmap-log.txt", $"{DateTime.Now}: \r\nsettings log\r\n\r\n");
        }

        private void OnLogMessage(object sender, MessageLogger e)
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (LogPanel.Children.Count > 1000)
                    LogPanel.Children.Clear();

                var color = e.Severity switch
                {
                    MESSAGE_SEVERITY.GOOD => Colors.LimeGreen,
                    MESSAGE_SEVERITY.WARN => Colors.Yellow,
                    MESSAGE_SEVERITY.ERROR => Colors.Red,
                    _ => Colors.White
                };

                LogPanel.Children.Add(new TextBlock {Text = e.ComposeMsg(), Foreground = new SolidColorBrush(color)});

                if (CheckAutoScroll.IsChecked == true)
                    LogScroller.ScrollToEnd();
            });
        }

        // ---------------------------------------------------------------- xml helpers

        private static void Set(TextBox box, XmlElement node, string attr)
        {
            box.Text = node.ToText(attr, string.Empty);
        }

        private static bool GetBool(XmlElement node, string attr)
        {
            return node.ToText(attr, "False").ToBool();
        }

        private static int GetInt(XmlElement node, string attr)
        {
            return int.TryParse(node.ToText(attr, "0"), out int value) ? value : 0;
        }

        // ---------------------------------------------------------------- labels tab

        private BuildSet _labelsSelectedSet;

        private void BuildLabelsTab()
        {
            LabelsSetsPanel.Children.Clear();
            LabelsEntriesPanel.Children.Clear();

            foreach (BuildSet set in FilesManager.BuildSets)
            {
                var row = new StackPanel {Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 6};

                var check = new CheckBox {IsChecked = set.IsEnabled, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center};
                var captured = set;
                check.IsCheckedChanged += (s, e) => captured.IsEnabled = check.IsChecked == true;

                var open = new Button {Content = $"{set.Name} ({set.Entries.Count})", HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch};
                open.Click += (s, e) =>
                {
                    _labelsSelectedSet = captured;
                    ShowLabelEntries(captured);
                };

                row.Children.Add(check);
                row.Children.Add(open);
                LabelsSetsPanel.Children.Add(row);
            }

            if (_labelsSelectedSet != null)
                ShowLabelEntries(_labelsSelectedSet);
        }

        private void ShowLabelEntries(BuildSet set)
        {
            LabelsEntriesPanel.Children.Clear();

            if (set == null)
                return;

            LabelsEntriesPanel.Children.Add(new TextBlock {Text = set.Name, FontWeight = Avalonia.Media.FontWeight.Bold, Margin = new Avalonia.Thickness(0, 0, 0, 6)});

            foreach (BuildingEntry entry in set.Entries)
            {
                var row = new StackPanel {Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 8};

                var enabled = new CheckBox {IsChecked = entry.IsEnabled, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center};
                var capturedEntry = entry;
                enabled.IsCheckedChanged += (s, e) => capturedEntry.IsEnabled = enabled.IsChecked == true;

                var showName = new CheckBox {IsChecked = entry.ShowName, Content = "name", VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center};
                showName.IsCheckedChanged += (s, e) => capturedEntry.ShowName = showName.IsChecked == true;

                var text = new TextBlock {Text = $"{entry.Description}  ({entry.Location})  [map {entry.Map}]", VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center};

                row.Children.Add(enabled);
                row.Children.Add(showName);
                row.Children.Add(text);
                LabelsEntriesPanel.Children.Add(row);
            }
        }

        private void OnLabelsEnableAll(object sender, RoutedEventArgs e) => SetAllLabels(true);

        private void OnLabelsDisableAll(object sender, RoutedEventArgs e) => SetAllLabels(false);

        private void SetAllLabels(bool enabled)
        {
            foreach (BuildSet set in FilesManager.BuildSets)
            {
                set.IsEnabled = enabled;

                foreach (BuildingEntry entry in set.Entries)
                    entry.IsEnabled = enabled;
            }

            BuildLabelsTab();
        }

        private void OnLabelsShowNames(object sender, RoutedEventArgs e) => SetAllNames(true);

        private void OnLabelsHideNames(object sender, RoutedEventArgs e) => SetAllNames(false);

        private void SetAllNames(bool shown)
        {
            foreach (BuildSet set in FilesManager.BuildSets)
            foreach (BuildingEntry entry in set.Entries)
                entry.ShowName = shown;

            if (_labelsSelectedSet != null)
                ShowLabelEntries(_labelsSelectedSet);
        }

        private void OnLabelsRefresh(object sender, RoutedEventArgs e) => BuildLabelsTab();

        // ---------------------------------------------------------------- server commands tab

        private enum ServerArgKind
        {
            Text,
            Password,
            Combo
        }

        private sealed class ServerArg
        {
            public string Label;
            public bool Required;
            public string Watermark;
            public ServerArgKind Kind = ServerArgKind.Text;
            public string[] Options;
        }

        private sealed class ServerCommand
        {
            public string Name;
            public string Category;
            public string Description;
            public string Usage;
            public AccessLevel Required;
            public ServerArg[] Args = Array.Empty<ServerArg>();
        }

        private sealed class ServerArgField
        {
            public ServerArg Arg;
            public TextBox Text;
            public ComboBox Combo;
        }

        private static readonly string[] PrivilegeOptions = {"Normal", "Room admin", "Server admin"};
        private static readonly string[] LoginSystemOptions = {"Username + password", "Password only"};

        private static readonly ServerCommand[] ServerCommands =
        {
            // ---- Users
            new ServerCommand
            {
                Name = "adduser", Category = "Users", Required = AccessLevel.RoomAdmin,
                Description = "Create a new account.",
                Usage = "adduser <username> <password> <room> <privileges>",
                Args = new[]
                {
                    new ServerArg {Label = "Username", Required = true, Watermark = "up to 16 characters"},
                    new ServerArg {Label = "Password", Required = true, Kind = ServerArgKind.Password, Watermark = "up to 16 characters"},
                    new ServerArg {Label = "Room", Required = true, Watermark = "an existing room name"},
                    new ServerArg {Label = "Privileges", Required = true, Kind = ServerArgKind.Combo, Options = PrivilegeOptions}
                }
            },
            new ServerCommand
            {
                Name = "removeuser", Category = "Users", Required = AccessLevel.RoomAdmin,
                Description = "Permanently delete an account.",
                Usage = "removeuser <username>",
                Args = new[] {new ServerArg {Label = "Username", Required = true}}
            },
            new ServerCommand
            {
                Name = "setpassword", Category = "Users", Required = AccessLevel.RoomAdmin,
                Description = "Change the password of an account.",
                Usage = "setpassword <username> <new password>",
                Args = new[]
                {
                    new ServerArg {Label = "Username", Required = true},
                    new ServerArg {Label = "New password", Required = true, Kind = ServerArgKind.Password}
                }
            },
            new ServerCommand
            {
                Name = "setprivileges", Category = "Users", Required = AccessLevel.ServerAdmin,
                Description = "Change an account's access level.",
                Usage = "setprivileges <username> <privileges>",
                Args = new[]
                {
                    new ServerArg {Label = "Username", Required = true},
                    new ServerArg {Label = "Privileges", Required = true, Kind = ServerArgKind.Combo, Options = PrivilegeOptions}
                }
            },
            new ServerCommand
            {
                Name = "kick", Category = "Users", Required = AccessLevel.RoomAdmin,
                Description = "Disconnect a user (they can log back in).",
                Usage = "kick <username>",
                Args = new[] {new ServerArg {Label = "Username", Required = true}}
            },
            new ServerCommand
            {
                Name = "ban", Category = "Users", Required = AccessLevel.RoomAdmin,
                Description = "Block an account and disconnect it.",
                Usage = "ban <username>",
                Args = new[] {new ServerArg {Label = "Username", Required = true}}
            },
            new ServerCommand
            {
                Name = "enableuser", Category = "Users", Required = AccessLevel.RoomAdmin,
                Description = "Lift a ban so a user can log in again.",
                Usage = "enableuser <username>",
                Args = new[] {new ServerArg {Label = "Username", Required = true}}
            },
            new ServerCommand
            {
                Name = "userinfo", Category = "Users", Required = AccessLevel.ServerAdmin,
                Description = "Show details for an account.",
                Usage = "userinfo <username>",
                Args = new[] {new ServerArg {Label = "Username", Required = true}}
            },
            new ServerCommand
            {
                Name = "allusersonline", Category = "Users", Required = AccessLevel.ServerAdmin,
                Description = "List all users currently connected.",
                Usage = "allusersonline"
            },
            new ServerCommand
            {
                Name = "allusers", Category = "Users", Required = AccessLevel.ServerAdmin,
                Description = "List every registered account.",
                Usage = "allusers"
            },
            new ServerCommand
            {
                Name = "listusers", Category = "Users", Required = AccessLevel.RoomAdmin,
                Description = "Machine-readable account list used by the User Manager.",
                Usage = "listusers"
            },

            // ---- Rooms
            new ServerCommand
            {
                Name = "addroom", Category = "Rooms", Required = AccessLevel.ServerAdmin,
                Description = "Create a new room, optionally protected by a password.",
                Usage = "addroom <room name> [password]",
                Args = new[]
                {
                    new ServerArg {Label = "Room name", Required = true},
                    new ServerArg {Label = "Password", Kind = ServerArgKind.Password, Watermark = "optional"}
                }
            },
            new ServerCommand
            {
                Name = "removeroom", Category = "Rooms", Required = AccessLevel.ServerAdmin,
                Description = "Delete a room (the 'General' room cannot be removed).",
                Usage = "removeroom <room name>",
                Args = new[] {new ServerArg {Label = "Room name", Required = true}}
            },
            new ServerCommand
            {
                Name = "setroom", Category = "Rooms", Required = AccessLevel.ServerAdmin,
                Description = "Move a user into another room.",
                Usage = "setroom <username> <room name>",
                Args = new[]
                {
                    new ServerArg {Label = "Username", Required = true},
                    new ServerArg {Label = "Room name", Required = true}
                }
            },
            new ServerCommand
            {
                Name = "allusersinroom", Category = "Rooms", Required = AccessLevel.RoomAdmin,
                Description = "List the accounts in a room.",
                Usage = "allusersinroom [room name]  (blank = your own room)",
                Args = new[] {new ServerArg {Label = "Room name", Watermark = "optional - defaults to your room"}}
            },
            new ServerCommand
            {
                Name = "allrooms", Category = "Rooms", Required = AccessLevel.ServerAdmin,
                Description = "List all rooms and their user counts.",
                Usage = "allrooms"
            },
            new ServerCommand
            {
                Name = "listrooms", Category = "Rooms", Required = AccessLevel.ServerAdmin,
                Description = "Machine-readable room list used by the User Manager.",
                Usage = "listrooms"
            },
            new ServerCommand
            {
                Name = "setroompassword", Category = "Rooms", Required = AccessLevel.ServerAdmin,
                Description = "Set a room's password (only when the login system is password-only).",
                Usage = "setroompassword <room name> <password>",
                Args = new[]
                {
                    new ServerArg {Label = "Room name", Required = true},
                    new ServerArg {Label = "Password", Required = true, Kind = ServerArgKind.Password}
                }
            },
            new ServerCommand
            {
                Name = "getroompassword", Category = "Rooms", Required = AccessLevel.ServerAdmin,
                Description = "Show a room's password (only when the login system is password-only).",
                Usage = "getroompassword <room name>",
                Args = new[] {new ServerArg {Label = "Room name", Required = true}}
            },

            // ---- Server
            new ServerCommand
            {
                Name = "settings", Category = "Server", Required = AccessLevel.ServerAdmin,
                Description = "List, read or change a server setting.",
                Usage = "settings                  list all\nsettings <key>             read one\nsettings <key> <value>     change one",
                Args = new[]
                {
                    new ServerArg {Label = "Key", Watermark = "optional - e.g. kicktimer, port, autocreateaccounts"},
                    new ServerArg {Label = "Value", Watermark = "optional - the new value"}
                }
            },
            new ServerCommand
            {
                Name = "setloginsys", Category = "Server", Required = AccessLevel.ServerAdmin,
                Description = "Choose how users log in.",
                Usage = "setloginsys <system>",
                Args = new[] {new ServerArg {Label = "Login system", Required = true, Kind = ServerArgKind.Combo, Options = LoginSystemOptions}}
            },
            new ServerCommand
            {
                Name = "setkicktime", Category = "Server", Required = AccessLevel.ServerAdmin,
                Description = "Set how long a kicked user must wait before reconnecting (seconds).",
                Usage = "setkicktime <seconds>",
                Args = new[] {new ServerArg {Label = "Seconds", Required = true, Watermark = "e.g. 60"}}
            },
            new ServerCommand
            {
                Name = "setmaxusersconnection", Category = "Server", Required = AccessLevel.ServerAdmin,
                Description = "Set the maximum number of simultaneous connections.",
                Usage = "setmaxusersconnection <max>",
                Args = new[] {new ServerArg {Label = "Max connections", Required = true, Watermark = "e.g. 100"}}
            },
            new ServerCommand
            {
                Name = "getport", Category = "Server", Required = AccessLevel.ServerAdmin,
                Description = "Show the server port.",
                Usage = "getport"
            },
            new ServerCommand
            {
                Name = "statistics", Category = "Server", Required = AccessLevel.ServerAdmin,
                Description = "Show server packet statistics.",
                Usage = "statistics"
            }
        };

        private readonly List<ServerArgField> _serverArgFields = new();
        private readonly Dictionary<string, Button> _serverCommandButtons = new();
        private ServerCommand _selectedServerCommand;

        private void BuildServerCommTab()
        {
            string category = null;
            foreach (ServerCommand cmd in ServerCommands)
            {
                if (cmd.Category != category)
                {
                    category = cmd.Category;
                    ServerCommandListPanel.Children.Add(new TextBlock
                    {
                        Text = category,
                        FontSize = 11,
                        FontWeight = FontWeight.SemiBold,
                        Foreground = Brush.Parse("#8FA6C8"),
                        Margin = new Thickness(6, 8, 0, 3)
                    });
                }

                var button = new Button {Content = cmd.Name};
                button.Classes.Add("cmd");
                ServerCommand captured = cmd;
                button.Click += (s, e) => SelectServerCommand(captured);
                _serverCommandButtons[cmd.Name] = button;
                ServerCommandListPanel.Children.Add(button);
            }

            SocketClient.Connected += (s, e) => Dispatcher.UIThread.Post(UpdateSendState);
            SocketClient.Disconnected += (s, e) => Dispatcher.UIThread.Post(UpdateSendState);
            SocketClient.Waiting += (s, e) => Dispatcher.UIThread.Post(UpdateSendState);

            EventSink.ServerResponseMessageEvent += (s, e) => Dispatcher.UIThread.Post(() => AppendServerOutput(e.Message));

            if (ServerCommands.Length > 0)
                SelectServerCommand(ServerCommands[0]);

            UpdateSendState();
        }

        private void SelectServerCommand(ServerCommand cmd)
        {
            _selectedServerCommand = cmd;

            foreach (KeyValuePair<string, Button> pair in _serverCommandButtons)
            {
                if (pair.Key == cmd.Name)
                {
                    if (!pair.Value.Classes.Contains("selected"))
                        pair.Value.Classes.Add("selected");
                }
                else
                {
                    pair.Value.Classes.Remove("selected");
                }
            }

            ServerCommandTitle.Text = cmd.Name;
            ServerCommandDescription.Text = cmd.Description;
            ServerCommandUsage.IsVisible = !string.IsNullOrEmpty(cmd.Usage);
            ServerCommandUsage.Text = ServerCommandUsage.IsVisible ? "Usage:\n" + cmd.Usage : string.Empty;

            ServerArgsPanel.Children.Clear();
            _serverArgFields.Clear();

            if (cmd.Args.Length == 0)
            {
                ServerArgsPanel.Children.Add(new TextBlock
                {
                    Text = "This command takes no arguments.",
                    Foreground = Brush.Parse("#9AA3B2"),
                    FontStyle = FontStyle.Italic
                });
            }
            else
            {
                foreach (ServerArg arg in cmd.Args)
                    ServerArgsPanel.Children.Add(BuildArgRow(arg));
            }

            UpdateSendState();
        }

        private Control BuildArgRow(ServerArg arg)
        {
            var row = new StackPanel {Spacing = 4};

            row.Children.Add(new TextBlock
            {
                Text = arg.Required ? arg.Label + " *" : arg.Label + "  (optional)",
                Foreground = Brush.Parse(arg.Required ? "#E6E9EF" : "#9AA3B2"),
                FontSize = 12
            });

            if (arg.Kind == ServerArgKind.Combo)
            {
                var combo = new ComboBox {Width = 240, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left};
                foreach (string option in arg.Options)
                    combo.Items.Add(option);
                combo.SelectedIndex = 0;
                row.Children.Add(combo);
                _serverArgFields.Add(new ServerArgField {Arg = arg, Combo = combo});
            }
            else
            {
                var box = new TextBox
                {
                    Width = 320,
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left,
                    Watermark = arg.Watermark
                };

                if (arg.Kind == ServerArgKind.Password)
                    box.PasswordChar = '\u2022';

                row.Children.Add(box);
                _serverArgFields.Add(new ServerArgField {Arg = arg, Text = box});
            }

            return row;
        }

        private void UpdateSendState()
        {
            bool connected = NetworkManager.SocketClient.IsConnected;
            AccessLevel mine = NetworkManager.SocketClient.AccessLevel;
            ServerCommand cmd = _selectedServerCommand;

            ServerSendButton.IsEnabled = connected && cmd != null && mine >= cmd.Required;

            if (cmd == null)
            {
                ServerAccessNote.Text = string.Empty;
                return;
            }

            if (!connected)
                ServerAccessNote.Text = "Not connected - connect from the Network tab to send commands.";
            else if (mine < cmd.Required)
                ServerAccessNote.Text = $"Requires {Pretty(cmd.Required)} access (you have {Pretty(mine)}).";
            else
                ServerAccessNote.Text = $"Access: {Pretty(cmd.Required)}";
        }

        private void OnServerSendClick(object sender, RoutedEventArgs e)
        {
            ServerCommand cmd = _selectedServerCommand;
            if (cmd == null || !NetworkManager.SocketClient.IsConnected)
                return;

            if (NetworkManager.SocketClient.AccessLevel < cmd.Required)
            {
                AppendServerOutput("You do not have enough privileges for this command.");
                return;
            }

            var args = new List<string>();
            foreach (ServerArgField field in _serverArgFields)
            {
                string value = field.Arg.Kind == ServerArgKind.Combo
                    ? field.Combo.SelectedIndex.ToString()
                    : (field.Text.Text ?? string.Empty).Trim();

                if (field.Arg.Required && string.IsNullOrEmpty(value))
                {
                    AppendServerOutput($"'{field.Arg.Label}' is required.");
                    return;
                }

                args.Add(value);
            }

            for (int i = args.Count - 1; i >= 0 && string.IsNullOrEmpty(args[i]); i--)
                args.RemoveAt(i);

            NetworkManager.SocketClient.Send(new PRemoteMessage(ServerMessageType.Normal, cmd.Name, args));
        }

        private void OnServerClearOutputClick(object sender, RoutedEventArgs e) => ServerOutputBox.Text = string.Empty;

        private void AppendServerOutput(string message)
        {
            if (string.IsNullOrEmpty(message))
                return;

            ServerOutputBox.Text = (ServerOutputBox.Text ?? string.Empty) + $"{DateTime.Now:HH:mm:ss}: {message}{Environment.NewLine}";
            ServerOutputBox.CaretIndex = ServerOutputBox.Text?.Length ?? 0;
        }

        private static string Pretty(AccessLevel level) => level switch
        {
            AccessLevel.RoomAdmin => "room admin",
            AccessLevel.ServerAdmin => "server admin",
            _ => "normal"
        };
    }
}
