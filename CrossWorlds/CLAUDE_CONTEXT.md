# Crossworlds BCE — Claude Context Primer

> Paste this at the start of any Claude Chat session to get full project context.
> Last updated: 2026-06-28

---

## What This Project Is

**Crossworlds BCE** is a multiplayer action RPG built in Unity. Players log in, meet in a shared hub, enter portals into combat arenas, kill monsters, earn loot, level up, and craft upgrades. Think Diablo-meets-MMO-hub.

The goal of Phase 1 is to prove the core loop works end-to-end with real players on a live server before adding any extra systems.

**Core loop:** Hub → Portal → Arena → Kill monsters → Loot → Level up → Craft → Return to hub → Repeat.

---

## Tech Stack

| Layer | Tech |
|---|---|
| Game engine | Unity 6000.0.77f1, URP, IL2CPP |
| Networking | Mirror, KCP transport, UDP 7777 |
| Server OS | Ubuntu 22.04 LTS on OVH VPS |
| Auth server | Node.js / Express, port 3000 |
| Database | MySQL 8, DB name `rod_online` |
| Dashboard | Node.js / Express + Socket.io, port 4000 |
| Web/download | Nginx, playcrossworlds.com, SSL via Certbot |
| Deployment | SCP binary → VPS, systemd services |

**Server:** 15.204.243.36 / playcrossworlds.com  
**SSH:** `ssh ubuntu@playcrossworlds.com`

---

## Project Structure

```
VPS /opt/rod-auth/
  server.js          — Auth server (all game API endpoints)
  .env               — DB creds, JWT secret (never commit)

VPS /opt/rod-dashboard/
  server.js          — Dashboard server
  public/index.html  — Dashboard UI

VPS /game/Builds/
  CrossworldsBCE.x86_64     — Unity Linux server binary
  CrossworldsBCE_Data/      — Must match binary name exactly
  GameAssembly.so            — Must come from same build as UnityPlayer.so
  UnityPlayer.so

VPS /var/www/rod/
  index.html                    — Public download page
  roadmap.html                  — Phase 1 roadmap
  downloads/CrossworldsBCE.zip  — Windows client

Unity scenes (by build index):
  0 — LoginScene
  1 — CharacterSelect
  2 — Hub
  (Portal/Arena scene — in progress)
```

---

## Classes

| Index | Name | Role |
|---|---|---|
| 0 | Engineer | — |
| 1 | Guardian | Tank / frontline |
| 2 | Shadowblade | Burst / rogue |
| 3 | Cleric | Healer / support |
| 4 | Arcanist | Ranged magic DPS |

Defined in `CLASS_NAMES` array in `/opt/rod-auth/server.js`.

---

## Phase 1 Roadmap — Current Status

We are **ahead of schedule**. Server backend for Weeks 4–6 was built in a single Claude Code session. All remaining Phase 1 work is Unity client only.

| Week | Focus | Server | Unity Client |
|---|---|---|---|
| 1 | Foundation (Mirror, login, hub, chat) | ✅ | ✅ |
| 2 | Portal system | ✅ | 🔶 Portal→arena transition still needed |
| 3 | Combat / 5 classes | ✅ | 🔶 Enemy AI + hit confirmation still needed |
| 4 | Loot + inventory | ✅ API live | 🔶 **Active** — enemies, drops, inventory UI |
| 5 | Progression (XP/levels) | ✅ API live | ○ Next |
| 6 | Crafting | ✅ API live | ○ Next |
| 7 | Polish (VFX, SFX, HUD) | — | ○ |
| 8 | Playtest (10–20 testers) | — | ○ |

**The server is not a blocker for anything in Phase 1.** Every API Unity needs is live.

---

## Database Schema

### Core tables
```sql
accounts        — id, username, email, password_hash, role, active, last_login
characters      — id, account_id, class_index, class_name, level, xp, gold,
                  stat_str, stat_agi, stat_int, stat_vit, pos_x/y/z, online
```

### New system (active — use these for all new features)
```sql
items           — id VARCHAR(64) PK, name, rarity ENUM(common/uncommon/rare/epic),
                  item_type ENUM(weapon/armor_head/armor_chest/armor_legs/ring/trinket/material),
                  stat_bonus JSON, icon_id, sell_value, crafted

inventory       — id, character_id FK, slot_index, item_id FK, quantity, equipped
                  UNIQUE(character_id, slot_index)

professions     — character_id FK, profession_id VARCHAR(32), skill_level, skill_xp
                  PK(character_id, profession_id)

recipes         — id, profession_id, skill_level_required, result_item_id FK, result_quantity

recipe_ingredients — recipe_id FK, item_id FK, quantity
                     PK(recipe_id, item_id)
```

