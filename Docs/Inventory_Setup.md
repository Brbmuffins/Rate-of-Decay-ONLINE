# Inventory / Equipment / Items — Manual Unity Setup

Covers code changes to `Assets/Items/Scripts/` (`ItemData.cs`, `Inventory.cs`,
`Equipment.cs`, `InventorySlot.cs`, new `EquipmentSlot.cs`) and two new item
assets (`Health Pack.asset`, `Rusty Pistol.asset`).

As always: **Ctrl+S after every step**.

---

## 1. New items need icons

`Health Pack.asset` and `Rusty Pistol.asset` were created with `icon: {fileID: 0}`
(no sprite assigned — can't set a sprite reference from a text file safely).

1. Select **Health Pack** asset in `Assets/Items/`.
2. Drag a sprite into the `Icon` field (reuse `Scrapper.png` or any
   placeholder for now).
3. Repeat for **Rusty Pistol**.
4. Ctrl+S (these are assets, not scene — saves automatically on assign, but
   double check via Project view there's no "*" on the asset).

---

## 2. Wire `playerHealth` on `InventoryUI`

`InventoryUI.cs` now has a `playerHealth` field (type `Health`), which it
passes down to every `InventorySlot` it creates at runtime. A **Consumable**
item (like Health Pack) clicked in inventory calls
`inventory.UseItem(slotIndex, playerHealth)`, which heals and consumes it.

1. Make sure the player (Engineer) has a `Health` component (see
   `Docs/CombatSystem_Setup.md` — if you only added `Health` to Bob so far,
   add one to the Engineer too with whatever max HP makes sense).
2. Select the GameObject with the `InventoryUI` component.
3. Drag the Engineer's **Health** component → `Player Health` field.
4. Ctrl+S.

---

## 3. Equipment panel — add `EquipmentSlot` to each equip slot

Previously, clicking an equipped item did nothing (no unequip path existed).
`EquipmentSlot.cs` is new and handles click-to-unequip (returns the item to
inventory).

For each slot Image under **Equipment Panel** (Head, Chest, Legs, Weapon):

1. Select the slot GameObject.
2. Add Component → `EquipmentSlot`.
3. Drag the player's **Equipment** component → `Equipment` field.
4. Drag the player's **Inventory** component → `Inventory` field.
5. Set `Slot Type` dropdown to match the slot (Head/Chest/Legs/Weapon).
6. Ctrl+S.

### Verify
- Equip an item (click it in inventory) — it should disappear from inventory
  and appear in the matching equipment slot (via existing `EquipmentUI`
  refresh).
- Click the equipped item in the Equipment Panel — it should return to the
  inventory grid and clear from the equipment slot.
- Equip a second item to the same slot while one is already equipped — the
  old one should be swapped back into the inventory (no item loss).

---

## 4. Try the new items

- **Health Pack** — stackable consumable, heals 25 HP via `Health.Heal()`
  when clicked in inventory (consumes one from the stack).
- **Rusty Pistol** — non-stackable weapon, equips to the `Weapon` slot.

To test without building a pickup, you can temporarily call
`inventory.AddItem(healthPackItemData)` from anywhere, or create an
`ItemPickup` in the scene (existing component) with `item` set to one of
these assets.

---

## 5. Better tooltips & hover feedback (no manual steps needed)

`TooltipUI.cs` now builds a richer tooltip automatically:
- Item name colored by `ItemRarity` (Common = white, Uncommon = green,
  Rare = blue, Epic = purple, Legendary = orange).
- Description on its own line.
- A contextual hint line ("Click to use" / "Click to equip" /
  "Click to unequip").
- Panel auto-sizes to fit the text.

Hovering any inventory or equipment slot also nudges it to 1.08x scale for a
quick highlight, and resets on exit. This is all done in code — nothing to
wire in the Editor.

To make use of rarity, set the `Rarity` dropdown on any `ItemData` asset
(defaults to `Common`/white, so nothing breaks if left unset).

---

## Long-term ideas (not built yet)
- **Looting**: tie `Health.onDeath` (on Bob) to spawning an `ItemPickup` with
  a loot table.
- **Crafting**: a `Recipe` ScriptableObject (list of required `ItemData` +
  counts → output `ItemData`), with a `CraftingStation` or UI panel that
  calls `Inventory.RemoveItem` for each ingredient and `AddItem` for the
  result.
- **Stack splitting / drag-drop reordering**: would need `InventorySlot` to
  implement `IBeginDragHandler`/`IDropHandler` instead of just click.

---

## Files added/changed this session
- `Assets/Items/Scripts/ItemData.cs` — added `ItemType` enum
  (`Generic`/`Consumable`/`Equipment`/`QuestItem`) and `healAmount` field.
- `Assets/Items/Scripts/Inventory.cs` — added `UseItem(index, health)` for
  consumables.
- `Assets/Items/Scripts/Equipment.cs` — `EquipItem` now returns the
  previously-equipped item via `out` param (swap support); added
  `UnequipItem` returning the unequipped item.
- `Assets/Items/Scripts/InventorySlot.cs` — click now handles equip-swap
  (returns old item to inventory) and consumable use; added `playerHealth`
  field.
- `Assets/Items/Scripts/InventoryUI.cs` — added `playerHealth` field, passed
  down to each `InventorySlot` it creates.
- `Assets/Items/Scripts/EquipmentSlot.cs` (new) — click-to-unequip for
  equipment panel slots.
- `Assets/Items/Health Pack.asset` (new) — consumable, heals 25.
- `Assets/Items/Rusty Pistol.asset` (new) — equippable weapon.
