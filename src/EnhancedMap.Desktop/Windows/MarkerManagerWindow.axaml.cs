using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using EnhancedMap.Core;
using EnhancedMap.Core.MapObjects;

namespace EnhancedMap.Desktop.Windows
{
    public partial class MarkerManagerWindow : Window
    {
        private BuildingEntry _editing;

        public MarkerManagerWindow()
        {
            InitializeComponent();

            Closing += (s, e) =>
            {
                e.Cancel = true;
                Hide();
            };

            Reload();
        }

        public void Reload()
        {
            FilterCombo.Items.Clear();
            FilterCombo.Items.Add("All categories");
            foreach (BuildSet set in FilesManager.BuildSets)
                FilterCombo.Items.Add(set.Name);
            FilterCombo.SelectedIndex = 0;

            CategoryCombo.Items.Clear();
            foreach (BuildSet set in FilesManager.BuildSets)
                CategoryCombo.Items.Add(set.Name);

            FacetCombo.Items.Clear();
            foreach (MapEntry map in Global.Maps)
                if (map != null)
                    FacetCombo.Items.Add(map.Name);
            if (HasFelTram())
                FacetCombo.Items.Add("Fel/Tram");

            BeginNew();
            RefreshList();
        }

        private static bool HasFelTram()
        {
            return Global.Maps.Length > 2 && Global.Maps[0]?.Name.ToLower() == "felucca" && Global.Maps[1]?.Name.ToLower() == "trammel";
        }

        private int EffectiveFacet()
        {
            int facet = FacetCombo.SelectedIndex;
            if (HasFelTram() && facet == 6)
                facet = 7;
            return facet;
        }

        private void BeginNew()
        {
            _editing = null;
            FormTitle.Text = "New marker";
            ApplyButton.Content = "Add";
            MarkerList.SelectedItem = null;
            DescriptionBox.Text = string.Empty;
            TextX.Text = MouseManager.Location.X.ToString();
            TextY.Text = MouseManager.Location.Y.ToString();
            ShowNameCheck.IsChecked = false;

            if (CategoryCombo.ItemCount > 0 && CategoryCombo.SelectedIndex < 0)
                CategoryCombo.SelectedIndex = 0;

            int facet = Global.Facet;
            if (HasFelTram() && facet == 7)
                FacetCombo.SelectedIndex = 6;
            else if (facet >= 0 && facet < FacetCombo.ItemCount)
                FacetCombo.SelectedIndex = facet;
            else
                FacetCombo.SelectedIndex = 0;
        }

        private void RefreshList()
        {
            BuildingEntry selected = _editing;
            MarkerList.Items.Clear();

            string categoryFilter = FilterCombo.SelectedIndex > 0 ? FilterCombo.SelectedItem?.ToString() : null;
            string search = SearchBox.Text?.ToLower();

            foreach (BuildSet set in FilesManager.BuildSets)
            {
                if (categoryFilter != null && set.Name != categoryFilter)
                    continue;

                foreach (BuildingEntry entry in set.Entries)
                {
                    if (!string.IsNullOrEmpty(search) && (entry.Description == null || !entry.Description.ToLower().Contains(search)))
                        continue;

                    var item = CreateRow(entry);
                    MarkerList.Items.Add(item);

                    if (selected != null && ReferenceEquals(entry, selected))
                        MarkerList.SelectedItem = item;
                }
            }
        }

        private static ListBoxItem CreateRow(BuildingEntry entry)
        {
            var grid = new Grid {ColumnDefinitions = new ColumnDefinitions("*,140,60,120")};
            AddCell(grid, 0, entry.Description ?? string.Empty);
            AddCell(grid, 1, string.Format("({0}, {1})", entry.Location.X, entry.Location.Y));
            AddCell(grid, 2, entry.Map.ToString());
            AddCell(grid, 3, entry.Parent != null ? entry.Parent.Name : string.Empty);
            return new ListBoxItem {Content = grid, Tag = entry};
        }

        private static void AddCell(Grid grid, int column, string text)
        {
            var label = new TextBlock
            {
                Text = text,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(6, 2, 6, 2),
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            Grid.SetColumn(label, column);
            grid.Children.Add(label);
        }

        private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!(MarkerList.SelectedItem is ListBoxItem item) || !(item.Tag is BuildingEntry entry))
                return;

            _editing = entry;
            FormTitle.Text = "Edit marker";
            ApplyButton.Content = "Update";
            DescriptionBox.Text = entry.Description ?? string.Empty;
            TextX.Text = entry.Location.X.ToString();
            TextY.Text = entry.Location.Y.ToString();
            ShowNameCheck.IsChecked = entry.ShowName;
            CategoryCombo.SelectedIndex = FilesManager.BuildSets.IndexOf(entry.Parent);

            int facet = HasFelTram() && entry.Map == 7 ? 6 : entry.Map;
            if (facet >= 0 && facet < FacetCombo.ItemCount)
                FacetCombo.SelectedIndex = facet;
        }

        private void OnFilterChanged(object sender, SelectionChangedEventArgs e)
        {
            RefreshList();
        }

        private void OnSearchChanged(object sender, TextChangedEventArgs e)
        {
            RefreshList();
        }

        private void OnNewClick(object sender, RoutedEventArgs e)
        {
            BeginNew();
        }

        private void OnApplyClick(object sender, RoutedEventArgs e)
        {
            short x = short.TryParse(TextX.Text, out short px) ? px : (short) 0;
            short y = short.TryParse(TextY.Text, out short py) ? py : (short) 0;
            int facet = EffectiveFacet();

            if (CategoryCombo.SelectedIndex < 0)
                return;

            BuildSet category = FilesManager.BuildSets[CategoryCombo.SelectedIndex];

            if (_editing == null)
            {
                var entry = new BuildingEntry(category, DescriptionBox.Text, new Position(x, y), facet);
                entry.ShowName = ShowNameCheck.IsChecked == true;
                category.Entries.Add(entry);
                RenderObjectsManager.AddBuilding(new BuildingObject(entry));
                _editing = entry;
            }
            else
            {
                if (!ReferenceEquals(_editing.Parent, category))
                {
                    _editing.Parent.Entries.Remove(_editing);
                    _editing.Parent = category;
                    category.Entries.Add(_editing);
                }

                _editing.Description = DescriptionBox.Text;
                _editing.Location.X = x;
                _editing.Location.Y = y;
                _editing.Map = facet;
                _editing.ShowName = ShowNameCheck.IsChecked == true;

                BuildingObject obj = RenderObjectsManager.Get<BuildingObject>().FirstOrDefault(o => o.Entry == _editing);
                obj?.UpdatePosition(_editing.Location);
            }

            RefreshList();
        }

        private void OnRemoveClick(object sender, RoutedEventArgs e)
        {
            if (!(MarkerList.SelectedItem is ListBoxItem item) || !(item.Tag is BuildingEntry entry))
                return;

            entry.Parent?.Entries.Remove(entry);

            BuildingObject obj = RenderObjectsManager.Get<BuildingObject>().FirstOrDefault(o => o.Entry == entry);
            if (obj != null)
            {
                RenderObjectsManager.RemoveBuilding(obj);
                obj.Dispose();
            }

            if (ReferenceEquals(_editing, entry))
                BeginNew();

            RefreshList();
        }

        private void OnSaveClick(object sender, RoutedEventArgs e)
        {
            FilesManager.Save();
        }

        private void OnCloseClick(object sender, RoutedEventArgs e)
        {
            Hide();
        }
    }
}
