# Crossworlds (BCE) — Server Reference
*Keep this private. Contains usernames and file locations.*

---

## The Big Picture — How Login Works

```
[Player opens game]
       ↓
[Types username + password into login screen]
       ↓
[Unity client sends POST to Auth Server at 15.204.243.36:3000/login]
       ↓
[Auth Server checks the MySQL database — does this user exist? is the password correct?]
       ↓
[Auth Server sends back a JWT token — a signed key proving who the player is]
       ↓
[Unity client connects to the Game Server on port 7777, includes the JWT token]
       ↓
[Game Server validates the token — if good, loads the player's character and spawns them]
       ↓
[Player is in the game]
```

The Auth Server and Game Server never trust the client directly.
The JWT token is the proof of identity passed between them.

---

## Users

### MySQL — Database Users

MySQL has two users. Think of MySQL like a filing cabinet.

| User | Password | What it does |
|------|----------|-------------|
| `root` | *(set during MySQL install)* | Full admin access. Only use this to manage the database itself. Never used by the game. |
| `rodgame` | `R0dG@me$ecure2024!` | The game's database account. The auth server and game server log in as this user to read/write player data. Has access to the `rod_online` database only. |

**To log into MySQL as root:**
```bash
sudo mysql
```

**To log into MySQL as the game user:**
```bash
mysql -u rodgame -p rod_online
# enter password when prompted
```

---

### Linux — System Users

| User | What it does |
|------|-------------|
| `ubuntu` | Your admin account. This is you when you SSH in. Has sudo access. |
| `rod-auth` | *(service user, no login)* The auth server process runs as this user for security. You never log in as it. |

**To SSH into the server:**
```bash
ssh ubuntu@15.204.243.36
```

---

## The Auth Server

**What it is:** A small Node.js app that handles player accounts. It's the only thing that talks to the accounts table in the database.

**Where it lives:** `/opt/rod-auth/`

**Key files:**
```
/opt/rod-auth/
├── server.js          ← the actual app code
├── .env               ← passwords and secrets (never share this file)
└── package.json
```

**The .env file contains:**
```
DB_HOST=localhost
DB_USER=rodgame
DB_PASSWORD=R0dG@me$ecure2024!
DB_NAME=rod_online
JWT_SECRET=<your long hex secret>
PORT=3000
```

**API endpoints:**
| Endpoint | Method | What it does |
|----------|--------|-------------|
| `/health` | GET | Returns `{"status":"ok"}` — just confirms it's running |
| `/register` | POST | Creates a new account. Body: `{ username, email, password }` |
| `/login` | POST | Logs in. Body: `{ username, password }`. Returns a JWT token. |

---

## The Game Server

**What it is:** Your Unity build running in headless mode (no graphics). Mirror Networking listens for player connections on port 7777.

**Where it lives:** `/game/`

**Key files:**
```
/game/
├── Crossworlds.x86_64    ← the server binary (run this)
├── Crossworlds_Data/     ← game data (required, don't delete)
├── GameAssembly.so             ← compiled game code (required)
├── UnityPlayer.so              ← Unity runtime (required)
└── logs/
    └── server.log              ← game server logs
```

---

## System Services

Both servers run as services that start automatically and restart if they crash.

| Service | What it runs | Auto-starts |
|---------|-------------|-------------|
| `rod-auth` | Auth server (Node.js, port 3000) | ✅ Yes |
| `rod-gameserver` | Unity game server (port 7777) | ✅ Yes (won't start until binary exists) |
| `mysql` | Database | ✅ Yes |

**Useful commands:**

```bash
# Check if something is running
sudo systemctl status rod-auth
sudo systemctl status rod-gameserver
sudo systemctl status mysql

# Start / stop / restart
sudo systemctl start rod-auth
sudo systemctl stop rod-auth
sudo systemctl restart rod-auth

# Watch live logs
sudo journalctl -u rod-auth -f
sudo journalctl -u rod-gameserver -f

# Watch game server log directly
tail -f /var/log/crossworlds.log

# Check auth server is responding
curl http://localhost:3000/health
```

---

## Firewall — Open Ports

```
Port 22   TCP — SSH (how you connect to the server)
Port 7777 UDP — Mirror Networking (how players connect to the game)
Port 3000 TCP — Auth server (how the game client logs in)
```

---

## Quick Reference — "What do I do if..."

| Problem | Command |
|---------|---------|
| Auth server is down | `sudo systemctl restart rod-auth` |
| Game server crashed | `sudo systemctl restart rod-gameserver` |
| Check why something failed | `sudo journalctl -u rod-gameserver -n 50` |
| Deploy a new Unity build | Upload to `/game/`, then `sudo systemctl restart rod-gameserver` |
| Edit the auth server config | `sudo nano /opt/rod-auth/.env` then `sudo systemctl restart rod-auth` |
| Get into the database | `sudo mysql` then `USE rod_online;` |
