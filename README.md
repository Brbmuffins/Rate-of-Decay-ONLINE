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
| Download Page | http://15.204.243.36 |
| Server Manager | http://15.204.243.36:4000 |
| GM Dashboard | http://15.204.243.36:4000/gm-dashboard |

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

**Manager Dashboard** — `http://15.204.243.36:4000` · HTTP Basic Auth (admin credentials in private notes)
- Player account overview
- Service health and controls
- Built on Node.js / Express at `/opt/rod-dashboard/` · systemd managed

**GM Server Dashboard** — `http://15.204.243.36:4000/gm-dashboard` · token auth (in `.env` on VPS)
- Live crossworlds status (green/red pill)
- Spawn events pulled from server log
- Last 50 log lines, color-coded by type
- Restart game server button (auto-reloads after 8s)
- Download full server log
- Link to Uptime Kuma monitoring

**Auth Server** — `http://15.204.243.36:3000` · systemd service `rod-auth` · do not expose publicly
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
- `Health` — shields, damage reduction, absorb window, downed state (players), gear stat channels, `onDeath` UnityEvent
- `StatusEffectManager` — Slow, Stagger, Suppress, DamageOverTime, Exposed, Tethered; per-tick DoT, `ConsumeDebuffStacks()` for Shadowblade's Dark Harvest
- `EnemyAI` — aggro, move, attack, stealth suppression window, status-gated actions
- `AbilityCaster` — Cone / Circle / Rectangle targeting indicators; full 20+ ability spellbook
- `WaveChest` — hold-E activation, prep window, per-wave enemy spawning, player-count scaling, loot on clear
- `WaveManager` — scene-level arena orchestrator; wave definitions with mob/elite/boss mix, difficulty multiplier per cycle (×1.2), shared boss health pool, loot score = wave × difficulty × √playerCount, arena boundary leash, fail detection

### Editor Automation (`BCE →` menu)
| Menu Item | What it does |
|-----------|-------------|
| `Setup/0 ▶ Create Character Select Scene` | Builds CharacterSelect.unity with 3D preview camera, layer 31 isolation, EventSystem |
| `Setup/1 ▶ Create Login Scene` | Builds LoginScene with NetworkManager, authenticator, KCP transport, UI |
| `Setup/2 ▶ Clean GameWorld` | Removes stray NetworkManager components from GameWorld |
| `Setup/3 ▶ Fix Build Settings` | Sets scene order: Login(0) → CharacterSelect(1) → Hub(2) |
| `Setup/4 ▶ Create Class Prefabs (5 Classes)` | Creates Warden / Ironclad / Shadowblade / Cleric / Arcanist prefabs, assigns AnimatorController, wires to NetworkManager `classPrefabs` + `spawnPrefabs` |
| `Setup/5 ▶ Fix Animator Controllers` | Re-assigns AnimatorController to existing prefabs if missing |
| `Build Hub Scene` | Rebuilds Hub.unity: gray ground plane, directional light, 8 spawn points, RodChatManager |

### UI Systems
- **ESC Menu** (`EscMenu.cs`) — Escape key → Resume / Logout / Quit. Self-bootstrapping, persists across scenes.
- **Chat** (`RodChatManager.cs`) — Enter or T to open. Mirror-networked, all clients see all messages.
- **Who's Online** (`PlayerListUI.cs`) — P to toggle. Top-right panel, shows all connected players with class color. Updates instantly on join/leave.
- **Nameplates** (`PlayerNameplate.cs`) — Floating billboard above each player. Shows name + class. Hides on local player. Fades 20–40u from camera.

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

## Known TODOs

| Priority | Item |
|----------|------|
| High | **HTTPS / Cloudflare** — all current URLs use plain HTTP (`http://15.204.243.36:3000`, `http://15.204.243.36:4000`). Route through Cloudflare proxy with SSL to secure auth tokens and JWT traffic before launch |
| Medium | Clean up stale prefabs — `Engineer.prefab`, `Guardian.prefab`, `Wraith.prefab`, `Medic.prefab`, `PlayerPrefab.prefab` still in `Assets/Game/Prefabs/` |
| Medium | Add Arcanist to CharacterSelect scene preview |
| Low | Position save on scene exit (currently only on disconnect/quit) |

---

## Project Structure

```
Rate 