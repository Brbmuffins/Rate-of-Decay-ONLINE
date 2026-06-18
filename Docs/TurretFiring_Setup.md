# Turret Firing — Visuals & Manual Setup

`TurretController.cs` already found targets and dealt damage, but it was
invisible — nothing showed the turret actually firing. It now adds:

- **Recoil kick** — the barrel snaps back along its local forward axis on
  each shot and eases back to rest (`recoilDistance` / `recoilRecoverySpeed`).
- **Muzzle flash** — a small particle burst at the barrel tip. If you don't
  assign one, a basic yellow/orange flash is created automatically at
  runtime, so this works with zero setup.
- **Tracer line** — a short-lived cyan `LineRenderer` from the muzzle to the
  target on every shot, so you can actually see the shot land.

As always: **Ctrl+S after every step**.

---

## 1. Placeholder turret (no setup required)
The capsule placeholder spawned by `AbilityCaster` (when `turretPrefab` is
unset on the "Deploy Turret" ability) now automatically gets a
`TurretController`. Just enter Play mode, deploy the turret near something
tagged `Enemy` with a `Health` component, and it should rotate to face,
fire on its `fireRate` timer, show a flash + tracer, and deal damage.

## 2. If you build a real Turret prefab/model
Using the `Engineer Turret 1` or `Walking Turret` model:
1. Add `TurretController` to the root.
2. Optional: drag the barrel/cylinder child (the part that should rotate
   and recoil) onto `Barrel`. If left empty, the whole turret root recoils
   and faces the target — fine for now since `transform.LookAt` already
   rotates the root.
3. Optional: create an empty child at the barrel tip, name it
   `MuzzlePoint`, and drag it onto `Muzzle Point` — this is where the
   tracer/flash originate. If left empty, it uses the barrel's position.
4. Optional: assign your own `Particle System` to `Muzzle Flash` if you
   want a custom effect instead of the auto-generated one.
5. Assign this object as `Deploy Turret → Turret Prefab` on the
   `AbilityCaster` component (Hierarchy → player → AbilityCaster) if you
   want the real model used instead of the capsule placeholder.

## 3. Testing without real enemies
`TurretController.targetTag` defaults to `Enemy`. To test:
- Add an object to the scene, tag it `Enemy`, and add a `Health` component.
- Deploy a turret within `range` (default 8) — it should track, fire,
  flash, tracer, and reduce the enemy's health every `1/fireRate` seconds.

## Verify
- Deploy turret near a tagged `Enemy` with `Health` — turret rotates to
  face it.
- On each shot: barrel kicks back and recovers, a brief yellow flash
  appears at the muzzle, and a cyan tracer line flashes toward the target.
- Enemy's `Health` decreases by `damage` each shot.
- No target in range — turret idles, no firing/flash/tracer.
