# Adding EnhancedMap support directly to TazUO

This document is a self-contained implementation guide for building the EnhancedMap
"shared map / party tracker" feature directly into TazUO, **replacing the external
EnhancedMap.TazUO plugin**.

The plugin currently uses the client's Unicode prompt packet (`0xC2`) to ask for the
server address/port. This guide replaces that with **server-side gump packets** (`0xB0`
to display, `0xB1` to intercept the reply) so the user gets a proper configuration
window with fields for **address, port, username and password**.

All byte layouts and API names below were verified against the actual TazUO source
(`PlayTazUO/TazUO`, `main`) and the EnhancedMap server (`EnhancedMapServerNetCore`).
Where a symbol lives in a specific file, the path is given.

---

## 1. What EnhancedMap is

EnhancedMap is a separate, lightweight TCP service that lets players on the same shard
see each other on an out-of-game map / overlay. There are **two independent network
connections** involved:

| Connection | Owner | Purpose |
|---|---|---|
| UO shard socket | TazUO (`AsyncNetClient.Socket`) | Read local player state; inject the config gump; swallow the gump reply |
| EnhancedMap socket | New code in this guide (`EnhancedMapClient`) | Talk the EnhancedMap protocol to the map server |

The EnhancedMap protocol is **completely unrelated** to the UO protocol. It is a simple
3-byte-header packet protocol. Do not put these packets on the shard socket.

> NOTE: Do not confuse this with TazUO's existing `Network/EnhancedPackets` folder.
> That is the Ultima Online "Enhanced Client" protocol and has nothing to do with
> EnhancedMap.

---

## 2. High-level design

Add three new pieces to `ClassicUO.Client`:

1. **`EnhancedMapConfig`** – persisted settings (address, port, username, password).
2. **`EnhancedMapClient`** – background socket that speaks the EnhancedMap protocol.
3. **`EnhancedMapManager`** – orchestration: owns the client, snapshots player state
   once per second, injects the config gump, and intercepts the gump reply.

Two tiny hooks are added to the client core:

- **Incoming injection (config gump):** call `PacketParser.Instance.Append(bytes, true)`
  to feed a synthetic `0xB0` packet into the normal packet pipeline.
- **Outgoing interception (gump reply):** add one check in `AsyncNetClient.Send(...)`
  (right where `Plugin.ProcessSendPacket` is already called) so `0xB1` replies to our
  gump are parsed and blocked before they reach the shard.

Receiving other players' data never touches the UO socket – the EnhancedMap socket is
read on its own thread.

Suggested new folder/file layout:

```
src/ClassicUO.Client/Game/Managers/EnhancedMap/
    EnhancedMapConfig.cs
    EnhancedMapClient.cs
    EnhancedMapManager.cs
    EnhancedMapGump.cs          (packet build/parse helpers)
```

---

## 3. Transport and framing

- TCP, **little-endian** everywhere, including the header length.
- Frame: `[id : 1][lenLow : 1][lenHigh : 1][payload ...]`.
  - `len` = **total** frame size including the 3-byte header.
  - `len` is little-endian: `len = lenLow | (lenHigh << 8)`.

Reference (server): `EnhancedMapServerNetCore/Network/CircularBuffer.cs::GetPacketLength`,
`Network/PacketWriter.cs::WriteSize`. Reference (plugin): `EmBridgeClient.cs`.

```
0       1       2       3 ...
+-------+-------+-------+----------------------+
|  id   |lenLow |lenHi  |   payload (len-3)     |
+-------+-------+-------+----------------------+
```

### Primitive encoding (payload)

| Type | Bytes | Encoding |
|---|---|---|
| bool | 1 | `0x00` / `0x01` |
| byte | 1 | raw |
| ushort | 2 | little-endian |
| uint | 4 | little-endian |
| float | 4 | IEEE-754 little-endian |
| string | N | raw ASCII bytes, length supplied separately (no terminator on the wire) |

---

## 4. Handshake

The server's protocol version constant is `0x02`
(`EnhancedMapServerNetCore/Main.cs`: `public static byte Protocol => 0x02;`).

Sequence:

```
Client                                    Server
  |  --- 0x2B  [0x00] -------------------->  |   protocol request
  |  <-- 0x2B  [protocol:1] ---------------- |   protocol response (0x02)
  |  --- 0x1F  [userLen][passLen][u][p] --->  |   login request
  |  <-- 0x1F  [1][level][nameLen][name][proto] |  login accepted
```

- If credentials are wrong the server simply **closes the socket** (no `0x1F` reply).
- The server always reports protocol `0x02`. The client must not hard-fail if it
  disagrees; the plugin ignores the protocol number entirely and proceeds.

