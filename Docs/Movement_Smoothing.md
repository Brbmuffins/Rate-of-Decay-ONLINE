# Movement Smoothing — What Changed

Player movement felt choppy because `PlayerMovement` was writing directly to
`transform.position`/`transform.rotation` in `Update()` while a non-kinematic
`Rigidbody` was also being driven by physics — the two fought every frame,
causing stutter, and rotation snapped instantly instead of turning smoothly.

## Code changes (`PlayerMovement.cs`)
- Input is still read in `Update()`, but movement/rotation are now applied in
  `FixedUpdate()` via `rb.MovePosition()` / `rb.MoveRotation()`, which is the
  correct way to move a Rigidbody.
- Rotation now turns smoothly with `Quaternion.Slerp` at `rotationSpeed`
  (default `12`) instead of snapping instantly to face the move direction.
- New public field: `rotationSpeed` — higher = snappier turning, lower =
  more "weighty" turning. Tune in the Inspector if 12 feels off.

## Scene change (already applied, just Ctrl+S to keep it)
- The player's `Rigidbody` had **Interpolate = None**. Changed to
  **Interpolate** in `SampleScene.unity` — this smooths the visual position
  between physics steps and is required for `MovePosition`-based movement to
  look smooth at high frame rates.

## Manual steps
None required — already wired in the scene file. Just confirm in the
Inspector (select the player, `Rigidbody` component) that **Interpolate**
shows as **Interpolate**, and **Ctrl+S**.

## Verify
- Walking with WASD should feel smooth, no more per-frame jitter.
- Turning to face a new direction should rotate over a few frames instead of
  snapping instantly.
- Jumping still works (jump request is queued in `Update`, applied in
  `FixedUpdate`).
