using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using EnhancedMap.Core;
using EnhancedMap.Core.MapObjects;
using EnhancedMap.Core.Network;
using EnhancedMap.Desktop.Windows;
using EnhancedMap.Diagnostic;

namespace EnhancedMap.Desktop
{
    public partial class MainWindow : Window
    {
        private readonly DispatcherTimer _timer;
        private long _counter;
        private long _nextRefresh;
        private bool _requestRefresh = true;

        public MainWindow()
        {
            InitializeComponent();

            Global.MainWindow = this;
            MainCore.IsRunning = true;

            SettingsWindow = new SettingsWindow();
            ChatWindow = new ChatWindow();

#if BETA
            Title = $"Enhanced Map - {MainCore.MapVersion} - BETA";
#else
            Title = $"Enhanced Map - {MainCore.MapVersion}";
#endif

            Map.SignalRequested += SendSignal;
            Map.TextTyped += OnTextTyped;

            BuildContextMenu();

            NotificationManager.Notified += OnNotified;

            SocketClient.Connected += (s, e) => SetStatus("Online");
            SocketClient.Waiting += (s, e) => SetStatus("Waiting...");
            SocketClient.Disconnected += (s, e) => SetStatus("Offline");

            _timer = new DispatcherTimer {Interval = TimeSpan.FromMilliseconds(1000.0 / Math.Max(1, Global.SettingsCollection["fps"].ToInt()))};
            _timer.Tick += OnTimerTick;

            Opened += OnOpened;
            Closing += OnClosing;
        }

        public SettingsWindow SettingsWindow { get; }
        public ChatWindow ChatWindow { get; }
        public PlacesEditorWindow PlacesEditorWindow { get; private set; }
        public SearcherWindow SearcherWindow { get; private set; }
        public CoordsConverterWindow CoordsConverterWindow { get; private set; }
        public SharedLabelWindow SharedLabelWindow { get; private set; }

        public void SetTimerIntervalByFps(int fps)
        {
            _timer.Interval = TimeSpan.FromMilliseconds(1000.0 / Math.Max(1, fps));
        }

        // ---------------------------------------------------------------- lifecycle

        private async void OnOpened(object sender, EventArgs e)
        {
            Logger.Log("Initializing main window...");

            try
            {
                FilesManager.Load();
                FilesManager.Guardlines.ForEach(s => RenderObjectsManager.AddGuardline(new GuardLineObject(s)));
                FilesManager.Houses.ForEach(s => RenderObjectsManager.AddHouse(new HouseObject(s)));
                FilesManager.BuildSets.ForEach(s => s.Entries.ForEach(a => RenderObjectsManager.AddBuilding(new BuildingObject(a))));
            }
            catch (Exception ex)
            {
                Logger.Error("Unable to load definitions: " + ex.Message);
            }

            try
            {
                if (MapsManager.CheckMapExists())
                    MapsManager.LoadMaps();
                else
                    await RunMapMaker();

                Global.Facet = Global.Maps.FirstOrDefault(s => s != null)?.Index ?? 0;
                Global.FacetChanged += (s, args) => { UpdateMapsForFacet(); _requestRefresh = true; };
                UpdateMapsForFacet();
            }
            catch (Exception ex)
            {
                Logger.Error("Unable to load maps: " + ex.Message);
            }

            try
            {
                HouseReader.LoadXml();
            }
            catch
            {
                // houses xml optional
            }

            RenderObjectsManager.AddLabel(Global.CurrentLabelObject);
            RenderObjectsManager.AddMask(RoseMask.RoseInstance);
            RenderObjectsManager.AddMask(PanicMask.PanicInstance);
            RenderObjectsManager.AddMask(FPSMask.FPSInstance);
            RenderObjectsManager.AddMask(ConnectionMask.ConnectionInstance);
            RenderObjectsManager.AddMask(CoordinatesMask.CoortinatesInstance);
            RenderObjectsManager.AddMask(ZoomMask.ZoomInstance);
            RenderObjectsManager.AddMask(CrossMask.CorssInstance);

            Global.SetZoomIndex(Global.SettingsCollection["zoomIndex"].ToInt());

            _timer.Start();

            if (Global.SettingsCollection["autologin"].ToBool())
                NetworkManager.Connect();

            if (Global.SettingsCollection["openchatatstart"].ToBool())
                ChatWindow.Show(this);
        }