Login response fields (server `P LoginResponse`, `Network/Packets.cs`):

```
byte  accepted     // 0x01 = accepted, anything else = refused
byte  level        // AccountLevel: 0 Normal, 1 RoomAdmin, 2 ServerAdmin
byte  nameLen
byte[] name        // ASCII, nameLen bytes
byte  protocol     // 0x02
```

Server login handling: `EnhancedMapServerNetCore/Network/PacketHandlers.cs::LoginRequest`
and `Managers/AccountManager.cs::CanConnect`.

---

## 5. Packet reference

### 5.1 Client → Server

| ID | Name | Payload |
|---|---|---|
| `0x2B` | ProtocolRequest | `byte 0x00` |
| `0x1F` | LoginRequest | `byte userLen`, `byte passLen`, `ASCII username`, `ASCII password` |
| `0x25` | PlayerData | see 5.3 |
| `0x21` | RemoteAdminCmd (optional) | `byte type`, `byte keywordLen`, `ASCII keyword`, `byte argCount`, `argCount × (byte len + ASCII arg)` |
| `0x23` | ChatMessage (optional) | `ushort msgLen`, `uint colorARGB`, `ASCII message` |
| `0x27` | Alert (optional) | `ushort x`, `ushort y` |
| `0x31` | SharedLabel (optional) | see 5.4 |

### 5.2 Server → Client

| ID | Name | Payload |
|---|---|---|
| `0x2B` | ProtocolResponse | `byte protocol` |
| `0x1F` | LoginResponse | see section 4 |
| `0x21` | ServerResponseCmd | `byte type`, `ushort msgLen`, `ASCII msg` |
| `0x23` | ChatMessage | `ushort msgLen`, `uint colorARGB`, `ASCII msg`, `byte nameLen`, `ASCII name` |
| `0x25` | UserData (other players) | see 5.3 |
| `0x27` | AlertAdvice | `ushort x`, `ushort y`, `byte nameLen`, `ASCII name` |
| `0x29` | UserConnection | `bool connected`, `byte nameLen`, `ASCII name` |
| `0x2D` | ChatMessageResponse | `bool allow` |
| `0x2F` | ServerMessage | `ushort msgLen`, `ASCII msg`, `uint colorARGB` |
| `0x31` | SharedLabel | see 5.4 |

### 5.3 Player data (`0x25`)

The client sends its own state approximately **once per second**; the server relays it
to everyone else in the same room, appending the sender's name.

Client → server:
```
ushort x
ushort y
byte   facet/map
ushort hits
ushort stamina
ushort mana
ushort maxHits
ushort maxStamina
ushort maxMana
byte   flag        // 0 none, 1 poisoned, 2 yellow hits, 3 paralyzed, 4 dead, 5 hidden
byte   death       // legacy, send 0 (server skips it)
byte   panic       // bool
uint   colorARGB
byte   fontNameLen
ASCII  fontName
float  fontSize
byte   fontStyle
```

Server → client (relayed) appends, after `fontStyle`:
```
byte   nameLen
ASCII  name
```

References: plugin `EmBridgeClient.SendPlayerData`; server `PUserData`; client parser
`EnhancedMap.Core/.../Packets/PacketHandlers.cs::PlayerData`.

### 5.4 Shared label (`0x31`)

```
byte  remove
ushort x
ushort y
byte  map
if remove:
    byte nameLen, ASCII name
else:
    byte descLen, ASCII description
    byte nameLen, ASCII name
```

> The client→server form (no name) and server→client form (with name) differ; the packet
> body layout above is on the **server → client** direction that we must parse.

---

## 6. TazUO integration points (verified APIs)

### 6.1 Injecting a server→client packet (for the config gump)

`ClassicUO.Network.PacketHandlers.PacketParser` (internal, same assembly):

```csharp
// src/ClassicUO.Client/Network/PacketHandlers/PacketParser.cs
public void Append(Span<byte> data, bool fromPlugins);
public static readonly PacketParser Instance;   // singleton
```

Append a fully-formed UO packet and it will be dispatched to the normal handler
(`0xB0` → `Network/PacketHandlers/OpenGump.cs::Handle`). This is exactly the path plugins
use (`src/ClassicUO.Client/Network/Plugin.cs::OnPluginRecv`).

```csharp
PacketParser.Instance.Append(gumpPacket, true);
```

### 6.2 Intercepting a client→server packet (for the gump reply)

`ClassicUO.Network.AsyncNetClient.Send` already calls `Plugin.ProcessSendPacket(ref message)`
before encryption (`src/ClassicUO.Client/Network/AsyncNetClient.cs`, around line 403):

