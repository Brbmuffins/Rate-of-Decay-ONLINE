# Networked Combat Change Notes

Date: 2026-06-29

## Goal

Begin moving combat from client-local behavior toward a server-authoritative Mirror flow.

## Files Changed

### Assets/Game/Combat/Scripts/Health.cs

- Converted `Health` from `MonoBehaviour` to `NetworkBehaviour`.
- Added Mirror `SyncVar` fields for:
  - `maxHealth`
  - `currentHealth`
  - player downed state
- Added SyncVar hooks so health bars and downed-state listeners receive updates when server state syncs to clients.
- Added server initialization in `OnStartServer()` so network-spawned prefabs with `currentHealth = 0` initialize to `maxHealth`.
- Added client initialization in `OnStartClient()` so UI listeners receive the initial synced HP/downed values.
- Added a guard that rejects client-only attempts to mutate combat state while connected to a Mirror session.
- Preserved the existing public API: `TakeDamage`, `Heal`, `ApplyShield`, `Revive`, damage redirect, absorption, damage reduction, and gear stat methods still use the same names/signatures.

## Why This Matters

Before this change, HP was a local Unity field. A client-side ability could reduce an enemy's local HP without that being a server-owned, synchronized truth. With this change, health is ready to be owned by the server and replicated to clients.

### Assets/Game/UI/AbilityCaster.cs

- Converted `AbilityCaster` from `MonoBehaviour` to `NetworkBehaviour`.
- Kept local input processing restricted to the local player.
- Allowed the server-side component to remain active so it can validate and execute combat commands.
- Added a server command for ability equip changes:
  - `CmdEquipSpell(int spellbookIndex, int slot)`
- Added a server command for ability casts:
  - `CmdFinalizeCast(int spellbookIndex, Vector3 castPosition, Quaternion castRotation, Vector3 castScale, float aimTime)`
- Added server validation for casts:
  - spellbook index must be valid
  - ability must be equipped on the server
  - server cooldown must be ready
- Added server-side cast proxy objects so existing circle/cone/rectangle hit logic can run on the server with the same geometry the client aimed.
- Added `RpcCastConfirmed(...)` so other clients can play basic cast feedback after the server accepts a cast.
- Kept client-side cast feedback for the local player so input still feels responsive.

## Why This Matters

Before this change, player abilities directly applied `Health.TakeDamage(...)` from the client. Now connected clients route casts to the server first. The server validates the equipped ability and cooldown, then applies the existing damage/heal/shield logic.

### Assets/Game/Editor/RodPrefabBuilder.cs

- Updated future class prefab generation so generated class prefabs include:
  - `Health`
  - `StatusEffectManager`
  - `CharacterStats`
  - `AbilityCaster`
- Added a Unity editor menu item:
  - `BCE/Setup/4f ▶ Add Combat Stack To Class Prefabs`
- The new menu item updates existing class prefabs through Unity's prefab editing API instead of hand-editing prefab YAML.

## Why This Matters

Networked combat cannot work unless player prefabs have the same networked combat components on both server and clients. This setup step gives the project a repeatable way to add the required stack to current and future class prefabs.

## Current Scope

This is a foundation pass. It primarily supports the existing generic ability flow:

- circle damage
- cone damage
- rectangle damage
- basic shields
- basic heals/damage routed through existing handlers where they run on the server
- cooldown validation on the server
- equipment/loadout sync from client to server

## Known Remaining Work

- Convert bespoke deployables and class-specific handlers to server-spawned/networked objects where needed.
- Add stronger server validation for range, aim direction, line of sight, and cast position.
- Sync ability loadouts back to observing clients if remote UI/inspection needs it.
- Replace placeholder cast confirmation with proper hit confirmation, damage numbers, and networked VFX.
- Move dodge/i-frame state to the server so server-side enemy attacks respect player evasions.
- Run `BCE/Setup/4f ▶ Add Combat Stack To Class Prefabs` in Unity so existing class prefab assets receive the new combat components.
- Add server-side tests/play-mode checks inside Unity once the editor regenerates project files.

## Verification

- Static inspection confirmed new Mirror hooks/commands are present.
- `dotnet build .\Assembly-CSharp.csproj --no-restore` could not complete because Unity's generated `Temp/obj/Assembly-CSharp/project.assets.json` file is missing. Open Unity or run a restore/regeneration step before using this build command.
