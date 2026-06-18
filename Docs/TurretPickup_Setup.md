# Turret Pickup — Manual Unity Setup

Lets the player pick a deployed turret back up into inventory, and gates
"Deploy Turret" on actually having a `Turret` item.

Covers: `AbilityCaster.cs` (new `inventory` field on the component, new
`turretItem` field per-ability), new `Assets/Combat/Scripts/TurretPickup.cs`,
new `Assets/Items/Turret.asset`, and `Inventory.cs` (`HasItem`).

As always: **Ctrl+S after every step**.

---

## 1. Give the Turret item an icon
1. Select **Assets/Items/Turret.asset**.
2. Drag a sprite into `Icon` (placeholder is fine).

## 2. Wire `AbilityCaster`
1. Select the Engineer/player GameObject (has `AbilityCaster`).
2. Drag the player's **Inventory** component → `Inventory` field (top-level,
   not per-ability).
3. Expand `Abilities[0]` ("Deploy Turret") → drag **Turret.asset** →
   `Turret Item`.
4. Ctrl+S.

> Leaving `Turret Item` empty on an ability keeps old behavior (no inventory
> check, no item consumed) — only ability 0 needs this.

## 3. Give the player a starting Turret (for testing)
The player's `Inventory.items` list is a normal serialized list.
1. Select the player, find the `Inventory` component.
2. Expand `Items`, add an entry: `Item` → **Turret.asset**, `Count` → 1 (or
   more, up to `maxStackSize = 3`).
3. Ctrl+S.

Without at least one Turret item, pressing `1` will do nothing (cast is
blocked — see `hasTurretAvailable` check in `AbilityCaster.Update()`).

## 4. Add `TurretPickup` to the turret prefab
1. Open the turret prefab assigned to `Abilities[0].Turret Prefab`.
2. Add Component → `TurretPickup`.
3. Leave `Item` and `Inventory` empty — `AbilityCaster.FinalizeCast()` fills
   these in at runtime when the turret is deployed.
4. Set `Interact Range` if you want something other than the default `3`.
5. Ctrl+S the prefab.

---

## How it works
- Casting "Deploy Turret" (key `1`) now requires `Inventory.HasItem(Turret)`.
  On successful cast, `Inventory.RemoveItem(Turret)` is called and the
  spawned turret's `TurretPickup.item`/`inventory` are set.
- Walk up to the deployed turret (within `interactRange`, default 3 units)
  and press **F** — it's destroyed and `Turret` is added back to inventory
  via `Inventory.AddItem()`.
- Cooldown still applies independently — picking the turret back up doesn't
  reset the cooldown timer.

## Verify
- With 1 Turret in inventory, cast Deploy Turret — item count should drop to
  0 (check inventory UI), turret spawns.
- Press `1` again — nothing happens (no indicator), since `hasTurretAvailable`
  is false.
- Walk to the turret, press `F` — turret disappears, Turret item count back
  to 1 in inventory.
- Cast again — works as before.