```csharp
public void Send(Span<byte> message, bool ignorePlugin = false, bool skipEncryption = false)
{
    if (!IsConnected || message is [])
        return;

    if (!ignorePlugin && Plugin.Enabled && !Plugin.ProcessSendPacket(ref message))
        return;
    // ... encryption / enqueue
}
```

Add one line **before** that check (so it works even when plugin support is disabled):

```csharp
// EnhancedMap: consume replies to our own config gump
if (EnhancedMapManager.HandleOutgoingPacket(ref message))
    return;
```

`HandleOutgoingPacket` returns `true` when it has consumed the packet (i.e. the packet
must be dropped and never reach the shard).

### 6.3 Player state and gumps

- Local player: `World.Player` (`ClassicUO.Game.GameObjects.Player` / `Mobile`).
  - `Player.X`, `Player.Y`, `Player.Z`
  - `Player.Hits`, `Player.HitsMax`, `Player.Stamina`, `Player.StaminaMax`,
    `Player.Mana`, `Player.ManaMax`
  - facet: `(byte)Player.Map`
- Add gumps / windows: `UIManager.Add(...)` (`ClassicUO.Game.UI.UIManager`).
- Send a normal gump reply (what the client does when a server gump button is clicked):
  `GameActions.ReplyGump(localSerial, serverSerial, buttonId, switches, entries)`
  (`ClassicUO.Network` / `ClassicUO.Game.Managers.GameActions`).

---

## 7. `EnhancedMapConfig` — settings

Store next to the other TazUO data. Keep it simple JSON so it does not depend on the
profile system.

```csharp
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ClassicUO.Game.Managers.EnhancedMap;

internal sealed class EnhancedMapConfig
{
    public string Address { get; set; } = "";
    public int Port { get; set; } = 8887;
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";

    [JsonIgnore]
    public bool IsConfigured => !string.IsNullOrWhiteSpace(Address) && Port is > 0 and <= 65535;

    [JsonIgnore]
    public static string FilePath => Path.Combine(
        ClassicUO.Utility.CUOEnviroment.ExecutablePath, "Data", "EnhancedMap.json");

    public static EnhancedMapConfig Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<EnhancedMapConfig>(File.ReadAllText(FilePath))
                       ?? new EnhancedMapConfig();
        }
        catch { /* ignore, fall through to defaults */ }

        return new EnhancedMapConfig();
    }

    public bool Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath,
                JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
            return true;
        }
        catch { return false; }
    }
}
```

---

## 8. `EnhancedMapClient` — the EnhancedMap socket

This is a direct port of the plugin's `EmBridgeClient.cs` into TazUO. It owns a
background thread, performs the handshake, sends player data on demand, and raises a
callback for each incoming packet.

