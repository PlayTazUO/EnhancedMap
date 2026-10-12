using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using EnhancedMap.Core;
using EnhancedMap.Core.MapObjects;

namespace EnhancedMap.Desktop.Windows
{
    public partial class PlacesEditorWindow : Window
    {
        public PlacesEditorWindow()
        {
            InitializeComponent();

            FilesManager.BuildSets.ForEach(s => CategoryCombo.Items.Add(s.Name));

            foreach (MapEntry map in Global.Maps)
                if (map != null)
                    FacetCombo.Items.Add(map.Name);

            if (Global.Maps.Length > 2 && Global.Maps[0]?.Name.ToLower() == "felucca" && Global.Maps[1]?.Name.ToLower() == "trammel")
                FacetCombo.Items.Add("Fel/Tram");

            TextX.Text = MouseManager.Location.X.ToString();
            TextY.Text = MouseManager.Location.Y.ToString();
            FacetCombo.SelectedIndex = Global.Facet;

            Closing += (s, e) =>
            {
                e.Cancel = true;
                Hide();
            };
        }

        public PlacesEditorWindow(HouseEntry entry) : this()
        {
            DescriptionBox.Text = entry.Description;
            TextX.Text = entry.Location.X.ToString();
            TextY.Text = entry.Location.Y.ToString();
            FacetCombo.SelectedIndex = entry.Map;
            CategoryCombo.IsEnabled = false;
            ShowNameCheck.IsEnabled = false;
            AddButton.Tag = entry;
        }

        public PlacesEditorWindow(BuildingEntry entry) : this()
        {
            DescriptionBox.Text = entry.Description;
            TextX.Text = entry.Location.X.ToString();
            TextY.Text = entry.Location.Y.ToString();
            FacetCombo.SelectedIndex = entry.Map == 7 ? 6 : entry.Map;
            CategoryCombo.SelectedIndex = FilesManager.BuildSets.IndexOf(entry.Parent);
            ShowNameCheck.IsChecked = entry.ShowName;
            AddButton.Tag = entry;
        }

        private void OnCategoryChanged(object sender, SelectionChangedEventArgs e)
        {
        }

        private int EffectiveFacet()
        {
            int facet = FacetCombo.SelectedIndex;
            if (Global.Maps.Length > 2 && Global.Maps[0]?.Name.ToLower() == "felucca" && Global.Maps[1]?.Name.ToLower() == "trammel" && facet == 6)
                facet = 7;
            return facet;
        }

        private void OnCancelClick(object sender, RoutedEventArgs e)
        {
            if (AddButton.Tag is BuildingEntry build)
            {
                build.Parent.Entries.Remove(build);
                BuildingObject buildingObj = RenderObjectsManager.Get<BuildingObject>().FirstOrDefault(s => s.Entry == build);
                buildingObj?.Dispose();
                Close();
            }
            else if (AddButton.Tag is HouseEntry house)
            {
                HouseObject houseObj = RenderObjectsManager.Get<HouseObject>().FirstOrDefault(s => s.Entry == house);
                houseObj?.Dispose();
                FilesManager.Houses.Remove(house);
                Close();
            }
            else
            {
                Close();
            }
        }

        private void OnAddClick(object sender, RoutedEventArgs e)
        {
            short x = short.TryParse(TextX.Text, out short px) ? px : (short) 0;
            short y = short.TryParse(TextY.Text, out short py) ? py : (short) 0;
            int facet = EffectiveFacet();

            if (AddButton.Tag is BuildingEntry build)
            {
                build.Description = DescriptionBox.Text;
                build.Location.X = x;
                build.Location.Y = y;
                build.Map = facet;
                build.Parent.Entries.Remove(build);
                if (CategoryCombo.SelectedIndex >= 0)
                {
                    build.Parent = FilesManager.BuildSets[CategoryCombo.SelectedIndex];
                    FilesManager.BuildSets[CategoryCombo.SelectedIndex].Entries.Add(build);
                }

                build.ShowName = ShowNameCheck.IsChecked == true;
                build.IsEnabled = true;

                RenderObjectsManager.AddBuilding(new BuildingObject(build));
                Close();
            }
            else if (AddButton.Tag is HouseEntry)
            {
                Close();
            }
            else
            {
                if (CategoryCombo.SelectedIndex < 0)
                    return;

                var entry = new BuildingEntry(FilesManager.BuildSets[CategoryCombo.SelectedIndex], DescriptionBox.Text, new Position(x, y), facet);
                FilesManager.BuildSets[CategoryCombo.SelectedIndex].Entries.Add(entry);
                entry.IsEnabled = true;
                RenderObjectsManager.AddBuilding(new BuildingObject(entry));
                Close();
            }
        }
    }
}
