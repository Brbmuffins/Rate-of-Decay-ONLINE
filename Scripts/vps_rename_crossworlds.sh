#!/usr/bin/env bash
# ═══════════════════════════════════════════════════════════════════════════════
#  VPS RENAME — Rate of Decay ONLINE → Crossworlds (BCE)
#
#  Run this as ubuntu on your VPS:
#    bash vps_rename_crossworlds.sh
#
#  What this does:
#   1. Stops the old rod-server systemd service
#   2. Renames the systemd unit file → crossworlds.service
#   3. Renames the game binary in /game/
#   4. Renames the log file reference
#   5. Reloads systemd and enables / starts crossworlds.service
#   6. Verifies it's running
#
#  Nothing in the database, nginx, or auth server is touched.
#  Nginx serves /var/www/game/ (download dir) — rename the zip there separately.
# ═══════════════════════════════════════════════════════════════════════════════

set -euo pipefail

OLD_SERVICE="rod-server"
NEW_SERVICE="crossworlds"
OLD_BINARY="RateOfDecayOnline.x86_64"
NEW_BINARY="Crossworlds.x86_64"
OLD_DATA_DIR="RateOfDecayOnline_Data"
NEW_DATA_DIR="Crossworlds_Data"
GAME_DIR="/game"
SERVICE_DIR="/etc/systemd/system"
OLD_LOG="/var/log/rod-server.log"
NEW_LOG="/var/log/crossworlds.log"

echo "══════════════════════════════════════════════"
echo "  Crossworlds Rename Script — VPS"
echo "══════════════════════════════════════════════"

# ── 1. Stop old service ───────────────────────────────────────────────────────
echo "[1/7] Stopping $OLD_SERVICE..."
sudo systemctl stop "$OLD_SERVICE" 2>/dev/null || echo "  (was not running — continuing)"

# ── 2. Rename systemd unit ────────────────────────────────────────────────────
echo "[2/7] Renaming systemd unit..."
OLD_UNIT="$SERVICE_DIR/$OLD_SERVICE.service"
NEW_UNIT="$SERVICE_DIR/$NEW_SERVICE.service"

if [ -f "$OLD_UNIT" ]; then
    sudo cp "$OLD_UNIT" "$NEW_UNIT"
    # Update the binary path and log references inside the unit file
    sudo sed -i \
        -e "s|$OLD_BINARY|$NEW_BINARY|g" \
        -e "s|rod-server|crossworlds|g" \
        -e "s|RateOfDecayOnline|Crossworlds|g" \
        "$NEW_UNIT"
    sudo systemctl disable "$OLD_SERVICE" 2>/dev/null || true
    sudo rm -f "$OLD_UNIT"
    echo "  ✓ $NEW_UNIT written"
else
    echo "  ⚠ $OLD_UNIT not found — check if the service was named differently."
    echo "    List your service files with: ls /etc/systemd/system/*.service"
    echo "    Then manually copy and edit the correct one to $NEW_UNIT"
    echo "    (script will continue — start the new service manually afterward)"
fi

# ── 3. Rename binary ──────────────────────────────────────────────────────────
echo "[3/7] Renaming binary in $GAME_DIR..."
if [ -f "$GAME_DIR/$OLD_BINARY" ]; then
    sudo mv "$GAME_DIR/$OLD_BINARY" "$GAME_DIR/$NEW_BINARY"
    sudo chmod +x "$GAME_DIR/$NEW_BINARY"
    echo "  ✓ $NEW_BINARY"
else
    echo "  ⚠ $OLD_BINARY not found in $GAME_DIR — may already be renamed or path is different"
fi

# ── 4. Rename _Data directory ─────────────────────────────────────────────────
echo "[4/7] Renaming _Data directory..."
if [ -d "$GAME_DIR/$OLD_DATA_DIR" ]; then
    sudo mv "$GAME_DIR/$OLD_DATA_DIR" "$GAME_DIR/$NEW_DATA_DIR"
    echo "  ✓ $NEW_DATA_DIR"
else
    echo "  ⚠ $OLD_DATA_DIR not found — may already be renamed or Unity named it differently"
fi

# ── 5. Create new log symlink / file ──────────────────────────────────────────
echo "[5/7] Setting up log path..."
if [ -f "$OLD_LOG" ]; then
    sudo mv "$OLD_LOG" "$NEW_LOG" 2>/dev/null || true
fi
sudo touch "$NEW_LOG" 2>/dev/null || true
echo "  ✓ Log: $NEW_LOG"

# ── 6. Reload systemd and start new service ───────────────────────────────────
echo "[6/7] Reloading systemd and starting crossworlds.service..."
sudo systemctl daemon-reload
sudo systemctl enable "$NEW_SERVICE"
sudo systemctl start "$NEW_SERVICE"
sleep 3

# ── 7. Verify ─────────────────────────────────────────────────────────────────
echo "[7/7] Verification..."
echo ""
sudo systemctl status "$NEW_SERVICE" --no-pager
echo ""
echo "--- last 15 lines of $NEW_LOG ---"
tail -15 "$NEW_LOG" 2>/dev/null || echo "(log empty — server just started)"
echo ""
echo "══════════════════════════════════════════════"
echo "  Done. crossworlds.service is live."
echo ""
echo "  Useful commands going forward:"
echo "    sudo systemctl status crossworlds"
echo "    sudo systemctl restart crossworlds"
echo "    tail -f $NEW_LOG"
echo "══════════════════════════════════════════════"

# ── Optional: rename client zip in nginx download dir ─────────────────────────
# Uncomment if you have a zip in /var/www/game/:
#
# OLD_ZIP="/var/www/game/RateOfDecayOnline-Windows.zip"
# NEW_ZIP="/var/www/game/Crossworlds-Windows.zip"
# if [ -f "$OLD_ZIP" ]; then
#     sudo mv "$OLD_ZIP" "$NEW_ZIP"
#     echo "  ✓ Client zip renamed to Crossworlds-Windows.zip"
# fi