        private void OnClosing(object sender, WindowClosingEventArgs e)
        {
            Logger.Log("Main window closing.");
            try
            {
                SettingsWindow.SaveSettings();
                FilesManager.Save();
                HouseReader.SaveXml();
            }
            catch (Exception ex)
            {
                Logger.Error("Error while saving on exit: " + ex.Message);
            }

            MainCore.IsRunning = false;
        }

        private async Task RunMapMaker()
        {
            var wizard = new MapMakerWindow();
            await wizard.ShowDialog(this);

            if (wizard.Succeeded)
            {
                MapsManager.LoadMaps();
                Global.Facet = Global.Maps.FirstOrDefault(s => s != null)?.Index ?? 0;
            }
        }

        private async void OnMapMakerClick(object sender, RoutedEventArgs e)
        {
            _timer.Stop();
            await RunMapMaker();
            UpdateMapsForFacet();
            _requestRefresh = true;
            _timer.Start();
        }

        private void UpdateMapsForFacet()
        {
            int facet = Global.Facet;

            foreach (RenderMap m in RenderObjectsManager.Get<RenderMap>().ToList())
            {
                if (facet < 0 || facet >= Global.Maps.Length || Global.Maps[facet] == null || m.MapEntry == null || m.MapEntry.Index != facet)
                {
                    RenderObjectsManager.RemoveMap(m);
                    m.Unload();
                }
            }

            if (facet < 0 || facet >= Global.Maps.Length || Global.Maps[facet] == null)
                return;

            if (!RenderObjectsManager.Get<RenderMap>().Any(m => m.MapEntry != null && m.MapEntry.Index == facet))
            {
                bool detailed = Global.SettingsCollection["mapkind"].ToInt() == 0;
                string path = Path.Combine(AppContext.BaseDirectory, "Maps", (detailed ? "2D" : "") + "map" + facet + ".png");

                if (File.Exists(path))
                {
                    var renderMap = new RenderMap(Global.Maps[facet], path);
                    renderMap.Load();
                    RenderObjectsManager.AddMap(renderMap);
                }
                else
                {
                    Logger.Warn("Map image not found: " + path);
                }
            }
        }

        private void OnTimerTick(object sender, EventArgs e)
        {
            NetworkManager.SocketClient.Sync();

            _counter += (long) _timer.Interval.TotalMilliseconds;
            if (_counter >= 100L)
            {
                _nextRefresh++;
                _counter -= 100L;

                if (_nextRefresh % 8L == 0L)
                    _requestRefresh = true;

                if (UpdatePosition() || NetworkManager.SocketClient.ReceivedPackets)
                    _requestRefresh = true;

                if (_requestRefresh)
                {
                    _requestRefresh = false;
                    Map.InvalidateVisual();
                    CoordsText.Text = $"({(int) Global.X}, {(int) Global.Y})";
                }
            }
        }

        private bool UpdatePosition()
        {
            if (Global.FreeView || Global.TrackedUser == null || Global.TrackedUser.IsDisposing)
                return false;

            bool ok = false;

            if (Global.X != Global.TrackedUser.Position.X)
            {
                Global.X = Global.TrackedUser.Position.X;
                ok = true;
            }

            if (Global.Y != Global.TrackedUser.Position.Y)
            {
                Global.Y = Global.TrackedUser.Position.Y;
                ok = true;
            }

            if (!Global.IsFacetChanged && Global.Facet != Global.TrackedUser.Map)
                Global.Facet = Global.TrackedUser.Map;

            return ok;
        }

        // ---------------------------------------------------------------- notifications

        private void OnNotified(NotificationManager.Severity severity, string message)
        {
            Dispatcher.UIThread.Post(() =>
            {
                var dialog = new MessageDialog("Enhanced Map", message);
                if (IsVisible)
                    dialog.Show(this);
                else
                    dialog.Show();
            });
        }

        private void OnTextTyped(string text)
        {
            if (!ChatWindow.IsVisible)
                ChatWindow.Show(this);
            ChatWindow.AppendText(text);
        }

