# Crossworlds (BCE) — Combat Bible
*Last updated: June 2026 — full redesign pass*

---

## Philosophy

**Active, readable, positional.** No tab targeting. No standing still casting. Every button press is a decision. Movement is constant — the game never rewards staying still.

**Four pillars:**
- Dodge is a resource, not a panic button. Two charges means you plan.
- Combos reward knowledge, not reflexes. A Wraith who knows when to detonate beats button-mashers.
- Positioning matters every second. Zones, tethers, and pull abilities only work if someone built for them.
- Every build can survive. No hard trinity — roles exist but no one is helpless without a healer.

---

## Depth Without Complexity — The Design Contract

This is the most important principle in the document. Read it before implementing anything.

**The system is wide, not deep.** A new player on day one has:
- 1 core with 4 abilities (2 locked + movement + ultimate)
- 3 flex slots from the talent rows
- Auto-attack always available

That's 7 total things to press. It's not overwhelming. It feels like a complete kit.

**But variety explodes when you look across all options:**

| Layer | What a player chooses | How many options |
|-------|-----------------------|-----------------|
| Core | Which identity/playstyle | 9 cores |
| Locked abilities | Set per core (defines the kit) | 2 per core = 18 unique abilities |
| Movement abilities | Set per core | 9 unique movement tools |
| Ultimate | Set per core | 9 ultimates, all dramatically different |
| Flex Row 1 | Primary damage style | 3 options |
| Flex Row 2 | Engagement tool | 3 options |
| Flex Row 3 | Situational weapon | 3 options |
| Trait Rows A/B/C | Passive modifiers | 3×3 = 9 passive options |
| Gear preset | Stat emphasis | 6 named presets |

**Total unique build permutations:** 9 cores × 27 talent combos × 6 gear presets = **1,458 distinct builds.** Most of them are genuinely good. None are "broken."

**The learning curve is gradual by design:**
- Day 1: Learn your 4 core abilities. Win with those.
- After a few runs: Start swapping Flex Row 3 for encounters that punish your defaults.
- Level 2/4/6: Trait rows unlock one at a time. New choice, not new overwhelm.
- After 10+ runs on a core: Exclusive traits unlock — rewarding investment, not demanding it.

**What makes it feel like GW2/LoL/Smite:**
- Every core plays differently — a Mechanist and a Stormcaller share zero abilities
- Cross-class combos exist and feel satisfying, but aren't required
- Movement ability is always on a short cooldown — you're never just standing and trading
- The 6 talent rows give you enough choice to feel crafted, not enough to feel analysed to death

This is deep without being technically difficult. You do not need a spreadsheet to play well. You need to know your kit, read the encounter, and swap one row before entering.

---

## The Combat Loop

```
Move (WASD) → Read enemy → Position → Dodge (Space) → Cast ability → Hit + feedback → CD starts → Move
```

Nothing happens while standing still. Dodge roll is the primary defensive action (2 charges, 5s each, 0.35s i-frames). Abilities chain between dodge windows. Smart play weaves auto-attacks between ability casts.

**Auto-attack baseline:** Always available, no cooldown, 8 dmg × Power factor. A player using no abilities still contributes. This is the damage floor — no build ever feels dead.

**No global cooldown.** Each ability slot cools independently. Rewards players who learn their cooldown timers.

**Condition loop (parallel):** DoT builds (Wraith, Pyromancer) run a second loop: apply stacks → tick damage → stack more → detonate at threshold. Rewards patience and timing over button mashing.

---

## The Nine Cores

A core is your identity. It defines your passive trait, two locked ability slots, movement ability, and ultimate. You pick a core at character creation and only change it at the character screen — not mid-run.

### WRAITH — Stealth / Assassination
*Goes in, disrupts, gets out. Wins by knowing exactly when to detonate.*

**Passive:** Bounty System — bonus XP and credit on stealth kills.

| Slot | Ability | CD | Notes |
|------|---------|----|-------|
| 1 (locked) | Phase Cloak | 10s | Enter stealth. +50% dmg on next ability from stealth. |
| 2 (locked) | Null Field | 12s | Zone: suppress + 4/s DoT for 5s. Loads debuff stacks. |
| Move | Shadow Step | 8s | Short blink in movement direction. |
| Ultimate | Collapse | 40s | Detonate all debuffs on all enemies in range — 20 dmg/stack. |

