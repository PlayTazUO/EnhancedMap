using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using EnhancedMap.Core;

namespace EnhancedMap.Desktop.Windows
{
    public partial class CoordsConverterWindow : Window
    {
        public CoordsConverterWindow()
        {
            InitializeComponent();

            foreach (MapEntry map in Global.Maps)
                if (map != null)
                    MapCombo.Items.Add(map.Index);

            if (MapCombo.ItemCount > 0)
                MapCombo.SelectedIndex = 0;

            Closing += (s, e) =>
            {
                e.Cancel = true;
                Hide();
            };
        }

        private void OnModeChanged(object sender, SelectionChangedEventArgs e)
        {
            if (XyPanel == null || LatLongPanel == null)
                return;

            bool xy = ModeCombo.SelectedIndex == 0;
            XyPanel.IsVisible = xy;
            LatLongPanel.IsVisible = !xy;
        }

        private void OnGoClick(object sender, RoutedEventArgs e)
        {
            if (!Global.FreeView)
                Global.FreeView = true;

            if (ModeCombo.SelectedIndex == 0)
            {
                Global.X = Parse(TextBoxX.Text);
                Global.Y = Parse(TextBoxY.Text);
            }
            else
            {
                int degLat = Parse(DegLat.Text);
                int minLat = Parse(MinLat.Text);
                int degLong = Parse(DegLong.Text);
                int minLong = Parse(MinLong.Text);

                string latDir = (LatDir.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "N";
                string longDir = (LongDir.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "W";

                var point = Utility.GetLatLong(degLat, minLat, degLong, minLong, latDir, longDir);
                Global.X = point.X;
                Global.Y = point.Y;
            }

            if (MapCombo.SelectedIndex >= 0 && MapCombo.SelectedIndex != Global.Facet)
                Global.Facet = MapCombo.SelectedIndex;
        }

        private void OnResetClick(object sender, RoutedEventArgs e)
        {
            if (Global.FreeView)
                Global.FreeView = false;

            TextBoxX.Text = TextBoxY.Text = "0";
            DegLat.Text = MinLat.Text = DegLong.Text = MinLong.Text = "0";
            MapCombo.SelectedIndex = 0;
        }

        private static int Parse(string text)
        {
            return int.TryParse(text, out int value) ? value : 0;
        }
    }
}
