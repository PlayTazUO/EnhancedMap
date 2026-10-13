using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using EnhancedMap.Core;
using EnhancedMap.Core.Network;

namespace EnhancedMap.Desktop.Windows
{
    public partial class UserManagerWindow : Window
    {
        private const string UserPrefix = "LSTUSR|";
        private const string UsersEnd = "LSTUSR_END";
        private const string RoomPrefix = "LSTROM|";
        private const string RoomsEnd = "LSTROM_END";

        private static readonly string[] PrivilegeOptions = {"Normal", "Room admin", "Server admin"};

        private sealed class UserEntry
        {
            public string Name;
            public string Room;
            public int Level;
            public bool Banned;
            public bool Online;
        }

        private readonly List<UserEntry> _users = new List<UserEntry>();
        private UserEntry _selected;
        private bool _confirmingDelete;
        private string _pendingSelectName;

        public UserManagerWindow()
        {
            InitializeComponent();

            foreach (string option in PrivilegeOptions)
                PrivilegeCombo.Items.Add(option);

            EventSink.ServerResponseMessageEvent += OnServerResponse;

            Closing += (s, e) =>
            {
                e.Cancel = true;
                Hide();
            };

            SetMode(true);
            UpdateState();
        }

        // ---------------------------------------------------------------- server commands

        private void SendCommand(string keyword, params string[] args)
        {
            if (!NetworkManager.SocketClient.IsConnected)
            {
                AppendOutput("Not connected to the server.");
                return;
            }

            NetworkManager.SocketClient.Send(new PRemoteMessage(ServerMessageType.Normal, keyword, args.ToList()));
        }

        public void Refresh()
        {
            _pendingSelectName = _selected?.Name;

            UserList.Items.Clear();
            _users.Clear();
            _selected = null;
            RoomCombo.Items.Clear();

            StatusText.Text = "Loading...";
            SetMode(true);

            if (!NetworkManager.SocketClient.IsConnected)
            {
                StatusText.Text = "Not connected.";
                return;
            }

            SendCommand("listusers");

            // Only server admins may enumerate rooms; sending it without the privilege
            // would make the server drop the session.
            if (NetworkManager.SocketClient.AccessLevel >= AccessLevel.ServerAdmin)
                SendCommand("listrooms");
        }

        private void OnServerResponse(object sender, ResponseServerEntry e)
        {
            Dispatcher.UIThread.Post(() => HandleResponse(e.Message));
        }

        private void HandleResponse(string message)
        {
            if (string.IsNullOrEmpty(message))
                return;

            if (message.StartsWith(UserPrefix, StringComparison.Ordinal))
            {
                string[] parts = message.Split('|');
                if (parts.Length < 6)
                    return;

                var entry = new UserEntry
                {
                    Name = parts[1],
                    Room = parts[2],
                    Level = int.TryParse(parts[3], out int level) ? level : 0,
                    Banned = parts[4].Equals("true", StringComparison.OrdinalIgnoreCase),
                    Online = parts[5].Equals("true", StringComparison.OrdinalIgnoreCase)
                };

                _users.Add(entry);
                UserList.Items.Add(CreateRow(entry));

                if (!RoomCombo.Items.Contains(entry.Room) && !string.IsNullOrEmpty(entry.Room))
                    RoomCombo.Items.Add(entry.Room);

                return;
            }

            if (message == UsersEnd)
            {
                StatusText.Text = _users.Count == 1 ? "1 account" : $"{_users.Count} accounts";

                if (_pendingSelectName != null)
                {
                    UserEntry match = _users.FirstOrDefault(u => u.Name == _pendingSelectName);
                    _pendingSelectName = null;

                    if (match != null)
                    {
                        foreach (object item in UserList.Items)
                        {
                            if (item is ListBoxItem row && row.Tag == match)
                            {
                                UserList.SelectedItem = row;
                                break;
                            }
                        }
                    }
                }

                return;
            }

            if (message.StartsWith(RoomPrefix, StringComparison.Ordinal))
            {
                string[] parts = message.Split('|');
                if (parts.Length >= 2 && !string.IsNullOrEmpty(parts[1]) && !RoomCombo.Items.Contains(parts[1]))
                    RoomCombo.Items.Add(parts[1]);
                return;
            }

            if (message == RoomsEnd)
                return;

            AppendOutput(message);
        }

        // ---------------------------------------------------------------- list / form

        private static ListBoxItem CreateRow(UserEntry entry)
        {
            var grid = new Grid {ColumnDefinitions = new ColumnDefinitions("150,130,110,*")};
            AddCell(grid, 0, entry.Name);
            AddCell(grid, 1, entry.Room);
            AddCell(grid, 2, PrivilegeOptions[Math.Clamp(entry.Level, 0, PrivilegeOptions.Length - 1)]);
            AddCell(grid, 3, entry.Banned ? "Banned" : entry.Online ? "Online" : "Offline");
            return new ListBoxItem {Content = grid, Tag = entry};
        }