**Play pattern:** Cloak → Null Field free-cast → stack conditions → Collapse. Any CC from teammates multiplies the detonation window.

---

### SENTINEL — Tank / Crowd Control
*Takes hits, punishes enemies who focus them, controls single targets.*

**Passive:** Threat Protocol — stacking DR bonus on repeated hits. Triage Loop — low-HP self-heals passively.

| Slot | Ability | CD | Notes |
|------|---------|----|-------|
| 1 (locked) | Breach Slam | 6s | Dash forward, stagger all enemies hit for 0.8s. |
| 2 (locked) | Iron Tether | 9s | Lock one enemy in place for 5s — leashed to you. |
| Move | Shield Charge | 10s | Rush forward, absorbing 30 damage en route. |
| Ultimate | Last Bastion | 50s | Deploy hardlight wall blocking all projectiles for 10s. |

**Play pattern:** Breach Slam staggers → immediately Kinetic Reversal absorbs their counterattack → Iron Tether the priority target → release burst. Siege Mode when overwhelmed.

---

### MECHANIST — Deployables / Zone Control
*Builds the battlefield before the fight starts. Strongest when the team positions around structures.*

**Passive:** Overengineered — deployables gain bonus effect when planted in overlapping zones.

| Slot | Ability | CD | Notes |
|------|---------|----|-------|
| 1 (locked) | Sentinel Drop | 6s | Deploy auto-turret (max 3 out). Sustained DPS anchor. |
| 2 (locked) | Shock Mine | 5s | Proximity explosive — 40 burst, 2.5u blast radius. |
| Move | Boost Jets | 7s | Short hover burst — clears terrain and gaps. |
| Ultimate | System Overload | 45s | All deployables overdrive simultaneously for 10s. |

**Play pattern:** Proactive, not reactive. Drop Sentinel and mines before enemies arrive. Direct casts finish stragglers. If you're casting reactively, you're playing it wrong.

---

### LIFEBINDER — Healing / Support
*Reactive, high-stakes. Not a passive heal-bot. At their best in dire situations.*

**Passive:** Triage Loop — allies below 25% HP receive passive heal ticks in range.

| Slot | Ability | CD | Notes |
|------|---------|----|-------|
| 1 (locked) | Restoration Beacon | 6s | Deploy: heals 12 HP every 3s to allies in 8u for 30s. |
| 2 (locked) | Transfer Protocol | 9s | Redirect 100% of ally's incoming damage to yourself for 5s. |
| Move | Lifeline Rush | 8s | Dash to target ally, healing both on arrival. |
| Ultimate | System Rollback | 60s | Rollback entire team — position + HP 5 seconds back. |

**Play pattern:** Place Beacon at the tank's position. Shield the tank. Eat their burst damage via Transfer Protocol while Beacon heals you back. Rollback is the "we don't lose" button — save it.

---

### PHASER — Teleportation / Burst
*Controls space. Every fight happens on their terms.*

**Passive:** Slipstream — after any blink or teleport, +20% movement speed for 3s.

| Slot | Ability | CD | Notes |
|------|---------|----|-------|
| 1 (locked) | Phase Shift | 4s | Teleport 10u in aimed direction. Fastest repositioning. |
| 2 (locked) | Singularity | 9s | Pull all enemies toward a point for 3s. |
| Move | Fold | 6s | Instant position swap with a Phase Relay. |
| Ultimate | Event Horizon | 50s | 60 AoE damage + Exposed 8s to all enemies hit. |

**Play pattern:** Phase Shift into flanking position → Singularity bunches enemies → team focuses the cluster. Near Phase Relay, Singularity pull lasts 5s total.

---

### PYROMANCER — Burn / DoT Stacking *(new)*
*Applies burn stacks and detonates them. The condition damage specialist.*

**Passive:** Ignition — enemies at 5+ burn stacks take +20% damage from all sources (team benefits).

