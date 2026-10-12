using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using EnhancedMap.Core;
using EnhancedMap.Core.MapObjects;
using EnhancedMap.Core.Network;
using EnhancedMap.Diagnostic;

namespace EnhancedMap.Desktop.Windows
{
    public partial class ChatWindow : Window
    {
        private ChatEntry _lastChatEntry = ChatEntry.Invalid;

        public ChatWindow()
        {
            InitializeComponent();

            ChatManager.MessageWrited += OnMessageWrited;

            InputBox.IsEnabled = false;

            SocketClient.Connected += (s, e) => Dispatcher.UIThread.Post(() => InputBox.IsEnabled = true);
            SocketClient.Disconnected += (s, e) => Dispatcher.UIThread.Post(() => InputBox.IsEnabled = false);
            SocketClient.Waiting += (s, e) => Dispatcher.UIThread.Post(() => InputBox.IsEnabled = false);

            Closing += (s, e) =>
            {
                e.Cancel = true;
                Hide();
            };
        }

        public void AppendText(string text)
        {
            if (InputBox.IsEnabled)
            {
                InputBox.Text += text;
                InputBox.Focus();
            }
        }

        private void OnMessageWrited(object sender, ChatEntry e)
        {
            Dispatcher.UIThread.Post(() =>
            {
                UserObject user = RenderObjectsManager.GetUser(e.Name);
                if (user == null)
                {
                    Logger.Error("[CHAT] User '" + e.Name + "' not found");
                    return;
                }

                var msg = string.Format("[{0}] {1}:   {2}", e.Time.ToString("HH:mm"), e.Name, e.Message);
                _lastChatEntry = e;

                if (Messages.Children.Count > 500)
                    Messages.Children.Clear();

                var block = new TextBlock
                {
                    Text = msg,
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = ToBrush(user.Hue.Color)
                };
                Messages.Children.Add(block);
                Scroller.ScrollToEnd();
            });
        }

        private void OnInputKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter)
                return;

            bool newline = e.KeyModifiers.HasFlag(KeyModifiers.Shift);

            if (!newline)
            {
                e.Handled = true;
                if (string.IsNullOrEmpty(InputBox.Text))
                    return;

                if (NetworkManager.SocketClient.IsConnected)
                {
                    string t = InputBox.Text.TrimEnd();
                    NetworkManager.SocketClient.Send(new PChatMessage(t, Global.PlayerInstance.Hue.Color));
                }

                InputBox.Text = string.Empty;
            }
            else
            {
                e.Handled = true;
                InputBox.Text += Environment.NewLine;
            }
        }

        private static IBrush ToBrush(EnhancedMap.Drawing.Color color)
        {
            return new SolidColorBrush(Avalonia.Media.Color.FromArgb(color.A, color.R, color.G, color.B));
        }
    }
}