```csharp
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace ClassicUO.Game.Managers.EnhancedMap;

internal sealed class EnhancedMapClient : IDisposable
{
    private readonly List<byte> _rx = new();
    private readonly object _sendLock = new();

    private Socket _socket;
    private Thread _thread;
    private volatile bool _running;
    private volatile bool _accepted;

    private string _host = "", _user = "", _pass = "";
    private int _port;

    public bool IsAlive => _socket != null;
    public bool IsAccepted => _accepted;

    /// <summary>Raised on the receive thread with (packetId, payload).</summary>
    public Action<byte, byte[]> OnPacket;

    public void Connect(string host, int port, string user, string pass)
    {
        Disconnect();
        _host = host; _port = port; _user = user; _pass = pass;
        _rx.Clear();
        _running = true;
        _thread = new Thread(Run) { IsBackground = true, Name = "EnhancedMap" };
        _thread.Start();
    }

    private void Run()
    {
        try
        {
            IPAddress[] addrs = Dns.GetHostAddresses(_host);
            IPAddress addr = Array.Find(addrs, a => a.AddressFamily == AddressFamily.InterNetwork)
                             ?? (addrs.Length > 0 ? addrs[0] : null);
            if (addr == null) return;

            var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
            {
                NoDelay = true
            };
            socket.Connect(new IPEndPoint(addr, _port));
            _socket = socket;

            SendRaw(new EmPacket(0x2B).U8(0).Build()); // protocol request

            var buffer = new byte[8192];
            while (_running)
            {
                int n;
                try { n = socket.Receive(buffer); }
                catch (SocketException) { break; }
                if (n <= 0) break;
                Parse(buffer, n);
            }
        }
        catch { /* connection error; reconnect is driven by the manager tick */ }
        finally
        {
            _accepted = false;
            _running = false;
            try { _socket?.Dispose(); } catch { }
            _socket = null;
        }
    }

    private void Parse(byte[] data, int len)
    {
        for (int i = 0; i < len; i++) _rx.Add(data[i]);

        while (true)
        {
            if (_rx.Count < 3) return;
            int id = _rx[0];
            int size = _rx[1] | (_rx[2] << 8);
            if (size < 3) { _rx.Clear(); return; }
            if (_rx.Count < size) return;

            byte[] payload = _rx.GetRange(3, size - 3).ToArray();
            _rx.RemoveRange(0, size);
            Handle(id, payload);
        }
    }

    private void Handle(byte id, byte[] payload)
    {
        switch (id)
        {
            case 0x2B: // protocol response -> login
                SendLogin();
                break;
            case 0x1F: // login response
                _accepted = payload.Length > 0 && payload[0] == 1;
                if (!_accepted) _running = false;
                break;
        }

        OnPacket?.Invoke(id, payload);
    }

    private void SendLogin()
    {
        byte[] u = Encoding.ASCII.GetBytes(_user);
        byte[] p = Encoding.ASCII.GetBytes(_pass);
        var pkt = new EmPacket(0x1F);
        pkt.U8((byte)u.Length).U8((byte)p.Length).Raw(u).Raw(p);
        SendRaw(pkt.Build());
    }

    public void SendPlayerData(ushort x, ushort y, byte map,
        ushort hp, ushort stam, ushort mana,
        ushort maxhp, ushort maxstam, ushort maxmana,
        byte flag, bool panic, uint color,
        string font, float fontSize, byte fontStyle)
    {
        if (!IsAlive || !_accepted) return;

        byte[] fontBytes = Encoding.ASCII.GetBytes(font);
        var p = new EmPacket(0x25);
        p.U16(x).U16(y).U8(map)
         .U16(hp).U16(stam).U16(mana)
         .U16(maxhp).U16(maxstam).U16(maxmana)
         .U8(flag)
         .U8(0)                 // legacy death byte
         .U8((byte)(panic ? 1 : 0))
         .U32(color)
         .U8((byte)fontBytes.Length).Raw(fontBytes)
         .F32(fontSize)
         .U8(fontStyle);
        SendRaw(p.Build());
    }

    private void SendRaw(byte[] data)
    {
        Socket socket = _socket;
        if (socket == null) return;
        lock (_sendLock)
        {
            try
            {
                int sent = 0;
                while (sent < data.Length)
                {
                    int n = socket.Send(data, sent, data.Length - sent, SocketFlags.None);
                    if (n <= 0) break;
                    sent += n;
                }
            }
            catch { _running = false; }
        }
    }

    public void Disconnect()
    {
        _running = false;
        _accepted = false;
        try { _socket?.Shutdown(SocketShutdown.Both); } catch { }
        try { _socket?.Close(); } catch { }
        _socket = null;
    }

    public void Dispose() => Disconnect();
}

/// <summary>Little-endian writer with the EnhancedMap 3-byte header.</summary>
internal sealed class EmPacket
{
    private readonly List<byte> _b = new(32);

    public EmPacket(byte id) { _b.Add(id); _b.Add(0); _b.Add(0); }

    public EmPacket U8(byte v) { _b.Add(v); return this; }
    public EmPacket U16(ushort v) { _b.Add((byte)v); _b.Add((byte)(v >> 8)); return this; }
    public EmPacket U32(uint v)
    {
        _b.Add((byte)v); _b.Add((byte)(v >> 8)); _b.Add((byte)(v >> 16)); _b.Add((byte)(v >> 24));
        return this;
    }
    public EmPacket F32(float v) { _b.AddRange(BitConverter.GetBytes(v)); return this; }
    public EmPacket Raw(byte[] data) { _b.AddRange(data); return this; }

    public byte[] Build()
    {
        int len = _b.Count;
        _b[1] = (byte)(len & 0xff);
        _b[2] = (byte)((len >> 8) & 0xff);
        return _b.ToArray();
    }
}
```

---

## 9. Config gump via server-side gump packets

### 9.1 UO gump display packet `0xB0` (server → client)

```
byte   0xB0
ushort length                 // big-endian, total packet length
uint   serial                 // gump serial (our id; echoed back in the reply)
uint   gumpID                 // gump type / definition id
int    x                      // big-endian
int    y                      // big-endian
ushort layoutLength           // big-endian, bytes of the layout text
byte[] layout                 // ASCII gump command string
ushort textLineCount          // big-endian
repeat textLineCount times:
    ushort charCount          // big-endian (number of UTF-16 code units)
    byte[] utf16be            // charCount * 2 bytes, big-endian unicode
```