| Slot | Ability | CD | Notes |
|------|---------|----|-------|
| 1 (locked) | Flame Strike | 5s | Cone fire — 20 direct damage + 2 burn stacks. |
| 2 (locked) | Molten Ground | 10s | Zone: applies 1 burn/s to enemies standing inside for 6s. |
| Move | Fire Step | 8s | Dash and leave a fire trail for 2s — crosses burn 1 stack. |
| Ultimate | Supernova | 45s | Detonate ALL burn stacks on ALL enemies in range — 15 dmg/stack. Mirror of Wraith's Collapse for fire. |

**Play pattern:** Flame Strike applies stacks fast. Molten Ground zones enemies while stacking passively. At 5+ stacks, Ignition passive is live — the whole team deals more damage. Supernova is the detonation payoff. Devastating against immune-window bosses because stacks load during immunity.

---

### STORMCALLER — Chain Lightning / Mobility *(new)*
*High-mobility mage. Chains damage across targets. Never stops moving.*

**Passive:** Discharge — every 3rd ability cast auto-fires chain lightning (3 targets: 15/10/5 dmg). No button press needed.

| Slot | Ability | CD | Notes |
|------|---------|----|-------|
| 1 (locked) | Arc Chain | 7s | Lightning jumps 5 targets — 30/25/20/15/10 damage. |
| 2 (locked) | Storm Step | 6s | Dash any direction + leave shock trail 2s (crossing enemies stunned 0.5s). |
| Move | Thunder Rush | 5s | Fastest movement ability in game — pure speed dash, no combat effect. |
| Ultimate | Tempest | 50s | Call a storm at target point — chain lightning strike every 0.5s for 8s. |

**Play pattern:** Keep moving to cycle Discharge passive. Storm Step repositions and punishes pursuers simultaneously. Arc Chain into packed enemies on Pyromancer's Molten Ground = massive synergy.

---

### WARDEN — Summons / Nature CC *(new)*
*Commands spirits and roots enemies. Wins through presence rather than burst.*

**Passive:** Bond — each active summon gives +5% damage and +10 max HP.

| Slot | Ability | CD | Notes |
|------|---------|----|-------|
| 1 (locked) | Summon Spirit Wolf | 20s | Melee AI companion attacks nearby enemies for 30s. |
| 2 (locked) | Nature's Grasp | 11s | Roots up to 3 enemies in vines for 2s. |
| Move | Wilder Surge | 9s | Charge forward — all summons teleport to new position. |
| Ultimate | Call of the Wild | 60s | Summon 3 spirits simultaneously for 20s. Full Bond passive = 15% damage + 30 HP. |

**Play pattern:** Keep summons alive to maintain Bond stacks. Nature's Grasp roots clusters for team follow-up. Wilder Surge repositions both you and your spirits. Ultimate creates overwhelming multi-target pressure.

---

### REAPER — Life Drain / Dark CC *(new)*
*Siphons life, spreads fear, controls with death magic. Slow to ramp, devastating at full charge.*

**Passive:** Soul Harvest — each enemy death within 12u heals you for 15 and adds 1 charge to ultimate.