        private static void AddCell(Grid grid, int column, string text)
        {
            var label = new TextBlock
            {
                Text = text ?? string.Empty,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(6, 2, 6, 2),
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            Grid.SetColumn(label, column);
            grid.Children.Add(label);
        }

        private void SetMode(bool isNew)
        {
            _confirmingDelete = false;
            RemoveButton.Content = "Delete account";

            FormTitle.Text = isNew ? "New user" : _selected?.Name ?? "User";
            PasswordLabel.Text = isNew ? "Password" : "New password";
            UsernameBox.IsEnabled = isNew;
            AddButton.IsVisible = isNew;
            PasswordButton.IsVisible = !isNew;
            RoomButton.IsEnabled = !isNew;
            PrivilegeButton.IsEnabled = !isNew;
            ActionsSeparator.IsVisible = !isNew;
            ActionsPanel.IsVisible = !isNew;

            if (isNew)
            {
                UsernameBox.Text = string.Empty;
                PasswordBox.Text = string.Empty;
                RoomCombo.SelectedIndex = -1;
                PrivilegeCombo.SelectedIndex = 0;
            }
        }

        private void SelectUser(UserEntry entry)
        {
            _selected = entry;
            SetMode(false);

            UsernameBox.Text = entry.Name;
            PasswordBox.Text = string.Empty;
            PrivilegeCombo.SelectedIndex = Math.Clamp(entry.Level, 0, PrivilegeOptions.Length - 1);

            if (!RoomCombo.Items.Contains(entry.Room) && !string.IsNullOrEmpty(entry.Room))
                RoomCombo.Items.Add(entry.Room);
            RoomCombo.SelectedItem = entry.Room;

            BanButton.IsEnabled = !entry.Banned;
            EnableButton.IsEnabled = entry.Banned;
        }

        private void UpdateState()
        {
            bool connected = NetworkManager.SocketClient.IsConnected;
            RefreshButton.IsEnabled = connected;
            NewButton.IsEnabled = connected;
        }

        // ---------------------------------------------------------------- events

        private void OnRefreshClick(object sender, RoutedEventArgs e)
        {
            Refresh();
        }

        private void OnNewClick(object sender, RoutedEventArgs e)
        {
            UserList.SelectedItem = null;
            _selected = null;
            SetMode(true);
        }

        private void OnCloseClick(object sender, RoutedEventArgs e)
        {
            Hide();
        }

        private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (UserList.SelectedItem is ListBoxItem item && item.Tag is UserEntry entry)
                SelectUser(entry);
        }

        private void OnAddClick(object sender, RoutedEventArgs e)
        {
            string username = UsernameBox.Text?.Trim() ?? string.Empty;
            string password = PasswordBox.Text ?? string.Empty;

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                AppendOutput("Username and password are required.");
                return;
            }

            if (!(RoomCombo.SelectedItem is string room) || string.IsNullOrEmpty(room))
            {
                AppendOutput("Select a room.");
                return;
            }

            int level = Math.Max(0, PrivilegeCombo.SelectedIndex);
            SendCommand("adduser", username, password, room, level.ToString());
            Refresh();
        }

        private void OnSetPasswordClick(object sender, RoutedEventArgs e)
        {
            if (_selected == null)
                return;

            string password = PasswordBox.Text ?? string.Empty;
            if (string.IsNullOrEmpty(password))
            {
                AppendOutput("Enter the new password first.");
                return;
            }

            SendCommand("setpassword", _selected.Name, password);
            Refresh();
        }

        private void OnSetPrivilegesClick(object sender, RoutedEventArgs e)
        {
            if (_selected == null)
                return;

            int level = Math.Max(0, PrivilegeCombo.SelectedIndex);
            SendCommand("setprivileges", _selected.Name, level.ToString());
            Refresh();
        }

        private void OnSetRoomClick(object sender, RoutedEventArgs e)
        {
            if (_selected == null)
                return;

            if (!(RoomCombo.SelectedItem is string room) || string.IsNullOrEmpty(room))
            {
                AppendOutput("Select a room.");
                return;
            }

            SendCommand("setroom", _selected.Name, room);
            Refresh();
        }

        private void OnKickClick(object sender, RoutedEventArgs e)
        {
            if (_selected == null)
                return;

            SendCommand("kick", _selected.Name);
            Refresh();
        }

        private void OnBanClick(object sender, RoutedEventArgs e)
        {
            if (_selected == null)
                return;

            SendCommand("ban", _selected.Name);
            Refresh();
        }

        private void OnEnableClick(object sender, RoutedEventArgs e)
        {
            if (_selected == null)
                return;

            SendCommand("enableuser", _selected.Name);
            Refresh();
        }

        private void OnRemoveClick(object sender, RoutedEventArgs e)
        {
            if (_selected == null)
                return;

            if (!_confirmingDelete)
            {
                _confirmingDelete = true;
                RemoveButton.Content = "Click again to confirm";
                return;
            }

            SendCommand("removeuser", _selected.Name);
            _selected = null;
            Refresh();
        }

        private void OnClearOutputClick(object sender, RoutedEventArgs e)
        {
            OutputBox.Text = string.Empty;
        }

        private void AppendOutput(string message)
        {
            if (string.IsNullOrEmpty(message))
                return;

            OutputBox.Text = (OutputBox.Text ?? string.Empty) + $"{DateTime.Now:HH:mm:ss}: {message}{Environment.NewLine}";
            OutputBox.CaretIndex = OutputBox.Text?.Length ?? 0;
        }
    }
}
