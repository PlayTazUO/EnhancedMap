using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Assistant;

/// <summary>
///     Minimal EnhancedMap socket client. Speaks the EnhancedMapServer protocol:
///       framing    : [id][lenLo][lenHi]
///       handshake  : sends 0x2B protocol request, then 0x1F login after the server replies
///       keep-alive : 0x25 player data every second
/// </summary>
internal sealed class EmBridgeClient : IDisposable
{
    private readonly List<byte> _rx = new();
    private readonly object _sendLock = new();

    private Socket? _socket;
    private Thread? _thread;
    private volatile bool _running;
    private volatile bool _accepted;

    private string _host = "";
    private int _port;
    private string _user = "";
    private string _pass = "";

    public bool IsAlive => _socket != null;
    public bool IsAccepted => _accepted;

    public void Connect(string host, int port, string user, string pass)
    {
        Disconnect();

        _host = host;
        _port = port;
        _user = user;
        _pass = pass;
        _rx.Clear();
        _running = true;

        _thread = new Thread(Run) {IsBackground = true, Name = "EnhancedMapBridge"};
        _thread.Start();
    }

    private void Run()
    {
        try
        {
            IPAddress[] addresses = Dns.GetHostAddresses(_host);
            IPAddress? addr = Array.Find(addresses, a => a.AddressFamily == AddressFamily.InterNetwork) ?? (addresses.Length > 0 ? addresses[0] : null);
            if (addr == null)
                throw new Exception("Cannot resolve host");

            var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp) {NoDelay = true};
            socket.Connect(new IPEndPoint(addr, _port));
            _socket = socket;

            Log.Info($"Connected to EnhancedMap server {_host}:{_port}");

            SendRaw(new EmPacket(0x2B).U8(0).Build());

            var buffer = new byte[8192];
            while (_running)
            {
                int n;
                try
                {
                    n = socket.Receive(buffer);
                }
                catch (SocketException)
                {
                    break;
                }

                if (n <= 0)
                    break;

                Parse(buffer, n);
            }
        }
        catch (Exception e)
        {
            Log.Warning("Bridge connection error: " + e.Message);
        }
        finally
        {
            _accepted = false;
            _running = false;
            try
            {
                _socket?.Dispose();
            }
            catch
            {
                // ignore
            }

            _socket = null;
        }
    }

    private void Parse(byte[] data, int len)
    {
        for (int i = 0; i < len; i++)
            _rx.Add(data[i]);

        while (true)
        {
            if (_rx.Count < 3)
                return;

            int id = _rx[0];
            int size = _rx[1] | (_rx[2] << 8);

            if (size < 3)
            {
                _rx.Clear();
                return;
            }

            if (_rx.Count < size)
                return;

            var payload = _rx.GetRange(3, size - 3).ToArray();
            _rx.RemoveRange(0, size);
            Handle(id, payload);
        }
    }

    private void Handle(int id, byte[] payload)
    {
        switch (id)
        {
            case 0x2B: // protocol response -> send login
                SendLogin();
                break;
            case 0x1F: // login response
                if (payload.Length > 0 && payload[0] == 1)
                {
                    _accepted = true;
                    Log.Info("Login accepted by EnhancedMap server.");
                }
                else
                {
                    Log.Warning("EnhancedMap server refused login. Check username/password.");
                    _running = false;
                }

                break;
        }
    }

    private void SendLogin()
    {
        byte[] user = Encoding.ASCII.GetBytes(_user);
        byte[] pass = Encoding.ASCII.GetBytes(_pass);

        var p = new EmPacket(0x1F);
        p.U8((byte)user.Length);
        p.U8((byte)pass.Length);
        p.Raw(user);
        p.Raw(pass);
        SendRaw(p.Build());
    }

    public void SendPlayerData(ushort x, ushort y, byte map, ushort hp, ushort stam, ushort mana, ushort maxhp, ushort maxstam, ushort maxmana, byte flag, bool panic, uint color, string font, float fontSize, byte fontStyle)
    {
        if (!IsAlive || !_accepted)
            return;

        var p = new EmPacket(0x25);
        p.U16(x);
        p.U16(y);
        p.U8(map);
        p.U16(hp);
        p.U16(stam);
        p.U16(mana);
        p.U16(maxhp);
        p.U16(maxstam);
        p.U16(maxmana);
        p.U8(flag);
        p.U8(0); // death (unused by server)
        p.U8((byte)(panic ? 1 : 0));
        p.U32(color);
        byte[] fontBytes = Encoding.ASCII.GetBytes(font);
        p.U8((byte)fontBytes.Length);
        p.Raw(fontBytes);
        p.F32(fontSize);
        p.U8(fontStyle);
        SendRaw(p.Build());
    }

    private void SendRaw(byte[] data)
    {
        Socket? socket = _socket;
        if (socket == null)
            return;

        lock (_sendLock)
        {
            try
            {
                int sent = 0;
                while (sent < data.Length)
                {
                    int n = socket.Send(data, sent, data.Length - sent, SocketFlags.None);
                    if (n <= 0)
                        break;
                    sent += n;
                }
            }
            catch (Exception e)
            {
                Log.Warning("Send failed: " + e.Message);
                _running = false;
            }
        }
    }

    public void Disconnect()
    {
        _running = false;
        _accepted = false;

        try
        {
            _socket?.Shutdown(SocketShutdown.Both);
        }
        catch
        {
            // ignore
        }

        try
        {
            _socket?.Close();
        }
        catch
        {
            // ignore
        }

        _socket = null;
    }

    public void Dispose() => Disconnect();
}

/// <summary>Simple EnhancedMap packet writer with the 3-byte [id][len] header.</summary>
internal sealed class EmPacket
{
    private readonly List<byte> _b = new(32);

    public EmPacket(byte id)
    {
        _b.Add(id);
        _b.Add(0);
        _b.Add(0);
    }

    public EmPacket U8(byte v)
    {
        _b.Add(v);
        return this;
    }

    public EmPacket U16(ushort v)
    {
        _b.Add((byte)(v & 0xff));
        _b.Add((byte)((v >> 8) & 0xff));
        return this;
    }

    public EmPacket U32(uint v)
    {
        _b.Add((byte)(v & 0xff));
        _b.Add((byte)((v >> 8) & 0xff));
        _b.Add((byte)((v >> 16) & 0xff));
        _b.Add((byte)((v >> 24) & 0xff));
        return this;
    }

    public EmPacket F32(float v)
    {
        _b.AddRange(BitConverter.GetBytes(v));
        return this;
    }

    public EmPacket Raw(byte[] data)
    {
        _b.AddRange(data);
        return this;
    }

    public byte[] Build()
    {
        int len = _b.Count;
        _b[1] = (byte)(len & 0xff);
        _b[2] = (byte)((len >> 8) & 0xff);
        return _b.ToArray();
    }
}
