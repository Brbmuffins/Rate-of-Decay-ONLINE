# Rate of Decay ONLINE

**Genre:** 4-5 player co-op action combat  
**Engine:** Unity 6.4 (URP)  
**Setting:** Post-collapse tech dystopia with scrap-tech aesthetic

## Overview

Rate of Decay ONLINE is a fast-paced co-op shooter where four elite operators fight through hostile environments using tactical deployables and class-based abilities. No leveling. No grinding. Just gear attunements and skillful group combat.

## Core Gameplay

- **32 Abilities** across 5 specialized classes
- **Gear + Attunements** — the *only* progression. No XP, no levels: power comes from equipped gear and the attunements socketed into it
- **Deployable System** — turrets, shields, relays, and traps with output stacking
- **Wave Chests** — escalating enemy waves with prep windows for team coordination
- **Status Effects** — Slow, Stagger, Suppress, Exposed, Tethered, DoT, Debuffed
- **5-Second Rollback** — System Rollback ability lets teams undo the last 5 seconds of combat
- **Threat-Based Aggro** — Guardian's Threat Protocol redirects enemy focus

## Classes

**Engineer** — Tech specialist with shock mines, drones, and turret overload  
**Guardian** — Frontline tank with shields, tethers, and aggro control  
**Phaser** — Spatial assassin with phase shifts and singularity pulls  
**Medic** — Support with healing, shields, and team revivals  
**Wraith** — Stealth operator with null fields and shadow relays  

## Passive Systems

Each class has a passive that rewards playstyle:
- **Overengineered** (Engineer) — Deployables stack output multipliers
- **Phase Charge** (Phaser) — 6 ability casts trigger ×1.4 damage multiplier
- **Threat Protocol** (Guardian) — Damage stacks grant damage redirect + 20% DR
- **Triage Loop** (Medic) — Heal allies, gain 8% self-heal per ally heal
- **Bounty System** (Wraith) — Elite kills reset ability cooldowns

## Progression — Gear & Attunements

No leveling by design. A character's power is entirely gear-driven:

- **Gear** (`ItemData`) carries innate stat bonuses and a number of attunement sockets.
- **Attunements** (`Attunement`, ScriptableObjects) are tiered upgrades you socket into gear.
- **`CharacterStats`** aggregates everything equipped and feeds it into combat: bonus Max Health and Damage Reduction (via `Health`), plus Damage, Cooldown Reduction, and Move Speed multipliers consumed by `AbilityCaster` and `PlayerMovement`.

See `Docs/BackendSetup.txt` Part 11 for the full system and authoring steps.

## Current State

**Backend:** Complete. All 5 classes, 32 abilities, combat systems, passive logic, and the gear/attunement progression are implemented in C#. `Health.cs` is the single source of truth for HP (the old `Stats.cs` was retired).

**Frontend:** Ready for Unity wiring. Full setup documentation provided with:
- VFX asset mapping (brbmuffins Technologies & Dark Arts packs)
- Animation clips (Human Spellcasting animations)
- Ability icon assignments (Free RPG Icons pack)
- Explicit Unity 6.4 setup instructions

## Project Structure

```
Assets/
├── Characters/       — Player prefabs, animators, character models
├── Combat/          — Health, damage, status effects, AI, wave chests
├── Abilities/       — Deployable behaviors (turrets, relays, zones)
├── UI/              — AbilityCaster, ability bar, character select
├── Player/          — Movement, handlers (tether, shields, dash)
├── brbmuffins */    — Asset packs (VFX, animations, icons, models)
└── Scenes/          — Level scenes
```

## Setup

See `Docs/BackendSetup.txt` for complete wiring guide (11 parts):
1. Combat systems overview
2. Per-class prefab setup
3. Ability prefab creation
4. Enemy prefab setup
5. Tags & layers
6. Scene hierarchy
7. Hook call order
8. VFX asset reference
9. Animation setup
10. Icon assignments
11. Compiler notes

## Next Steps

1. Wire up player prefabs with all components
2. Create ability prefabs with VFX
3. Set up animator controllers with state machines
4. Configure AbilityBar UI and icon assignments
5. Test class abilities in gameplay
6. Balance numbers based on playtesting

**Assets to source:** see `Docs/AssetShoppingList.txt` — prioritized list of the gaps (audio, sci-fi icons, class models, ore-node mesh) with free sources. Audio + tech icons are the highest-impact for combat feel.

---

"It's called the ocean, you idiot — the big blue wet thing. Don't drink it. For your health!" —Dr. Steve Brule
