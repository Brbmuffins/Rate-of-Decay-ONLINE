# Rate of Decay ONLINE

**Genre:** 4-10 player co-op combat MMO  
**Engine:** Unity 6 LTS · Universal Render Pipeline  
**Networking:** Mirror (KCP transport) · Server-authoritative  
**Backend:** Node.js auth server · MySQL · VPS at 15.*.*.*

---
---

## Classes

| Class | Role | Identity |
|-------|------|----------|
| **Engineer** | Damage / Control | Turrets, shock mines, arc cannon — builds the killzone before enemies arrive |
| **Guardian** | Tank / CC | Iron Tether, Breach Slam, threat management — the anvil the team fights around |
| **Wraith** | Stealth / Burst | Null fields, shadow relays, backstab burst — punishes poor positioning |
| **Medic** | Support | Emergency revivals, heal shields, sustained team uptime |
| **Phaser** | Assassin | Phase shifts, singularity pulls, spatial repositioning |

Each class has **4 equipped abilities** drawn from a larger class spellbook (GW2-style) plus one ultimate. See [`COMBAT.md`](COMBAT.md) for full ability tables, combos, and design intent per class.

---

## Core Systems

**Dodge Roll** — 2 charges, 0.35s i-frames, resource not a reaction button. The foundation of combat feel.

**Gear + Attunements** — the only progression. Gear carries stat bonuses and attunement sockets. No leveling by design.

**Deployable System** — turrets, shields, relays, and traps with output stacking (Engineer passive: Overengineered).

**Status Effects** — Slow, Stagger, Suppress, Exposed, Tethered, DoT, Debuffed.

**System Rollback** — team ultimate that rewinds the last 5 seconds of combat state.

**Threat Protocol** — Guardian passive that redirects enemy aggro based on damage taken.

Full design rationale and system breakdown: [`DESIGN_DOCUMENT.md`](DESIGN_DOCUMENT.md)

---

## Current State

**Networking** — Mirror host/client working end-to-end. Login → GameWorld with player spawn and camera follow confirmed.

**Editor automation** — Everything is wired via `RoD →` Unity menu items. No manual drag-and-drop required:
- `RoD/Setup/0` — builds CharacterSelect.unity (3D model preview, class list, abilities panel, VFX per class)
- `RoD/Setup/1` — builds LoginScene from scratch (NetworkManager, auth, transport, EventSystem, UI)
- `RoD/Setup/2` — cleans GameWorld of stray NetworkManager components
- `RoD/Setup/3` — fixes build settings scene order (Login → CharacterSelect → GameWorld)
- `RoD/Setup/4` — creates Engineer/Guardian/Wraith/Medic prefabs, auto-assigns to NetworkManager
- `RoD/Setup/5` — assigns AnimatorController to all class prefabs
- `RoD/World/Populate GameWorld with NPCs` — places Zompy, Bob, Kodiac, Turret with VFX and ground plane

**Backend combat** — Complete. All 5 classes, 32 abilities, passive systems, gear/attunement progression implemented in C#.

**Frontend** — Mirror networking live. Player movement, camera follow, NPC patrol working. Animation, ability bar, and UI hookup next.

---

## Project Structure

```
Assets/
├── Game/
│   ├── Abilities/       — Ability scripts, deployable behaviors
│   ├── Audio/           — Sound hooks
│   ├── Characters/      — Player prefabs, NPC controller, character models
│   ├── Combat/          — Health, damage, status effects, EnemyAI, wave chests
│   ├── Editor/          — RodEditorSetup, RodPrefabBuilder, RodNpcBuilder
│   ├── Items/           — Gear, attunements, inventory
│   ├── Networking/      — RodNetworkManager, RodNetworkAuthenticator
│   ├── Player/          — Movement, CameraFollow, tether/shield/stealth handlers
│   ├── Prefabs/         — Class prefabs (Engineer, Guardian, Wraith, Medic)
│   ├── Scenes/          — LoginScene.unity
│   └── UI/              — LoginManager, LoginScreenVFX
├── brbmuffins */        — VFX asset packs (Dark Arts, Studio, Technologies, Skybox...)
├── Mirror/              — Mirror Networking
└── ...                  — Third-party assets
COMBAT.md                — Combat bible: dodge system, class breakdowns, combos
DESIGN_DOCUMENT.md       — Full system design and architecture decisions
Docs/                    — Setup guides for backend, abilities, inventory, and more
```

---

## Setup

**Prerequisites:** Unity 6 LTS, Mirror (included in `Assets/Mirror`)

**First-time scene setup (run in order):**
1. Open the project in Unity
2. `RoD → Setup → 0 ▶ Create Character Select Scene`
3. `RoD → Setup → 1 ▶ Create Login Scene`
4. `RoD → Setup → 4 ▶ Create Class Prefabs`
5. `RoD → Setup → 5 ▶ Fix Animator Controllers`
6. `RoD → World → Populate GameWorld with NPCs`
7. Press Play in LoginScene → click **HOST (DEV)** → pick class → **ENTER WORLD**

For the auth server and MySQL backend, see `Docs/BackendSetup.txt`.

---

## About

Mostly smarter people than Brbmuffins. Some unity store asset. Lots of AI. Not much of my own original work really. Puprets 

---

