using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
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
                    CheckTryReconnect.IsChecked = GetBool(network, "tryreconnect");
                    NumReconnect.Value = GetInt(network, "tryreconnecttime");
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
                    CheckShowPlacesIcons.IsChecked = GetBool(general, "showplacesicons");
                    CheckHideLessImportantPlaces.IsChecked = GetBool(general, "hidelessimportantplaces");
                    CheckShowTownsNames.IsChecked = GetBool(general, "showtownsnames");
                    CheckShowServerBounds.IsChecked = GetBool(general, "showserverbounds");
                    CheckShowGuardlines.IsChecked = GetBool(general, "showguardlines");
                    CheckShowCoordinates.IsChecked = GetBool(general, "showcoordinates");
                    CheckShowHouses.IsChecked = GetBool(general, "showhouses");
                    CheckShowMobilesAround.IsChecked = GetBool(general, "showmobilesaroundyou");
                    CheckDontRenderUsersDiffFacet.IsChecked = GetBool(general, "dontrenderusersindifferentfacets");
                    Global.SettingsCollection["centeruser"] = general.ToText("centeruser", Global.SettingsCollection["centeruser"].ToString());
                }

                XmlElement application = root["application"];
                if (application != null)
                {
                    int clienttype = GetInt(application, "clienttype");
                    RadioClassic.IsChecked = clienttype == 0;
                    RadioEnhanced.IsChecked = clienttype == 1;
                    RadioOrion.IsChecked = clienttype == 2;
                    CheckShowScrollbars.IsChecked = GetBool(application, "showscrollbarls");
                    CheckOpenChatAtStart.IsChecked = GetBool(application, "openchatatstart");
                    CheckShowIncomingMsg.IsChecked = GetBool(application, "showincomingmsg");
                    CheckSoundsIncomingMsg.IsChecked = GetBool(application, "soundsincomingmsg");
                    CheckLoadMapsOnDemand.IsChecked = GetBool(application, "loadmapsondemand");
                    CheckShowFps.IsChecked = GetBool(application, "showfps");
                    decimal fps = GetInt(application, "fps");
                    NumFps.Value = fps < 1 ? 30 : fps;
                    decimal cf = GetInt(application, "chatfontsize");
                    NumChatFontSize.Value = cf < 6 ? 10 : cf;
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
            CheckTryReconnect.IsChecked = s["tryreconnect"].ToBool();
            NumReconnect.Value = s["tryreconnecttime"].ToInt();

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
            CheckShowPlacesIcons.IsChecked = s["showplacesicons"].ToBool();
            CheckHideLessImportantPlaces.IsChecked = s["hidelessimportantplaces"].ToBool();
            CheckShowTownsNames.IsChecked = s["showtownsnames"].ToBool();
            CheckShowServerBounds.IsChecked = s["showserverbounds"].ToBool();
            CheckShowGuardlines.IsChecked = s["showguardlines"].ToBool();
            CheckShowCoordinates.IsChecked = s["showcoordinates"].ToBool();
            CheckShowHouses.IsChecked = s["showhouses"].ToBool();
            CheckShowMobilesAround.IsChecked = s["showmobilesaroundyou"].ToBool();
            CheckDontRenderUsersDiffFacet.IsChecked = s["dontrenderusersindifferentfacets"].ToBool();

            RadioClassic.IsChecked = s["clienttype"].ToInt() == 0;
            RadioEnhanced.IsChecked = s["clienttype"].ToInt() == 1;
            RadioOrion.IsChecked = s["clienttype"].ToInt() == 2;
            CheckShowScrollbars.IsChecked = s["showscrollbarls"].ToBool();
            CheckOpenChatAtStart.IsChecked = s["openchatatstart"].ToBool();
            CheckShowIncomingMsg.IsChecked = s["showincomingmsg"].ToBool();
            CheckSoundsIncomingMsg.IsChecked = s["soundsincomingmsg"].ToBool();
            CheckLoadMapsOnDemand.IsChecked = s["loadmapsondemand"].ToBool();
            CheckShowFps.IsChecked = s["showfps"].ToBool();
            NumFps.Value = s["fps"].ToInt();
            NumChatFontSize.Value = s["chatfontsize"].ToInt();
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
                xml.WriteAttributeString("tryreconnect", (CheckTryReconnect.IsChecked == true).ToString());
                xml.WriteAttributeString("tryreconnecttime", ((int) (NumReconnect.Value ?? 30000)).ToString());
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
                WriteCheck(xml, "showplacesicons", CheckShowPlacesIcons);
                WriteCheck(xml, "hidelessimportantplaces", CheckHideLessImportantPlaces);
                WriteCheck(xml, "showtownsnames", CheckShowTownsNames);
                WriteCheck(xml, "showserverbounds", CheckShowServerBounds);
                WriteCheck(xml, "showguardlines", CheckShowGuardlines);
                WriteCheck(xml, "showcoordinates", CheckShowCoordinates);
                WriteCheck(xml, "showhouses", CheckShowHouses);
                WriteCheck(xml, "showmobilesaroundyou", CheckShowMobilesAround);
                WriteCheck(xml, "dontrenderusersindifferentfacets", CheckDontRenderUsersDiffFacet);
                xml.WriteEndElement();

                xml.WriteStartElement("application");
                int clienttype = RadioEnhanced.IsChecked == true ? 1 : RadioOrion.IsChecked == true ? 2 : 0;
                xml.WriteAttributeString("clienttype", clienttype.ToString());
                WriteCheck(xml, "showscrollbarls", CheckShowScrollbars);
                WriteCheck(xml, "openchatatstart", CheckOpenChatAtStart);
                WriteCheck(xml, "showincomingmsg", CheckShowIncomingMsg);
                WriteCheck(xml, "soundsincomingmsg", CheckSoundsIncomingMsg);
                WriteCheck(xml, "loadmapsondemand", CheckLoadMapsOnDemand);
                WriteCheck(xml, "showfps", CheckShowFps);
                xml.WriteAttributeString("fps", ((int) (NumFps.Value ?? 30)).ToString());
                xml.WriteAttributeString("chatfontsize", ((int) (NumChatFontSize.Value ?? 10)).ToString());
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
            s["tryreconnect"] = CheckTryReconnect.IsChecked == true;
            s["tryreconnecttime"] = (int) (NumReconnect.Value ?? 30000);

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
            s["showplacesicons"] = CheckShowPlacesIcons.IsChecked == true;
            s["hidelessimportantplaces"] = CheckHideLessImportantPlaces.IsChecked == true;
            s["showtownsnames"] = CheckShowTownsNames.IsChecked == true;
            s["showserverbounds"] = CheckShowServerBounds.IsChecked == true;
            s["showguardlines"] = CheckShowGuardlines.IsChecked == true;
            s["showcoordinates"] = CheckShowCoordinates.IsChecked == true;
            s["showhouses"] = CheckShowHouses.IsChecked == true;
            s["showmobilesaroundyou"] = CheckShowMobilesAround.IsChecked == true;
            s["dontrenderusersindifferentfacets"] = CheckDontRenderUsersDiffFacet.IsChecked == true;

            s["clienttype"] = RadioEnhanced.IsChecked == true ? 1 : RadioOrion.IsChecked == true ? 2 : 0;
            s["showscrollbarls"] = CheckShowScrollbars.IsChecked == true;
            s["openchatatstart"] = CheckOpenChatAtStart.IsChecked == true;
            s["showincomingmsg"] = CheckShowIncomingMsg.IsChecked == true;
            s["soundsincomingmsg"] = CheckSoundsIncomingMsg.IsChecked == true;
            s["loadmapsondemand"] = CheckLoadMapsOnDemand.IsChecked == true;
            s["showfps"] = CheckShowFps.IsChecked == true;
            s["fps"] = (int) (NumFps.Value ?? 30);
            s["chatfontsize"] = (int) (NumChatFontSize.Value ?? 10);
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
            if (NetworkManager.SocketClient.IsConnected || NetworkManager.SocketClient.Status == ConnectionStatus.Waiting)
            {
                NetworkManager.Disconnect(false);
            }
            else
            {
                ApplyToCollection();
                NetworkManager.Connect();
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

        private static readonly string[] ServerCommands =
        {
            "adduser", "addroom", "removeuser", "removeroom", "setroom", "kick", "ban", "enableuser",
            "setpassword", "allusersonline", "userinfo", "sendmsg", "setprivileges", "allrooms", "statistics", "allusersinroom"
        };

        private readonly List<(TextBox Box, ComboBox Combo, bool IsCombo)> _serverArgFields = new();

        private void BuildServerCommTab()
        {
            foreach (string cmd in ServerCommands)
                ServerCommandCombo.Items.Add(cmd);

            SocketClient.Connected += (s, e) => Dispatcher.UIThread.Post(() => SetServerCommEnabled(true));
            SocketClient.Disconnected += (s, e) => Dispatcher.UIThread.Post(() => SetServerCommEnabled(false));
            SocketClient.Waiting += (s, e) => Dispatcher.UIThread.Post(() => SetServerCommEnabled(false));

            EventSink.ServerResponseMessageEvent += (s, e) => Dispatcher.UIThread.Post(() =>
            {
                ServerOutputBox.Text = (ServerOutputBox.Text ?? string.Empty) + $"{DateTime.Now:HH:mm:ss}: {e.Message}{Environment.NewLine}";
                ServerOutputBox.CaretIndex = ServerOutputBox.Text?.Length ?? 0;
            });

            SetServerCommEnabled(false);
        }

        private void SetServerCommEnabled(bool enabled)
        {
            ServerCommandCombo.IsEnabled = enabled;
            ServerSendButton.IsEnabled = enabled;
        }

        private void OnServerCommandChanged(object sender, SelectionChangedEventArgs e)
        {
            ServerArgsPanel.Children.Clear();
            _serverArgFields.Clear();

            int i = ServerCommandCombo.SelectedIndex;
            if (i < 0)
                return;

            ServerCommandDescription.Text = DescribeCommand(ServerCommands[i]);

            foreach ((string label, bool required, bool combo) field in FieldsFor(ServerCommands[i]))
            {
                var row = new StackPanel {Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 8};
                row.Children.Add(new TextBlock {Text = field.label + (field.required ? " *:" : ":"), Width = 120, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center});

                if (field.combo)
                {
                    var cb = new ComboBox {Width = 140};
                    cb.Items.Add("Normal");
                    cb.Items.Add("RoomAdmin");
                    cb.Items.Add("ServerAdmin");
                    cb.SelectedIndex = 0;
                    row.Children.Add(cb);
                    _serverArgFields.Add((null, cb, true));
                }
                else
                {
                    var tb = new TextBox {Width = 220};
                    row.Children.Add(tb);
                    _serverArgFields.Add((tb, null, false));
                }

                ServerArgsPanel.Children.Add(row);
            }
        }

        private void OnServerSendClick(object sender, RoutedEventArgs e)
        {
            int i = ServerCommandCombo.SelectedIndex;
            if (i < 0 || NetworkManager.SocketClient.AccessLevel == AccessLevel.Normal)
                return;

            var args = new List<string>();

            foreach ((TextBox box, ComboBox combo, bool isCombo) in _serverArgFields)
                args.Add(isCombo ? combo.SelectedIndex.ToString() : string.IsNullOrEmpty(box.Text) ? string.Empty : box.Text);

            ServerOutputBox.Text = string.Empty;
            NetworkManager.SocketClient.Send(new PRemoteMessage(ServerMessageType.Normal, ServerCommands[i], args));
        }

        private static string DescribeCommand(string cmd) => cmd switch
        {
            "adduser" => "Add a new user",
            "addroom" => "Add a new room",
            "removeuser" => "Remove a user",
            "removeroom" => "Remove a room",
            "setroom" => "Assign an existing room to a user",
            "kick" => "Kick a user from the server",
            "ban" => "Ban and kick a user",
            "enableuser" => "Enable a user (after a ban)",
            "setpassword" => "Set a new password for a user",
            "allusersonline" => "List all online users",
            "userinfo" => "Get info for a user",
            "sendmsg" => "Send a game message to all users",
            "setprivileges" => "Set account access level",
            "allrooms" => "List all rooms",
            "statistics" => "Server packet statistics",
            "allusersinroom" => "List users in a room",
            _ => string.Empty
        };

        private static IEnumerable<(string label, bool required, bool combo)> FieldsFor(string cmd)
        {
            switch (cmd)
            {
                case "adduser":
                    yield return ("Username", true, false);
                    yield return ("Password", false, false);
                    yield return ("Room", true, false);
                    yield return ("Privileges", true, true);
                    break;
                case "addroom":
                    yield return ("Room name", true, false);
                    break;
                case "removeuser":
                    yield return ("Username", false, false);
                    break;
                case "removeroom":
                    yield return ("Room name", true, false);
                    break;
                case "setroom":
                    yield return ("Username", true, false);
                    yield return ("Room name", true, false);
                    break;
                case "kick":
                case "ban":
                case "enableuser":
                    yield return ("Username", false, false);
                    break;
                case "setpassword":
                    yield return ("Username", false, false);
                    yield return ("New password", false, false);
                    break;
                case "userinfo":
                case "sendmsg":
                    yield return (cmd == "sendmsg" ? "Message" : "Username", true, false);
                    break;
                case "setprivileges":
                    yield return ("Username", true, false);
                    yield return ("Privileges", true, true);
                    break;
                case "allusersinroom":
                    yield return ("Room", false, false);
                    break;
            }
        }
    }
}
