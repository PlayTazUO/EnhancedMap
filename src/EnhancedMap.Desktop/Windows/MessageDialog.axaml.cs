using Avalonia.Controls;
using Avalonia.Interactivity;

namespace EnhancedMap.Desktop.Windows
{
    public partial class MessageDialog : Window
    {
        public MessageDialog()
        {
            InitializeComponent();
        }

        public MessageDialog(string title, string message) : this(title, false, message)
        {
        }

        public MessageDialog(string title, bool ignorable, string message) : this()
        {
            Title = title;
            MessageBox.Text = message;
            if (ignorable)
                OkayButton.Content = "Ignore";
        }

        private void OnOkayClick(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
