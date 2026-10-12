using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using EnhancedMap.Core;
using EnhancedMap.Diagnostic;

namespace EnhancedMap.Desktop.Windows
{
    public partial class MapMakerWindow : Window
    {
        private const string MAPS_DOWNLOAD_URL = "http://razorenhanced.org/download/EnhancedMapMaps/";

        private static readonly List<MapEntry> DefaultEntries = new()
        {
            new MapEntry(0, 0, 7168, 4096, "Felucca"),
            new MapEntry(1, 1, 7168, 4096, "Trammel"),
            new MapEntry(2, 2, 2304, 1600, "Ilshenar"),
            new MapEntry(3, 3, 2560, 2048, "Malas"),
            new MapEntry(4, 4, 1448, 1448, "Tokuno"),
            new MapEntry(5, 5, 1280, 4096, "TerMur")
        };

        private readonly List<MapEntry> _customMaps = new();
        private bool _busy;

        public MapMakerWindow()
        {
            InitializeComponent();

            MapsManager.RemoveMapsFiles();
            Global.Maps.Clear();
        }

        public bool Succeeded { get; private set; }

        private void ShowStep(Control panel)
        {
            KindPanel.IsVisible = ReferenceEquals(panel, KindPanel);
            OriginalPanel.IsVisible = ReferenceEquals(panel, OriginalPanel);
            CustomPanel.IsVisible = ReferenceEquals(panel, CustomPanel);
            ProgressPanel.IsVisible = ReferenceEquals(panel, ProgressPanel);
        }

        private void OnOriginalClick(object sender, RoutedEventArgs e) => ShowStep(OriginalPanel);

        private void OnCustomClick(object sender, RoutedEventArgs e) => ShowStep(CustomPanel);

        private void OnBackClick(object sender, RoutedEventArgs e) => ShowStep(KindPanel);

        private void OnCloseClick(object sender, RoutedEventArgs e) => Close();

        // ------------------------------------------------------------- custom maps

        private void OnAddCustomClick(object sender, RoutedEventArgs e)
        {
            string name = NameBox.Text?.Trim();
            if (string.IsNullOrEmpty(name) || !name.ContainsAnyLetter())
            {
                AppendProgress("Please insert a valid name.");
                return;
            }

            int index = (int) (IndexBox.Value ?? 0);
            if (_customMaps.Exists(s => s.Index == index))
            {
                AppendProgress($"A map with index {index} already exists.");
                return;
            }

            int width = (int) (WidthBox.Value ?? 0);
            int height = (int) (HeightBox.Value ?? 0);
            if (width <= 0 || height <= 0)
            {
                AppendProgress("Please insert a valid map size.");
                return;
            }

            var map = new MapEntry(index, _customMaps.Count, width, height, name);
            _customMaps.Add(map);
            CustomList.Items.Add($"{map.Index}  {map.Name}  {map.Width}x{map.Height}");

            NameBox.Text = string.Empty;
            IndexBox.Value = 0;
            WidthBox.Value = 0;
            HeightBox.Value = 0;
        }

        private void OnRemoveCustomClick(object sender, RoutedEventArgs e)
        {
            int i = CustomList.SelectedIndex;
            if (i < 0)
                return;

            _customMaps.RemoveAt(i);
            CustomList.Items.RemoveAt(i);
        }

        // ------------------------------------------------------------- generation

        private void OnGenerateOriginalClick(object sender, RoutedEventArgs e) => Generate(DefaultEntries);

        private void OnGenerateCustomClick(object sender, RoutedEventArgs e)
        {
            if (_customMaps.Count == 0)
            {
                AppendProgress("No maps added.");
                return;
            }

            Generate(_customMaps);
        }

        private async void Generate(List<MapEntry> maps)
        {
            if (_busy)
                return;

            string? folder = await PickFolder();
            if (folder == null)
                return;

            Global.UOPath = folder;

            _busy = true;
            ShowStep(ProgressPanel);
            ProgressBox.Text = string.Empty;
            ProgressTitle.Text = "Generating maps...";
            CloseButton.IsEnabled = false;

            var progress = new Progress<string>(AppendProgress);

            bool ok = await Task.Run(() =>
            {
                bool a = MapsManager.CreateImages(maps, true, progress);
                bool b = MapsManager.CreateImages(maps, false, progress);
                return a && b;
            });

            foreach (MapEntry map in maps)
                Global.Maps[map.Index] = map;

            MapsManager.SaveMaps();

            Succeeded = true;
            _busy = false;

            AppendProgress(ok ? "Done!" : "Finished with errors (see log above).");
            ProgressTitle.Text = "Map generation complete";
            CloseButton.IsEnabled = true;
        }

        private async void OnDownloadClick(object sender, RoutedEventArgs e)
        {
            if (_busy)
                return;

            _busy = true;
            ShowStep(ProgressPanel);
            ProgressBox.Text = string.Empty;
            ProgressTitle.Text = "Downloading maps...";
            CloseButton.IsEnabled = false;

            var progress = new Progress<string>(AppendProgress);

            bool ok = await Task.Run(() => DownloadMaps(progress));

            if (ok)
            {
                MapsManager.SaveMaps();
                Succeeded = true;
            }

            _busy = false;
            AppendProgress(ok ? "Download complete." : "Download failed.");
            ProgressTitle.Text = "Download complete";
            CloseButton.IsEnabled = true;
        }

        private bool DownloadMaps(IProgress<string> progress)
        {
            try
            {
                Directory.CreateDirectory(Path.Combine(AppContext.BaseDirectory, "Maps"));

                using var client = new HttpClient {Timeout = TimeSpan.FromMinutes(5)};

                for (int i = 0; i < 6; i++)
                {
                    foreach (string prefix in new[] {"2Dmap", "map"})
                    {
                        string name = prefix + i + ".png";
                        string url = MAPS_DOWNLOAD_URL + name;
                        string dest = Path.Combine(AppContext.BaseDirectory, "Maps", name);

                        progress.Report("Downloading " + name + "...");
                        byte[] data = client.GetByteArrayAsync(url).GetAwaiter().GetResult();
                        File.WriteAllBytes(dest, data);
                    }
                }

                return true;
            }
            catch (Exception e)
            {
                progress.Report("Error: " + e.Message);
                Logger.Error("Map download failed: " + e);
                return false;
            }
        }

        private async Task<string?> PickFolder()
        {
            var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Select the Ultima Online directory",
                AllowMultiple = false
            });

            if (folders.Count == 0)
                return null;

            return folders[0].TryGetLocalPath();
        }

        private void AppendProgress(string text)
        {
            ProgressBox.Text = (ProgressBox.Text ?? string.Empty) + text + Environment.NewLine;
            ProgressBox.CaretIndex = ProgressBox.Text?.Length ?? 0;
        }
    }

    internal static class MapMakerStringExtensions
    {
        public static bool ContainsAnyLetter(this string s)
        {
            foreach (char c in s)
            {
                if (char.IsLetter(c))
                    return true;
            }

            return false;
        }
    }
}
