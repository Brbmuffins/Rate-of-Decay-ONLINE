# Networking — Troubleshooting Context

**When to load:** Mirror spawn errors, `Could not spawn assetId=...`, players not appearing online, prefab registration issues, client/server connection failures.

---

## Key Files

| File | Role |
|------|------|
| `Assets/Game/Networking/RodNetworkManager.cs` | Core NetworkManager: headless auto-start, class prefab spawning, spawn position logic, dual prefab registration |
| `Assets/Game/Networking/RodNetworkAuthenticator.cs` | JWT verification on server; populates `conn.authenticationData` with `RodPlayerAuth` |
| `Assets/Game/Networking/PlayerIdentity.cs` | SyncVar `playerName` + `classIndex`; triggers nameplate + player list on spawn |
| `Assets/Game/Networking/RodChatManager.cs` | Mirror-networked chat; `[Command]` + `[ClientRpc]`; singleton; creates EventSystem in Hub |
| `Assets/Game/Networking/RodPositionSaver.cs` | Saves player position to DB on disconnect via POST `/character/position` |
| `Assets/Game/Editor/RodPrefabBuilder.cs` | Editor tool (Setup/4): creates class prefabs + registers them in NetworkManager |

---

## How Players Spawn

1. Client connects → `RodNetworkAuthenticator` sends JWT to server
2. Server verifies JWT via `GET /character` (auth server port 3000)
3. Auth stores `RodPlayerAuth { username, classIndex, characterId, spawnPos }` on `conn.authenticationData`
4. Client sends `CreatePlayerMessage`
5. `RodNetworkManager.OnServerAddPlayer()` reads auth data, picks class prefab, calls `Instantiate` + `NetworkServer.AddPlayerForConnection`
6. Mirror spawns the object on all clients via `NetworkClient.RegisterPrefab`

**Spawn position priority:**
1. Saved position from DB (if non-zero)
2. `NetworkStartPosition` objects in scene (8 in ring, radius 4)
3. Random ring fallback at radius 3, Y=1

---

## The assetId Problem (SOLVED — do not regress)

**Symptom:** `Could not spawn assetId=XXXXXXXX` in online build. Dev/HOST mode works fine.

**Root cause:** In editor builds, `NetworkIdentity._assetId` is computed at runtime from the prefab GUID. In player builds, it must be baked into the serialized prefab file. `PrefabUtility.SaveAsPrefabAsset()` does NOT trigger `OnValidate`, so `_assetId` stays 0 in the file. `NetworkClient.RegisterPrefab()` silently skips assetId=0 prefabs.

**Fix (in `RodPrefabBuilder.cs`):**
```csharp
// After SaveAsPrefabAsset, force reimport to trigger OnValidate
AssetDatabase.Refresh();
foreach (var path in builtPaths)
    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
AssetDatabase.SaveAssets();
```

**Runtime safety net (in `RodNetworkManager.OnStartClient()`):**
```csharp
// Dual registration: spawnPrefabs list (processed by base.OnStartClient)
// + direct RegisterPrefab call (belt and suspenders)
if (classPrefabs != null)
    foreach (var p in classPrefabs)
        if (p != null && !spawnPrefabs.Contains(p))
            spawnPrefabs.Add(p);
base.OnStartClient();
if (classPrefabs != null)
    foreach (var prefab in classPrefabs)
        if (prefab != null) NetworkClient.RegisterPrefab(prefab);
```

**Rule:** Any time prefabs are recreated (run Setup/4), you MUST rebuild the client binary before testing online. The assetId is baked at build time.

---

## Class Prefabs

Registered in `RodNetworkManager.classPrefabs[]` and `spawnPrefabs` (both).

| Index | Class | Notes |
|-------|-------|-------|
| 0 | Warden | |
| 1 | Ironclad | |
| 2 | Shadowblade | |
| 3 | Cleric | |
| 4 | Arcanist | |

**After running Setup/4 (Create Class Prefabs):** rebuild the client. assetIds change when prefabs are recreated.

---

## SyncVars

`PlayerIdentity.cs` syncs on all clients immediately after spawn:
- `playerName` (string) — username from auth
- `classIndex` (int) — 0–4

These drive: nameplates, player list UI, class coloring.

---

## Known Pitfalls

| Pitfall | What happens | Fix |
|---------|-------------|-----|
| `assetId=0` in prefab file | Silent skip in `RegisterPrefab`, spawn fails online | Force reimport after SaveAsPrefabAsset (see above) |
| Old binary on VPS after prefab rebuild | assetId mismatch between client and server prefabs | Upload fresh server build after any Setup/4 run |
| Binary name mismatch in systemd | Service fails to start, no game server | Binary is `CrossworldsBCE.x86_64` — check `/game/Builds/` |
| First-time login spawns at (0,0,0) | All players underground or overlapping | Zero-check on DB coords — treat all-zero as "no saved pos" |
| `autoCreatePlayer = false` | Must send `CreatePlayerMessage` manually | `CharacterSelectManager` sends this on "Enter World" |

---

## Active TODOs

- Arcanist (index 4) may be missing from old LoginScene after Setup/4 — verify classPrefabs has 5 entries
- Old stale prefabs in `Assets/Game/Prefabs/`: Engineer.prefab, Guardian.prefab, Wraith.prefab, Medic.prefab, PlayerPrefab.prefab — safe to delete, replaced by class-named prefabs
