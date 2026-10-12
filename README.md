# EnhancedMap

A free, cross-platform Ultima Online mapping tool. The client and server have been fully
rewritten on **.NET 10** with an **Avalonia** UI and **SkiaSharp** rendering, replacing the
original WinForms/GDI+ desktop. Both the desktop app and the server now run on **Windows,
Linux and macOS (Apple Silicon)** — the old desktop was Windows-only.

## Cross-platform, updated client & server

- The desktop app and server are both cross-platform and share the same `EnhancedMap.Core`
  networking/protocol code.
- Releases contain **separate, self-contained zips per platform/architecture**:
  Windows (x64), Linux (x64) and macOS (Apple Silicon / arm64). Self-contained means the
  .NET runtime is bundled — no SDK or runtime install is required to run them.
- **Version compatibility:** the desktop app and server were updated together and are
  released as a matched pair. The new desktop app may **not work with older server
  installations** (and older clients may not work with the new server). Always run the
  desktop and server from the **same release**.

## Projects

| Project | Description |
| --- | --- |
| `src/EnhancedMap.Core` | Platform-independent core: networking/protocol, settings, map model, and the SkiaSharp map renderer/generator. |
| `src/EnhancedMap.Desktop` | Cross-platform Avalonia desktop app (main map window, chat, settings, Map Maker, tools). Runs standalone. |
| `EnhancedMapServerNetCore` | The cross-platform server that EnhancedMap clients connect to. |

## Downloads

