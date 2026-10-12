using Avalonia.Controls;
using Avalonia.Interactivity;
using EnhancedMap.Core;
using EnhancedMap.Core.MapObjects;
using EnhancedMap.Core.Network;

namespace EnhancedMap.Desktop.Windows
{
    public partial class SharedLabelWindow : Window
    {
        public SharedLabelWindow()
        {
            InitializeComponent();

            foreach (MapEntry m in Global.Maps)
            {
                if (m == null)
                    continue;

                MapCombo.Items.Add(m.Index);
                if (MapCombo.ItemCount - 1 == Global.Facet)
                    MapCombo.SelectedIndex = MapCombo.ItemCount - 1;
            }

            TextBoxX.Text = MouseManager.Location.X.ToString();
            TextBoxY.Text = MouseManager.Location.Y.ToString();

            Closing += (s, e) =>
            {
                e.Cancel = true;
                Hide();
            };
        }

        private void OnSendClick(object sender, RoutedEventArgs e)
        {
            short x = short.TryParse(TextBoxX.Text, out short px) ? px : (short) 0;
            short y = short.TryParse(TextBoxY.Text, out short py) ? py : (short) 0;
            byte map = (byte) (MapCombo.SelectedIndex < 0 ? 0 : MapCombo.SelectedIndex);

            if (NetworkManager.SocketClient.IsConnected)
            {
                NetworkManager.SocketClient.Send(new PSharedLabel((ushort) x, (ushort) y, map, DescriptionBox.Text));
                RenderObjectsManager.AddSharedLabel(new SharedLabelObject(Global.PlayerInstance, x, y, map, DescriptionBox.Text));
            }
        }
    }
}
