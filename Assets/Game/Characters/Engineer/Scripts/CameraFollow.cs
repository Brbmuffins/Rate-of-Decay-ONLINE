using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

// ═══════════════════════════════════════════════════════════════════════════
//  CameraFollow — WoW-style 3rd-person camera
//
//  Right mouse held   → lock cursor, orbit camera
//  Left mouse held    → lock cursor, orbit camera (character faces cam on move)
//  Either released    → unlock cursor (can click UI)
//  Scroll wheel       → zoom in / out
//  Both mouse held    → auto-walk forward (handled in PlayerMovement)
//
//  Setting Target snaps the camera behind the character immediately —
//  no lerp from world origin on zone-in.
// ═══════════════════════════════════════════════════════════════════════════

public class CameraFollow : MonoBehaviour
{
    [Header("Distance")]
    public float distance    = 7f;
    public float minDistance = 1.5f;
    public float maxDistance = 20f;
    public float zoomSpeed   = 4f;

    [Header("Orbit")]
    [Tooltip("Degrees per pixel of mouse movement. 0.15–0.35 is typical MMO range.")]
    public float mouseSensitivity = 0.25f;
    public float minPitch = -20f;
    public float maxPitch =  70f;

    [Header("Follow")]
    public float heightOffset = 1.6f;

    // ── Target property — snaps camera immediately on assign ──────────────
    Transform _target;
    public Transform target
    {
        get => _target;
        set
        {
            _target = value;
            if (_target != null) SnapToTarget();
        }
    }

    // Read by PlayerMovement
    public float Yaw            => _yaw;
    public bool  RightMouseHeld => _rightHeld && !_typingInUI;

    float   _yaw;
    float   _pitch = 18f;
    bool    _rightHeld;
    bool    _leftHeld;
    bool    _typingInUI;
    bool    _prevLookActive;   // detect first frame of look to discard stale delta
    bool    _leftStartedOnUI;  // true if left-click began over a UI element — skip cursor lock
    Vector3 _smoothPos;

    // ── Lifecycle ─────────────────────────────────────────────────────────

    void Start()
    {
        // Snap if target was set before Start (e.g. via Inspector)
        if (_target != null) SnapToTarget();

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;
    }

    void LateUpdate()
    {
        if (_target == null) return;

        var mouse = Mouse.current;
        if (mouse == null) return;

        _rightHeld = mouse.rightButton.isPressed;
        _leftHeld  = mouse.leftButton.isPressed;

        // Track whether this left-click started on a UI element.
        // We check IsPointerOverGameObject() only on the press frame to avoid the
        // "invisible canvas always returns true" problem — checking per-frame would
        // permanently kill camera rotation.
        if (mouse.leftButton.wasPressedThisFrame)
            _leftStartedOnUI = EventSystem.current != null
                               && EventSystem.current.IsPointerOverGameObject();
        if (!_leftHeld)
            _leftStartedOnUI = false;

        // Block orbit when typing OR when chat is open — the second check covers the
        // one-frame gap between ActivateInputField() and currentSelectedGameObject being set.
        var selGO = EventSystem.current?.currentSelectedGameObject;
        _typingInUI = (selGO != null && selGO.GetComponent<TMPro.TMP_InputField>() != null)
                   || (RodChatManager.Instance != null && RodChatManager.Instance.IsOpen);

        // Left-click only activates camera orbit if it didn't start on UI (e.g. chat box).
        // Right-click always orbits (it has no UI use outside the game world).
        bool lookActive = (_rightHeld || (_leftHeld && !_leftStartedOnUI)) && !_typingInUI;

        // ── Cursor lock / unlock ──────────────────────────────────────────
        if (lookActive && Cursor.lockState != CursorLockMode.Locked)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible   = false;
        }
        else if (!lookActive && Cursor.lockState == CursorLockMode.Locked)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible   = true;
        }

        // ── Camera rotation ───────────────────────────────────────────────
        // mouse.delta accumulates while cursor is free. On the FIRST frame we enter
        // look mode that stale delta would cause a violent swing — discard it.
        bool justEnteredLook = lookActive && !_prevLookActive;
        _prevLookActive = lookActive;

        if (lookActive && !justEnteredLook)
        {
            Vector2 delta = mouse.delta.ReadValue();
            // Clamp per-frame delta to prevent single-frame spikes
            delta.x = Mathf.Clamp(delta.x, -50f, 50f);
            delta.y = Mathf.Clamp(delta.y, -50f, 50f);
            _yaw   += delta.x * mouseSensitivity;
            _pitch -= delta.y * mouseSensitivity;
            _pitch  = Mathf.Clamp(_pitch, minPitch, maxPitch);
        }

        // ── Zoom ──────────────────────────────────────────────────────────
        float scroll = mouse.scroll.ReadValue().y;
        if (Mathf.Abs(scroll) > 0.01f)
            distance = Mathf.Clamp(distance - scroll * zoomSpeed * 0.01f,
                                   minDistance, maxDistance);

        // ── Position — instant follow, no lag ─────────────────────────────
        // Positional smoothing causes the character to drift out of frame.
        // WoW-style cameras follow position immediately; smoothness comes from
        // character animation, not camera lag.
        _smoothPos = _target.position;

        Quaternion rot    = Quaternion.Euler(_pitch, _yaw, 0f);
        Vector3    offset = rot * new Vector3(0f, 0f, -distance);
        Vector3    lookAt = _smoothPos + Vector3.up * heightOffset;

        transform.position = lookAt + offset;
        transform.LookAt(lookAt);
    }

    // ── Public helpers ────────────────────────────────────────────────────

    /// <summary>Instantly places the camera behind the target. Call after setting target.</summary>
    public void SnapToTarget()
    {
        if (_target == null) return;

        _yaw       = _target.eulerAngles.y;
        _smoothPos = _target.position;

        Quaternion rot    = Quaternion.Euler(_pitch, _yaw, 0f);
        Vector3    offset = rot * new Vector3(0f, 0f, -distance);
        Vector3    lookAt = _smoothPos + Vector3.up * heightOffset;

        transform.position = lookAt + offset;
        transform.LookAt(lookAt);
    }
}
