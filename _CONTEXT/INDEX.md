# Crossworlds BCE — Context Index

Load ONE file from this directory when troubleshooting a specific area.
Each file is self-contained: key files, known pitfalls, current state, active TODOs.

| File | Use when working on... |
|------|----------------------|
| [NETWORKING.md](NETWORKING.md) | Mirror spawn failures, assetId errors, prefab registration, `Could not spawn`, player not appearing |
| [UI_INPUT.md](UI_INPUT.md) | Chat typing, ESC menu, camera orbit/re-centering, cursor lock, EventSystem, WASD during chat |
| [AUTH_LOGIN.md](AUTH_LOGIN.md) | Login flow, JWT, CharacterSelect, character data, auth server calls |
| [VPS_SERVER.md](VPS_SERVER.md) | Deploying builds, systemd services, server logs, auth server, dashboard |
| [SCENE_SETUP.md](SCENE_SETUP.md) | Hub scene rebuild, Setup menu tools, class prefabs, build settings |

## Quick Facts

- **Engine:** Unity 6000.0.77f1, URP
- **Networking:** Mirror, KCP transport, UDP port 7777
- **Server IP:** 15.204.243.36
- **Auth server:** port 3000 (Node.js/Express, MySQL)
- **Dashboard:** port 4000
- **Binary:** `/game/Builds/CrossworldsBCE.x86_64`
- **Scene order:** LoginScene(0) → CharacterSelect(1) → Hub(2)
- **Classes:** Warden(0), Ironclad(1), Shadowblade(2), Cleric(3), Arcanist(4)

## Root Docs

| Doc | Purpose |
|-----|---------|
| `README.md` | Systems overview, class table, editor tools, known TODOs |
| `DEVDOC.md` | Full player guide, controls, GM commands, VPS reference |
| `COMBAT.md` | All 5 classes, ability tables, combos, design intent |
| `DESIGN_DOCUMENT.md` | Game design pillars, zone design, progression |
| `SERVER_REFERENCE.md` | **PRIVATE** — MySQL credentials, JWT secret, do not share |