### Old gear system (legacy — Unity still reads on spawn, do not remove)
```sql
item_template   — old stat-range gear templates
item_instance   — old rolled-stat gear instances
character_gear  — old equipped slots (0–5)
loot_tables     — old enemy drop weights
```

### Phase 2 stubs (schema exists, no endpoints yet)
```sql
gold_transactions     — economy audit log
marketplace_listings  — auction house
guilds                — guild roster
guild_members         — guild membership
```

### Seeded items
`sword_copper`, `plate_copper`, `ring_copper`, `material_copper_shard`, `material_copper_bar`, `sword_iron`, `plate_iron`, `staff_apprentice`, `dagger_shadow`, `tome_cleric`

### Seeded recipes (Mining)
- 3× copper_shard → copper_bar (skill 1)
- 2× copper_bar → ring_copper (skill 3)
- 3× copper_bar → sword_copper (skill 5)
- 4× copper_bar → plate_copper (skill 5)

---

## Auth Server API (`playcrossworlds.com:3000`)

JWT required on authenticated endpoints: `Authorization: Bearer <token>`

### Auth
```
POST /register          — {username, email, password}
POST /login             — {username, password} → {token}
GET  /api/health        — {status, uptime, db, timestamp}
```

### Character — old system (do not modify, Unity depends on these)
```
POST  /character                — create or return character
GET   /character                — load character + gear for spawn
PATCH /character/position       — save position on disconnect
POST  /character/gear/equip     — equip item_instance to slot
GET   /items                    — all item_templates
```

### Progression — new system
```
POST /api/character/save-progress
  body: {characterId, level, xp, gold, stat_str, stat_agi, stat_int, stat_vit}
```

### Inventory — new system
```
GET  /api/inventory/:characterId          — all slots joined with items table
POST /api/inventory/save                  — {characterId, slots:[{slot_index, item_id, quantity, equipped}]}
                                            bulk upsert, deletes slots not in payload
POST /api/inventory/equip                 — {characterId, slot_index, equipped:0|1}
```

### Professions & Crafting — new system
```
GET  /api/professions/:characterId        — auto-seeds Mining lv1 if none
GET  /api/recipes?profession=mining       — recipes with full ingredient list
POST /api/craft                           — {characterId, recipeId}
                                            validates skill, deducts ingredients,
                                            awards 10xp, levels up at skill_level×50 xp
```

### Response shape (all /api/* endpoints)
```json
{ "success": true,  "data": { ... } }
{ "success": false, "error": "human readable — Unity shows this directly" }
```

---

## Unity → Server Integration Map

When building Unity client features, these are the exact calls to make:

**On character spawn / scene load:**
1. `GET /character` — loads character data including level/xp/gold/stats
2. `GET /api/inventory/:characterId` — populate bag UI + apply equipped stats

**On item pickup:**
- `POST /api/inventory/save` (full slot array)

**On equip/unequip:**
- `POST /api/inventory/equip` → recalculate player stats from stat_bonus JSON

**On level up or gold change:**
- `POST /api/character/save-progress` — call on level-up and hub return (not on every kill)

**On forge/crafting UI open:**
- `GET /api/recipes?profession=mining`
- `GET /api/professions/:characterId`

**On craft button press:**
- `POST /api/craft` → on success refresh inventory; on failure show `error` string in popup

**On hub return / disconnect:**
- `POST /api/inventory/save`
- `POST /api/character/save-progress`
- `PATCH /character/position`

---

## Immediate Next Tasks (Week 4 Unity)

This is what needs to be built right now in Unity:

### Enemies
- 2 enemy prefabs: melee grunt + ranged
- NavMesh bake in arena scene
- Aggro radius → chase → attack (hitbox frame) → death animation
- Wave spawner: spawn N enemies, escalate per wave

### Drop System
- `DropTable` ScriptableObject on each enemy prefab (references `items` table IDs by string)
- On enemy death: roll drop → instantiate `WorldItem` prefab
- `WorldItem`: float + rotate + glow color by rarity (common=white, uncommon=green, rare=blue, epic=purple)
- Pickup sphere collider → add to inventory array → `POST /api/inventory/save`
- Gold drop: add to `character.gold` → include in next `save-progress` call