Parser: `src/ClassicUO.Client/Network/PacketHandlers/OpenGump.cs`
(`ReadUInt32BE` → serial, gumpID, x, y; `ReadUInt16BE` → cmd length; `ReadASCII`;
`ReadUInt16BE` line count; per line `ReadUInt16BE` then `ReadUnicodeBE`).

> UO packets use **big-endian** lengths/fields even though EnhancedMap uses
> little-endian. Do not mix the two.

### 9.2 Gump reply packet `0xB1` (client → server)

```
byte   0xB1
ushort length                 // big-endian
uint   serial                 // the serial from 0xB0 (use this to recognise our gump)
uint   gumpID                 // the gumpID from 0xB0
uint   buttonId
uint   switchCount
repeat switchCount times: uint switchId
uint   entryCount
repeat entryCount times:
    ushort entryId            // = the textentry id from the layout
    ushort charCount          // big-endian
    byte[] utf16be
```

Reference (client): `ClassicUO.Network.OutgoingPackets.Send_GumpResponse`
(`src/ClassicUO.Client/Network/OutgoingPackets.cs`). The reply is produced by
`Gump.OnButtonClick` → `GameActions.ReplyGump(...)`.

### 9.3 Layout command syntax (as parsed by TazUO)

Source: `Network/PacketHandlers/Helpers/GumpHelpers.cs`.

| Command | Template |
|---|---|
| background | `{ resizepic x y gumpId width height }` |
| label | `{ text x y hue lineIndex }` (`lineIndex` indexes the text-lines array) |
| text entry | `{ textentry x y width height hue entryId lineIndex }` (`lineIndex` = initial text; must be a valid index into the lines array) |
| reply button | `{ button x y normalId pressedId 1 page buttonId }` (action `1` = activate/reply) |
| page button | `{ button x y normalId pressedId 0 page buttonId }` (action `0` = switch page) |
| checkbox | `{ checkbox x y inactiveId activeId initialState switchId }` |
| radio | `{ radio x y inactiveId activeId initialState switchId }` |

Notes:
- `textentry`'s initial text is an **index** into the shared text-lines array, not a
  quoted literal. Always point it at a valid index (use an empty string line for blank).
- Standard art ids: background `0x0A28`; OK/confirm button `0x0FB1`/`0x0FB2`;
  cancel button `0x0FB3`/`0x0FB4`.
- The first text entry in a gump receives keyboard focus automatically.

### 9.4 Building and injecting the gump

```csharp
using System;
using System.Collections.Generic;
using System.Text;
using ClassicUO.Network.PacketHandlers;

namespace ClassicUO.Game.Managers.EnhancedMap;

internal static class EnhancedMapGump
{
    // Pick fixed, distinctive ids so they don't collide with real server gumps.
    public const uint ConfigSerial = 0x0E4D0001;
    public const uint ConfigGumpId = 0x0E4D0002;

    public const int ButtonSave = 1;
    public const int ButtonCancel = 0;

    public const ushort EntryAddress = 1;
    public const ushort EntryPort = 2;
    public const ushort EntryUsername = 3;
    public const ushort EntryPassword = 4;

    public static void Show(EnhancedMapConfig cfg)
    {
        var lines = new List<string>
        {
            "EnhancedMap Setup",          // 0 title
            "Server Address:",            // 1
            "Port:",                      // 2
            "Username:",                  // 3
            "Password:",                  // 4
            cfg.Address ?? "",            // 5 initial address
            cfg.Port.ToString(),          // 6 initial port
            cfg.Username ?? "",           // 7 initial username
            cfg.Password ?? "",           // 8 initial password
            "Save",                       // 9
            "Cancel"                      // 10
        };

        const string layout =
            "{ resizepic 0 0 0x0A28 430 265 }" +
            "{ text 130 20 0x0386 0 }" +
            "{ text 30 70 0x0481 1 }" +
            "{ textentry 150 65 250 22 0x0481 1 5 }" +
            "{ text 30 105 0x0481 2 }" +
            "{ textentry 150 100 250 22 0x0481 2 6 }" +
            "{ text 30 140 0x0481 3 }" +
            "{ textentry 150 135 250 22 0x0481 3 7 }" +
            "{ text 30 175 0x0481 4 }" +
            "{ textentry 150 170 250 22 0x0481 4 8 }" +
            "{ button 100 220 0x0FB1 0x0FB2 1 0 1 }" +
            "{ button 250 220 0x0FB3 0x0FB4 1 0 0 }" +
            "{ text 155 227 0x0481 9 }" +
            "{ text 305 227 0x0481 10 }";

        byte[] packet = BuildGumpPacket(ConfigSerial, ConfigGumpId, 220, 160, layout, lines);
        PacketParser.Instance.Append(packet, true);
    }

    private static byte[] BuildGumpPacket(
        uint serial, uint gumpId, int x, int y, string layout, IReadOnlyList<string> lines)
    {
        var body = new List<byte>(256);

        WriteU32BE(body, serial);
        WriteU32BE(body, gumpId);
        WriteU32BE(body, (uint)x);
        WriteU32BE(body, (uint)y);

        byte[] layoutBytes = Encoding.ASCII.GetBytes(layout);
        WriteU16BE(body, (ushort)layoutBytes.Length);
        body.AddRange(layoutBytes);

        WriteU16BE(body, (ushort)lines.Count);
        foreach (string line in lines)
        {
            byte[] utf16 = Encoding.BigEndianUnicode.GetBytes(line ?? "");
            WriteU16BE(body, (ushort)(utf16.Length / 2));
            body.AddRange(utf16);
        }

        int total = body.Count + 3;
        var packet = new byte[total];
        packet[0] = 0xB0;
        packet[1] = (byte)((total >> 8) & 0xff);
        packet[2] = (byte)(total & 0xff);
        body.CopyTo(packet, 3);
        return packet;
    }

    private static void WriteU16BE(List<byte> b, ushort v)
    {
        b.Add((byte)(v >> 8));
        b.Add((byte)v);
    }

    private static void WriteU32BE(List<byte> b, uint v)
    {
        b.Add((byte)(v >> 24));
        b.Add((byte)(v >> 16));
        b.Add((byte)(v >> 8));
        b.Add((byte)v);
    }
}
```

