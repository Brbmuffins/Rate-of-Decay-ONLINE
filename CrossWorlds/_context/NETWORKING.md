# NETWORKING.md — Mirror / KCP Troubleshooting

> Load this file when: Mirror spawn failures, `Could not spawn`, assetId errors,
> prefab not appearing, players not seeing each other, KCP transport issues.

**Last verified:** 2026-06-28

---

## Stack Facts
- **Networking:** Mirror, KCP transport
- **Game port:** UDP 7777 (hardcoded in Unity — do not change)
- **Server binary:** `/game/Builds/CrossworldsBCE.x86_64`
- **Server log:** `/var/log/crossworlds.log`
- **Scene order:** LoginScene(0) → CharacterSelect(1) → Hub(2)

---

## Most Common Failures

### `Could not spawn` / assetId mismatch
The server and client builds are out of sync. The prefab's `assetId` in the client doesn't match the server.

**Fix:**
1. Do a clean build of BOTH server and client from the same Unity project state
2. Deploy the new server binary: `scp -r ./Build/* ubuntu@playcrossworlds.com:/game/Builds/`
3. Restart: `sudo systemctl restart crossworlds`
4. Confirm server started clean: `sudo journalctl -u crossworlds -n 30 --no-pager`

**Never** deploy a client without also deploying a matching server — they must come from the same build.

### Player appears on host but not on client
The player prefab is not registered in NetworkManager's `Spawnable Prefabs` list.

**Fix:**
- Open NetworkManager in the Hub scene → Spawn Info → Spawnable Prefabs
- Every networked prefab that the server spawns must be in this list
- After adding, rebuild and redeploy both server and client

### `SIGSEGV at il2cpp::vm::Runtime::Init`
`GameAssembly.so` and `UnityPlayer.so` are from different build sessions.

**Fix:** They must be deployed together from the same build. Never replace one without the other.
```bash
scp -r ./Build/* ubuntu@playcrossworlds.com:/game/Builds/
chmod +x /game/Builds/CrossworldsBCE.x86_64
sudo systemctl restart crossworlds
```

### `_Data/` folder name mismatch
The data folder name must exactly match the binary name.
- Binary: `CrossworldsBCE.x86_64` → Data folder must be: `CrossworldsBCE_Data/`
- If you rename the binary, rename the folder to match.

### Players disconnect immediately on join
Check the auth flow — the game server calls the auth server on player connect.
```bash
sudo journalctl -u crossworlds-auth -n 30 --no-pager
sudo journalctl -u crossworlds -n 30 --no-pager
```
Look for JWT errors or `401` responses. The JWT token from login must be passed to the game server on connect.

---

## Player Spawn Flow

```
Unity Client                    Auth Server (3000)      Game Server (7777)
     |                               |                        |
     |── POST /login ───────────────>|                        |
     |<─ {token} ─────────────────── |                        |
     |── GET /character ────────────>|                        |
     |<─ {character data} ──────────-|                        |
     |── Connect UDP 7777 ──────────────────────────────────> |
     |   (sends token + character data)                       |
     |                                                        |── spawn player prefab
     |<─ NetworkIdentity spawned ──────────────────────────── |
```

---

## Key Mirror Components

| Component | Location | Purpose |
|---|---|---|
| NetworkManager | Hub scene | Server/client lifecycle, spawnable prefabs |
| KcpTransport | NetworkManager GameObject | UDP transport config, port 7777 |
| NetworkIdentity | Player prefab | Identifies networked object |
| NetworkTransform | Player prefab | Syncs position/rotation |

---

## Deploy Commands
```bash
# Upload new build
scp -r ./Build/* ubuntu@playcrossworlds.com:/game/Builds/

# On server
chmod +x /game/Builds/CrossworldsBCE.x86_64
sudo systemctl restart crossworlds
sudo journalctl -u crossworlds -n 30 --no-pager
tail -f /var/log/crossworlds.log
```

---

## Active Issues
- **`orientation:F3`** — `PATCH /character/position` receives float as formatted string (`"0.000"`) instead of number (`0.0`). Fix is in Unity: ensure position values serialize as raw floats, not formatted strings.