### Inventory UI
- 4×6 bag grid, opens on keybind (B or I)
- Slot hover tooltip: item name, type, stats, rarity color
- Right-click slot → equip → `POST /api/inventory/equip` → recalc stats
- Load from `GET /api/inventory` on scene start
- Gold counter on HUD

---

## Known Bugs / Open Issues

| Issue | Location | Notes |
|---|---|---|
| `orientation:F3` float format | `PATCH /character/position` | Unity serializing float as `"0.000"` string instead of `0.0` number |
| `GmConsole.cs` spam | Unity server build | Needs `#if !UNITY_SERVER` guard — spamming InvalidOperationException every frame |
| Chat log prefix missing | `CmdSendChat` server-side | Add `Debug.Log("[CHAT] username: message")` |
| Uptime Kuma not configured | port 3001 on VPS | Monitoring service running, web UI needs setup |
| Discord link | `/var/www/rod/index.html` | Shows "Coming Soon" |

---

## Server Conventions (rules for any new code)

1. **URL namespaces** — each system gets its own: `/api/inventory/*`, `/api/craft`, `/api/professions/*`, `/api/combat/*` (future), `/api/quests/*` (future)

2. **Response shape** — always `{ success: true, data: {...} }` or `{ success: false, error: "..." }` on `/api/*` endpoints. Old endpoints (`/login`, `/character`) use their own shape — don't change them.

3. **Error messages** — write them as if a player will read them: `"missing ingredient: material_copper_bar (need 3, have 1)"` not `"error"`

4. **Auth** — `requireJWT` middleware + `ownedCharacter()` ownership check on every endpoint that touches player data

5. **Transactions** — any write touching more than one table goes in a `beginTransaction / commit / rollback` block

6. **SQL** — always parameterized queries `pool.execute('... WHERE id = ?', [id])`, never string interpolation

7. **Logs** — structured prefix: `[CRAFT]`, `[LOOT]`, `[PROGRESS]`, `[LOGIN]`, `[LOGOUT]`, `[GM]`, always include `char#${id}` and username

8. **Migrations** — additive only, always `DEFAULT` on new columns, `CREATE TABLE IF NOT EXISTS` on new tables, use INFORMATION_SCHEMA check for `ADD COLUMN` (MySQL 8 doesn't support `ADD COLUMN IF NOT EXISTS`)

9. **New systems checklist** — schema → seed data → endpoints → curl smoke test → dashboard stats → restart + check logs → update CROSSWORLDS.md

10. **Ports are frozen** — 3000 (auth), 4000 (dashboard), 7777/UDP (game), 80/443 (nginx), 3001 (Kuma)

11. **Unity calls port 3000 directly** — not through Nginx. JWT expires 24h. Float values must be JSON numbers, not formatted strings.

---

## Phase 2 (planned, not started)

These schemas are already in the DB. When Phase 1 ships, build in this order based on playtest feedback:

- **Marketplace** — `gold_transactions` + `marketplace_listings` tables ready, need endpoints + Unity AH UI
- **Guilds** — `guilds` + `guild_members` tables ready, need endpoints + Unity guild panel
- **Quests** — schema not yet created, quest giver NPCs, kill/gather/deliver types, quest log UI
- **Talent trees** — per-class 3-path trees, points every 5 levels, respec gold sink
- **More dungeons** — iron tier, boss encounters, world boss event
- **World expansion** — second hub biome, outdoor overworld, mounts, housing

---

## What NOT to Build in Phase 1

Auction house endpoints, guild endpoints, mounts, housing, PvP, raids, talent trees, mail, a 6th class, multiple continents, dozens of items.

The schemas exist. The features don't. Keep it that way until playtest says otherwise.

---

## Useful Commands

```bash
# SSH in
ssh ubuntu@playcrossworlds.com

# Service management
sudo systemctl restart crossworlds-auth
sudo systemctl restart crossworlds-dashboard
sudo systemctl restart crossworlds
sudo systemctl status crossworlds-auth crossworlds-dashboard crossworlds

# Logs
sudo journalctl -u crossworlds-auth -n 50 --no-pager
sudo journalctl -u crossworlds -n 50 --no-pager
tail -f /var/log/crossworlds.log

# Database
mysql -u rodgame -p'<pass from /opt/rod-auth/.env>' rod_online

# Deploy new Unity build
scp -r ./Build/* ubuntu@playcrossworlds.com:/game/Builds/
ssh ubuntu@playcrossworlds.com "chmod +x /game/Builds/CrossworldsBCE.x86_64 && sudo systemctl restart crossworlds"

# Smoke test health endpoint
curl https://playcrossworlds.com:3000/api/health
```