### 9.5 Parsing the `0xB1` reply

This is the counterpart to the injection above. It runs from the outgoing-packet hook.

```csharp
// Returns true when the packet was our gump reply and must be blocked.
public static bool HandleOutgoingPacket(ref Span<byte> message)
{
    if (message.Length < 15 || message[0] != 0xB1)
        return false;

    uint serial = ReadU32BE(message, 3);
    if (serial != EnhancedMapGump.ConfigSerial)
        return false;

    int p = 7;
    uint gumpId = ReadU32BE(message, p); p += 4;
    uint button = ReadU32BE(message, p); p += 4;

    uint switchCount = ReadU32BE(message, p); p += 4;
    p += (int)switchCount * 4;                       // checkboxes (unused here)

    uint entryCount = ReadU32BE(message, p); p += 4;
    var entries = new Dictionary<ushort, string>();
    for (int i = 0; i < entryCount && p + 4 <= message.Length; i++)
    {
        ushort entryId = ReadU16BE(message, p); p += 2;
        ushort chars = ReadU16BE(message, p); p += 2;
        int bytes = chars * 2;
        if (p + bytes > message.Length) bytes = message.Length - p;
        string text = Encoding.BigEndianUnicode.GetString(message.Slice(p, bytes)).TrimEnd('\0').Trim();
        p += chars * 2;
        entries[entryId] = text;
    }

    if (button == EnhancedMapGump.ButtonSave)
        ClassicUO.Game.Managers.EnhancedMapManager.ApplyConfigFromGump(entries);

    return true; // block: never send this to the shard
}

private static ushort ReadU16BE(Span<byte> b, int o) => (ushort)((b[o] << 8) | b[o + 1]);
private static uint ReadU32BE(Span<byte> b, int o) =>
    ((uint)b[o] << 24) | ((uint)b[o + 1] << 16) | ((uint)b[o + 2] << 8) | b[o + 3];
```

`ApplyConfigFromGump` reads entries with keys `EntryAddress/Port/Username/Password`,
validates the port, stores the config, saves it, and connects.

### 9.6 Alternative for a direct, in-client implementation

Because this code lives *inside* TazUO, you can skip the `0xB0`/`0xB1` round-trip
entirely and derive a class from `ClassicUO.Game.UI.Gumps.Gump` (as `MessageBoxGump`
does), adding `StbTextBoxEN` controls and calling `ApplyConfigFromGump` directly from an
`OnButtonClick` override. The packet approach above is still documented because it is
required if the feature is ever shipped as a plugin, and because the request was
specifically to use server-side gumps.

---

## 10. `EnhancedMapManager` — orchestration

Responsibilities:

1. Load config on startup.
2. On entering world, if not configured, `EnhancedMapGump.Show(config)` (deferred ~2 s so
   the keystroke that entered the world does not close it immediately — this was the
   reason the plugin deferred prompts; the same applies to injected gumps).
