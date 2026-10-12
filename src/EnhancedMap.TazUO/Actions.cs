using System.Text;

namespace Assistant;

/// <summary>
///     Bridges live TazUO state (position, facet, vitals, connection) to an EnhancedMap server.
///     The server address/port is requested in-game using the client's Unicode prompt (0xC2) via
///     <see cref="ClientPackets" />.
/// </summary>
public static class Actions
{
    private enum SetupStep
    {
        Idle,
        Address,
        Port
    }

    private static BridgeConfig? _config;
    private static EmBridgeClient? _client;
    private static bool _initialized;

    private static volatile bool _uoConnected;

    private static SetupStep _setup = SetupStep.Idle;
    private static string _pendingAddress = string.Empty;

    private static int _x, _y, _z;
    private static int _map;
    private static uint _playerSerial;

    private static ushort _hits, _stam, _mana;
    private static ushort _maxHits, _maxStam, _maxMana;

    private static volatile bool _dirty = true;
    private static DateTime _lastSend = DateTime.MinValue;
    private static DateTime _lastConnectAttempt = DateTime.MinValue;

    /// <summary>Called when the plugin is first initialized.</summary>
    public static void OnInitialize()
    {
        if (_initialized)
            return;

        _initialized = true;

        try
        {
            _config = BridgeConfig.Load();

            if (_config.IsConfigured)
            {
                Log.Info($"Using EnhancedMap server {_config.Address}:{_config.Port}");
                ConnectBridge();
            }
            else
            {
                Log.Info("No EnhancedMap server configured yet - will ask in-game once connected.");
            }
        }
        catch (Exception e)
        {
            Log.Error("Initialization failed: " + e);
        }
    }

    private static void ConnectBridge()
    {
        if (_config == null || !_config.IsConfigured)
            return;

        try
        {
            _client?.Dispose();
            _client = new EmBridgeClient();
            _client.Connect(_config.Address, _config.Port, _config.Username, _config.Password);
            _lastConnectAttempt = DateTime.UtcNow;
        }
        catch (Exception e)
        {
            Log.Error("Unable to start bridge client: " + e.Message);
        }
    }

    /// <summary>Server -> client packets. Tracks facet and vitals for the local player.</summary>
    public static bool OnPacketReceived(ref byte[] data, ref int length)
    {
        try
        {
            if (length >= 1)
            {
                byte id = data[0];

                switch (id)
                {
                    case 0x1B: // EnterWorld -> player serial
                        if (length >= 5)
                            _playerSerial = ReadU32BE(data, 1);
                        break;

                    case 0xBF: // ExtendedCommand -> map change (sub 8)
                        if (length >= 4 && ReadU16BE(data, 1) == 8)
                        {
                            _map = data[3];
                            _dirty = true;
                        }

                        break;

                    case 0xA1: // UpdateHitpoints: serial, hitsMax, hits
                        if (length >= 9 && ReadU32BE(data, 1) == _playerSerial)
                        {
                            _maxHits = ReadU16BE(data, 5);
                            _hits = ReadU16BE(data, 7);
                            _dirty = true;
                        }

                        break;

                    case 0xA2: // UpdateMana: serial, manaMax, mana
                        if (length >= 9 && ReadU32BE(data, 1) == _playerSerial)
                        {
                            _maxMana = ReadU16BE(data, 5);
                            _mana = ReadU16BE(data, 7);
                            _dirty = true;
                        }

                        break;

                    case 0xA3: // UpdateStamina: serial, stamMax, stam
                        if (length >= 9 && ReadU32BE(data, 1) == _playerSerial)
                        {
                            _maxStam = ReadU16BE(data, 5);
                            _stam = ReadU16BE(data, 7);
                            _dirty = true;
                        }

                        break;
                }
            }
        }
        catch
        {
            // never let a parse error disrupt the client
        }

        return true;
    }

    /// <summary>
    ///     Client -> server packets. Catches (and swallows) the response to our own Unicode prompt,
    ///     so the real game server never sees it.
    /// </summary>
    public static bool OnPacketSent(ref byte[] data, ref int length)
    {
        try
        {
            if (length >= 19 && data[0] == 0xC2)
            {
                ulong token = ReadU64BE(data, 3);
                if (token == ClientPackets.TokenAddress || token == ClientPackets.TokenPort)
                {
                    bool canceled = ReadU32BE(data, 11) == 0;
                    const int textStart = 19; // header(3) + data(8) + flag(4) + lang(3) + zero(1)
                    int chars = Math.Max(0, (length - textStart) / 2);
                    string text = chars > 0 ? Encoding.Unicode.GetString(data, textStart, chars * 2) : string.Empty;
                    text = text.TrimEnd('\0').Trim();

                    HandlePromptResponse(token, text, canceled);
                    return false; // block: this prompt was ours, not for the game server
                }
            }
        }
        catch
        {
            // fall through and allow the packet
        }

        return true;
    }

