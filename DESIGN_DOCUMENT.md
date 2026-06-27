# Crossworlds (BCE) — Master Design Document
*Last updated: June 2026*

---

## Vision

A tight, polished sci-fi action MMO built for a small crew of friends. Combat feels like Guild Wars 2 — active, movement-driven, readable — wrapped in a post-apocalyptic world where gear is everything and no one sits at max level waiting to get good. Target audience: 10 players max. Target feel: AAA mechanics at indie scope.

---

## What You Already Have (Don't Rebuild These)

Reading the existing code reveals a surprisingly mature foundation:

- **Gear-driven progression** — no XP/levels; all power comes from `Equipment` + `Attunement` sockets. This is the right call. Keep it.
- **Snapshot/Rollback system** — 5-second team-wide time reversal. This is a killer raid mechanic. Lean into it.
- **ClassAbilityPool** — 4 equipped abilities drawn from a larger class spellbook. Already matches the GW2 feel you want.
- **CharacterStats** — gear feeds DamageMultiplier, CooldownReduction, MoveSpeedMultiplier, HealMultiplier. Clean design.
- **StatusEffectManager** — robust debuff/buff layer already in place.
- **Engineer class** — distinct sci-fi identity with deployables, phase relays, nanite swarms, singularities.

---

## Tech Stack Decision

### Game Engine: Stay on Unity

You're already there, the asset library is massive, and the open-source networking options are excellent. Do not switch.

**Unity version**: Stay on Unity 6 LTS (or 2022 LTS if already on it). Use **URP** — you already have URP VFX packages.

### Networking: Mirror Networking

Mirror is open source (MIT license), free, battle-tested in indie MMOs, and designed as a spiritual successor to UNET. It supports the server-authoritative model you need.

```
Install via Unity Package Manager:
https://github.com/MirrorNetworking/Mirror
```

Key Mirror concepts you'll use:
- `NetworkManager` — manages connections, spawning, scenes
- `NetworkIdentity` — marks GameObjects as networked
- `[SyncVar]` — auto-syncs variables (health, position) to all clients
- `[Command]` — client → server calls (player inputs ability)
- `[ClientRpc]` — server → all clients (ability VFX, hit confirmation)