3. Once configured, connect the `EnhancedMapClient`.
4. On `Client.Game` tick (every frame), send player data if ≥ 1 s elapsed and reconnect
   if the socket has died (backoff ~10 s).
5. Handle incoming EnhancedMap packets (5.2) and surface them to the map/UI.

```csharp
using System;
using ClassicUO.Game.Managers.EnhancedMap;

namespace ClassicUO.Game.Managers;

internal static class EnhancedMapManager
{
    private static EnhancedMapConfig _config = EnhancedMapConfig.Load();
    private static EnhancedMapClient _client;
    private static bool _initialized;

    private static bool _gumpQueued;
    private static DateTime _gumpQueuedAt;
    private static DateTime _lastSend = DateTime.MinValue;
    private static DateTime _lastConnect = DateTime.MinValue;

    // snapshot fields (written on the game thread, read on the network thread)
    private static ushort _x, _y, _hp, _stam, _mana, _maxHp, _maxStam, _maxMana;
    private static byte _map, _flag;

    public static void Load()
    {
        _initialized = true;
        _client = new EnhancedMapClient { OnPacket = OnEmPacket };
        if (_config.IsConfigured)
            Connect();
    }

    public static void OnEnterWorld()
    {
        if (_config.IsConfigured)
        {
            Connect();
            return;
        }

        _gumpQueued = true;                  // show config gump shortly after entering
        _gumpQueuedAt = DateTime.UtcNow;
    }

    /// <summary>Call from the game update loop.</summary>
    public static void Update()
    {
        if (!_initialized) return;

        if (_gumpQueued && (DateTime.UtcNow - _gumpQueuedAt) >= TimeSpan.FromSeconds(2))
        {
            _gumpQueued = false;
            EnhancedMapGump.Show(_config);
        }

        if (!_config.IsConfigured) return;

        if (!_client.IsAlive && (DateTime.UtcNow - _lastConnect) > TimeSpan.FromSeconds(10))
            Connect();

        if (!_client.IsAccepted) return;

        UpdateSnapshot();

        if (DateTime.UtcNow - _lastSend >= TimeSpan.FromSeconds(1))
        {
            _lastSend = DateTime.UtcNow;
            _client.SendPlayerData(_x, _y, _map,
                _hp, _stam, _mana, _maxHp, _maxStam, _maxMana,
                _flag, false, 0xFFFFFFFF, "Segoe UI", 12f, 0);
        }
    }

    private static void UpdateSnapshot()
    {
        var p = World.Player;
        if (p == null) return;

        _x = (ushort)Math.Clamp(p.X, 0, ushort.MaxValue);
        _y = (ushort)Math.Clamp(p.Y, 0, ushort.MaxValue);
        _map = (byte)p.Map;

        _hp = p.Hits;      _maxHp = p.HitsMax;
        _stam = p.Stamina; _maxStam = p.StaminaMax;
        _mana = p.Mana;    _maxMana = p.ManaMax;

        _flag = _maxHp > 0 && _hp == 0 ? (byte)4 : (byte)0;
    }

    private static void Connect()
    {
        _lastConnect = DateTime.UtcNow;
        _client.Connect(_config.Address, _config.Port, _config.Username, _config.Password);
    }

    public static void ApplyConfigFromGump(System.Collections.Generic.Dictionary<ushort, string> e)
    {
        _config.Address = e.GetValueOrDefault(EnhancedMapGump.EntryAddress, "").Trim();
        _config.Port = int.TryParse(e.GetValueOrDefault(EnhancedMapGump.EntryPort, ""), out int port)
                       && port is > 0 and <= 65535 ? port : 8888;
        _config.Username = e.GetValueOrDefault(EnhancedMapGump.EntryUsername, "").Trim();
        _config.Password = e.GetValueOrDefault(EnhancedMapGump.EntryPassword, "") ?? "";
        _config.Save();

        _client.Disconnect();
        Connect();
    }

    // --- hooks -------------------------------------------------------------

    // Called from AsyncNetClient.Send (see 6.2)
    public static bool HandleOutgoingPacket(ref Span<byte> message)
        => EnhancedMapGump.HandleOutgoingPacket(ref message);

    // Called on the EnhancedMap network thread
    private static void OnEmPacket(byte id, byte[] payload)
    {
        // Dispatch to your map/overlay UI. See section 5.2 for layouts.
    }
}
```

Wiring the hooks:

- **Enter world:** in `Network/PacketHandlers/EnterWorld.cs` (or wherever the world scene
  becomes active) call `EnhancedMapManager.OnEnterWorld()`.
