# Rate of Decay ONLINE

**Genre:** 4-5 player co-op action combat  
**Engine:** Unity 6.4 (URP)  
**Setting:** Post-collapse tech dystopia with scrap-tech aesthetic

## Overview

Rate of Decay ONLINE is a fast-paced co-op shooter where four elite operators fight through hostile environments using tactical deployables and class-based abilities. No leveling. No grinding. Just gear attunements and skillful group combat.

## Core Gameplay

- **32 Abilities** across 5 specialized classes
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

## Current State

**Backend:** Complete. All 5 classes, 32 abilities, combat systems, and passive logic fully implemented in C#.

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

---

Built with Claude Code. Backend by Haiku 4.5.
