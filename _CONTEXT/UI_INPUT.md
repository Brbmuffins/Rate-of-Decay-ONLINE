# UI & Input — Troubleshooting Context

**When to load:** Chat not working, can't type, camera re-centering, cursor locking, ESC menu issues, WASD moving during chat, clicking re-centers cursor, EventSystem errors.

---

## Key Files

| File | Role |
|------|------|
| `Assets/Game/Networking/RodChatManager.cs` | Chat UI; text input via `Keyboard.current.onTextInput`; singleton; creates EventSystem |
| `Assets/Game/UI/EscMenu.cs` | Self-bootstrap ESC menu; checks `RodChatManager.IsOpen` before handling Escape |
| `Assets/Game/Characters/Engineer/Scripts/CameraFollow.cs` | WoW-style camera; cursor lock/unlock; `_typingInUI` flag checks `RodChatManager.IsOpen` |
| `Assets/Game/Characters/Engineer/Scripts/PlayerMovement.cs` | WASD movement; `IsTypingInUI()` checks `RodChatManager.IsOpen` to block input during chat |
| `Assets/Game/UI/GmConsole.cs` | GM console; self-bootstrap; toggle with ` or F1 |
| `Assets/Game/UI/PlayerListUI.cs` | Who's Online panel; P to toggle; always `blocksRaycasts = false` |
| `Assets/Game/UI/PlayerNameplate.cs` | Floating billboard above each player |

---

## The Missing EventSystem Problem (SOLVED)

**Symptom:** ESC menu buttons don't respond to clicks. Every left-click re-centers cursor. Chat works but TMP interaction is unreliable.

**Root cause:** Hub scene is built by `HubSceneBuilder`, which wipes all non-Network GameObjects (no `NetworkIdentity`) — including the EventSystem carried over from LoginScene. Without `EventSystem.current`, `IsPointerOverGameObject()` returns false on every frame, so CameraFollow treats every left-click as a world click and locks the cursor.

**Fix (in `RodChatManager.OnStartClient()`):**
```csharp
static void EnsureEventSystem()
{
    if (EventSystem.current != null) return;
    var go = new GameObject("EventSystem",
        typeof(EventSystem),
        typeof(InputSystemUIInputModule));
    DontDestroyOnLoad(go);
}
```
This runs once when the first client connects to Hub. `DontDestroyOnLoad` ensures it survives any subsequent scene loads.

---

## Chat Input Architecture

**Why not TMP_InputField.onSubmit?** Unreliable with Unity's new Input System when EventSystem is missing or uses wrong Input Module.

**Current approach — bypass TMP entirely:**
- Text captured via `Keyboard.current.onTextInput` (raw Input System event, no EventSystem dependency)
- `_typedText` string managed manually
- `_input.text` updated as display only (TMP field is read-only effectively)
- Backspace handled in `Update()`
- Enter/Escape handled in `Update()` via `wasPressedThisFrame`

**Subscribe/unsubscribe pattern:**
```csharp
// OpenInput()
Keyboard.current.onTextInput -= OnTextInput; // guard double-subscribe
Keyboard.current.onTextInput += OnTextInput;

// CloseInput()
Keyboard.current.onTextInput -= OnTextInput;

// OnDestroy()
if (Keyboard.current != null)
    Keyboard.current.onTextInput -= OnTextInput;
```

**Chat open/close keys:**
- **Enter** or **T** — open chat
- **Enter** while open — send and close
- **Escape** while open — close without sending

---

## Camera Cursor Lock Rules (CameraFollow.cs)

```
Right mouse held     → lock cursor, orbit
Left mouse held      → lock cursor, orbit (unless click started on UI)
Either released      → unlock cursor
Chat open            → never lock (checked via RodChatManager.IsOpen)
Typing in TMP field  → never lock (checked via EventSystem.currentSelectedGameObject)
```

**Critical: first-frame delta discard.** When entering orbit mode, `mouse.delta` has accumulated while cursor was free. Without discarding this, entering orbit causes a violent camera swing. `_prevLookActive` flag tracks this.

**`_leftStartedOnUI`:** Left-click origin is checked on `wasPressedThisFrame` only (not every frame). Checking `IsPointerOverGameObject()` every frame was found to permanently block orbit because faded canvases with GraphicRaycaster always return true.

---

## WASD During Chat (PlayerMovement.cs)

New Input System does NOT consume key events when `TMP_InputField` is focused. WASD moves the character while typing without an explicit check.

```csharp
static bool IsTypingInUI()
{
    var sel = EventSystem.current?.currentSelectedGameObject;
    return (sel != null && sel.GetComponent<TMP_InputField>() != null)
        || (RodChatManager.Instance != null && RodChatManager.Instance.IsOpen);
}
```

Both conditions needed: EventSystem check for other input fields, `IsOpen` for chat (which uses raw keyboard bypass).

---

## ESC Menu (EscMenu.cs)

**Priority order when Escape is pressed:**
1. If `RodChatManager.Instance.IsOpen` → do nothing (let chat handle it)
2. If a `TMP_InputField` is focused via EventSystem → defocus it, return
3. Otherwise → toggle ESC menu

```csharp
if (RodChatManager.Instance != null && RodChatManager.Instance.IsOpen)
    return;
```

**Sorting orders:**
- Chat: 100
- ESC Menu: 200
- GM Console: 999

---

## CanvasGroup Rules (Critical Pattern)

Every UI element that can be hidden MUST set all three:
```csharp
_cg.alpha          = 0f;
_cg.blocksRaycasts = false;  // alpha=0 alone does NOT stop raycasts
_cg.interactable   = false;
```

**Chat exception:** `blocksRaycasts` stays `true` always (even when faded). This prevents the faded chat area from counting as "world click" and triggering cursor lock. Only `interactable` is toggled.

---

## Known Pitfalls

| Symptom | Cause | Fix |
|---------|-------|-----|
| Every click re-centers cursor | No EventSystem → `IsPointerOverGameObject()` always false | `EnsureEventSystem()` in `RodChatManager.OnStartClient()` |
| ESC menu buttons don't respond | No EventSystem → Button clicks never fire | Same fix above |
| Escape opens ESC menu while typing chat | EscMenu didn't check `RodChatManager.IsOpen` | Fixed in EscMenu.Update() |
| Camera orbits while typing | `_typingInUI` only checked EventSystem, not chat's `IsOpen` | Both checks in CameraFollow and PlayerMovement |
| WASD moves player while typing | New Input System doesn't consume key events | `IsTypingInUI()` with `IsOpen` check in PlayerMovement |
| Chat text doesn't appear | `onTextInput` not subscribed, or `_cg.interactable = false` when opening | Check `OpenInput()` sets interactable before subscribing |
| Clicking faded chat re-centers cursor | `blocksRaycasts = false` when faded → counts as world click | Keep `blocksRaycasts = true` always on chat canvas |

---

## Active TODOs

- HubSceneBuilder should add EventSystem + InputSystemUIInputModule directly (currently created at runtime by RodChatManager as a workaround)
- `T` key opens chat — ensure it doesn't conflict with any future ability keybind on that key