| Slot | Ability | CD | Notes |
|------|---------|----|-------|
| 1 (locked) | Soul Rend | 6s | Single target — 25 damage, heal self for 12. |
| 2 (locked) | Terror Wave | 10s | Cone fear — enemies flee for 2s (can't act or attack). |
| Move | Death Fade | 10s | Brief phased sprint — untargetable for 1.5s while moving. |
| Ultimate | Reaping | 55s | Channel 4s: 8 dmg/s to all nearby enemies, heal for each tick. Charges via Soul Harvest. |

**Play pattern:** Soul Rend sustains your HP without a healer. Terror Wave creates space or sets up fleeing enemies for teammate follow-up. Death Fade is the escape — use it when cornered, not proactively. Reaping charges faster in packs — better in wave content than single bosses.

---

## The Talent System (MoP-Style)

Six rows of three choices each. Pick one per row. All three options in any row are equal in total value — only situationally different. Swap freely in the hub between encounters.

**The design rule:** A "wrong" pick should be at most 10–15% weaker than the "right" pick in any given encounter. Not game-breaking — just suboptimal.

### Flex Row 1 — Primary Damage Style
*Choose based on enemy composition.*

| Option | Name | Description | Best vs. |
|--------|------|-------------|---------|
| Burst | Overcharged Shot | Hold to charge — 50–80 direct damage, interrupts channels. | Single boss |
| Zone | Proximity Burst | 20–55 AoE based on distance — full at 3u, falls off at 8u. | Packed groups |
| Chain | Arc Chain | Lightning jumps 5 targets — 30/25/20/15/10. | 3+ enemy packs |

### Flex Row 2 — Engagement Tool
*Choose based on your role in the team.*

| Option | Name | Description | Best vs. |
|--------|------|-------------|---------|
| Mobility | Surge | Dash 6u, knock back enemies hit 3u. Initiate or escape. | Open arenas |
| Defense | Barrier | Apply 50 absorb shield to yourself, grows +5 per hit absorbed. | High burst encounters |
| Control | Flash Freeze | Slow all enemies in 4u by 60% for 3s. Chilled targets take +10% next hit. | Mobile enemy packs |

### Flex Row 3 — Situational Weapon
*The encounter-tuning slot. Swap this most often.*

| Option | Name | Description | Best vs. |
|--------|------|-------------|---------|
| Silence | Silence | Prevent one target from using abilities 2.5s. | Caster elites, bosses |
| Counterstrike | Counterstrike | Absorb next hit, return as 150% AoE burst. | Melee engagers |
| Rally Cry | Rally Cry | All allies in 14u gain +10% damage for 6s. | Team fights, multi-phase bosses |

### Trait Row A — Combat Modifier (passive)
*Unlocks at character level 2.*

| Option | Name | Description |
|--------|------|-------------|
| Offensive | Executioner | +20% damage to targets below 25% HP. |
| Defensive | Iron Skin | +10 armor when hit above 50% HP. Stacks up to 3×. |
| Utility | Swiftfoot | Dodge charges recharge 1s faster each. |

### Trait Row B — Scaling Modifier (passive)
*Unlocks at character level 4.*

| Option | Name | Description |
|--------|------|-------------|
| Offensive | Ravager | Crits apply 1 bleed stack — 0.5/s for 5s. Crits refresh. |
| Defensive | Last Stand | Below 20% HP take 30% less damage. |
| Utility | Tactician | Each kill reduces all cooldowns by 2s. |

### Trait Row C — Team or Self (passive)
*Unlocks at character level 6.*

| Option | Name | Description |
|--------|------|-------------|
| Offensive | Exploit Weakness | +15% damage to Exposed, Silenced, or Rooted targets. |
| Defensive | Second Wind | Once per fight: survive a lethal hit at 1 HP. Resets on full HP restore. |
| Utility | Field Medic | Reviving an ally is 35% faster. Revived ally returns with 20% more HP. |

---

## Stat Architecture

Eight stats, allocated by gear. Named gear presets make this approachable without exposing the math.

| Stat | Effect |
|------|--------|
| Power | Scales direct ability damage |
| Precision | Critical hit chance (1000 = 50% crit) |
| Toughness | Reduces incoming direct damage |
| Vitality | +10 max HP per point |
| Healing Power | Scales outgoing heals |
| Condition Damage | Scales DoT tick damage |
| Expertise | Extends condition duration |
| Ferocity | Increases critical hit multiplier |

**Named presets (players pick a preset, not individual stats):**

| Preset | Stats | Playstyle |
|--------|-------|-----------|
| Berserker | Power + Precision + Ferocity | Maximum burst damage, zero survivability |
| Soldier | Power + Toughness + Vitality | Sustained damage, self-sufficient |
| Mender | Healing Power + Vitality + Toughness | Support/heal focus |
| Viper | Power + Condition Damage + Expertise | DoT specialist, pairs with Pyromancer/Wraith |
| Paladin | Power + Toughness + Healing Power | Damage-capable tank |
| Celestial | All stats (reduced values) | Jack-of-all-trades, flexible |

### Combat Formulas

```
DirectDamage     = (AbilityBase × Power) / (1000 + Toughness × 0.5)
ConditionDamage  = BaseStackRate × (1 + ConditionDamage × 0.001)
CritDamage       = DirectDamage × (1.5 + Ferocity × 0.0015)
HealAmount       = BaseHeal + (HealingPower × coefficient)
MaxHP            = 200 + (Vitality × 10)

Base stats at level 1: Power=1000, Toughness=1000, Vitality=10 → 300 HP
```

**Why these formulas prevent "broken" builds:**
- Full glass cannon (2000 Power vs. 1000 Power) gives ~1.33× damage, not 2×. The Toughness divisor is always active.
- Condition stacks are capped — Pyromancer's burn caps at 10 stacks. Adding more is wasted output.
- Critical hit chance hard caps at 100% (1000 Precision + traits).

---

## Build Archetypes + Guard Rails

### Archetype Labels
When a player's picks match a pattern, the hub build card names it. This sets expectations before entering an encounter.

| Label | Pattern | Strength | Vulnerability |
|-------|---------|----------|---------------|
| Glass Cannon | Burst row + Executioner + Berserker gear | Single-target burst, finishes fast | Dies to sustained damage, no escape |
| Bruiser | Barrier + Last Stand + Soldier gear | Self-sufficient, hard to kill | Slower clear, team-dependent for burst |
| Controller | Silence + Flash Freeze + CC traits | Locks down elites and bosses | Lower personal damage output |
| Support Carry | Rally Cry + Field Medic + Mender gear | Team multiplier, revival speed | Can't solo effectively |
| Condition Specialist | Chain + Viper gear + Exploit Weakness | Scales infinitely in long fights | Weak in short immunity-window fights |
| All-Rounder | Celestial gear + balanced rows | Never bad, never great | Outclassed in specialized encounters |

### Death Recap
Shown every time a player is downed. One screen, three facts, one suggestion.

```
You were downed

You took 78 burst damage in 1.8s from Iron Elite.
Your build had no absorb in slot 4.
You had 1 dodge charge available — not used.

→ Barrier (Row 2, center) would have absorbed 50 of that.
  Consider swapping in hub.
```

The recap points to a specific pick, not general advice. It teaches without shaming.

### Hub Build Preview Card
Shown when approaching a portal in the hub.

```
[WRAITH — Glass Cannon]
Phase Cloak · Null Field · Overcharged Shot · Silence
Executioner · Ravager · Exploit Weakness
Gear: Berserker

Strong vs:    single targets, high-burst windows
Weaker vs:    sustained packs, no escape tool

This zone: elite mobs + caster boss
Favours: single-target burst, silence
```

---

## HUD Design

**Health bar:** Green. Shield absorb in blue layered on top. HP number displayed.

**Dodge pips:** Two small squares below health bar. Filled = ready, empty = recharging (shows timer).

**Ability slots:** Six slots (1, 2, 3, 4, movement, ultimate). Greyed + countdown when on cooldown. Glow rim when ready. Ultimate slot uses a distinct colour per core.

**Condition stacks on target:** Small icons + count below enemy nameplate. Burn (flame), Slow (snowflake), Suppress (mute), Stagger (spiral), Exposed (broken shield), Tethered (chain).

**Damage floats:**
- White = direct damage
- Yellow = critical hit
- Orange = condition damage
- Green = heal
- Blue = shield absorbed
- Size scales with magnitude. Floats below 5 dmg suppressed (no clutter from auto-attacks).

---

## Enemy Type Library

| Type | Description | Tuning knobs |
|------|-------------|-------------|
| Grunt | Low HP, melee rush. Pack threat, not individual. | Count, speed |
| Shielded | Front absorb blocks projectiles — must flank or use AoE. | Shield HP, arc angle |
| Caster Elite | Channelled ranged attack. Interrupt with Silence or stagger. | Channel time, damage |
| Berserker | High damage, charges on a 3s window. Tests dodge timing. | Charge CD, speed |
| Tether Anchor | Roots nearest player unless destroyed. Punishes no-burst builds. | HP, root radius |
| Void Crawler | Applies Suppress on hit — targets can't use abilities for 1s. | Attack rate |
| Healer Drone | Regenerates nearby enemies until destroyed. Focus priority target. | Heal rate, HP |
| World Boss | Shared HP pool. Phase abilities. Immunity windows reward DoT. | Phase HP, immunity duration |

### Encounter Tuning Knobs
Adjust encounters without touching ability numbers:

- **Enemy HP pools** — high HP rewards sustained DoT, punishes pure burst
- **Immunity windows** — direct damage blocked; condition stacks still load during immunity
- **Spawn rate