# Rate of Decay ONLINE — Developer & Feature Reference

> **Living document.** Update this file as features are added. Last updated: June 27, 2026.

---

## Table of Contents

1. [What This Game Is](#1-what-this-game-is)
2. [How to Play (Player Guide)](#2-how-to-play-player-guide)
3. [Controls Reference](#3-controls-reference)
4. [GM Console](#4-gm-console)
5. [UI Systems](#5-ui-systems)
6. [Multiplayer & Networking](#6-multiplayer--networking)
7. [Technical Architecture](#7-technical-architecture)
8. [VPS & Server Infrastructure](#8-vps--server-infrastructure)
9. [Scene Layout](#9-scene-layout)
10. [Character Classes](#10-character-classes)
11. [Asset Inventory](#11-asset-inventory)
12. [Known Issues & Pending Work](#12-known-issues--pending-work)
13. [Key Files Reference](#13-key-files-reference)

---

## 0. In-Game Features — Everything That Exists Right Now

A quick-reference card for what's actually built and how to access it. Start here.

### Always-On Systems (no setup, just exist)
- **Nameplates** — floating name + class above every other player. Auto-attached, always visible, fades at 40u.
- **Who's Online panel** — top-right corner, shows all connected players and their class. Press **P** to toggle.
- **ESC menu** — press **Escape** anywhere in the game world (not while typing). Shows Resume / Logout / Quit.
- **GM Console** — press **`** (backtick) or **F1**. Dark panel bottom-left. Type `help` to see all commands. Only works if your username is in the GM list — see [Section 4](#4-gm-console).

### Login Screen Features
- **Tab** cycles forward between Username → Password → Server IP fields
- **Shift+Tab** cycles backward
- Username field is auto-focused on load — you can start typing immediately
- Server IP is saved to PlayerPrefs — persists between play sessions

### In-World Controls (Hidden / Non-Obvious)
- **Right mouse held** — locks cursor and orbits camera. Release to free cursor for UI clicks. You do not need to press anything else.
- **Left mouse held** — also orbits camera (WoW convention: left mouse = move + turn)
- **Scroll wheel** — zoom camera in/out, from 1.5u to 20u
- **T** — opens chat (in addition to Enter)
- **Enter** while chat is open — sends the message
- **Click anywhere outside chat** — closes chat and frees cursor

### Chat System
- Messages are networked — everyone in the same scene sees them
- Chat fades in when active and fades out when idle
- The chat box never blocks mouse input when faded (explicit `blocksRaycasts = false` — see [Section 7](#7-technical-architecture) for why this matters)

### GM Console — What You Can Actually Do With It
The console is a runtime dev tool. Key use cases beyond the command list:

- **Level building:** `pos` prints coords → `tp x y z` teleports you there → iterate until you find the right spot
- **Confirm connectivity:** `players` lists every client the server knows about, with position. If someone is online but invisible, this tells you where they are.
- **Debugging spawns:** If a player is underground, `goto <name>` puts you next to them so you can see what's happening.
- **Speed testing:** `speed 5` + `fly` lets you traverse the Hub in seconds to check layout, portal placement, VFX.
- **God mode during builds:** `god` + `noclip` = walk through everything, can't die, useful when scene objects have wrong colliders.

### Who's Online Panel — Details
- Visible by default when you enter the world — not hidden until you press P
- Your character is listed first, marked with ★
- Colors match nameplates: Engineer=blue, Guardian=gold, Wraith=purple, Medic=green
- Refreshes the instant someone connects or disconnects (event-driven, not just a timer)
- Never intercepts mouse clicks — `blocksRaycasts = false` hardcoded

### Nameplates — Details
- Local player's nameplate is always hidden — you never see your own
- Two lines: Name (white, bold) above, Class (class color) below
- Text has a dark outline for readability against any background
- Billboard: always rotates to face the camera, no matter the angle
- The canvas that holds the nameplate is a separate GameObject from the player — it moves in LateUpdate so it doesn't inherit the player's rotation or scale

### ESC Menu — Details
- Does not intercept Escape while you're typing in a chat or console input field
- Logout button properly handles all three Mirror states: host, client-only, server-only
- Full-screen dark overlay is a child of the panel — so hiding the panel hides everything including the overlay (important: overlay as a sibling stays visible even when the menu is "closed")
- Sorting order 200 — always on top of chat and player list, below GM console (999)

---

## 1. What This Game Is

**Rate of Decay ONLINE** is a multiplayer 3rd-person action game built in Unity 6 using Mirror networking. Players log in, select a character class, and enter a shared social Hub world. From the Hub, portals lead to gameplay zones.

- **Engine:** Unity 6000.0.77f1
- **Networking:** Mirror (KCP transport, UDP)
- **Backend:** Node.js/Express + MySQL on a Linux VPS
- **Renderer:** Universal Render Pipeline (URP)

---

## 2. How to Play (Player Guide)

### Getting the Client
Download from **http://15.204.243.36** → click *Download for Windows* → unzip → run `RateOfDecayONLINE.exe`.

> ⚠️ Alpha — invite only. An account must be created via the web registration form before logging in.

### First Login Flow
1. **Login screen** — enter username, password, and server IP (`15.204.243.36`). Tab cycles between fields. Shift+Tab goes backward.
2. **Character Select** — choose your class, click *Enter World*.
3. **Hub** — social area with portals. Run around, chat with other players, choose a portal to enter a zone.

### Registering an Account
No in-game registration yet. Accounts are created via the server's web registration endpoint. Ask a GM or admin for access.

---

## 3. Controls Reference

### Movement
| Input | Action |
|-------|--------|
| W / S | Move forward / backward |
| A / D | Strafe left / right |
| Left Shift | Sprint |
| Space | Jump |

### Camera
| Input | Action |
|-------|--------|
| **Right mouse held** | Lock cursor + orbit camera (yaw + pitch) |
| **Left mouse held** | Lock cursor + orbit camera |
| **Scroll wheel** | Zoom in / out (1.5 – 20 units) |
| Release mouse button | Unlock cursor — click UI freely |

> Camera is WoW-style: cursor is **free by default**. Hold right or left mouse to orbit. Releasing returns the cursor so you can click chat, portals, etc.

### UI Shortcuts
| Input | Action |
|-------|--------|
| **Enter** or **T** | Open / send chat |
| **Escape** | Open ESC menu (Resume / Logout / Quit) |
| **P** | Toggle Who's Online panel |
| **Tab** | Cycle forward between login input fields |
| **Shift + Tab** | Cycle backward between login input fields |
| **` (backtick)** or **F1** | Toggle GM Console |

---

## 4. GM Console

### Access
Press `` ` `` (backtick, top-left of keyboard) or **F1** in-game. The console appears in the bottom-left corner of the screen.

### Access Control
Only usernames in the allowlist inside `Assets/Game/UI/GmConsole.cs` can use commands. Opening the console works for everyone, but commands return *Access denied* unless your username is approved.

**Current GM users:**
```
DevPlayer, brbmuffins, ForYurHealth, YaDingusMD, SleepyBoySteve
```

To add a GM: open `GmConsole.cs`, add the username to `GM_USERS`.

### Command Reference

| Command | Description |
|---------|-------------|
| `speed <n>` | Set movement speed multiplier. `speed 1` = normal, `speed 3` = 3× faster. |
| `fly` | Toggle fly mode. Disables gravity. Use **Space** to go up, **Left Ctrl** to go down. |
| `god` | Toggle invulnerability. |
| `heal` | Instantly heal to full HP. |
| `kill` | Kill all GameObjects tagged "Enemy" in the current scene. |
| `spawn [count]` | Spawn red enemy capsules near you (default 1, max 50). Uses `EnemyAI` + `Health` components. |
| `wave [n]` | Start the WaveManager. If `n` is provided, jump to that wave index. |
| `tp <x> <y> <z>` | Teleport your player to world coordinates. Example: `tp 0 1 0` |
| `pos` | Print your current world position to the console. Useful for placing objects. |
| `players` | List all connected players, their class, and world position. |
| `goto <name>` | Teleport yourself to another player by username. Example: `goto brbmuffins` |
| `noclip` | Toggle collision. Disables all colliders on your player — walk through walls. |
| `clear` | Clear the console log. |
| `help` | Print all commands. |

### Console Tips
- Use **Up / Down arrow** keys to scroll through command history.
- `pos` is your best friend when building levels — use it to get coordinates, then paste into `tp`.
- `players` is the fastest way to confirm both clients are connected and visible to the server.
- The console is client-side only. `spawn` creates local objects; they are not networked enemies.

---

## 5. UI Systems

All UI managers are **self-bootstrapping** — they create themselves at runtime via `[RuntimeInitializeOnLoadMethod]`. No scene objects or prefabs required. They persist across scene loads via `DontDestroyOnLoad`.

### Chat (`RodChatManager.cs`)
- Press **Enter** or **T** to open. Type a message and press **Enter** to send.
- Chat is networked via Mirror — all clients in the same scene see all messages.
- The chat canvas uses `CanvasGroup.blocksRaycasts = false` when hidden, so it never blocks mouse input for the camera. This was a critical bug fix — invisible canvases blocking `IsPointerOverGameObject()` prevented camera orbit entirely.

### ESC Menu (`EscMenu.cs`)
- Press **Escape** to open (does not intercept while typing in a text field).
- Three buttons: **Resume** (close menu), **Logout** (disconnect + return to Login screen), **Quit** (exit application).
- Sorting order 200 — renders on top of everything except GM console.
- The overlay and card are *children* of the panel — so `SetActive(false)` hides the entire thing including the transparent overlay. This matters because an overlay sibling can block all clicks even when invisible.

### Player List / Who's Online (`PlayerListUI.cs`)
- Press **P** to toggle. Visible by default on game start.
- Top-right corner. Auto-resizes based on player count.
- Shows player name and class, color-coded by class. Local player marked with ★.
- Refreshes immediately when any player joins or leaves, plus polls every 2 seconds.
- `CanvasGroup.blocksRaycasts = false` always — never intercepts mouse input.

### Nameplates (`PlayerNameplate.cs`)
- Floating world-space canvas 2.4 units above each player.
- Always faces the camera (billboard).
- Shows player name (white, bold) and class name (class color).
- **Local player's nameplate is hidden** — you don't need to see your own.
- Fades out smoothly between 20 and 40 world units from the camera.
- Auto-attached by `PlayerIdentity.OnStartClient()` — no prefab or component wiring required.
- Destroyed automatically when the player object is destroyed.

### GM Console (`GmConsole.cs`)
- See [Section 4](#4-gm-console) above.
- Sorting order 999 — renders on top of everything when open.
- Bottom-left, 55% screen width × 38% height.

---

## 6. Multiplayer & Networking

### How Players Connect
1. Client logs in → JWT returned from auth server (port 3000).
2. Client fetches character data using JWT → class and last saved position.
3. `RodNetworkAuthenticator` verifies the JWT server-side, stores `RodPlayerAuth` on `conn.authenticationData`.
4. Client sends `CreatePlayerMessage` (username + selected class as fallback).
5. Server reads auth data, picks the correct class prefab, determines spawn position, instantiates the player, calls `NetworkServer.AddPlayerForConnection`.

### Spawn Position Logic
1. **Saved position from DB** — used if the character has a non-zero saved position (i.e., logged out somewhere in the world).
2. **NetworkStartPosition** — if no saved position, Mirror picks from the `NetworkStartPosition` objects placed in the scene (8 points in a ring at radius 4 around the Hub center).
3. **Random ring scatter** — fallback if no `NetworkStartPosition` objects exist. Spawns at a random point 3 units from origin, Y=1.

> **Important:** First-time logins have `spawnX/Y/Z = 0` in the DB. This used to cause all players to spawn at `(0,0,0)` overlapping each other inside the ground. Fix: zero-check on DB coords — if all three are zero, treat as no saved position and use the scatter/start-position logic.

### SyncVars (Networked Data)
`PlayerIdentity.cs` syncs:
- `playerName` — username string
- `classIndex` — integer (0=Engineer, 1=Guardian, 2=Wraith, 3=Medic)

These are available on all clients as soon as the player spawns. Nameplates and PlayerListUI read from these.

### Position Saving (`RodPositionSaver.cs`)
- Attached to player GameObjects on the server at spawn time (if `auth.characterId > 0`).
- On disconnect or application quit, sends a POST to the auth server to save the player's last position.
- This is what populates `spawnX/Y/Z` in the DB for the next session.

### Class Prefabs
Registered in `RodNetworkManager`:
| Index | Class | Notes |
|-------|-------|-------|
| 0 | Engineer | |
| 1 | Guardian | |
| 2 | Wraith | |
| 3 | Medic | |

All class prefabs must be registered with `NetworkClient.RegisterPrefab()` on the client — done in `RodNetworkManager.OnStartClient()`.

---

## 7. Technical Architecture

### Auth System
```
Client                    Auth Server (port 3000)         Game Server (port 7777)
  │── POST /login ──────────────▶│                               │
  │◀── JWT token ────────────────│                               │
  │── GET /character (JWT) ──────▶│                               │
  │◀── RodPlayerAuth ────────────│                               │
  │─── Mirror connect ───────────────────────────────────────────▶│
  │    (AuthRequest with JWT)                                     │
  │                              │◀── GET /verify (JWT) ─────────│
  │                              │─── character data ────────────▶│
  │◀── AuthSuccess + spawn ──────────────────────────────────────│
```

JWT payload key: `accountId` (not `userId`). The FK column in the DB is `account_id`.

### Headless Server Detection
The dedicated server build uses IL2CPP targeting Linux. Unity sets `SystemInfo.graphicsDeviceType = GraphicsDeviceType.Null` in headless builds. All client-only code checks for this:

```csharp
if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
    return; // skip on dedicated server
```

`RodNetworkManager.Start()` detects this and calls `StartServer()` automatically — no manual start required.

### Camera System (`CameraFollow.cs`)
- **WoW-style**: cursor free by default, right or left mouse hold locks cursor and orbits.
- Position follows instantly (no smoothing) — smoothing caused character drift.
- First-frame delta discarded on entering orbit mode — prevents violent camera swing from accumulated mouse delta while cursor was free.
- `_typingInUI` flag (checks `EventSystem.currentSelectedGameObject` for `TMP_InputField`) prevents orbit while typing in chat or console.
- `IsPointerOverGameObject()` is NOT used — any invisible canvas with a `GraphicRaycaster` returns `true` permanently and breaks all orbit.

### Canvas Raycasting Rules
Critical pattern used throughout:
```csharp
canvasGroup.alpha = 0f;
canvasGroup.blocksRaycasts = false;  // MUST be explicit — alpha=0 alone does NOT stop raycasting
canvasGroup.interactable = false;
```
Every UI system that can be hidden follows this pattern. Forgetting `blocksRaycasts = false` causes invisible elements to eat mouse input.

---

## 8. VPS & Server Infrastructure

**Server IP:** `15.204.243.36`

### Services & Ports
| Port | Service | Notes |
|------|---------|-------|
| 22 | SSH | Root access |
| 80 | Nginx | Public download / landing page |
| 3000 | Node.js auth server (`rod-auth`) | **DO NOT TOUCH** — handles all JWT auth |
| 4000 | Manager dashboard | Basic Auth protected |
| 4000/gm-dashboard | GM server health dashboard | Token auth — see bookmark below |
| 7777 UDP | Mirror game server (`rod-server`) | Unity KCP transport |
| 3001 | Uptime Kuma | Monitoring (credentials in private notes) |

### GM Dashboard
`http://15.204.243.36:4000/gm-dashboard?token=<ADMIN_TOKEN>` — token stored in `.env` on VPS. Shows: rod-server status, player spawn count, last 50 log lines (color-coded), restart button, log download, Uptime Kuma link.

### Useful Server Commands
```bash
# Check game server status
systemctl status rod-server

# Live log tail
tail -f /var/log/rod-server.log

# Restart game server
systemctl restart rod-server

# Check all listening ports
ss -tlnp | grep LISTEN

# Game server binary location
ls /game/Builds/
```

### Game Server Binary
- **Binary:** `/game/Builds/Portalis.x86_64`
- **Data:** `/game/Builds/Portalis_Data/`
- **Log:** `/var/log/rod-server.log`
- Managed by systemd service `rod-server`

### Download Page
Public landing page at `http://15.204.243.36`:
- Dark decay aesthetic, "Survive Together. Decay Alone." tagline
- Download button → `/downloads/RateOfDecayONLINE.zip`
- To activate: build Windows client in Unity, zip output folder, drop as `/var/www/rod/downloads/RateOfDecayONLINE.zip`

---

## 9. Scene Layout

### Build Settings Order
| Index | Scene | Purpose |
|-------|-------|---------|
| 0 | LoginScene | Login + registration UI |
| 1 | CharacterSelect | Class picker, model preview |
| 2 | Hub | Social area, portals to zones |

### Hub Scene
Built by `Assets/Game/Editor/HubSceneBuilder.cs` — run via menu **RoD → Build Hub Scene**.

Contents:
- **Ground:** 160×160 grass plane
- **Inner tree ring:** 10 trees at radius 26
- **Outer backdrop:** 22 trees at radius 40
- **Bushes/ferns/cattails:** 22 scattered at radius 12–27
- **Flowers:** 18 scattered at radius 5–20
- **Metal ore nodes:** 6 scattered at radius 14–24
- **Crystal clusters:** 4 at diagonal points (radius 17)
- **Portals:** 3 (blue/green/yellow) at 120° intervals, radius 21, each with a light pillar VFX
- **Central shrine:** Magic circle + 5 candles ring
- **Particles:** FireFlies, DustMotes, GroundFog (4 cardinal points)
- **Skybox:** FS017_Sunset
- **Spawn points:** 8 `NetworkStartPosition` objects in ring at radius 4 — Mirror uses these for player spawning

> After running HubSceneBuilder, press **Ctrl+S** to save Hub.unity or changes won't persist.

---

## 10. Character Classes

| Index | Class | Color |
|-------|-------|-------|
| 0 | Engineer | Blue (`#59BFFF`) |
| 1 | Guardian | Gold (`#FFCC33`) |
| 2 | Wraith | Purple (`#B366FF`) |
| 3 | Medic | Green (`#59FF8C`) |

Class colors are used consistently across nameplates and the player list UI.

Class is selected in CharacterSelect, stored in the DB, synced to the server via `RodPlayerAuth.classIndex` on login, and broadcast to all clients via `PlayerIdentity.classIndex` (SyncVar).

---

## 11. Asset Inventory

### Vegetation
`Assets/Vegetation_Stylized_Pack_ByLuxArtStudios/`
- Trees: S_Tree_A through S_Tree_J (10 varieties)
- Bushes: S_Bush_A/B
- Ferns: S_Fern_A/C
- Cattails: S_Cattail_A
- Flowers: S_Flowers_A/C/E/G

### Magic / Fantasy Props
`Assets/brbmuffins Studio/brbmuffins Magic Pack/Prefabs/`
- Portals: Portal blue, Portal green, Portal yellow
- Crystal effects: Crystal effect blue, green, red
- Magic circles: `Magic circles/Magic circle.prefab`

### VFX
`Assets/brbmuffins VFX/brbmuffins Free VFX/Prefab/`
- `FX_LightPillar.prefab` — rising light column (used at portals)

`Assets/brbmuffins Technologies/brbmuffins Particle Pack/EffectExamples/`
- `Misc Effects/Prefabs/FireFlies.prefab`
- `Misc Effects/Prefabs/DustMotesEffect.prefab`
- `Misc Effects/Prefabs/Candles.prefab`
- `Smoke & Steam Effects/Prefabs/GroundFog.prefab`

### Metal Ore
`Assets/Metal Ore/Prefabs/` — Silver, Gold, Moon, Iron

### Skyboxes
`Assets/brbmuffins Skybox/Panoramics/`
- FS017_Sunset — currently used in Hub

### Zone Assets (Starting Zone — not yet built)
- `tavern.obj`, hut, bridge assets ready for use

---

## 12. Known Issues & Pending Work

### Active Bugs / Pinned
| Issue | Status | Notes |
|-------|--------|-------|
| Character materials pink in build | Pending fix | Materials use Unity Standard shader, not URP. Fix: **Edit → Rendering → Materials → Convert Selected Built-in Materials to URP** in the Unity editor. Must be done manually. |
| Camera feel reference | Pinned | Tyler wants to compare CameraFollow against a sample scene in the project before tuning further. |
| Portals not functional | Pending | Portals are decorative. Need zone scene loading on interact. |

### Pending Features
| Feature | Notes |
|---------|-------|
| Starting Zone scene | Has tavern.obj, hut, bridge assets. Scene not built yet. |
| Functional portals | Load zone scenes on proximity/click. |
| Windows client zip | Build + zip → `/var/www/rod/downloads/` to activate download button on site. |
| `WaveManager.JumpToWave(n)` | Needs adding to WaveManager if `wave <n>` GM command is to work. |
| CI/CD pipeline | GitHub Actions workflow exists (`.github/workflows/build-and-deploy.yml`) but needs secrets. Deferred until after project rename. |
| In-game registration | Currently accounts are server-side only. |
| Player list in zones | Who's Online panel works but hasn't been tested outside Hub. |
| Domain name | VPS accessible by IP only. Add a domain → point A record → add Certbot HTTPS. |

---

## 13. Key Files Reference

### Unity — Networking
| File | Purpose |
|------|---------|
| `Assets/Game/Networking/RodNetworkManager.cs` | Mirror NetworkManager; headless auto-start; class prefab spawning; spawn position logic |
| `Assets/Game/Networking/RodNetworkAuthenticator.cs` | JWT verification; populates `conn.authenticationData` with `RodPlayerAuth` |
| `Assets/Game/Networking/PlayerIdentity.cs` | SyncVar playerName + classIndex; auto-attaches nameplate + triggers player list refresh |
| `Assets/Game/Networking/RodChatManager.cs` | Mirror networked chat; CanvasGroup raycasting fix |
| `Assets/Game/Networking/RodPositionSaver.cs` | Saves player position to DB on disconnect |

### Unity — UI
| File | Purpose |
|------|---------|
| `Assets/Game/UI/LoginManager.cs` | Login + register UI; Tab field cycling; headless bail-out |
| `Assets/Game/UI/CharacterSelectManager.cs` | Class picker; sends CreatePlayerMessage on Enter World |
| `Assets/Game/UI/EscMenu.cs` | Self-bootstrap ESC menu; Resume / Logout / Quit |
| `Assets/Game/UI/GmConsole.cs` | Self-bootstrap GM console; all GM commands |
| `Assets/Game/UI/PlayerListUI.cs` | Self-bootstrap Who's Online panel; P to toggle |
| `Assets/Game/UI/PlayerNameplate.cs` | World-space billboard above players; auto-attached; distance fade |

### Unity — Characters
| File | Purpose |
|------|---------|
| `Assets/Game/Characters/Engineer/Scripts/CameraFollow.cs` | WoW-style camera; all camera logic lives here |

### Unity — Editor Tools
| File | Purpose |
|------|---------|
| `Assets/Game/Editor/HubSceneBuilder.cs` | Menu: RoD → Build Hub Scene. Populates Hub.unity with all environment assets. |
| `Assets/Game/Editor/BuildScript.cs` | RoD/Build/ menu items + CI entry points |

### Server (VPS)
| Path | Purpose |
|------|---------|
| `/game/Builds/Portalis.x86_64` | Game server binary |
| `/var/log/rod-server.log` | Game server log |
| `/var/www/rod/` | Public website (Nginx port 80) |
| `/var/www/rod/downloads/` | Drop `RateOfDecayONLINE.zip` here to activate download button |

---

*Rate of Decay ONLINE — internal developer reference. Keep updated as features ship.*