- **Update:** in `GameScene.Update` / `Client.Game.Update` call `EnhancedMapManager.Update()`.
- **Outgoing packets:** add the `HandleOutgoingPacket` check to `AsyncNetClient.Send`
  (section 6.2).
- **Startup:** call `EnhancedMapManager.Load()` during client/game initialization.

---

## 11. Handling incoming data (rendering other players)

For each incoming packet (section 5.2):

- `0x29` UserConnection → create/remove a tracked user object.
- `0x25` UserData → update that user's position, facet, vitals, flag and panic state.
- `0x23` ChatMessage → show in a chat window / system log.
- `0x27` AlertAdvice → drop a temporary marker at (x, y) and log.
- `0x31` SharedLabel → add/remove a labelled marker.
- `0x2F` ServerMessage → system log with the given colour.
- `0x2D` ChatMessageResponse → chat send acknowledgment (optional).

Flag mapping used by the EnhancedMap client for `0x25`:

| flag | meaning |
|---|---|
| 0 | normal |
| 1 | poisoned |
| 2 | yellow hits |
| 3 | paralyzed |
| 4 | dead |
| 5 | hidden |

The reference implementation of all of this is
`src/EnhancedMap.Core/Core/Network/Packets/PacketHandlers.cs` (`PlayerData`,
`UserConnection`, `NewAlert`, `NewChatMessage`, `SharedLabel`, `ServerMessage`) and the
object/model classes under `src/EnhancedMap.Core/Core/MapObjects/` and
`src/EnhancedMap.Core/Drawing/`.

---

## 12. Implementation checklist

1. Add `EnhancedMapConfig`, `EnhancedMapClient`, `EnhancedMapGump`, `EnhancedMapManager`
   (sections 7–10).
2. `AsyncNetClient.Send`: add the `HandleOutgoingPacket` guard (section 6.2).
3. `EnterWorld` handler: call `EnhancedMapManager.OnEnterWorld()`.
4. `GameScene.Update` / `Client.Game.Update`: call `EnhancedMapManager.Update()`.
5. Client/game init: call `EnhancedMapManager.Load()`.
6. Wire `OnEmPacket` to the map/overlay UI (section 11).
7. Test matrix:
   - first run (no config) → gump appears after ~2 s in world
   - valid config → `0x2B`/`0x1F` handshake succeeds, `0x25` sent every second
   - bad credentials → server closes; client retries with backoff
   - gump Save writes `Data/EnhancedMap.json` and reconnects
   - gump Cancel dismisses without changes
   - gump reply never reaches the shard (packet logger shows no `0xB1`)

---

## 13. Reference: current plugin implementation

The plugin is a working implementation of the EnhancedMap side; use it as the source of
truth when porting. (It has been removed from this repository; the table records where
each concern lived in the original `src/EnhancedMap.TazUO/` plugin.)

| Concern | Original plugin file |
|---|---|
| EnhancedMap protocol client | `src/EnhancedMap.TazUO/EmBridgeClient.cs` |
| Settings persistence | `src/EnhancedMap.TazUO/BridgeConfig.cs` |
| Setup gump replacement (current prompt flow) | `src/EnhancedMap.TazUO/Actions.cs` |
| Server-side packet injection helpers | `src/EnhancedMap.TazUO/ClientPackets.cs` |
| Plugin entry point / hooks | `src/EnhancedMap.TazUO/Engine.cs` |

Server-side truth for every packet layout:

| Packet | Server file |
|---|---|
| `0x1F` login req/resp | `EnhancedMapServerNetCore/Network/PacketHandlers.cs`, `Network/Packets.cs` (`PLoginResponse`) |
| `0x2B` protocol | `Network/PacketHandlers.cs`, `Network/Packets.cs` (`PProtocolResponse`) |
| `0x25` player data | `Network/PacketHandlers.cs` (`UserData`), `Network/Packets.cs` (`PUserData`) |
| `0x23` chat | `Network/Packets.cs` (`PChatMessage`) |
| `0x27` alert | `Network/Packets.cs` (`PAlertAdvice`) |
| `0x29` user connection | `Network/Packets.cs` (`PUserConnection`) |
| `0x2F` server message | `Network/Packets.cs` (`PServerMessage`) |
| `0x31` shared label | `Network/Packets.cs` (`PSharedLabel`) |
| framing / header | `Network/CircularBuffer.cs`, `Network/PacketWriter.cs` |
| login/account rules | `Managers/AccountManager.cs`, `Managers/UserManager.cs`, `Internals/Room.cs` |
| default port | `Configuration/Config.cs` (`Port = 8887`) |
