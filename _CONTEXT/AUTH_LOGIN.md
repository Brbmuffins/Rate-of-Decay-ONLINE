# Auth & Login — Troubleshooting Context

**When to load:** Login failures, JWT errors, character not loading, wrong class spawning, register/login endpoints, CharacterSelect issues, `authenticationData` being null.

---

## Key Files

| File | Role |
|------|------|
| `Assets/Game/UI/LoginManager.cs` | Login + register UI; Tab/Shift+Tab field cycling; dev HOST button; headless bail-out |
| `Assets/Game/UI/CharacterSelectManager.cs` | Class picker; 3D preview via RenderTexture layer 31; sends `CreatePlayerMessage` on Enter World |
| `Assets/Game/Networking/RodNetworkAuthenticator.cs` | Client sends JWT; server verifies via auth server; stores `RodPlayerAuth` on connection |
| `Assets/Game/Networking/RodNetworkManager.cs` | `OnServerAddPlayer()` reads `conn.authenticationData` to pick class prefab + spawn position |
| `/opt/rod-auth/` (VPS) | Node.js auth server — all account and character endpoints |

---

## Login Flow (Step by Step)

```
1. LoginManager
   ├── POST /login → { token: JWT }
   └── GET /character (Authorization: Bearer JWT)
       └── returns: classIndex, spawnX/Y/Z, characterId, gear

2. CharacterSelectManager
   ├── Displays class picker (reads classIndex from response)
   ├── Stores server IP from PlayerPrefs
   └── On "Enter World" click:
       ├── NetworkManager.singleton.networkAddress = serverIP
       ├── StartClient()
       └── Sends CreatePlayerMessage { username, classIndex } after connection

3. RodNetworkAuthenticator (server-side)
   ├── Receives AuthRequest { token }
   ├── GET /character?token=... from auth server
   ├── On success: stores RodPlayerAuth on conn.authenticationData
   └── Sends AuthResponseMessage { success: true }

4. RodNetworkManager.OnServerAddPlayer()
   ├── Reads conn.authenticationData as RodPlayerAuth
   ├── Picks classPrefabs[auth.classIndex]
   ├── Determines spawn position (DB → NetworkStartPosition → fallback)
   └── NetworkServer.AddPlayerForConnection(conn, playerObj)
```

---

## RodPlayerAuth (Data Stored on Connection)

```csharp
public class RodPlayerAuth
{
    public string username;
    public int    classIndex;
    public int    characterId;
    public float  spawnX, spawnY, spawnZ;
}
```

This is set server-side only — cannot be spoofed by the client. Used in:
- `RodNetworkManager.OnServerAddPlayer()` — spawn position and class
- `RodChatManager.CmdSendChat()` — pulling username for chat messages
- `RodPositionSaver` — needs characterId to save position

---

## Dev / HOST Mode

LoginManager has a **HOST** button that bypasses JWT and auth server entirely. Use for local testing without the VPS.

In dev mode:
- `RodNetworkAuthenticator` skips JWT verification
- Username is taken from the `CreatePlayerMessage` sent by CharacterSelectManager
- Class is read from the message's `classIndex` field
- No DB position lookup — spawn position uses NetworkStartPosition or fallback

**Toggle:** `RodNetworkAuthenticator` has a `devMode` bool (set via Inspector or code).

---

## Auth Server Endpoints (VPS port 3000)

| Endpoint | Method | Purpose |
|----------|--------|---------|
| `/register` | POST | Create account: `{ username, password }` |
| `/login` | POST | Returns JWT: `{ username, password }` |
| `/health` | GET | Service check |
| `/character` | POST | Create/confirm character, returns full loadout |
| `/character` | GET | Spawn-path critical: class + last position + gear |
| `/character/position` | PATCH | Save position on disconnect |
| `/character/gear/equip` | POST | Equip item |
| `/items` | GET | All item templates |

**JWT payload key:** `accountId` (not `userId`). DB FK column is `account_id`.

---

## Character Data Flow

```
GET /character response → RodPlayerAuth
├── classIndex      → which prefab to spawn
├── spawnX/Y/Z      → where to place the player
│   └── Zero-check: if all three are 0, treat as "no saved position"
│       and use NetworkStartPosition or fallback ring
└── characterId     → used by RodPositionSaver to PATCH position on disconnect
```

**Common mistake:** First-time logins have `spawnX/Y/Z = 0` in DB. Without the zero-check, all new players spawn at (0,0,0) which is usually underground.

---

## Known Pitfalls

| Symptom | Cause | Fix |
|---------|-------|-----|
| Auth fails in online build, works in dev | JWT not sent / wrong endpoint URL | Verify server IP in PlayerPrefs; check auth server is running |
| Player spawns at (0,0,0) | Zero-check missing on DB spawn coords | Treat all-zero coords as "no saved position" |
| Wrong class spawns | `classIndex` mismatch between client selection and server auth data | Server uses auth data, not client message — check DB character record |
| `conn.authenticationData` is null in `OnServerAddPlayer` | Auth failed silently | Check `/var/log/crossworlds.log` for auth errors |
| `CmdSendChat` shows "Unknown" username | `conn.authenticationData` not cast to `RodPlayerAuth` | Auth must complete before chat is used |

---

## Account Management

Currently no in-game registration. To create accounts:
```bash
curl -X POST http://15.204.243.36:3000/register \
  -H "Content-Type: application/json" \
  -d '{"username":"PlayerName","password":"password123"}'
```

For the two-player chat test — register a second account this way and use it on the second client instance.

---

## Active TODOs

- In-game registration form (currently server-side only)
- HTTPS / Cloudflare SSL — all auth traffic is plain HTTP; JWT tokens exposed in transit
- Domain name → point A record → Certbot HTTPS → update hardcoded `http://15.204.243.36:3000` URLs in client
