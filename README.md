<p align="center">
  <img src="Docs/logo.png" alt="Crossworlds BCE" width="220"/>
</p>

# Crossworlds BCE

**Genre:** 4-10 player co-op combat MMO  
**Engine:** Unity 6 LTS · Universal Render Pipeline  
**Networking:** Mirror (KCP transport, port 7777) · Server-authoritative  
**Backend:** Node.js / Express 5 · MySQL · VPS auth server

---

## Quick Links

| | |
|--|--|
| Full Developer Reference | [`DEVDOC.md`](DEVDOC.md) |
| GM Commands | [DEVDOC → Section 4](DEVDOC.md#4-gm-console) |
| Controls | [DEVDOC → Section 3](DEVDOC.md#3-controls-reference) |
| Architecture | [DEVDOC → Section 7](DEVDOC.md#7-technical-architecture) |
| VPS & Server | [DEVDOC → Section 8](DEVDOC.md#8-vps--server-infrastructure) |
| Website / Download | https://playcrossworlds.com |
| Server Manager | http://playcrossworlds.com:4000 |
| GM Dashboard | http://playcrossworlds.com:4000/gm-dashboard |

---

## Classes

| Class | Role | Identity |
|-------|------|----------|
| **Warden** | Tank / Nature CC | Runic Snare, Battle Hymn, summon spirits — controls space and outlasts the fight |
| **Ironclad** | Tank / CC | Shieldwall Charge, Iron Rampart, Counter Blow — the anvil the team fights around |
| **Shadowblade** | DoT / Assassin | Shadow Veil, Dark Mark, Dark Harvest — stealth pressure and burst detonation |
| **Cleric** | Support / Heal | Soul Bond, Divine Spark, Temporal Grace — keeps the team alive under fire |
| **Arcanist** | Burst / Control | Arcane Step, Void Maw, Collapsing Void — spatial repositioning and burst payoff |

Each class has 4 equipped abilities + 1 ultimate. See [`COMBAT.md`](COMBAT.md) for full ability tables, combos, and design intent.

---

## Systems Built

### Networking
- Mirror host/client live end-to-end (KCP transport, port 7777)
- `RodNetworkAuthenticator` — JWT verify → server-side GET /character → stores class + spawn position in `conn.authenticationData`
- `RodNetworkManager` — server-authoritative class selection; reads DB class in production, client message in dev mode. Class prefabs are dual-registered: added to Mirror's `spawnPrefabs` (so `base.OnStartClient()` handles them) and directly via `NetworkClient.RegisterPrefab()` for belt-and-suspenders reliability in player builds
- `RodNetworkAuthenticator` dev mode — bypasses JWT for local HOST testing with one click

### Server Manager & Account Management

**Manager Dashboard** — `http://playcrossworlds.com:4000` · HTTP Basic Auth (admin credentials in private notes)
- Player account overview
- Service health and controls
- Built on Node.js / Express at `/opt/rod-dashboard/` · systemd managed

**GM Server Dashboard** — `http://playcrossworlds.com:4000/gm-dashboard` · token auth (in `.env` on VPS)
- Live crossworlds status (green/red pill)
- Spawn events pulled from server log
- Last 50 log lines, color-coded by type
- Restart game server button (auto-reloads after 8s)
- Download full server log
- Link to Uptime Kuma monitoring

**Auth Server** — `http://playcrossworlds.com:3000` (internal — Unity connects here directly, not through Nginx)
- `POST /register` — create account
- `POST /login` — returns JWT
- `GET /health` — service check
- `POST /character` — create/confirm character, returns full loadout
- `GET /character` — used at spawn: returns class index, last position, gear
- `PATCH /character/position` — saves position on disconnect
- `POST /character/gear/equip` · `GET /items`

### Authentication & Characters (VPS)
- Node.js / Express 5 auth server at `/opt/rod-auth/` · port 3000 · systemd service `rod-auth`
- Admin dashboard at `/opt/rod-dashboard/` · port 4000 · HTTP Basic auth
- `POST /register` · `POST /login` · `GET /health`
- `POST /character` — idempotent create/confirm, returns full character + 6-slot gear
- `GET /character` — spawn-path critical: class index + last saved position + equipped gear
- `PATCH /character/position` — saves X/Y/Z/map/orientation on disconnect
- `POST /character/gear/equip` · `GET /items`

### Database (rod_online · MySQL)
- `characters` — account FK, class_index, level, XP, position, online flag, last_logout
- `item_template` — static gear definitions, 12 seeded rows, 6 slots, per-stat ranges
- `item_instance` — per-player items with rolled stats
- `character_gear` — equipped loadout (one row per slot per character)
- `loot_tables` — exists, wired for future drop system

### Scene Flow
```
LoginScene → CharacterSelect → GameWorld
```
- `LoginManager` — full MMO login UI built in code; username/password auth, register panel, server IP field (saves to PlayerPrefs), animated title, dev HOST button
- `CharacterSelectManager` — 3D character preview via RenderTexture on layer 31; class list, ability readout, ENTER WORLD; reads server IP from PlayerPrefs before StartClient()
- `RodPositionSaver` — attached at runtime to server-side player objects; PATCH /character/position on disconnect or app quit via temporary coroutine host

### Combat

All combat scripts live in `Assets/Game/Combat/Scripts/`.

**Core systems**

| Script | Type | Purpose |
|---|---|---|
| `Health.cs` | MonoBehaviour | Server-authoritative HP — `maxHealth`, `currentHealth`, `IsAlive`, `Fraction`, `IsDowned`. Events: `onDeath`, `onDamageTaken(float)`, `onHealthChanged(float,float)`, `onKilledBy(GameObject)`, `onHealApplied(float)`, `onDownedChanged(bool)` |
| `StatusEffect.cs` | Plain C# | Single effect instance — type, duration, magnitude, source |
| `StatusEffectManager.cs` | MonoBehaviour | Applies / ticks / expires effects. Queries: `IsStaggered`, `IsBound`, `IsSilenced`, `GetSlowFraction()`. Types: Slow, Stagger, Silenced, Cursed, Weakened, Bound |
| `DropTable.cs` | ScriptableObject | `RollDrops()` → `(List<(itemId, qty)> items, int gold)`. Configurable weights, gold range, nothing-weight. Create via BCE/DropTable asset menu |
| `WorldItem.cs` | NetworkBehaviour | Floor loot — floats + rotates, collected on player trigger, server-despawns after 90s |

**Enemy AI**

`EnemyController.cs` — server-authoritative state machine:
```
Idle ──(player enters aggroRadius)──► Chase ──(in attackRange)──► Attack
  ▲                                      │
  └── leash: returns to spawn if too far ┘
Dead ◄── Health.onDeath
```
- Stagger: skips attack tick
- Bound: NavMeshAgent.isStopped = true
- Slow: `agent.speed *= (1 - GetSlowFraction())`
- Ranged variant: fires `EnemyProjectile` (server-spawned), backs off if target too close
- Death: logs `[LOOT]`, spawns WorldItem at death position, drops gold

**Wave system**

`WaveSpawner.cs` — NetworkBehaviour, server-driven:
- `StartWaves()` / `StopWaves()` called from portal arrival trigger
- Escalates: `baseEnemiesPerWave + (wave-1) × enemiesAddedPerWave`
- 67% grunt / 33% ranged split; elite every `eliteEveryNWaves` waves
- Waits for `enemiesAlive == 0` before advancing
- Announcements via `RodChatManager.Instance?.AddSystemMessage()`

**World Boss — Null Architect**

`WorldBossController.cs` — 4-phase NetworkBehaviour boss:

| Phase | Trigger | Key Mechanics |
|---|---|---|
| Phase 1 | Fight start | Melee + reflect pulse AoE every 18s (players still deal damage) |
| Transition | HP ≤ 60% | Immunity window (4s) → NullShard fracture spawns |
| Phase 2 | After transition | Tether web (pairs players, snap damage if they drift > 6u) + void drain AoE |
| Phase 3 | HP ≤ 30% | Boss gains Weakened (takes +25% damage); void drain doubles |
| Final Surge | HP ≤ 10% | 3× speed + 3× attack for 15s |
| Dead | HP = 0 | Guaranteed drops + rare roll + chat announcement |

SyncVars: `currentPhase`, `isImmune`, `isReflecting`, `isDraining`
UI: `WorldBossHealthBar` — self-bootstrapping ScreenSpaceOverlay, phase colour shifts, marker lines at 60% and 30%

### Editor Automation (`BCE →` menu)

Run in order from scratch:

| Step | Menu Item | Output |
|---|---|---|
| 0 | `Setup/0 ▶ Create Character Select Scene` | CharacterSelect.unity — 3D preview, layer 31, EventSystem |
| 1 | `Setup/1 ▶ Create Login Scene` | LoginScene.unity — NetworkManager, authenticator, KCP, UI |
| 2 | `Setup/2 ▶ Clean GameWorld` | Removes stray NetworkManager components |
| 3 | `Setup/3 ▶ Fix Build Settings` | Enforces Login(0) → CharSelect(1) → Hub(2) |
| 4 | `Setup/4 ▶ Create Class Prefabs (5 Classes)` | Warden / Ironclad / Shadowblade / Cleric / Arcanist prefabs, assetId baked, registered in NetworkManager |
| 4a | `Setup/4a ▶ Create Grunt Enemy Prefab` | `Assets/Game/Prefabs/Enemy_Grunt.prefab` + Grunt_DropTable.asset |
| 4b | `Setup/4b ▶ Create Ranged Enemy Prefab` | `Assets/Game/Prefabs/Enemy_Ranged.prefab` + Ranged_DropTable.asset |
| 4c | `Setup/4c ▶ Create Elite Enemy Prefab` | `Assets/Game/Prefabs/Enemy_Elite.prefab` + Elite_DropTable.asset |
| 4d | `Setup/4d ▶ Create WorldItem Prefab` | `Assets/Game/Prefabs/WorldItem.prefab` |
| 4e | `Setup/4e ▶ Create Wave Spawner (Arena)` | WaveSpawner + 4 cardinal spawn points in active scene |
| 5 | `Setup/5 ▶ Fix Animator Controllers` | Re-assigns AnimatorControllers to class prefabs |
| 6 | `Setup/6 ▶ Create World Boss (Null Architect)` | NullArchitect_Boss + NullShard.prefab in active arena scene |
| — | `Build Hub Scene` | Full Hub.unity rebuild from scratch |

**After 4a–4d and 6:** manually add all 5 combat prefabs to **NetworkManager → Registered Spawnable Prefabs**.  
**After step 4 or any prefab change:** rebuild the client — assetIds bake at build time.  
**After step 6:** bake NavMesh (Window → AI → Navigation → Bake), then Ctrl+S.

### UI Systems
- **ESC Menu** (`EscMenu.cs`) — Escape key → Resume / Logout / Quit. Self-bootstrapping, persists across scenes.
- **Chat** (`RodChatManager.cs`) — Enter or T to open. Mirror-networked, all clients see all messages.
- **Who's Online** (`PlayerListUI.cs`) — P to toggle. Top-right panel, shows all connected players with class color. Updates instantly on join/leave.
- **Nameplates** (`PlayerNameplate.cs`) — Floating billboard above each player. Shows name + class. Hides on local player. Fades 20–40u from camera.

![Multiplayer chat working](Docs/multiplayer-chat-working.png)

### GM Console (`GmConsole.cs`)
Self-bootstrapping — no scene object needed. Toggle with `` ` `` or **F1**.

| Command | Effect |
|---------|--------|
| `speed <n>` | Multiply move + sprint speed by n |
| `fly` | Toggle fly mode (gravity off, Space/Ctrl for vertical) |
| `god` | Toggle `Health.isInvulnerable` |
| `heal` | Full heal self |
| `kill` | Kill all `"Enemy"`-tagged objects |
| `spawn [n]` | Spawn n red capsule enemies near player |
| `wave [n]` | Start WaveManager or jump to wave n |
| `tp <x> <y> <z>` | Teleport player to world coords |
| `pos` | Print current world position (useful for level building) |
| `players` | List all connected players + class + position |
| `goto <name>` | Teleport to another player |
| `noclip` | Toggle player colliders off |
| `clear` / `help` | Clear log / list commands |

Access gated by `GM_USERS` allowlist in `GmConsole.cs`. Command history: ↑/↓ arrows.

### VFX
- brbmuffins Technologies particle pack (ElectricalSparks, EnergyExplosion, SmallExplosion, FireFlies, HeatDistortion)
- brbmuffins Dark Arts fantasy pack (Magic circle, Death magic circle, Lightning strike, Mana wall, Ground spikes, Fireball)
- `RodBillboard` — zone label text always faces camera
- `EnemyDeathVFX` — spawns death particles via `Health.onDeath`
- `LoginScreenVFX` — ambient atmosphere on login screen

---

## Design Pillars

- **Harder = more reward** — wave difficulty multiplier feeds loot score; higher cycles = rarer drops
- **Shared common goal** — one enemy pool, one boss HP bar; every player's damage contributes
- **Multiple paths** — Guardian tanks, Engineer builds, Medic sustains, Wraith pressures — all valid, all needed
- **Zone in and battle** — no lobby meta, no mandatory prep; pick class, enter arena, fight
- **Community crafting** — server hub (max-player zone) has trainers, shared loot pool, community-built upgrades *(planned)*
- **DoT class complete** — Wraith is fully playable with the corruption → stack → detonate loop

---

## Changelog

### 2026-06-28 — Stability & networking bug-fix pass

Audit pass across combat, networking, abilities, and status systems. All fixes are
localized and behavior-preserving for existing working paths.

- **Abilities** (`AbilityCaster.cs`) — added an `isLocalPlayer` guard so remote player
  clones no longer process local keyboard/mouse input. Previously pressing an ability key
  fired the ability on *every* player object on the client at once (and each grabbed the cursor).
- **Position save** (`RodPositionSaver.cs`) — floats now serialize with `InvariantCulture`.
  On OS locales that use a `,` decimal separator the old JSON was malformed (`"x":1,234`)
  and the `PATCH /character/position` save silently failed.
- **Spawning** (`RodNetworkManager.cs`) — `OnCreatePlayer` now rejects a duplicate
  `CreatePlayerMessage` (`conn.identity != null`), preventing orphaned player objects on the server.
- **Waves** (`WaveManager.cs`) — GM `wave <n>` (`JumpToWave`) now despawns current enemies
  instead of orphaning them alive and untracked.
- **Enemy AI** (`EnemyAI.cs`) — enemies drop dead/downed targets and ignore them when
  acquiring, instead of swinging forever at a corpse.
- **Status effects** (`StatusEffectManager.cs`) — re-applying an effect now refreshes its
  magnitude/source, so a stronger slow or curse actually takes effect.
- **Damage redirect** (`Health.cs`) — added a self-guard and re-entrancy guard to the
  Transfer Protocol redirect, eliminating an infinite loop when two targets redirect to each other.
- **Chat** (`RodChatManager.cs`) — server now logs `[CHAT] <user>: <msg>` so messages are
  visible in `crossworlds.log`.

> **Not yet addressed (architectural — tracked separately):** enemies are not
> `NetworkServer.Spawn`'d and combat damage is client-local; the GM allowlist is client-side.
> These require the server-authoritative combat build-out, not a point fix.

---

## Known TODOs / Open Bugs

| Priority | Item |
|----------|------|
| High | `GmConsole.cs` — needs `#if !UNITY_SERVER` guard (crashes server every frame) |
| Medium | Arena scene — needs NavMesh baked + WaveSpawner wired; portals in Hub are decorative (no scene transition yet) |
| Medium | Inventory bag UI — `InventoryBagUI.cs` not written; calls `GET /api/inventory/:characterId`, renders slots |
| Medium | Equip flow — slot click → `POST /api/inventory/equip` not wired |
| Medium | Portal transition — `OnTriggerEnter` in Hub portals → `NetworkManager.singleton.ServerChangeScene(arenaScene)` not wired |
| Medium | Clean up stale prefabs — `Engineer.prefab`, `Guardian.prefab`, `Wraith.prefab`, `Medic.prefab`, `PlayerPrefab.prefab` still in `Assets/Game/Prefabs/` |
| Medium | Add Arcanist to CharacterSelect scene preview |
| Low | `orientation:F3` — Unity sending float as formatted string in `PATCH /character/position` |
| Low | `CmdSendChat` — missing `[CHAT]` log line server-side |
| Low | Position save on scene exit (currently only on disconnect/quit) |

---

## Project Structure

```
Rate 