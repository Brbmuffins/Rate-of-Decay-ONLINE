# Crossworlds BCE — Context Index

Route AI agents here first. Load ONE file per troubleshooting session.
Each file is self-contained: key files, known pitfalls, current state, active TODOs.

---

## Quick Facts

| Key | Value |
|---|---|
| Engine | Unity 6000.0.77f1, URP, IL2CPP |
| Networking | Mirror + KCP transport, UDP 7777 |
| Server IP | 15.204.243.36 |
| Website | https://playcrossworlds.com (A record live, SSL active) |
| Download page | https://playcrossworlds.com (download + how-to-play) |
| Client download | https://playcrossworlds.com/downloads/CrossworldsBCE.zip |
| Auth server | Port 3000 — `/opt/rod-auth/server.js` |
| Dashboard | Port 4000 — `/opt/rod-dashboard/server.js` |
| Database | MySQL 8 — DB `rod_online`, user `rodgame` |
| Scene order | LoginScene(0) → CharacterSelect(1) → Hub(2) |
| Classes | Engineer(0), Guardian(1), Shadowblade(2), Cleric(3), Arcanist(4) |
| Phase 1 status | ~65% — server complete, Unity Weeks 4–7 remaining |

---

## ⚠️ Legacy Naming — DO NOT RENAME

VPS paths and DB names still use `rod` from the original project name **"Rate of Decay Online"**.
The game is now **Crossworlds BCE** but renaming these would break live services.

**Leave these names exactly as they are:**
- DB: `rod_online`, user `rodgame`
- Dirs: `/opt/rod-auth/`, `/opt/rod-dashboard/`, `/var/www/rod/`
- Nginx config: `/etc/nginx/sites-available/rod`
- systemd services are already renamed: `crossworlds-auth`, `crossworlds-dashboard`, `crossworlds`

---

## Context Files — Load When Troubleshooting

| File | Use when working on... |
|---|---|
| [`_context/NETWORKING.md`](_context/NETWORKING.md) | Mirror spawn failures, assetId errors, prefab registration, `Could not spawn`, player not appearing |
| [`_context/AUTH_LOGIN.md`](_context/AUTH_LOGIN.md) | Login flow, JWT, CharacterSelect, character data, 401/403 errors, auth server calls |
| [`_context/UI_INPUT.md`](_context/UI_INPUT.md) | Chat typing moves player, ESC menu, camera orbit, cursor lock, EventSystem, WASD during chat |
| [`_context/SCENE_SETUP.md`](_context/SCENE_SETUP.md) | Hub scene rebuild, class prefabs, NetworkManager settings, build settings, portal |
| [`_context/VPS_SERVER.md`](_context/VPS_SERVER.md) | Deploying builds, systemd services, reading logs, Nginx, SSL, DB access, dashboard |
| [`_context/COMBAT.md`](_context/COMBAT.md) | Class abilities, enemy design, drop tables, NavMesh, arena, damage systems |

---

## Source of Truth Docs

| Doc | Purpose |
|---|---|
| [`CROSSWORLDS.md`](CROSSWORLDS.md) | Master reference — full schema, all API endpoints, services, conventions, integration map |
| [`CLAUDE.md`](CLAUDE.md) | Claude Code VPS agent — behavior rules, process, code conventions |
| [`CLAUDE_CONTEXT.md`](CLAUDE_CONTEXT.md) | Paste-in primer for Claude Chat sessions |
| [`_design/ROADMAP.md`](_design/ROADMAP.md) | Phase 1 status week-by-week, open bugs, Phase 2 stubs |

---

## Active Bugs (don't lose track)

| Bug | File / Area | Priority |
|---|---|---|
| `orientation:F3` — float sent as formatted string | Unity → `PATCH /character/position` | Medium |
| `GmConsole.cs` — no `#if !UNITY_SERVER` guard, crashes server every frame | Unity | High |
| `CmdSendChat` — missing `[CHAT]` log line on server | Unity (Mirror command) | Low |

---

## VPS / Ops TODOs

| Task | Notes |
|---|---|
| Uptime Kuma web UI setup | Running on port 3001 — UI not configured yet |
| `StartLimitIntervalSec=0` on `crossworlds.service` | Would allow infinite restarts instead of stopping after 5 rapid crashes |
| Discord link on download page | Shows "Coming Soon" on `/var/www/rod/index.html` |
