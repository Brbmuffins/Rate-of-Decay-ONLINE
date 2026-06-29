# UI_INPUT.md — Chat, ESC Menu, Camera, Input, Cursor

> Load this file when: chat typing moves the player, ESC menu issues, camera
> orbit breaking, cursor lock problems, WASD firing during chat, EventSystem conflicts.

**Last verified:** 2026-06-28

---

## Known Working State
- ESC menu: opens/closes correctly
- Camera: orbits around player, re-centers on input
- Chat: opens on Enter, closes on Enter/ESC, WASD blocked during typing
- Cursor: locked during gameplay, visible when chat/menu open

---

## Chat System

### How it works
- Press Enter → open chat input
- Type message → Enter to send / ESC to cancel
- `CmdSendChat` fires on server, relays to all clients via `RpcReceiveChat`
- Input field must capture keyboard so WASD doesn't move player while typing

### Common failures

**WASD moves player while typing**
The `PlayerController` isn't checking whether the chat input field is focused.
```csharp
// In PlayerController.Update(), gate movement:
if (ChatUI.Instance != null && ChatUI.Instance.IsTyping) return;
```

**Chat input won't open / focus**
EventSystem conflict — only one EventSystem allowed per scene. Check for duplicates in the Hub scene hierarchy. Delete extras.

**Messages not received by other players**
`CmdSendChat` not running on the server, or `RpcReceiveChat` not reaching clients. Check:
```bash
sudo journalctl -u crossworlds -n 20 --no-pager
# Look for [CHAT] log lines — if missing, add:
# console.log(`[CHAT] ${username}: ${message}`) in CmdSendChat
```

> **Open bug:** `[CHAT]` log line is missing server-side in `CmdSendChat`. Add it.

---

## ESC Menu

### How it works
- ESC toggles the pause/menu panel
- Menu shows: Resume, Disconnect, (future: Settings)
- While menu is open, cursor is visible and unlocked

### Common failures

**ESC also unfocuses chat input**
Input priority conflict. ESC should close chat first if chat is open, then open menu on second press. Check input handling order in `InputManager` or the UI controller.

**Menu doesn't close on Resume**
Ensure the Resume button calls `UIManager.CloseMenu()` and re-locks the cursor.

---

## Camera

### How it works
- Right-click drag: orbit around player
- Scroll wheel: zoom in/out
- Re-centers automatically when player starts moving
- Camera does NOT move during chat (cursor visible)

### Common failures

**Camera drifts or doesn't re-center**
The re-center trigger is movement input. If WASD is blocked during chat (correct), camera won't re-center until chat closes — this is expected behavior.

**Camera goes underground or clips through walls**
Camera collision not implemented yet (Phase 1 scope). Workaround: increase minimum zoom distance.

**Camera orbits wrong pivot**
The pivot target should be the player character root, not a child bone or the camera itself. Check the camera controller's `target` reference.

---

## Cursor Lock

| State | Cursor | Lock |
|---|---|---|
| Normal gameplay | Hidden | Locked |
| Chat open | Visible | Unlocked |
| ESC menu open | Visible | Unlocked |
| Loading screen | Hidden | Locked |

```csharp
// Lock
Cursor.lockState = CursorLockMode.Locked;
Cursor.visible = false;

// Unlock
Cursor.lockState = CursorLockMode.None;
Cursor.visible = true;
```

---

## EventSystem Rules
- **One EventSystem per scene.** Duplicates cause input fights where clicks register twice or not at all.
- When loading a new scene additively, destroy the incoming EventSystem if one already exists.
- The Hub scene owns the canonical EventSystem.

---

## Online Players HUD
The online players list shows connected player names as a vertical list in the top-left.

- Script: `_scripts/OnlinePlayersHUD.cs`
- Refreshes every 1.5 seconds
- Uses `VerticalLayoutGroup` — no world-position dependency (fixes overlap bug)
- Replace `PlayerInfo` component reference with whatever holds `displayName` in your player prefab

---

## Active Issues
- `GmConsole.cs` — spamming `InvalidOperationException` every frame on the server build. Fix: add `#if !UNITY_SERVER` guard around the GmConsole initialization.
