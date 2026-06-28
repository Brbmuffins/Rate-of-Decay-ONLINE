# VPS & Server — Troubleshooting Context

**When to load:** Deploying builds, server not starting, service crashes, log inspection, auth server issues, dashboard, upload process, binary name problems.

---

## Key Info

| Item | Value |
|------|-------|
| Server IP | `15.204.243.36` |
| SSH | root access, port 22 |
| Game binary | `/game/Builds/CrossworldsBCE.x86_64` |
| Game data dir | `/game/Builds/CrossworldsBCE_Data/` |
| Game log | `/var/log/crossworlds.log` |
| Auth server path | `/opt/rod-auth/` |
| Dashboard path | `/opt/rod-dashboard/` |
| Download zip path | `/var/www/rod/downloads/RateOfDecayONLINE.zip` |
| Credentials | `SERVER_REFERENCE.md` (PRIVATE — do not share) |

---

## Services

| Service name | Port | What it is |
|-------------|------|------------|
| `rod-server` | 7777 UDP | Unity game server (Mirror/KCP) |
| `rod-auth` | 3000 | Node.js auth + character API |
| `rod-dashboard` | 4000 | GM/admin web dashboard |
| nginx | 80 | Public download page |
| Uptime Kuma | 3001 | Monitoring |

---

## Essential Commands

```bash
# Check all services
systemctl status rod-server rod-auth rod-dashboard

# Live game server log
tail -f /var/log/crossworlds.log

# Restart game server
systemctl restart rod-server

# Check UDP port 7777 is open
ss -ulnp | grep 7777

# Check binary exists and is executable
ls -la /game/Builds/CrossworldsBCE.x86_64

# Check what's listening
ss -tlnp
```

---

## Deploying a New Build

### Server Build (Linux x86_64 headless)
1. Unity → **File → Build Settings** → Linux, Dedicated Server
2. Output: zip the build folder
3. Upload via FileZilla to `/game/Builds/` on VPS
4. Ensure binary is named `CrossworldsBCE.x86_64` (must match systemd service `ExecStart`)
5. `chmod +x /game/Builds/CrossworldsBCE.x86_64`
6. `systemctl restart rod-server`

### Client Build (Windows)
1. Unity → **File → Build Settings** → Windows x86_64
2. Upload `.exe` + `_Data/` folder to `/var/www/rod/downloads/` as a zip
3. Zip name: `RateOfDecayONLINE.zip`

### FileZilla Settings
- Host: `15.204.243.36`, Port: 22, Protocol: SFTP

---

## Binary Name — Critical

The systemd service `rod-server` must reference the exact binary name. Past incident: build was renamed from `Crossworlds.x86_64` to `CrossworldsBCE.x86_64` but the service file still pointed to the old name → server silently failed to start.

**Check the service file:**
```bash
cat /etc/systemd/system/rod-server.service
# Look for ExecStart= line
```

If you ever rename the binary, update the service file and run:
```bash
systemctl daemon-reload
systemctl restart rod-server
```

---

## VPS Health Check (Claude Code Prompt)

Give this to Claude Code running on the VPS for a full health check:

```
Check the Crossworlds BCE game server health:
1. systemctl status rod-server rod-auth rod-dashboard
2. ss -ulnp | grep 7777 (UDP port open?)
3. tail -20 /var/log/crossworlds.log
4. ls -la /game/Builds/CrossworldsBCE.x86_64
5. curl -s http://localhost:3000/health
6. Report any errors or unexpected state
```

---

## Dashboard URLs

| URL | Access |
|-----|--------|
| `http://15.204.243.36` | Public download page |
| `http://15.204.243.36:4000` | Manager dashboard (HTTP Basic Auth) |
| `http://15.204.243.36:4000/gm-dashboard?token=<TOKEN>` | GM dashboard (token in VPS .env) |

GM Dashboard shows: server status, spawn events, last 50 log lines (color-coded), restart button, log download, Uptime Kuma link.

---

## Auth Server Notes

- **DO NOT restart `rod-auth` carelessly** — it handles all active JWTs and DB connections
- Auth server auto-starts on VPS reboot via systemd
- Logs: `journalctl -u rod-auth -f`
- Config: `/opt/rod-auth/.env` (JWT secret, DB credentials — see `SERVER_REFERENCE.md`)

---

## Known Pitfalls

| Symptom | Cause | Fix |
|---------|-------|-----|
| Server starts then crashes | UnityPlayer.so version mismatch | Upload matching `UnityPlayer.so` from build output |
| `Could not spawn` errors in game | Old binary on VPS after prefab rebuild | Upload fresh server build |
| Game server not listening on 7777 | Binary name wrong in systemd, or crash | Check binary name, check log for crash |
| Players connect but see no other players | Client and server have different prefab assetIds | Rebuild BOTH client and server after any prefab changes |
| Auth server returns 500 | DB connection issue or bad .env | Check `journalctl -u rod-auth`, verify MySQL is running |

---

## Active TODOs

- HTTPS / Cloudflare SSL — all traffic plain HTTP; JWT in transit is unencrypted
- Domain name (currently IP-only)
- CI/CD pipeline exists (`.github/workflows/build-and-deploy.yml`) but needs secrets configured — deferred
