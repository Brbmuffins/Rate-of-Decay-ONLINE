# Crossworlds BCE — Server System Reference

> **Insurance policy document.** If you lose context, read this top to bottom before touching anything.  
> Last verified: 2026-06-28

**Server IP:** 15.204.243.36  
**Domain:** playcrossworlds.com — DNS live, SSL active (Let's Encrypt via Certbot)  
**OS:** Ubuntu 22.04 LTS  
**SSH:** `ssh ubuntu@playcrossworlds.com`  
**Disk:** 388 GB total, ~7 GB used — plenty of headroom

---

## Phase 1 Status

> Quick reference for where the game loop stands. Server = auth server API + DB. Client = Unity.

| Week | Focus | Server | Unity Client |
|---|---|---|---|
| 1 | Foundation | ✅ Done | ✅ Done |
| 2 | Portal System | ✅ Done | 🔶 Portal → arena transition pending |
| 3 | Combat / Classes | ✅ Done | 🔶 Enemy AI + hit confirmation pending |
| 4 | Loot + Inventory | ✅ Done | 🔶 **Active** — enemies, drops, inventory UI |
| 5 | Progression | ✅ Done | ○ Up next — XP bar, level-up UI, stat panel |
| 6 | Crafting | ✅ Done | ○ Up next — mining portal, forge UI, recipes |
| 7 | Polish | — | ○ Damage numbers, VFX, SFX, HUD pass |
| 8 | Playtest | — | ○ 10–20 testers, stress test, Phase 2 vote |

**Server is not the bottleneck for any remaining Phase 1 work.** All APIs for Weeks 4–6 are live. Every remaining task is Unity client.

### What the server session built (beyond original plan)
- `items` master table + 10 seeded items
- `inventory` table + save/load/equip endpoints
- `level`, `xp`, `gold`, `stat_str/agi/int/vit` columns on `characters` + save-progress endpoint
- `professions`, `recipes`, `recipe_ingredients` tables + craft endpoint with skill validation
- `gold_transactions`, `marketplace_listings`, `guilds`, `guild_members` — schema stubbed for Phase 2
- `GET /api/health` endpoint
- Dashboard stats updated (class breakdown, items in circulation, recent logins)

---

## Services

| Service | Port | Unit file | Working dir |
|---|---|---|---|
| Auth server | 3000/TCP | `crossworlds-auth.service` | `/opt/rod-auth` |
| Admin dashboard | 4000/TCP | `crossworlds-dashboard.service` | `/opt/rod-dashboard` |
| Game server | 7777/UDP | `crossworlds.service` | `/game/Builds` |
| MySQL | 3306/TCP | built-in `mysql.service` | — |

All services use `Restart=on-failure`. Manage with:
```bash
sudo systemctl restart crossworlds-auth
sudo systemctl restart crossworlds-dashboard
sudo systemctl restart crossworlds
sudo systemctl status crossworlds-auth crossworlds-dashboard crossworlds
```

---

## File Locations

```
/opt/rod-auth/
  server.js          — Auth server (Express, port 3000)
  .env               — DB creds, JWT secret, port

/opt/rod-dashboard/
  server.js          — Dashboard server (Express + Socket.io, port 4000)
  public/index.html  — Dashboard UI
  public/icon.png    — Game icon (served to dashboard)

/game/Builds/
  CrossworldsBCE.x86_64     — Unity Linux server binary
  CrossworldsBCE_Data/      — Must match binary name exactly
  GameAssembly.so            — Must be from same build as UnityPlayer.so
  UnityPlayer.so

/var/www/rod/
  index.html         — Public download/landing page
  roadmap.html       — Phase 1 roadmap page
  icon.png           — Game icon (public)
  downloads/
    CrossworldsBCE.zip  — Windows client download

/var/log/crossworlds.log    — Game server log (Unity stdout)
/etc/nginx/sites-available/rod  — Nginx config (port 80, serves /var/www/rod)
```

---

## Database

**DB name:** `rod_online`  
**User:** `rodgame`  
**Pass:** in `/opt/rod-auth/.env`  
**Connect:** `mysql -u rodgame -p'<pass>' rod_online`

### Tables

> The old gear system (`item_template`, `item_instance`, `character_gear`, `loot_tables`) is still live but is being superseded by the new system. Do not delete the old tables — Unity still reads `character_gear` on spawn. New features should use the new system only.

| Table | System | Purpose |
|---|---|---|
| `accounts` | Core | Login accounts — id, username, email, password_hash, role, active, last_login |
| `characters` | Core | One per account — class_index, class_name, level, xp, gold, stat_str/agi/int/vit, pos, online |
| `item_template` | **Old gear** | Legacy gear templates (power/defense/speed/cdr/heal stat ranges) |
| `item_instance` | **Old gear** | Legacy gear instances (rolled stats, per character) |
| `character_gear` | **Old gear** | Legacy equipped slots (0–5) — Unity still reads this on spawn |
| `loot_tables` | **Old gear** | Legacy enemy loot weights (source_id, item_id, weight, qty range) |
| `items` | **New** | Item master (VARCHAR id like `sword_copper`, JSON stat_bonus, sell_value) |
| `inventory` | **New** | Inventory slots (character_id, slot_index, item_id, quantity, equipped) |
| `professions` | **New** | Character profession progress (character_id, profession_id, skill_level, skill_xp) |
| `recipes` | **New** | Craft recipes (id, profession_id, skill_level_required, result_item_id) |
| `recipe_ingredients` | **New** | Recipe → item requirements |
| `gold_transactions` | **Phase 2 stub** | Economy audit log — schema only, no endpoints yet |
| `marketplace_listings` | **Phase 2 stub** | Auction house listings — schema only, no endpoints yet |
| `guilds` | **Phase 2 stub** | Guild roster — schema only, no endpoints yet |
| `guild_members` | **Phase 2 stub** | Guild membership — schema only, no endpoints yet |

### Character classes
`class_index`: 0=Engineer, 1=Guardian, 2=Shadowblade, 3=Cleric, 4=Arcanist  
*(defined in `CLASS_NAMES` array in `/opt/rod-auth/server.js`)*

### Seeded items
`sword_copper`, `plate_copper`, `ring_copper`, `material_copper_shard`, `material_copper_bar`, `sword_iron`, `plate_iron`, `staff_apprentice` (Arcanist), `dagger_shadow` (Shadowblade), `tome_cleric` (Cleric)

### Seeded recipes (Mining profession)
- `recipe_copper_bar` — 3x copper_shard → copper_bar (skill 1)
- `recipe_copper_ring` — 2x copper_bar → ring_copper (skill 3)
- `recipe_copper_sword` — 3x copper_bar → sword_copper (skill 5)
- `recipe_copper_plate` — 4x copper_bar → plate_copper (skill 5)

---

## Auth Server API (`localhost:3000`)

All endpoints that require JWT expect: `Authorization: Bearer <token>`

### Auth
| Method | Path | Auth | Body / Notes |
|---|---|---|---|
| POST | `/register` | none | `{username, email, password}` |
| POST | `/login` | none | `{username, password}` → `{token}` |
| GET | `/api/health` | none | `{status, uptime, db, timestamp}` |

### Character (old gear system — do not modify)
| Method | Path | Auth | Notes |
|---|---|---|---|
| POST | `/character` | JWT | Create or return existing character |
| GET | `/character` | JWT | Load character + gear for spawn |
| PATCH | `/character/position` | JWT | Save position on disconnect |
| POST | `/character/gear/equip` | JWT | Equip item_instance to gear slot |
| GET | `/items` | none | All item_templates |

### Progression (new system)
| Method | Path | Auth | Body |
|---|---|---|---|
| POST | `/api/character/save-progress` | JWT | `{characterId, level, xp, gold, stat_str, stat_agi, stat_int, stat_vit}` |

### Inventory (new system)
| Method | Path | Auth | Notes |
|---|---|---|---|
| GET | `/api/inventory/:characterId` | JWT | Returns all slots joined with items table |
| POST | `/api/inventory/save` | JWT | `{characterId, slots:[{slot_index, item_id, quantity, equipped}]}` — bulk upsert, deletes missing slots |
| POST | `/api/inventory/equip` | JWT | `{characterId, slot_index, equipped:0\|1}` |

### Professions & Crafting (new system)
| Method | Path | Auth | Notes |
|---|---|---|---|
| GET | `/api/professions/:characterId` | JWT | Returns profession rows; auto-seeds Mining at level 1 if none |
| GET | `/api/recipes` | none | `?profession=mining` — returns recipes with ingredient list |
| POST | `/api/craft` | JWT | `{characterId, recipeId}` — validates skill, deducts ingredients, awards 10xp, levels up at skill_level×50 xp |

### Response shape (new endpoints)
Success: `{ success: true, data: ... }`  
Failure: `{ success: false, error: "..." }`

---

## Dashboard (`localhost:4000`)

- Basic auth protected (credentials in `/opt/rod-dashboard/.env`)
- **GM dashboard:** `http://playcrossworlds.com:4000/gm-dashboard?token=<ADMIN_TOKEN>`
  - Token set in `/opt/rod-dashboard/.env` as `ADMIN_TOKEN`
- **Tabs:** Services, Live Logs, Stats, Players, Management, Deploy, Game Stats, GM Commands

### Dashboard API endpoints
| Path | Notes |
|---|---|
| `GET /api/services` | Active status for all 3 services |
| `GET /api/services/:name/detail` | PID, memory, restarts |
| `POST /api/services/:name/:action` | start/stop/restart |
| `GET /api/players` | All accounts, ?search= supported |
| `GET /api/players/summary` | Count totals |
| `GET /api/players/:id` | Account + characters + gear |
| `POST /api/players` | Create account |
| `PUT /api/players/:id/password` | Reset password |
| `PUT /api/players/:id/ban` | Ban/unban |
| `PUT /api/players/:id/role` | Set player/gm/admin |
| `DELETE /api/characters/:id` | Delete character (cascades inventory) |
| `GET /api/gm/items` | item_template list for GM tool |
| `GET /api/gm/characters?account_id=` | Characters for GM tool |
| `POST /api/gm/give-item` | Grant item_instance with rolled stats |
| `PATCH /api/gm/character/:id/level` | Set level and XP |
| `PATCH /api/gm/character/:id/teleport` | Set saved position |
| `GET /api/stats` | CPU, RAM, disk, uptime |
| `GET /api/stats/game` | Online count, class breakdown, top chars, recent logins, items in circulation |
| `GET /api/deploy` | Binary stat |
| `POST /api/deploy/restart` | Restart crossworlds service |
| `GET /api/activity` | Last 50 admin actions |

---

## Game Server

- Unity IL2CPP Linux dedicated server, Mirror/KCP transport
- **Critical:** `GameAssembly.so` and `UnityPlayer.so` must be from the same Unity build session (different build IDs = SIGSEGV at `il2cpp::vm::Runtime::Init`)
- **Critical:** `_Data/` folder name must exactly match the binary name (`CrossworldsBCE_Data/` for `CrossworldsBCE.x86_64`)
- Log: `/var/log/crossworlds.log`

### Deploy a new build
```bash
scp -r ./Build/* ubuntu@playcrossworlds.com:/game/Builds/
ssh ubuntu@playcrossworlds.com "chmod +x /game/Builds/CrossworldsBCE.x86_64 && sudo systemctl restart crossworlds"
```

---

## Nginx

Serves `/var/www/rod` on port 80 and 443.  
Config: `/etc/nginx/sites-available/rod`  
`server_name playcrossworlds.com www.playcrossworlds.com _;`

**SSL is live.** Certbot has already issued and configured the Let's Encrypt cert:
```
/etc/letsencrypt/live/playcrossworlds.com/fullchain.pem
/etc/letsencrypt/live/playcrossworlds.com/privkey.pem
```

Certbot auto-renews via a systemd timer. To check: `sudo certbot renew --dry-run`

Nginx serves:
- `https://playcrossworlds.com/` → `/var/www/rod/index.html` (download page)
- `https://playcrossworlds.com/roadmap.html` → Phase 1 roadmap
- `https://playcrossworlds.com/downloads/CrossworldsBCE.zip` → client download (forced attachment)
- Static assets (png, js, css) cached 7 days

Reload after config changes: `sudo systemctl reload nginx`

---

## Accounts

- Account creation is **mod/admin only** via the dashboard — no self-registration on the public site
- Account roles: `player`, `gm`, `admin`
- Characters: one per account, created on first login from Unity

---

## Development Conventions

These rules apply to every new feature, endpoint, or table. Follow them and the system stays clean, debuggable, and easy to hand off.

---

### 1. Endpoint structure — keep systems in their own lane

Every game system gets its own URL namespace. Never mix concerns in one endpoint.

```
/api/inventory/*    — inventory only
/api/craft          — crafting only
/api/professions/*  — professions only
/api/combat/*       — future: combat only
/api/quests/*       — future: quests only
/api/guilds/*       — future: guilds only
/api/marketplace/*  — future: auction house only
```

If you're unsure whether something belongs in an existing endpoint or a new one, make a new one. Smaller endpoints are easier to test and easier to replace.

---

### 2. Response shape — always the same wrapper

Every new endpoint (under `/api/*`) returns this shape, no exceptions:

```json
// Success
{ "success": true, "data": { ... } }

// Failure
{ "success": false, "error": "human readable message" }
```

The old endpoints (`/login`, `/character`, etc.) predate this — don't change them or you'll break Unity. New endpoints only.

---

### 3. Error messages — specific, not generic

Bad:
```json
{ "success": false, "error": "internal server error" }
```

Good:
```json
{ "success": false, "error": "missing ingredient: material_copper_bar (need 3, have 1)" }
{ "success": false, "error": "requires mining level 5 (you have 2)" }
{ "success": false, "error": "inventory full" }
```

Unity reads these strings. Specific errors let the client show the right popup without guessing. When adding a new validation, write the message as if a player will read it.

---

### 4. Auth — validate JWT before touching the database

Use `requireJWT` middleware on every endpoint that writes or reads player-owned data:

```js
app.post('/api/your-endpoint', requireJWT, async (req, res) => { ... });
```

Then use `ownedCharacter(req, res, characterId)` to confirm the characterId in the request actually belongs to the JWT's account. Never trust a characterId from the client without this check.

```js
const char = await ownedCharacter(req, res, req.body.characterId);
if (!char) return; // ownedCharacter already sent the 403
```

Public read-only endpoints (like `/api/recipes`) do not need auth.

---

### 5. Database writes — always use transactions when touching multiple tables

If a craft, purchase, or trade touches more than one table, wrap it in a transaction. A partial write is worse than a failed write.

```js
const conn = await pool.getConnection();
try {
  await conn.beginTransaction();
  // ... all writes here ...
  await conn.commit();
} catch (e) {
  await conn.rollback();
  throw e;
} finally {
  conn.release();
}
```

Single-table writes (updating one row) do not need a transaction.

---

### 6. Parameterized queries — never interpolate into SQL

Always:
```js
pool.execute('SELECT * FROM items WHERE id = ?', [itemId])
```

Never:
```js
pool.execute(`SELECT * FROM items WHERE id = '${itemId}'`) // SQL injection
```

No exceptions. The linter won't catch this — it's on you.

---

### 7. Log lines — structured prefixes, always include character context

Every meaningful server action gets a log line with a bracketed prefix so you can grep it in the dashboard log viewer:

```js
console.log(`[CRAFT]    ${req.user.username} char#${char.id} crafted ${itemId}`);
console.log(`[LOGIN]    ${account.username} (id:${account.id})`);
console.log(`[PROGRESS] ${req.user.username} char#${char.id} → Lv${level} ${xp}xp`);
console.log(`[LOGOUT]   ${username} → ${map} (${x}, ${y}, ${z})`);
console.log(`[LOOT]     char#${charId} received ${itemId} from ${sourceId}`);
```

Errors go to `console.error` with enough context to reproduce:
```js
console.error(`POST /api/craft char#${characterId} recipe=${recipeId}: ${err.message}`);
```

Log prefixes to use:
- `[LOGIN]` `[LOGOUT]` — account events
- `[CRAFT]` — crafting
- `[LOOT]` — item drops
- `[PROGRESS]` — XP/level/gold saves
- `[CHAT]` — player messages (Unity side)
- `[GM]` — any GM action
- `[COMBAT]` — future
- `[TRADE]` — future marketplace

---

### 8. Database migrations — additive only, always check first

New columns always have a DEFAULT so existing rows are valid immediately:
```sql
ALTER TABLE characters ADD COLUMN new_stat INT NOT NULL DEFAULT 0;
```

New tables use `CREATE TABLE IF NOT EXISTS`.

Never remove a column or rename one without confirming Unity isn't reading it. Old code that reads a missing column will crash silently on spawn.

For MySQL 8 (what this server runs), `ADD COLUMN IF NOT EXISTS` does not work — use an INFORMATION_SCHEMA check instead:
```sql
SET @sql = IF(
  (SELECT COUNT(*) FROM information_schema.COLUMNS
   WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='characters' AND COLUMN_NAME='new_col') = 0,
  'ALTER TABLE characters ADD COLUMN new_col INT NOT NULL DEFAULT 0',
  'SELECT 1'
);
PREPARE s FROM @sql; EXECUTE s; DEALLOCATE PREPARE s;
```

---

### 9. New game systems — checklist

When adding a new system (combat, quests, zones, etc.), do these in order:

1. **Schema first** — write and run the migration SQL. Check `SHOW CREATE TABLE` to confirm.
2. **Seed data** — if the system needs static data (enemy templates, quest definitions), insert it with `INSERT IGNORE`.
3. **Auth server endpoints** — implement in `/opt/rod-auth/server.js`. Follow the response shape above.
4. **Smoke test with curl** — hit every new endpoint before touching Unity or the dashboard.
5. **Dashboard stats** — add the new system's counts/activity to `GET /api/stats/game` so it shows up in the Game Stats tab.
6. **Restart and confirm** — `sudo systemctl restart crossworlds-auth` then check `journalctl -u crossworlds-auth -n 20 --no-pager` for errors.
7. **Update this doc** — add the new endpoints to the API table and the new tables to the DB table above.

---

### 10. Ports — do not move these

| Port | What | Rule |
|---|---|---|
| 3000 | Auth server | Never proxy, never change |
| 4000 | Dashboard | Never proxy, never change |
| 7777 | Game server UDP | Never change — hardcoded in Unity Mirror config |
| 80 | Nginx / public site | OK to add HTTPS (443) via Certbot |
| 443 | Nginx SSL | Live via Certbot |
| 3001 | Uptime Kuma | Monitoring only, do not touch |

---

### 11. Unity integration notes

- Unity calls the auth server directly on port 3000 over plain HTTP — Unity does not go through Nginx
- JWT tokens expire in 24h (set in `.env` as `JWT_EXPIRES_IN=24h`) — Unity should re-login if it gets a 401
- Character position is saved on disconnect via `PATCH /character/position` — if Unity crashes without calling this, the player respawns at their last saved position
- The `online` flag on characters is set by the game server — dashboard shows it but does not control it
- All float values from Unity should be sent as JSON numbers, not strings — the `orientation:F3` bug was Unity sending `"0.000"` as a formatted string instead of `0.0`

### Unity integration checklist for new system endpoints (Weeks 4–6)

These APIs are live and waiting. When Unity connects to them:

**Inventory (Week 4)**
- On character spawn: `GET /api/inventory/:characterId` → populate bag UI and apply equipped stats
- On pickup: `POST /api/inventory/save` with updated slots
- On equip/unequip: `POST /api/inventory/equip` → recalculate player stats
- On hub return: `POST /api/inventory/save` (full sync)

**Progression (Week 5)**
- On login: character data from `GET /character` already includes level/xp/gold/stats — use those
- On level up or gold change: `POST /api/character/save-progress`
- Call save-progress when returning to hub, not on every kill (batched)

**Crafting (Week 6)**
- On forge open: `GET /api/recipes?profession=mining` → populate recipe list
- On profession panel open: `GET /api/professions/:characterId` → show skill level + xp bar
- On craft button: `POST /api/craft` → server validates and deducts → refresh inventory on success
- Error strings from `/api/craft` are player-readable — show them directly in the UI popup

---

## Current System State (as of 2026-06-28)

### Live service memory
| Service | Memory | Uptime note |
|---|---|---|
| crossworlds (game) | ~127 MB | Runs continuously |
| crossworlds-auth | ~16 MB | Node.js, lightweight |
| crossworlds-dashboard | ~19 MB | Node.js + Socket.io |
| mysql | ~399 MB | Uptime 1+ day |

### Database row counts (snapshot)
| Table | Rows |
|---|---|
| accounts | 8 |
| characters | 4 |
| items (seeded) | 10 |
| recipes (seeded) | 4 |
| recipe_ingredients | 4 |
| inventory | 3 |
| professions | 1 |
| All others | 0 |

### Web files at `/var/www/rod/`
- `index.html` — download/landing page
- `roadmap.html` — Phase 1 roadmap
- `icon.png` — game icon (1254×1254 PNG)
- `downloads/CrossworldsBCE.zip` — Windows client

### Game build at `/game/Builds/`
- `CrossworldsBCE.x86_64` (4.7 KB launcher)
- `CrossworldsBCE_Data/` (game data — must match binary name)
- `GameAssembly.so` (775 MB — IL2CPP compiled game code)
- `UnityPlayer.so` (81 MB — must be from same build as GameAssembly.so)
- `CrossworldsBCE_BackUpThisFolder_ButDontShipItWithYourGame/` — Unity debug symbols, safe to ignore
- `Crossworlds_BurstDebugInformation_DoNotShip/` — Burst compiler data, safe to ignore

### Credentials & secrets
All secrets live in `.env` files — never hardcode them anywhere else.
- Auth server config: `/opt/rod-auth/.env`
- Dashboard config: `/opt/rod-dashboard/.env`
  - Dashboard basic auth: `ADMIN_USER` / `ADMIN_PASS` in that file
  - GM dashboard token: `ADMIN_TOKEN` in that file — append `?token=<value>` to the GM dashboard URL

---

## Pending / Future Work

### Phase 1 — Unity client (server is ready, these are all Unity-side)

**Week 4 — Loot + Enemies**
- [ ] Enemy prefabs: grunt (melee) + ranged — NavMesh, aggro radius, attack hitbox, death
- [ ] Enemy wave spawner in arena (N enemies, escalating difficulty)
- [ ] DropTable ScriptableObject on enemy prefabs — rolls from `items` table IDs
- [ ] WorldItem prefab: float, rotate, glow by rarity color, pickup sphere
- [ ] Pickup → `POST /api/inventory/save` → refresh bag UI
- [ ] Inventory bag UI: 4×6 grid, rarity-colored tooltip, right-click to equip
- [ ] Equip → `POST /api/inventory/equip` → live stat recalc on player
- [ ] Gold counter on HUD

**Week 5 — Progression**
- [ ] XP bar and level display on HUD
- [ ] Level-up screen + stat point display per class
- [ ] Character sheet panel (C key) showing all stats + equipment
- [ ] Level-up VFX + sound
- [ ] `POST /api/character/save-progress` called on level up and hub return
- [ ] Level 1–15 XP curve tuning

**Week 6 — Crafting**
- [ ] Copper Golem dungeon or mining portal scene
- [ ] Ore node prefab with gather interaction + respawn timer
- [ ] Forge NPC in hub (opens crafting UI)
- [ ] Crafting UI: recipe list (from `/api/recipes`), ingredient check, craft button
- [ ] `POST /api/craft` → on success refresh inventory; show server error string in popup
- [ ] Profession skill bar UI (from `/api/professions`)

**Week 2 gap — still open**
- [ ] Portal → arena scene transition
- [ ] Basic circular arena geometry
- [ ] Enemy spawn points in arena
- [ ] Return to hub flow

**Week 3 gap — still open**
- [ ] Enemy chase → attack → death cycle
- [ ] Multiplayer hit confirmation (server-authoritative or rollback)

### Ops
- [ ] **Uptime Kuma** — running on port 3001, needs web UI setup at `http://15.204.243.36:3001`
- [ ] **StartLimitIntervalSec=0** on `crossworlds.service` — would allow infinite restarts instead of stopping after 5 rapid crashes

### Unity bugs
- [ ] `orientation:F3` bug in `PATCH /character/position` — Unity serializing float with format specifier instead of raw number
- [ ] `GmConsole.cs` needs `#if !UNITY_SERVER` guard (spamming InvalidOperationException every frame on server)
- [ ] Add `Debug.Log("[CHAT] username: message")` in server-side `CmdSendChat`

### Phase 2 (schema stubbed, no endpoints yet)
- [ ] Marketplace: `POST /api/marketplace/list`, `GET /api/marketplace`, `POST /api/marketplace/buy`
- [ ] Guilds: `POST /api/guilds/create`, `POST /api/guilds/join`, `GET /api/guilds/:id`
- [ ] Quest system: schema + endpoints + Unity quest log UI
- [ ] Talent trees: per-class paths, respec endpoint
- [ ] Second dungeon tier (iron-level)
- [ ] World boss event (timed, hub-wide)

### Site
- [ ] **Discord link** — community section on download page shows "Coming Soon"
