# Combat System — Manual Unity Setup

This doc covers the manual scene/editor steps needed after pulling 
`Assets/Combat/Scripts/` code: `Health.cs`, `HealthBarUI.cs`, `TurretController.cs`.


## 1. Give Bob a Health Bar

### 1a. Add Health to Bob
1. Select **Bob** in the Hierarchy.
2. Add Component → `Health`.
3. Set `Max Health` (e.g. 100).
4. **Tag Bob as `Enemy`** (top of Inspector → Tag dropdown). This tag already
   exists in TagManager — required for the turret to find him.
5. Ctrl+S.

### 1b. Build the health bar visual
Bob already has a `nameplateAndHealthbar` GameObject wired into `EnemyUI`
(`EnemyUi.cs`) that gets shown/hidden on reveal. Inside that GameObject:

1. Add a UI `Image` (e.g. name it `HealthBarBackground`) — dark/gray, fixed
   size, e.g. 100x12.
2. Add a child UI `Image` named `HealthBarFill`:
   - `Image Type = Filled`
   - `Fill Method = Horizontal`
   - `Fill Origin = Left`
   - Color: green/red, your choice
   - **Source Image must NOT be `None`** (use a plain white sprite or built-in
     `UISprite` — same issue we hit with the ability cooldown radial).
3. Ctrl+S.

### 1c. Wire HealthBarUI
1. Select the **nameplateAndHealthbar** GameObject (or Bob itself — either
   works as long as `fillImage` is reachable).
2. Add Component → `HealthBarUI`.
3. Drag **Bob** (the GameObject with the `Health` component) → `Health` field.
4. Drag **HealthBarFill** → `Fill Image` field.
5. Ctrl+S.

### Verify
- Enter Play mode, reveal Bob's nameplate (however `EnemyUI.ShowUI()` gets
  triggered — likely `RevealAura`/proximity).
- `HealthBarFill.fillAmount` should start at `1`.
- Once the turret starts hitting Bob (step 2), the fill should visibly shrink.

---

## 2. Make the Turret Deal Damage

`TurretController.cs` searches for the nearest GameObject tagged `Enemy`
within `range`, rotates to face it, and calls `Health.TakeDamage(damage)` on
a timer (`fireRate` shots/sec).

### 2a. Add TurretController to the turret prefab/instance
The turret is currently spawned from `turretPrefab` on `AbilityCaster`
(Deploy Turret, ability slot 1) — there's no `.prefab` asset yet, just the
FBX (`Assets/Characters/Engineer/Turret/Engineer Turret 1.fbx`).

1. If `turretPrefab` on `AbilityCaster` currently points at the raw FBX:
   - Drag the FBX into the scene once, add `TurretController` to it, set
     `Range`, `Fire Rate`, `Damage` to taste (defaults: 8 / 1 / 10).
   - Drag that configured GameObject back into `Assets/` to create a real
     `.prefab` (e.g. `Assets/Characters/Engineer/Turret/Turret.prefab`).
   - Re-assign `AbilityCaster.abilities[0].turretPrefab` to the new
     `.prefab` instead of the FBX.
   - Delete the temp scene instance.
2. If `turretPrefab` is already a proper prefab, just open it (double-click
   in Project view) and Add Component → `TurretController` there, then
   Ctrl+S the prefab.

### 2b. Confirm Bob is tagged `Enemy`
Already done in step 1a — `TurretController.targetTag` defaults to `"Enemy"`.

### Verify
- Enter Play mode, cast "Deploy Turret" (key `1`) near Bob (within `range`,
  default 8 units).
- Turret should rotate to face Bob and Bob's health bar (from step 1) should
  tick down roughly once per second.
- `Debug.Log` isn't added for firing — if you want a visible confirmation
  beyond the health bar, temporarily add `Debug.Log("Turret hit " +
  currentTarget.name)` inside `Fire()`.

---

## Files 
- `Assets/Combat/Scripts/Health.cs` — generic damageable component
  (`TakeDamage`, `Heal`, `onHealthChanged`, `onDeath` events).
- `Assets/Combat/Scripts/HealthBarUI.cs` — binds a `Health` to a `Filled`
  `Image` fill amount.
- `Assets/Combat/Scripts/TurretController.cs` — finds nearest `Enemy`-tagged
  target in range, rotates, fires on a timer.


