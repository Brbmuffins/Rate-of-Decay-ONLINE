# ROADMAP.md — Phase 1 Status (AI-readable)

> This is a structured snapshot for AI context. The visual HTML version is at the repo root.
> For actual game facts / DB schema, see CROSSWORLDS.md. For Unity tasks, read per-week below.

**Last verified:** 2026-06-28  
**Phase 1 scope:** 8 weeks → playtest 10–20 players, stress test, vote on Phase 2

---

## Overall Status

| Layer | Status |
|---|---|
| Server / VPS | ✅ Complete |
| Unity client | ⚠️ In progress — Weeks 4–7 remaining |
| Database schema | ✅ Complete (incl. Phase 2 stubs) |
| Web / download page | ✅ Live at playcrossworlds.com |

**~65% complete** — server work is done, Unity client work drives the remaining 35%.

---

## Week-by-Week

### Week 1 — Foundation ✅
- [x] VPS setup (Ubuntu 22.04, MySQL 8, Nginx, SSL)
- [x] Auth server: accounts, JWT login
- [x] Unity: LoginScene UI, POST /login, token storage
- [x] Basic UDP game server (Mirror/KCP)

### Week 2 — Characters & Hub ✅
- [x] Server: `POST /character`, `GET /character`, position save
- [x] 5-class system (Engineer, Guardian, Shadowblade, Cleric, Arcanist)
- [x] Hub scene with multiplayer spawn
- [x] CharacterSelect scene
- [ ] Portal → Arena transition (not yet complete, carries to Week 3)

### Week 3 — Chat & Dashboard ✅
- [x] Chat: CmdSendChat + RpcReceiveChat, WASD gating
- [x] Dashboard: player count, status, Socket.io live updates
- [x] ESC menu, camera orbit/zoom
- [x] Online Players HUD (vertical list, VerticalLayoutGroup)
- [ ] Portal transition still pending

### Week 4 — Combat & Loot ⚠️ (server done, Unity pending)

**Server ✅**
- [x] `items` table seeded (copper_shard, copper_bar, gear, materials)
- [x] `inventory` table
- [x] `GET /api/inventory/:characterId`
- [x] `POST /api/inventory/save`
- [x] `POST /api/inventory/equip`
- [x] Loot roll logic (server-side)

**Unity ❌**
- [ ] Enemy prefabs (grunt, ranged) with NavMesh + aggro + death
- [ ] Wave spawner for arena
- [ ] WorldItem prefab (floating pickup with rarity glow)
- [ ] Pickup triggers → POST /api/inventory/save
- [ ] Inventory bag UI (8×4 grid)
- [ ] Equip flow (right-click → POST /api/inventory/equip → stat update)
- [ ] Server-authoritative damage (Mirror Commands)

### Week 5 — Progression ⚠️ (server done, Unity pending)

**Server ✅**
- [x] `characters` table: level, xp, gold, stat columns
- [x] `POST /api/character/save-progress`
- [x] XP thresholds, level-up logic (server-side)

**Unity ❌**
- [ ] POST /api/character/save-progress calls on kill + session end
- [ ] XP bar HUD
- [ ] Level-up screen (flash + stat gains)
- [ ] Character sheet panel (stats, level, class)
- [ ] Gold display in HUD

### Week 6 — Crafting ⚠️ (server done, Unity pending)

**Server ✅**
- [x] `professions` table
- [x] `recipes` + `recipe_ingredients` tables seeded
- [x] `GET /api/professions/:characterId`
- [x] `GET /api/recipes?profession=mining`
- [x] `POST /api/craft` (consume materials, add result item, transaction-safe)

**Unity ❌**
- [ ] Mining portal / ore nodes in hub
- [ ] Ore node harvest → add to inventory → POST /api/inventory/save
- [ ] Forge NPC in hub
- [ ] Crafting UI: profession list → recipe list → craft button
- [ ] POST /api/craft integration + result display

### Week 7 — Polish ❌
- [ ] Floating damage numbers (normal/crit/heal/taken)
- [ ] Hit VFX per damage type
- [ ] Class ability VFX
- [ ] Enemy health bars
- [ ] Ability icon hotbar
- [ ] SFX pass
- [ ] HUD cleanup

### Week 8 — Playtest ❌
- [ ] Playtest 10–20 players
- [ ] Stress test (auth server, DB connections, game server)
- [ ] Bug fixes from playtest
- [ ] Phase 2 priority vote

---

## Open Bugs (tracked — don't close without fixing)

| Bug | Location | Priority |
|---|---|---|
| `orientation:F3` — float sent as formatted string | Unity `PATCH /character/position` | Medium |
| `GmConsole.cs` — no `#if !UNITY_SERVER` guard, crashes every frame on server | Unity | High |
| `CmdSendChat` — missing `[CHAT]` log line server-side | Unity (server command) | Low |

---

## Phase 2 — Not Building Yet

Schema is stubbed in DB, waiting on playtest vote. Candidates:

- Marketplace (`marketplace_listings` table exists)
- Guilds (`guilds`, `guild_members` tables exist)
- Quests
- Talent trees
- World expansion / more dungeons
- Player trading (`gold_transactions` table exists)
