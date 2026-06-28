# Scene Setup & Editor Tools — Troubleshooting Context

**When to load:** Rebuilding Hub scene, running Setup menu items, class prefab issues, build settings problems, scene order wrong, missing components after scene rebuild.

---

## Key Files

| File | Role |
|------|------|
| `Assets/Game/Editor/RodPrefabBuilder.cs` | Setup/4: creates class prefabs, force-reimports for assetId bake, registers in NetworkManager |
| `Assets/Game/Editor/HubSceneBuilder.cs` | "Build Hub Scene": wipes and rebuilds Hub.unity from scratch |
| `Assets/Game/Editor/LoginSceneBuilder.cs` | Setup/1: builds LoginScene with NetworkManager + UI |
| `Assets/Game/Editor/CharacterSelectBuilder.cs` | Setup/0: builds CharacterSelect with 3D preview |
| `Assets/Game/Editor/BuildScript.cs` | RoD/Build/ menu items; CI entry points |

---

## Scene Order (Build Settings)

Must be exactly:
| Index | Scene |
|-------|-------|
| 0 | LoginScene |
| 1 | CharacterSelect |
| 2 | Hub |

Run **BCE → Setup/3 → Fix Build Settings** to restore this if wrong.

---

## Setup Menu Items (BCE →)

Run these in order when setting up from scratch:

| Step | Menu Item | What it does |
|------|-----------|-------------|
| 0 | `Setup/0 ▶ Create Character Select Scene` | CharacterSelect.unity with 3D preview, layer 31, EventSystem |
| 1 | `Setup/1 ▶ Create Login Scene` | LoginScene with NetworkManager, authenticator, KCP transport, UI |
| 2 | `Setup/2 ▶ Clean GameWorld` | Removes stray NetworkManager components |
| 3 | `Setup/3 ▶ Fix Build Settings` | Enforces scene order: Login(0) → CharacterSelect(1) → Hub(2) |
| 4 | `Setup/4 ▶ Create Class Prefabs (5 Classes)` | Creates/updates all class prefabs, force-reimports for assetId, registers in NetworkManager |
| 5 | `Setup/5 ▶ Fix Animator Controllers` | Re-assigns AnimatorController to prefabs if missing |
| — | `Build Hub Scene` | Wipes and rebuilds Hub.unity with full environment |

**After Setup/4:** always rebuild the client binary. assetIds are baked at build time.

**After Build Hub Scene:** press **Ctrl+S** to save Hub.unity immediately.

---

## Hub Scene Contents

Built by `HubSceneBuilder.cs`. Wipes all non-Network objects and rebuilds:

- Ground: 160×160 grass plane
- Inner tree ring: 10 trees, radius 26
- Outer backdrop: 22 trees, radius 40
- Bushes/ferns/cattails/flowers: scattered radius 5–27
- Metal ore nodes: 6 at radius 14–24
- Crystal clusters: 4 at diagonal points radius 17
- Portals: 3 (blue/green/yellow) at 120° intervals, radius 21, light pillar VFX
- Central shrine: magic circle + 5 candles
- Particles: FireFlies, DustMotes, GroundFog (4 cardinal points)
- Skybox: FS017_Sunset
- Spawn points: 8 `NetworkStartPosition` in ring at radius 4

**Critical:** HubSceneBuilder destroys ALL GameObjects without NetworkIdentity. This includes:
- EventSystem (fixed at runtime by `RodChatManager.EnsureEventSystem()`)
- Any manually placed lights or objects — must be re-added to HubSceneBuilder code

---

## Class Prefab Registration

`RodPrefabBuilder.cs` (Setup/4) does:
1. Creates prefab GameObjects with required components
2. `SaveAsPrefabAsset()` to disk
3. **Force reimport** — critical for assetId bake:
   ```csharp
   AssetDatabase.Refresh();
   foreach (var path in builtPaths)
       AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
   AssetDatabase.SaveAssets();
   ```
4. Assigns to `nm.classPrefabs[]` AND `nm.spawnPrefabs` (both lists needed)

Without step 3, `_assetId = 0` in the prefab file → online spawning silently fails.

---

## Asset Paths Reference

### Vegetation
`Assets/Vegetation_Stylized_Pack_ByLuxArtStudios/`
- Trees: `S_Tree_A` through `S_Tree_J`
- Bushes: `S_Bush_A/B`, Ferns: `S_Fern_A/C`, Cattails: `S_Cattail_A`
- Flowers: `S_Flowers_A/C/E/G`

### Magic / Fantasy Props
`Assets/brbmuffins Studio/brbmuffins Magic Pack/Prefabs/`
- Portals: Portal blue, Portal green, Portal yellow
- Magic circles: `Magic circles/Magic circle.prefab`

### VFX
`Assets/brbmuffins VFX/brbmuffins Free VFX/Prefab/FX_LightPillar.prefab`
`Assets/brbmuffins Technologies/brbmuffins Particle Pack/EffectExamples/`
- `Misc Effects/Prefabs/FireFlies.prefab`
- `Misc Effects/Prefabs/DustMotesEffect.prefab`
- `Misc Effects/Prefabs/Candles.prefab`
- `Smoke & Steam Effects/Prefabs/GroundFog.prefab`

### Metal Ore
`Assets/Metal Ore/Prefabs/` — Silver, Gold, Moon, Iron

### Skyboxes
`Assets/brbmuffins Skybox/Panoramics/FS017_Sunset`

### AnimatorControllers (per class)
`Assets/Game/Animations/<ClassName>/<ClassName>Controller.controller`

---

## Known Pitfalls

| Symptom | Cause | Fix |
|---------|-------|-----|
| Online spawn fails after Setup/4 | prefabs reimported but client not rebuilt | Rebuild client — assetIds are baked at build time |
| Hub scene loses portals/trees after rebuild | HubSceneBuilder wipes everything | All environment must be defined in HubSceneBuilder code |
| EventSystem gone after Hub rebuild | HubSceneBuilder wipes non-Network objects | Fixed at runtime by RodChatManager.EnsureEventSystem() |
| Wrong scene loads after login | Build settings out of order | Run Setup/3 to restore order |
| Animator not animating on prefab | AnimatorController not assigned | Run Setup/5 to re-assign controllers |
| 4 classes instead of 5 in NetworkManager | Old scene predates Arcanist addition | Run Setup/4 again — should create all 5 |

---

## Active TODOs

- Add Arcanist to CharacterSelect 3D preview scene
- Clean up stale prefabs: `Assets/Game/Prefabs/` has old Engineer.prefab, Guardian.prefab, Wraith.prefab, Medic.prefab, PlayerPrefab.prefab
- HubSceneBuilder should add EventSystem + InputSystemUIInputModule directly (currently a runtime workaround)
- Position save on scene exit (currently only on disconnect/quit)
- Portals are decorative — need scene load logic on interact
