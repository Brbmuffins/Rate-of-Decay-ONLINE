# VPS_SERVER.md — Deploy, Services, Logs, Nginx

> Load this file when: deploying a new Unity build, restarting services, reading logs,
> server down, Nginx config, SSL cert issues, dashboard access.

**Last verified:** 2026-06-28

---

## Server Facts
- **IP:** 15.204.243.36
- **Domain:** playcrossworlds.com (DNS live, SSL active)
- **OS:** Ubuntu 22.04 LTS
- **SSH:** `ssh ubuntu@playcrossworlds.com`
- **Disk:** 388 GB total, ~7 GB used

---

## Services

| Service | Port | Unit file | Directory |
|---|---|---|---|
| Auth server | 3000/TCP | `crossworlds-auth.service` | `/opt/rod-auth/` |
| Dashboard | 4000/TCP | `crossworlds-dashboard.service` | `/opt/rod-dashboard/` |
| Game server | 7777/UDP | `crossworlds.service` | `/game/Builds/` |
| MySQL | 3306/TCP | `mysql.service` | — |
| Uptime Kuma | 3001/TCP | — | Needs web UI setup |

All services: `Restart=on-failure`

```bash
# Status
sudo systemctl status crossworlds-auth crossworlds-dashboard crossworlds

# Restart individual
sudo systemctl restart crossworlds-auth
sudo systemctl restart crossworlds-dashboard
sudo systemctl restart crossworlds

# Restart all
sudo systemctl restart crossworlds-auth crossworlds-dashboard crossworlds
```

---

## Reading Logs

```bash
# Auth server (most useful — API errors, logins, crafts)
sudo journalctl -u crossworlds-auth -n 50 --no-pager
sudo journalctl -u crossworlds-auth -f   # follow live

# Game server
sudo journalctl -u crossworlds -n 50 --no-pager
tail -f /var/log/crossworlds.log

# Dashboard
sudo journalctl -u crossworlds-dashboard -n 30 --no-pager
```

### Log prefix grep cheatsheet
```bash
# Filter by system
sudo journalctl -u crossworlds-auth | grep "\[CRAFT\]"
sudo journalctl -u crossworlds-auth | grep "\[LOGIN\]"
sudo journalctl -u crossworlds-auth | grep "\[PROGRESS\]"
sudo journalctl -u crossworlds-auth | grep "\[LOOT\]"
sudo journalctl -u crossworlds-auth | grep "ERROR\|error"
```

---

## Deploying a New Unity Build

```bash
# From your local machine (Windows PowerShell)
scp -r .\Build\* ubuntu@playcrossworlds.com:/game/Builds/

# On the server (or chain with &&)
ssh ubuntu@playcrossworlds.com "chmod +x /game/Builds/CrossworldsBCE.x86_64 && sudo systemctl restart crossworlds"

# Verify it started
ssh ubuntu@playcrossworlds.com "sudo journalctl -u crossworlds -n 20 --no-pager"
```

**Critical build rules:**
- `GameAssembly.so` and `UnityPlayer.so` must be from the same build session — different sessions = SIGSEGV on startup
- `CrossworldsBCE_Data/` folder name must exactly match the binary name
- Never deploy client without also deploying the matching server binary

---

## File Locations

```
/opt/rod-auth/
  server.js          — Auth server (ALL game API endpoints)
  .env               — DB creds, JWT secret — NEVER log or expose

/opt/rod-dashboard/
  server.js          — Dashboard server
  public/index.html  — Dashboard UI
  public/icon.png    — Game icon

/game/Builds/
  CrossworldsBCE.x86_64         — Unity server binary
  CrossworldsBCE_Data/           — Must match binary name
  GameAssembly.so                — Must match UnityPlayer.so
  UnityPlayer.so
  CrossworldsBCE_BackUpThisFolder_ButDontShipItWithYourGame/  — debug symbols, safe to ignore
  Crossworlds_BurstDebugInformation_DoNotShip/                — safe to ignore

/var/www/rod/
  index.html                     — Public download/landing page
  roadmap.html                   — Phase 1 roadmap
  icon.png                       — Game icon (1254×1254)
  downloads/CrossworldsBCE.zip   — Windows client

/var/log/crossworlds.log         — Unity game server stdout
```

---

## Nginx

Config: `/etc/nginx/sites-available/rod`

Serves:
- `https://playcrossworlds.com/` → `/var/www/rod/index.html`
- `https://playcrossworlds.com/roadmap.html`
- `https://playcrossworlds.com/downloads/CrossworldsBCE.zip` (forced attachment)
- Static assets (png, js, css) cached 7 days

```bash
# Reload config (no downtime)
sudo systemctl reload nginx

# Test config before reloading
sudo nginx -t
```

SSL: Let's Encrypt via Certbot. Cert path:
```
/etc/letsencrypt/live/playcrossworlds.com/fullchain.pem
/etc/letsencrypt/live/playcrossworlds.com/privkey.pem
```
Auto-renews via systemd timer. Check: `sudo certbot renew --dry-run`

---

## Database

```bash
# Connect
mysql -u rodgame -p"$(grep DB_PASS /opt/rod-auth/.env | cut -d= -f2)" rod_online

# Quick health checks
SHOW TABLES;
SELECT COUNT(*) FROM accounts;
SELECT COUNT(*) FROM characters;
SELECT COUNT(*) FROM inventory;
```

---

## Dashboard Access
- URL: `http://playcrossworlds.com:4000`
- Credentials: `ADMIN_USER` / `ADMIN_PASS` in `/opt/rod-dashboard/.env`
- GM dashboard: `http://playcrossworlds.com:4000/gm-dashboard?token=<ADMIN_TOKEN>`

---

## Health Check
```bash
curl http://localhost:3000/api/health
# Expected: { "status": "ok", "uptime": ..., "db": "connected", "timestamp": ... }
```

---

## Ports — Frozen
| Port | Service | Rule |
|---|---|---|
| 3000 | Auth server | Never proxy or change |
| 4000 | Dashboard | Never proxy or change |
| 7777/UDP | Game server | Never change — hardcoded in Unity |
| 80/443 | Nginx | SSL live |
| 3001 | Uptime Kuma | Do not touch |

---

## Active TODOs
- Uptime Kuma web UI needs setup at `http://15.204.243.36:3001`
- `StartLimitIntervalSec=0` on `crossworlds.service` — would allow infinite restarts vs stopping after 5 rapid crashes
- Discord link on download page shows "Coming Soon"
