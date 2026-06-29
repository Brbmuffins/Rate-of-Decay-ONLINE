# AUTH_LOGIN.md — Login Flow, JWT, CharacterSelect

> Load this file when: login not working, JWT errors, CharacterSelect failing to load,
> character data missing on spawn, 401/403 errors, token expiry issues.

**Last verified:** 2026-06-28

---

## Stack Facts
- **Auth server:** Node.js / Express, port 3000
- **File:** `/opt/rod-auth/server.js`
- **DB:** MySQL 8, `rod_online`, user `rodgame`
- **JWT:** expires 24h, secret in `/opt/rod-auth/.env`
- **Account creation:** admin-only via dashboard (no public self-registration)

---

## Full Login → Spawn Sequence

```
1. Unity LoginScene
   POST /login  {username, password}
   → receives {token}  (JWT, 24h expiry)

2. Unity CharacterSelect
   POST /character  {token in header}
   → creates character if first login, returns character row
   → fields returned: id, class_index, class_name, level, xp, gold,
                      stat_str, stat_agi, stat_int, stat_vit, pos_x/y/z

3. Unity connects to game server UDP 7777
   → sends token + character data in connection handshake

4. Game server validates token with auth server
   → spawns correct class prefab at saved position

5. On scene load (Hub)
   GET /api/inventory/:characterId  → populate bag + apply equipped stats
   GET /api/professions/:characterId → profession skill levels
```

---

## Endpoints

### POST /login
```
Body:    { username, password }
Returns: { token }   (JWT string)
Errors:  401 invalid credentials
         403 account banned
```

### POST /character
```
Header:  Authorization: Bearer <token>
Returns: character row (id, class_index, class_name, level, xp, gold,
         stat_str, stat_agi, stat_int, stat_vit, pos_x, pos_y, pos_z)
Notes:   Creates character on first call. Returns existing on subsequent calls.
         One character per account.
```

### GET /character
```
Header:  Authorization: Bearer <token>
Returns: character row + gear (from old item_instance / character_gear system)
Notes:   This is the spawn endpoint — Unity calls this every login.
         DO NOT MODIFY — Unity depends on this exact response shape.
```

---

## Character Classes (server-authoritative)
| class_index | class_name |
|---|---|
| 0 | Engineer |
| 1 | Guardian |
| 2 | Shadowblade |
| 3 | Cleric |
| 4 | Arcanist |

Defined in `CLASS_NAMES` array in `/opt/rod-auth/server.js`. The class_index is set at character creation and never changes.

---

## Common Failures

### 401 on every request after login
Token expired (24h limit) or malformed. Unity should catch 401 responses and re-trigger the login flow. Check `.env`: `JWT_EXPIRES_IN=24h`.

### CharacterSelect shows wrong class or no data
`GET /character` returned an error or empty. Check:
```bash
sudo journalctl -u crossworlds-auth -n 30 --no-pager
curl -H "Authorization: Bearer <token>" http://localhost:3000/character
```

### Character spawns at wrong position
`PATCH /character/position` didn't save cleanly on last disconnect. Known issue: Unity sends float as formatted string (`"0.000"`) instead of number — the `orientation:F3` bug. Also fires if Unity crashes without calling the disconnect handler.

### New player gets wrong class prefab
`class_index` in DB doesn't match the prefab NetworkManager spawns. The class index from `POST /character` must map 1:1 to prefab array index in NetworkManager's Registered Spawnable Prefabs.

---

## Auth Server Health Check
```bash
curl http://localhost:3000/api/health
# returns: { status: "ok", uptime, db: "connected", timestamp }

sudo journalctl -u crossworlds-auth -n 30 --no-pager
sudo systemctl restart crossworlds-auth
```

---

## Known TODOs
- Unity should handle 401 gracefully and return player to LoginScene rather than hanging
- `PATCH /character/position` float serialization bug (`orientation:F3`) — Unity side fix needed
