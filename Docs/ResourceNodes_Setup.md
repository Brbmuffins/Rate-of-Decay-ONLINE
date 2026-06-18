# Resource Nodes — Manual Unity Setup

New: `Assets/Combat/Scripts/ResourceNode.cs` — a harvestable node. Walk up,
press **F**, get an item. After `hitsToDeplete` harvests it disappears and
respawns after `respawnTime` seconds (same hide/respawn pattern as
`TurretPickup`/`ItemPickup`).

Also new: `Assets/Items/Animus Core.asset` — a second crafting resource
(no icon yet, see "Icons" below). And `Scrap Metal.asset` was fixed: it had
`equippable: 1` / `equipSlot: Weapon` left over from a copy-paste, which
would've let players "equip" scrap metal into the weapon slot. That's now
cleared, and its `maxStackSize` was bumped from `99` to `999` ("lots of
stacks" per your request — same bump applied to Animus Core).

As always: **Ctrl+S after every step**.

---

## 1. Wire up the existing Scrap Nodes
The scene already has multiple `ScrapNodeT1` instances (search "T1 Scrap Node
Objects" in the Hierarchy).

For each one:
1. Select it, **Add Component → Resource Node**.
2. Drag **Assets/Items/Scrap Metal.asset** → `Yield Item`.
3. Defaults are fine: `Hits To Deplete = 3`, `Respawn Time = 60`,
   `Interact Range = 3`.
4. Make sure it has a **Collider** (any non-trigger collider works — used
   only to know what to hide/show, not for physics).

## 2. Add an Animus Core node (placeholder visual)
No 3D model for this one yet, so use a primitive as a stand-in:
1. Hierarchy → right-click → **3D Object → Sphere** (or Cube). Rename it
   `AnimusCoreNode`.
2. Scale it down (e.g. `0.5, 0.5, 0.5`) and place it somewhere in the world.
3. **Add Component → Resource Node**.
4. Drag **Assets/Items/Animus Core.asset** → `Yield Item`.
5. Optional: give it a distinct material color (e.g. purple/teal) so it
   reads as "special" until you have real art.

## 3. Icons
- **Scrap Metal** already has an icon — no change needed.
- **Animus Core** has `icon: None`. The inventory/tooltip UI handles a
  missing icon fine (just shows an empty slot image), so this isn't
  blocking, but for a real icon:
  - Quickest: make a flat-color square texture in any image editor (even
    MS Paint), import it, set **Texture Type = Sprite (2D and UI)**.
  - AI options: Bing Image Creator, Adobe Firefly, or ChatGPT/DALL-E can
    generate a "glowing crystal core item icon, transparent background,
    game UI style" image — download as PNG, drop into
    `Assets/Items/Icons/`, set Texture Type to Sprite, then drag onto
    `Animus Core.asset → Icon`.
  - Free packs: the Unity Asset Store has free icon packs (search
    "RPG item icons") that often include crystal/core-style icons.

---

## How it works
- `ResourceNode.Update()` checks distance from the node to the player's
  `Inventory` transform; within `interactRange`, pressing **F** calls
  `Inventory.AddItem(yieldItem)`.
- If `AddItem` fails (inventory full), the hit doesn't count — nothing is
  consumed from the node.
- After `hitsToDeplete` successful harvests, the node hides (collider +
  renderers disabled) and respawns after `respawnTime`.

## Verify
- Walk up to a Scrap Node, press F three times — Scrap Metal count goes up
  by 3 in the inventory UI, node then disappears.
- Wait ~60s — node reappears, harvestable again.
- Repeat for the Animus Core placeholder — confirm a new "Animus Core" stack
  shows up in inventory with the Rare (blue) tooltip color.
- Try harvesting with a full inventory (10/10 slots, no existing stack of
  that item) — nothing should be consumed, no errors in console.
