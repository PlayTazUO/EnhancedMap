using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using EnhancedMap.Core;

namespace EnhancedMap.Desktop.Windows
{
    public partial class SearcherWindow : Window
    {
        public SearcherWindow()
        {
            InitializeComponent();

            Closing += (s, e) =>
            {
                e.Cancel = true;
                Hide();
            };
        }

        private void OnSearchChanged(object sender, RoutedEventArgs e)
        {
            Results.Items.Clear();

            string word = SearchBox.Text?.ToLower();
            if (string.IsNullOrEmpty(word) || word.Length < 2)
                return;

            foreach (BuildSet set in FilesManager.BuildSets)
            {
                foreach (BuildingEntry entry in set.Entries)
                {
                    if (entry.Description != null && entry.Description.ToLower().Contains(word))
                    {
                        var item = new ListBoxItem
                        {
                            Content = $"{entry.Description}   ({entry.Location})  [map {entry.Map}]",
                            Tag = entry
                        };
                        Results.Items.Add(item);
                    }
                }
            }
        }

        private void OnResultDoubleTapped(object sender, TappedEventArgs e)
        {
            if (Results.SelectedItem is ListBoxItem {Tag: BuildingEntry entry})
            {
                if (!Global.FreeView)
                    Global.FreeView = true;
                Global.X = entry.Location.X;
                Global.Y = entry.Location.Y;
                Global.Facet = entry.Map == 7 ? 0 : entry.Map;
            }
        }
    }
}