Grab the latest builds from the
[**latest release**](https://github.com/PlayTazUO/EnhancedMap/releases/latest). Each asset is
a self-contained zip — extract it and run the executable inside; nothing else needs to be
installed.

| Asset | Platform |
| --- | --- |
| `EnhancedMap-Desktop-win-x64.zip` | Desktop — Windows (x64) |
| `EnhancedMap-Desktop-linux-x64.zip` | Desktop — Linux (x64) |
| `EnhancedMap-Desktop-osx-arm64.zip` | Desktop — macOS (Apple Silicon) |
| `EnhancedMapServer-win-x64.zip` | Server — Windows (x64) |
| `EnhancedMapServer-linux-x64.zip` | Server — Linux (x64) |
| `EnhancedMapServer-osx-arm64.zip` | Server — macOS (Apple Silicon) |

The desktop executable is `EnhancedMap.Desktop` (`EnhancedMap.Desktop.exe` on Windows) and
the server executable is `EnhancedMapServer` (`EnhancedMapServer.exe` on Windows).

> On Linux/macOS, if the extracted executable is not marked executable, run
> `chmod +x EnhancedMap.Desktop` (or `EnhancedMapServer`) once. On macOS you may also need to
> allow the unsigned app via **System Settings → Privacy & Security → Open Anyway**.

---

## Desktop app setup

### 1. Install / extract

Download the desktop zip for your platform and extract it to a folder of your choice. Keep
the included `Definitions/` and `Icon/` folders next to the executable — they are required at
runtime and ship inside the zip.

### 2. First run — maps

If no maps are present, the **Map Maker** opens automatically:

1. Click **Browse...** and select your Ultima Online client folder. EnhancedMap scans it for
   terrain files (`mapN.mul` / `mapNLegacyMUL.uop`) and lists every map it finds.
2. Each entry is pre-filled with its size — detected from the file, or the standard facet
   dimensions. Adjust the index, name, width and height if your shard uses custom sizes, and
   remove any maps you don't want.
3. Click **Generate**.

It reads `radarcol.mul`, the terrain maps and `staidxN.mul` / `staticsN.mul`, then writes the
maps as PNGs into a `Maps/` folder next to the app. (`facet0N.dds` sources are not supported
yet.)

Map generation can be re-run any time from the toolbar.

### 3. Connect to an EnhancedMap server

Open **Settings → Network** and fill in:

| Field | Notes |
| --- | --- |
| **Username** | Must match an account on the server (max 16 chars). |
| **Password** | Must match that account (max 16 chars). |
| **Server IP** | Hostname or IP of the machine running `EnhancedMapServer`. |
| **Server port** | Default `8887` (must match the server's configured port). |
| **Remember credentials** | Stores username/password. |
| **Remember server** | Stores the IP/port. |
| **Auto login at start** | Connects automatically on launch. |
| **Try to reconnect** + reconnect delay | Automatically reconnects if the connection drops. |

Press **Connect**. A status indicator on the map shows the connection state. Once connected
you will see other players in your room, their vitals, chat, alerts and shared labels.

### 4. Desktop files

Everything is stored next to the executable:

- `Settings.xml` — application settings (including the connection details above).
- `Maps/` — generated/downloaded map images.
- `Definitions/`, `Icon/` — data shipped with the release.

---

## Server setup

The server (`EnhancedMapServer`) is a small console application. It stores its configuration
in a `Data/` folder next to the executable.

### 1. Install / extract

Download the server zip for your platform and extract it. Run the `EnhancedMapServer`
executable (or `EnhancedMapServer.exe` on Windows). The server listens on TCP port **8887**
by default.

### 2. First run — interactive setup

On first launch, if `Data/Server.xml`, `Data/Rooms.xml` or `Data/Accounts.xml` are missing,
the server walks you through setup. Press **Enter** to accept any value shown between `[ ]`:

- **Server settings** — port, kick timer (relog cooldown, seconds), credentials system
  (`0` = username + password, `1` = password only), max simultaneous connections per user,
  max active connections, and whether to auto-create accounts on first connect.
- **Room settings** — room name (default `General`) and, in password-only mode, a room
  password.
- **Administrator account** — username, password, access level
  (`0` = Normal, `1` = RoomAdmin, `2` = ServerAdmin) and room.

The configuration is written to the `Data/` folder and the server starts. Add more users and
rooms later with the console commands below, or by electing to auto-create accounts.

> If the server is started non-interactively (with `-headless`, or when the console input is
> redirected), it skips the prompts and uses safe defaults: port 8887, a `General` room, and
> no accounts. In that case create at least one account with `/adduser` (see below).

### 3. Console commands

While running, the server accepts commands on its console. Type `/help` for the full list.
Common ones:

| Command | Purpose |
| --- | --- |
| `/adduser` | Add a user (prompts for username, password, room, level). |
| `/removeuser` | Remove a user. |
| `/setpassword` | Set a user's password. |
| `/setprivileges` | Set a user's account level. |
| `/addroom` / `/removeroom` | Add or remove a room. |
| `/setroom` | Assign a user to a room. |
| `/setroompassword` / `/getroompassword` | Manage room passwords (password-only mode). |
| `/setloginsys` | Choose username + password or password-only per room. |
| `/allusers` / `/allrooms` / `/allusersonline` / `/allusersinroom` | List users and rooms. |
| `/setport` / `/getport` | Change or read the listening port. |
| `/settings` | List, get or set server settings (e.g. `/settings autocreateaccounts true`). |
| `/setkicktime` | Relog cooldown in seconds. |
| `/sendmsg` | Broadcast a message to all connected users. |
| `/kick` / `/ban` / `/enableuser` | Moderate users. |
| `/save` | Persist accounts, rooms and settings to disk. |
| `/restart` / `/exit` | Restart or stop the server. |

### 4. Command-line flags

| Flag | Effect |
| --- | --- |
| `-headless` | Skip interactive setup/prompts (use defaults / existing config). |
| `-external` | Run in external mode. |
| `-log` | Also write logs to `log.txt` in the current directory. |

A crash is written to `crash.txt` in the current directory.

### 5. Server files

| Path | Contents |
| --- | --- |
| `Data/Server.xml` | Server settings (port, timers, connection limits, credentials system). |
| `Data/Rooms.xml` | Rooms and their passwords. |
| `Data/Accounts.xml` | Accounts (username, password, room, level). |
| `Backup/` | Automatic backups of the above. |
| `log.txt` / `crash.txt` | Written when started with `-log` / on a crash. |

### 6. Networking

- Default port: **TCP 8887**. Allow it through your firewall and, if hosting publicly,
  forward it on your router.
- The server can host many rooms; each room is an isolated group of users. Clients only see
  other users in their own room.
- Access levels: `Normal` (regular user), `RoomAdmin` (manage a room), `ServerAdmin`
  (manage everything, can use the in-client server command panel).

---

## Build from source

Requires the **.NET 10 SDK**.

```sh
# build everything
dotnet build EnhancedMap.slnx

# run the desktop app
dotnet run --project src/EnhancedMap.Desktop

# run the server
dotnet run --project EnhancedMapServerNetCore/EnhancedMapServerNetCore
```

To produce a self-contained, cross-platform build for a specific platform:

```sh
# desktop
dotnet publish src/EnhancedMap.Desktop/EnhancedMap.Desktop.csproj -c Release \
  -r linux-x64 --self-contained true -o publish/desktop

# server (RID could be win-x64, linux-x64 or osx-arm64)
dotnet publish EnhancedMapServerNetCore/EnhancedMapServerNetCore/EnhancedMapServerNetCore.csproj \
  -c Release -r osx-arm64 --self-contained true -o publish/server
```

Releases are built automatically by GitHub Actions on every push to `master`; see
[`.github/workflows/release.yml`](.github/workflows/release.yml).

## TazUO integration

TazUO supports the EnhancedMap protocol built in. The former external TazUO plugin has
been removed.