    private static void BeginSetup()
    {
        _setup = SetupStep.Address;
        ClientPackets.AsciiMessage("EnhancedMap not configured. Please enter the server ADDRESS, then press Enter.");
        ClientPackets.UnicodePrompt(ClientPackets.TokenAddress);
    }

    private static void HandlePromptResponse(ulong token, string text, bool canceled)
    {
        if (canceled || string.IsNullOrEmpty(text))
        {
            Log.Warning("EnhancedMap setup cancelled.");
            _setup = SetupStep.Idle;
            return;
        }

        if (token == ClientPackets.TokenAddress)
        {
            _pendingAddress = text;
            _setup = SetupStep.Port;
            ClientPackets.AsciiMessage("EnhancedMap: now enter the server PORT, then press Enter (for example: 8887).");
            ClientPackets.UnicodePrompt(ClientPackets.TokenPort);
        }
        else if (token == ClientPackets.TokenPort)
        {
            int port = int.TryParse(text, out int p) && p > 0 && p <= 65535 ? p : 8887;

            if (_config == null)
                _config = BridgeConfig.Load();

            _config.Address = _pendingAddress;
            _config.Port = port;
            _config.Save();

            _setup = SetupStep.Idle;
            Log.Info($"EnhancedMap server set to {_config.Address}:{_config.Port}");
            ClientPackets.AsciiMessage($"EnhancedMap: server {_config.Address}:{_config.Port} saved.");
            ConnectBridge();
        }
    }

    /// <summary>Called when the player's position changes.</summary>
    public static void OnPlayerPositionChanged(int x, int y, int z)
    {
        _x = x;
        _y = y;
        _z = z;
        _dirty = true;
    }

    public static void OnConnected()
    {
        _uoConnected = true;
        _dirty = true;
        Log.Info("UO client connected.");

        if (_config is {IsConfigured: true})
        {
            if (_client is not {IsAlive: true})
                ConnectBridge();
        }
        else if (_setup == SetupStep.Idle)
        {
            BeginSetup();
        }
    }

    public static void OnDisconnected()
    {
        _uoConnected = false;
    }

    public static void OnClientClosing()
    {
        _client?.Dispose();
        _client = null;
    }

    public static bool OnHotkey(int key, int mod, bool pressed) => true;

    public static void OnMouse(int button, int wheel)
    {
    }

    public static void OnFocusGained()
    {
    }

    public static void OnFocusLost()
    {
    }

    /// <summary>Game loop - sends periodic updates and reconnects if needed.</summary>
    public static void Tick()
    {
        try
        {
            if (_config is not {IsConfigured: true})
                return;

            if (_client is not {IsAlive: true} && DateTime.UtcNow - _lastConnectAttempt > TimeSpan.FromSeconds(10))
                ConnectBridge();

            if (_client is not {IsAccepted: true} || !_uoConnected)
                return;

            bool timedOut = DateTime.UtcNow - _lastSend > TimeSpan.FromSeconds(1);
            if (!_dirty && !timedOut)
                return;

            _dirty = false;
            _lastSend = DateTime.UtcNow;

            byte flag = _maxHits > 0 && _hits == 0 ? (byte)4 : (byte)0; // 4 = dead

            _client.SendPlayerData(
                (ushort)Math.Clamp(_x, 0, ushort.MaxValue),
                (ushort)Math.Clamp(_y, 0, ushort.MaxValue),
                (byte)Math.Clamp(_map, 0, 255),
                _hits, _stam, _mana,
                _maxHits, _maxStam, _maxMana,
                flag,
                false,
                0xFFFFFFFF,
                "Segoe UI",
                12f,
                0);
        }
        catch
        {
            // never throw out of Tick
        }
    }

    private static uint ReadU32BE(byte[] d, int o) => (uint)((d[o] << 24) | (d[o + 1] << 16) | (d[o + 2] << 8) | d[o + 3]);

    private static ushort ReadU16BE(byte[] d, int o) => (ushort)((d[o] << 8) | d[o + 1]);

    private static ulong ReadU64BE(byte[] d, int o) =>
        ((ulong)d[o] << 56) | ((ulong)d[o + 1] << 48) | ((ulong)d[o + 2] << 40) | ((ulong)d[o + 3] << 32) |
        ((ulong)d[o + 4] << 24) | ((ulong)d[o + 5] << 16) | ((ulong)d[o + 6] << 8) | d[o + 7];
}
