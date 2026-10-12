using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using EnhancedMap.Core;

namespace EnhancedMap.Desktop.Windows
{
    public partial class MapMakerWindow : Window
    {
        private static readonly string[] StandardNames =
        {
            "Felucca", "Trammel", "Ilshenar", "Malas", "Tokuno", "TerMur"
        };

        private readonly List<MapRow> _rows = new();
        private bool _busy;

        public MapMakerWindow()
        {
            InitializeComponent();

            MapsManager.RemoveMapsFiles();
            Global.Maps.Clear();

            if (!string.IsNullOrEmpty(Global.UOPath))
            {
                FolderBox.Text = Global.UOPath;
                PopulateMaps(Global.UOPath);
            }
        }

        public bool Succeeded { get; private set; }

        private sealed class MapRow
        {
            public int Index;
            public TextBox Name;
            public TextBox Width;
            public TextBox Height;
        }

        // ---------------------------------------------------------------- files

        private async void OnBrowseClick(object sender, RoutedEventArgs e)
        {
            string? folder = await PickFolder();
            if (folder == null)
                return;

            Global.UOPath = folder;
            FolderBox.Text = folder;
            PopulateMaps(folder);
        }

        private void PopulateMaps(string folder)
        {
            SetStatus(string.Empty);
            MapsPanel.Children.Clear();
            _rows.Clear();

            List<(int Index, string Path)> files = MapsManager.FindMapFiles(folder);

            if (files.Count == 0)
            {
                SetStatus("No map files (mapN.mul / mapNLegacyMUL.uop) found in this folder.");
                return;
            }

            foreach ((int index, string path) in files)
            {
                string name = index < StandardNames.Length ? StandardNames[index] : $"Map {index}";

                if (!MapsManager.TryDetectMapSize(index, path, out int width, out int height)
                    && index < MapsManager.StandardMapSizes.Length)
                {
                    (width, height) = MapsManager.StandardMapSizes[index];
                }

                AddRow(index, name, width, height);
            }
        }

        private void AddRow(int index, string name, int width, int height)
        {
            var row = new MapRow
            {
                Index = index,
                Name = new TextBox { Text = name },
                Width = new TextBox { Text = width.ToString() },
                Height = new TextBox { Text = height.ToString() }
            };

            var indexLabel = new TextBlock
            {
                Text = index.ToString(),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 6, 0)
            };

            var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("70,*,130,130,32") };

            row.Name.Margin = new Thickness(0, 0, 6, 0);
            row.Width.Margin = new Thickness(0, 0, 6, 0);
            row.Height.Margin = new Thickness(0, 0, 6, 0);

            var remove = new Button
            {
                Content = "X",
                Width = 30,
                Padding = new Thickness(0),
                HorizontalAlignment = HorizontalAlignment.Left,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                Foreground = new SolidColorBrush(Color.Parse("#E5484D")),
                FontWeight = FontWeight.Bold
            };
            remove.Click += (s, e) =>
            {
                MapsPanel.Children.Remove(grid);
                _rows.Remove(row);
            };

            Grid.SetColumn(indexLabel, 0);
            Grid.SetColumn(row.Name, 1);
            Grid.SetColumn(row.Width, 2);
            Grid.SetColumn(row.Height, 3);
            Grid.SetColumn(remove, 4);

            grid.Children.Add(indexLabel);
            grid.Children.Add(row.Name);
            grid.Children.Add(row.Width);
            grid.Children.Add(row.Height);
            grid.Children.Add(remove);

            _rows.Add(row);
            MapsPanel.Children.Add(grid);
        }

        // ------------------------------------------------------------- generation

        private async void OnGenerateClick(object sender, RoutedEventArgs e)
        {
            if (_busy)
                return;

            if (string.IsNullOrEmpty(Global.UOPath))
            {
                SetStatus("Please choose your Ultima Online folder first.");
                return;
            }

            var maps = new List<MapEntry>();
            foreach (MapRow row in _rows)
            {
                int index = row.Index;
                string name = string.IsNullOrWhiteSpace(row.Name.Text) ? $"Map {index}" : row.Name.Text.Trim();

                if (!int.TryParse(row.Width.Text, out int width) || width <= 0
                    || !int.TryParse(row.Height.Text, out int height) || height <= 0)
                {
                    SetStatus($"Map {index} has an invalid size.");
                    return;
                }

                maps.Add(new MapEntry(index, index, width, height, name));
            }

            if (maps.Count == 0)
            {
                SetStatus("No maps to generate.");
                return;
            }

            SetStatus(string.Empty);

            _busy = true;
            MainPanel.IsVisible = false;
            ProgressPanel.IsVisible = true;
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

        private void OnCloseClick(object sender, RoutedEventArgs e) => Close();

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

        private void SetStatus(string text)
        {
            StatusText.Text = text;
        }

        private void AppendProgress(string text)
        {
            ProgressBox.Text = (ProgressBox.Text ?? string.Empty) + text + Environment.NewLine;
            ProgressBox.CaretIndex = ProgressBox.Text?.Length ?? 0;
        }
    }
}