**Transport**: KCP (Mirror's default, UDP-based) is fine for 10 players on LAN/local server. If you need relay/NAT traversal later, add the Fizzy Steamworks transport.

### Backend: MySQL + Custom Auth Server

Your server can run all of this:

```
[ Unity Game Server (Mirror) ] ←→ [ MySQL Database ]
         ↑
[ Auth HTTP Server (Node.js or Python/FastAPI) ]
         ↑
[ Game Client (Unity) ]
```

**Database**: MySQL or MariaDB (MariaDB is a drop-in replacement, slightly lighter).

**Auth Server**: A small Node.js or Python FastAPI server that handles:
1. Account creation / login
2. Issues a JWT token on success
3. Game server validates the JWT before allowing the client to join

**Why not SQLite?**: SQLite is single-writer. Fine for development, but once you have concurrent players writing to it (loot drops, inventory saves) you'll hit locking issues. Start with MySQL locally and you'll never need to migrate.

---

## SQL Database Schema

```sql
-- Accounts (owned by Auth server)
CREATE TABLE accounts (
    id          INT PRIMARY KEY AUTO_INCREMENT,
    username    VARCHAR(64) UNIQUE NOT NULL,
    email       VARCHAR(256) UNIQUE NOT NULL,
    password_hash VARCHAR(256) NOT NULL,  -- bcrypt
    created_at  TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Characters (one account can have multiple characters)
CREATE TABLE characters (
    id          INT PRIMARY KEY AUTO_INCREMENT,
    account_id  INT NOT NULL REFERENCES accounts(id),
    name        VARCHAR(64) UNIQUE NOT NULL,
    class_name  VARCHAR(64) NOT NULL,     -- 'Engineer', 'Vanguard', etc.
    equipped_abilities JSON,              -- array of 4 spellbook indices
    zone        VARCHAR(128) DEFAULT 'StartingZone',
    pos_x FLOAT DEFAULT 0, pos_y FLOAT DEFAULT 0, pos_z FLOAT DEFAULT 0,
    created_at  TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Inventory
CREATE TABLE inventory (
    id          INT PRIMARY KEY AUTO_INCREMENT,
    character_id INT NOT NULL REFERENCES characters(id),
    slot        INT NOT NULL,             -- 0-based bag slot
    item_id     VARCHAR(64) NOT NULL,     -- matches ItemData.itemId
    attunement_1 VARCHAR(64),            -- socket 1
    attunement_2 VARCHAR(64),            -- socket 2
    UNIQUE KEY (character_id, slot)
);

-- Equipment (what's actually worn)
CREATE TABLE equipment (
    character_id INT PRIMARY KEY REFERENCES characters(id),
    head        VARCHAR(64),
    chest       VARCHAR(64),
    hands       VARCHAR(64),
    legs        VARCHAR(64),
    feet        VARCHAR(64),
    weapon      VARCHAR(64),
    offhand     VARCHAR(64),
    -- attunements stored as JSON per slot
    attunements JSON
);

-- Dungeon / Raid completion log
CREATE TABLE dungeon_completions (
    id          INT PRIMARY KEY AUTO_INCREMENT,
    character_id INT NOT NULL REFERENCES characters(id),
    dungeon_id  VARCHAR(64) NOT NULL,
    difficulty  ENUM('normal','hard','nightmare') DEFAULT 'normal',
    completed_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    boss_kill_time_seconds INT,
    -- for weekly lockouts:
    week_number INT AS (WEEK(completed_at)) STORED
);

-- Loot table (server-side, never exposed to client)
CREATE TABLE loot_tables (
    id          INT PRIMARY KEY AUTO_INCREMENT,
    source_id   VARCHAR(64) NOT NULL,    -- bossId or chestId
    item_id     VARCHAR(64) NOT NULL,
    weight      INT NOT NULL DEFAULT 100, -- relative drop weight
    min_quantity INT DEFAULT 1,
    max_quantity INT DEFAULT 1
);
```

The game server connects to MySQL directly via a C# MySQL connector (`MySql.Data` or the lighter `MySqlConnector` NuGet package, which works in Unity).

---

## Combat Design — The GW2 Feel

### Core Rules
- **5 skills per loadout**: 4 from ClassAbilityPool + 1 class identity ultimate (long CD, big impact)
- **Active dodge roll**: i-frames, stamina-gated (2 charges, recharges over ~8s)
- **No hard trinity**: roles exist but are soft. Any build can survive. Good players cover gaps.
- **No tab-targeting**: all abilities are aimed, AoE ground-placed, or cone — player must position
- **Movement never stops**: abilities should mostly be castable while moving; some have a brief root as a trade-off

### Stat System (maps to your existing CharacterStats)

| Stat | Effect |
|------|--------|
| **Power** | +DamageMultiplier |
| **Resilience** | +DamageReduction |
| **Haste** | +CooldownReduction |
| **Swiftness** | +MoveSpeedMultiplier |
| **Vitality** | +MaxHealth (flat) |
| **Attunement** | unlocks socket bonuses on gear |

All stats come from gear only. No baseline growth. A fresh character with good gear is competitive.

### Dodge Roll Implementation

```csharp
// Add to PlayerMovement.cs
public float dodgeForce = 12f;
public float dodgeDuration = 0.35f;
public int   dodgeCharges = 2;
public float dodgeRecharge = 4f;

// On double-tap direction or dedicated key (Left Alt):
// 1. Apply velocity burst in current move direction (or backward if no input)
// 2. Set isInvulnerable = true on Health component for dodgeDuration
// 3. Play dodge animation
// 4. Consume one charge; start recharge timer
```

The `Health.cs` script already needs an `isInvulnerable` bool — add it and check it in TakeDamage.

### Wall Climbing

Use Unity's **CharacterController** or a third-party solution like **Kinematic Character Controller** (free on GitHub). For wall climbing specifically:
1. Raycast forward while airborne
2. If surface angle < 80°, activate "wall grab" state
3. Allow vertical movement along the wall surface
4. Launch off with a wall-jump input

This is a mid-project addition. Lock in ground movement first.

---

## Classes — Crossworlds Roster

Your Engineer is a great template. Here's a full 4-class roster that fits the sci-fi setting:

### 1. Engineer (You Have This)
*The Tactician — deploys turrets, mines, relays. Zones battlefields.*

**Abilities**: Nanite Swarm, Singularity, Null Field Zone, Phase Relay, Shock Mine
**Identity Passive**: Overengineered (existing), Phase Charge (existing)
**Ultimate**: Siege Mode (existing — turret form)
**Role feel**: Battlefield control, sustained AoE, team utility

### 2. Vanguard
*The Front Line — heavy armor, kinetic shields, gap closers.*

**Abilities**:
- **Shield Slam** — charge forward, knock back first target hit
- **Iron Barricade** — project a physical barrier (blocks projectiles for 4s)
- **Retaliation Field** — aura that reflects 30% of damage taken back to attacker
- **Plasma Anchor** — tether to an enemy, reducing their movement speed and pulling them toward you over 3s
- **[Ultimate] Colossus Stance** — 8s: immune to CC, 50% DR, all attacks knock back

**Role feel**: Tank/aggro anchor, CC chain, peel for teammates

### 3. Specter
*The Ghost — stealth, assassination bursts, repositioning.*

**Abilities**:
- **Phase Step** — short-range blink (2 charges)
- **Shadow Mark** — mark a target; your next attack deals +60% damage and applies Revealed
- **Smoke Screen** — AoE cloud that blinds enemies (50% miss chance) for 3s
- **Neural Spike** — interrupt + silence for 2s; 20s cooldown
- **[Ultimate] Phantom Protocol** — 6s full stealth; all abilities cost no stamina; next attack from stealth deals 200% damage

**Role feel**: Pick, silence, disrupt casters; high risk/reward

### 4. Medic
*The Lifeline — reactive heals, purges, resurrection tech.*

**Abilities**:
- **Restoration Beacon** (already exists!) — place AoE heal zone
- **Trauma Patch** — instant moderate heal on target; shorter CD if target is below 30% HP
- **Nanite Purge** — cleanse all debuffs from target; applies a 3s debuff immunity shield
- **Overcharge** — sacrifice 20% of your own HP to give target a 10s damage buff (+30%)
- **[Ultimate] Emergency Protocol** — channel 2s: revive all downed allies in 20m range at 50% HP, apply 5s damage immunity to all revived

**Role feel**: Reactive, high stakes, punishes poor positioning — not a passive heal-bot

---

## Dungeons — Design Blueprint

### Structure
Dungeons are **instanced zones** (Mirror's scene management handles this). Each dungeon:
- 3 bosses minimum
- 15–30 minute clear time at normal difficulty
- Designed for **3–5 players** (scales within that range via stat curves)

### Dungeon 1: The Collapsed Array
*An underground research facility overtaken by rogue AI and decayed nanite swarms.*

**Boss 1 — SENTRY-7 (Gate Boss)**
Simple tank-and-spank with periodic adds. Teaches players the room.
- Mechanic: Laser sweep (dodge left/right), Shield Phase (stop DPS, destroy shield turret), Turret Spawns

**Boss 2 — The Amalgam**
Two linked targets. Kill them within 10 seconds of each other or the survivor enrages.
- Mechanic: Position split (can't stack), Link Pulse (damage chain between linked targets), Enrage if separated too long

**Boss 3 — CORE INTELLIGENCE (Final Boss)**
The rogue AI in its physical cradle.
- Phase 1: Standard combat. Mechanic: Gravity Well (knockback + pulls players toward spikes)
- Phase 2 (50% HP): Activates your own Snapshot System against you — reverses last 3s of player positions (everyone teleports back). Players must anticipate this.
- Phase 3 (25% HP): Enrage, all damage +50%, spawns mirror copies of 2 random players

**Loot**: Gear tier "Array" — strong Haste/CDR focus, great for Engineer/Medic

---

### Raid 1: The Fractured Spire (10-player)
*A suspended megastructure where time is unstable.*

**Design philosophy**: Raid fights reward team coordination, not individual mechanics spam. With 10 players you can do 2 groups of 5 doing separate mechanics simultaneously.

**Boss 1 — The Wardens (3 mini-bosses)**
Classic "cleave" opener. Kill order matters. Third Warden enrages when the other two die unless all three die within 15s.

**Boss 2 — Temporal Rift Engine**
The Snapshot System becomes the core mechanic here.
- Every 45 seconds, the Rift Engine forces a 3-second rollback on half the raid (random)
- The other half must "anchor" by standing on glowing plates or they get rolled back too
- Players must communicate: "I'm anchored, you roll back, heal yourself when you return"

**Boss 3 — The Echo**
A mirror of your party composition. Spawns with abilities that counter whatever your group is running (tank heavy? it spawns shield-busters; stealth heavy? it has AoE reveals).
- Enrage timer: 8 minutes
- Adds spawn every 60s that must be killed before they reach the center (or +10% boss damage buff)

**Boss 4 — Decay Sovereign (Final)**
Multi-phase, 15-minute encounter.
- Phase 1: Split the raid into two groups (5/5), each fights a smaller clone
- Phase 2: Clones merge, full 10-player fight, HP = combined remaining from Phase 1 (reward killing clones fast)
- Phase 3 (25%): Unleashes Decay Field — floor is lava except for rotating safe zones, boss gains stacking damage buff every 5s

**Loot**: "Spire" tier gear — highest item level, unique Attunement sockets only available here

---

## Networking Architecture

```
GAME CLIENT (Unity + Mirror)
    │
    │  JWT in connection header (validated once at handshake)
    │
GAME SERVER (Unity Headless + Mirror NetworkManager)
    │         │
    │         └─→  MySQL (player data, inventory, loot, lockouts)
    │
AUTH SERVER (Node.js / FastAPI — runs on same machine)
    │
    └─→ MySQL (accounts table only)
```

### Server-side authority rules (Mirror)
- **All damage calculated on server** — client sends "I used ability X toward direction Y," server validates and applies damage
- **Loot rolls on server** — clients never see loot tables; server picks, sends result
- **Position is client-predicted, server-corrected** — Mirror's built-in reconciliation handles this
- **No client trust for stats** — CharacterStats always loaded from DB on login, never from client

### Authentication Flow
1. Client enters username/password
2. POST to Auth Server `/login` → returns `{ token: "jwt..." }`
3. Client includes token in Mirror's `NetworkAuthenticator` handshake
4. Game server validates JWT signature with shared secret
5. On valid: loads character data from MySQL, spawns player
6. On invalid: kick connection

Use `SimpleWebTransport` or Mirror's built-in `KcpTransport`. For auth, implement Mirror's `NetworkAuthenticator` base class.

---

## Development Roadmap

### Phase 1 — Foundation (Now → Month 2)
- [ ] Integrate Mirror Networking — player sees other players move
- [ ] Server-authoritative health/damage — abilities deal damage on server
- [ ] MySQL connection from Unity — save/load character position and equipment
- [ ] Auth server (Node.js, ~100 lines) — login issues JWT, game server validates it
- [ ] Basic character select screen → load into world (you have CharacterSelectUI, wire it up)

### Phase 2 — Combat Polish (Month 2–3)
- [ ] Dodge roll with i-frames on Health component
- [ ] Networked ability VFX (ClientRpc for effects, Command for damage)
- [ ] All 4 classes playable — Vanguard, Specter, Medic abilities implemented
- [ ] Status effect sync over network (StatusEffectManager → SyncList)
- [ ] Basic enemy AI that works server-side (EnemyAI.cs exists, make it server-authoritative)

### Phase 3 — First Dungeon (Month 3–4)
- [ ] Instanced scene loading (Mirror's `NetworkManager.ServerChangeScene`)
- [ ] Boss 1 and Boss 2 of The Collapsed Array
- [ ] Loot system — server rolls from loot_tables, sends to player inventory, saves to MySQL
- [ ] Dungeon entrance trigger in world → party system → load instance

### Phase 4 — Raid + Polish (Month 4–6)
- [ ] Party/group system (up to 10) with health bars for all members
- [ ] Raid instance: The Fractured Spire
- [ ] Temporal Rift Engine fight — hook into your existing SnapshotSystem
- [ ] Weekly lockout system (dungeon_completions table + server check on enter)
- [ ] Chat system (Mirror's built-in NetworkMessage is fine for 10 players)
- [ ] Mount system polish (your motorcycle exists, generalize MountController)

### Phase 5 — World + Feel (Ongoing)
- [ ] Wall climbing (Kinematic Character Controller)
- [ ] More zones beyond Starting Zone
- [ ] Sound design pass (you have Nature Sounds Pack — use it)
- [ ] Settings menu, keybind remapping, UI polish

---

## Open Source Stack Summary

| Purpose | Package | License |
|---------|---------|---------|
| Networking | [Mirror](https://github.com/MirrorNetworking/Mirror) | MIT |
| MySQL in Unity | [MySqlConnector](https://github.com/mysql-net/MySqlConnector) | MIT |
| Auth (Node.js) | Express + jsonwebtoken + bcrypt | MIT |
| Character Controller (future) | [Kinematic Character Controller](https://github.com/philipcass/KinematicCharacterController) | MIT |
| UI framework | TextMesh Pro (already have it) | Unity |

Everything here you can ship commercially. No royalties, no licensing issues.

---

## Quick Wins to Do This Week

1. **Install Mirror** and get two instances of Unity running in the same scene — just see players move together. That moment will feel incredible and tell you you're on the right path.
2. **Add `isInvulnerable` to Health.cs** and wire up a dodge roll. The movement script is already solid, this is a 30-minute addition.
3. **Stand up the auth server** — it's ~80 lines of Node.js. Once you have login working, everything else feels like a real game.