        private void SendSignal()
        {
            RenderObjectsManager.AddSignal(new SignalObject(MouseManager.Location.X, MouseManager.Location.Y));
            if (NetworkManager.SocketClient.IsConnected)
                NetworkManager.SocketClient.Send(new PAlert((ushort) MouseManager.Location.X, (ushort) MouseManager.Location.Y));
        }

        private void SetStatus(string status)
        {
            Dispatcher.UIThread.Post(() => StatusText.Text = status);
        }

        // ---------------------------------------------------------------- context menu

        private void BuildContextMenu()
        {
            var menu = new ContextMenu();
            menu.Opening += OnContextMenuOpening;
            Map.ContextMenu = menu;
        }

        private void OnContextMenuOpening(object sender, CancelEventArgs e)
        {
            var menu = (ContextMenu) sender;
            menu.Items.Clear();

            var usersItem = new MenuItem {Header = $"Users: {RenderObjectsManager.Get<UserObject>().Length}"};
            foreach (UserObject user in RenderObjectsManager.Get<UserObject>())
            {
                if (user == null || user.IsDisposing)
                    continue;

                var u = new MenuItem {Header = user.Name};
                var track = new MenuItem {Header = "Track"};
                var captured = user;
                track.Click += (s, args) =>
                {
                    Global.CenterOn(captured);
                    _requestRefresh = true;
                };
                var highlight = new MenuItem {Header = "Highlight", IsChecked = user.IsHighlighted};
                highlight.Click += (s, args) =>
                {
                    user.IsHighlighted = !user.IsHighlighted;
                    if (user.IsHighlighted)
                        Global.HighlightesUsername.Add(user.Name);
                    else
                        Global.HighlightesUsername.Remove(user.Name);
                };
                u.Items.Add(track);
                u.Items.Add(highlight);
                usersItem.Items.Add(u);
            }

            menu.Items.Add(usersItem);

            var markers = new MenuItem {Header = "Markers"};
            var addLocal = new MenuItem {Header = "Add local"};
            addLocal.Click += (s, args) =>
            {
                RenderObjectsManager.AddMarkerObject(new MarkerObject(null, MouseManager.Location.X, MouseManager.Location.Y));
                _requestRefresh = true;
            };
            var addShared = new MenuItem {Header = "Add shared"};
            addShared.Click += (s, args) =>
            {
                SharedLabelWindow ??= new SharedLabelWindow();
                SharedLabelWindow.Show(this);
            };
            markers.Items.Add(addLocal);
            markers.Items.Add(addShared);

            foreach (MarkerObject mark in RenderObjectsManager.Get<MarkerObject>())
            {
                var captured = mark;
                string owner = captured.Parent?.Name;
                var m = new MenuItem {Header = string.IsNullOrEmpty(owner) ? $"Marker ({captured.Position.X}, {captured.Position.Y})" : $"{owner}'s marker"};
                var remove = new MenuItem {Header = "Remove"};
                remove.Click += (s, args) => captured.Dispose();
                m.Items.Add(remove);
                markers.Items.Add(m);
            }

            menu.Items.Add(markers);

            var signals = new MenuItem {Header = "Signals"};
            var alert = new MenuItem {Header = "Send alert"};
            alert.Click += (s, args) => SendSignal();
            signals.Items.Add(alert);
            menu.Items.Add(signals);

            menu.Items.Add(new Separator());

            var freeView = new MenuItem {Header = "Free view", IsChecked = Global.FreeView};
            freeView.Click += (s, args) => Global.FreeView = !Global.FreeView;
            menu.Items.Add(freeView);

            var zoom = new MenuItem {Header = "Zoom"};
            foreach (float value in new[] {0.125f, 0.25f, 0.5f, 0.75f, 1f, 1.5f, 2f, 4f, 6f, 8f})
            {
                var z = value;
                var item = new MenuItem {Header = $"{z * 100:0.#}%", IsChecked = Global.Zoom == z};
                item.Click += (s, args) =>
                {
                    Global.SetInitialZoom(z);
                    _requestRefresh = true;
                };
                zoom.Items.Add(item);
            }

            menu.Items.Add(zoom);

            var maps = new MenuItem {Header = "Maps"};
            foreach (MapEntry map in Global.Maps)
            {
                if (map == null)
                    continue;

                var captured = map;
                var item = new MenuItem {Header = captured.Name, IsChecked = Global.Facet == captured.Index};
                item.Click += (s, args) =>
                {
                    Global.Facet = captured.Index;
                    Global.IsFacetChanged = true;
                    _requestRefresh = true;
                };
                maps.Items.Add(item);
            }

            menu.Items.Add(maps);

            var tools = new MenuItem {Header = "Tools"};
            var places = new MenuItem {Header = "Places editor"};
            places.Click += (s, args) => OpenPlacesEditor();
            var search = new MenuItem {Header = "Search..."};
            search.Click += (s, args) => OpenSearch();
            var coords = new MenuItem {Header = "Coords converter"};
            coords.Click += (s, args) => OpenCoordsConverter();
            tools.Items.Add(places);
            tools.Items.Add(search);
            tools.Items.Add(coords);
            menu.Items.Add(tools);
        }

        // ---------------------------------------------------------------- menu handlers

        private void OnConnectClick(object sender, RoutedEventArgs e)
        {
            if (NetworkManager.SocketClient.IsConnected)
                NetworkManager.Disconnect(false);
            else
            {
                SettingsWindow.ApplySettings();
                NetworkManager.Connect();
            }
        }

        private void OnFlipClick(object sender, RoutedEventArgs e)
        {
            Global.Angle = Global.Angle == 45f ? 0f : 45f;
            _requestRefresh = true;
            Map.InvalidateVisual();
        }

        private void OnExitClick(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void OnFreeViewClick(object sender, RoutedEventArgs e)
        {
            Global.FreeView = !Global.FreeView;
            FreeViewMenuItem.IsChecked = Global.FreeView;
        }

        private void OnZoomInClick(object sender, RoutedEventArgs e)
        {
            Global.ChangeZoom(true);
            _requestRefresh = true;
        }

        private void OnZoomOutClick(object sender, RoutedEventArgs e)
        {
            Global.ChangeZoom(false);
            _requestRefresh = true;
        }

        private void OnPlacesEditorClick(object sender, RoutedEventArgs e)
        {
            OpenPlacesEditor();
        }

        private void OpenPlacesEditor()
        {
            if (PlacesEditorWindow == null)
            {
                BuildingObject building = RenderObjectsManager.Get<BuildingObject>().LastOrDefault(s => (Global.Facet == s.Entry.Map || (Global.Facet == 0 || Global.Facet == 1) && s.Entry.Map == 7) && s.IsMouseOver);
                if (building != null)
                    PlacesEditorWindow = new PlacesEditorWindow(building.Entry);
                else
                {
                    HouseObject house = RenderObjectsManager.Get<HouseObject>().LastOrDefault(s => (Global.Facet == s.Entry.Map || (Global.Facet == 0 || Global.Facet == 1) && s.Entry.Map == 7) && s.IsMouseOver);
                    PlacesEditorWindow = house != null ? new PlacesEditorWindow(house.Entry) : new PlacesEditorWindow();
                }
            }

            PlacesEditorWindow.Show(this);
        }

        private void OnSearchClick(object sender, RoutedEventArgs e)
        {
            OpenSearch();
        }

        private void OpenSearch()
        {
            SearcherWindow ??= new SearcherWindow();
            SearcherWindow.Show(this);
        }

        private void OnCoordsConverterClick(object sender, RoutedEventArgs e)
        {
            OpenCoordsConverter();
        }

        private void OpenCoordsConverter()
        {
            CoordsConverterWindow ??= new CoordsConverterWindow();
            CoordsConverterWindow.Show(this);
        }

        private void OnChatClick(object sender, RoutedEventArgs e)
        {
            if (ChatWindow.IsVisible)
                ChatWindow.Hide();
            else
                ChatWindow.Show(this);
        }

        private void OnSettingsClick(object sender, RoutedEventArgs e)
        {
            SettingsWindow.RefreshCenterUsers();
            SettingsWindow.Show(this);
        }

        private void OnAboutClick(object sender, RoutedEventArgs e)
        {
            var dialog = new MessageDialog("About", $"Enhanced Map {MainCore.MapVersion}\n.NET 10 + Avalonia + SkiaSharp");
            dialog.Show(this);
        }
    }
}
