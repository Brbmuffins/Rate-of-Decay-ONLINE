using System.Collections;
using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour
{
    public float moveSpeed = 5f;
    public float sprintSpeed = 9f;
    public float jumpForce = 6f;
    public float rotationSpeed = 12f;
    public Camera cam;

    // ── Dodge Roll ────────────────────────────────────────────────
    [Header("Dodge Roll")]
    public float dodgeForce       = 14f;   // burst speed during dodge
    public float dodgeDuration    = 0.35f; // seconds of i-frames + movement burst
    public int   dodgeMaxCharges  = 2;     // maximum stored charges
    public float dodgeRecharge    = 5f;    // seconds to regain one charge
    // Assign a small ground-burst prefab (e.g. brbmuffins Technologies/.../SparksEffect.prefab)
    public GameObject dodgeVFX;

    private int   _dodgeCharges;
    private float _dodgeRechargeTimer;
    private bool  _isDodging;

    // ── Internals ─────────────────────────────────────────────────
    private Animator anim;
    private Rigidbody rb;
    private CharacterStats stats;
    private Health health;

    // Cached animator parameter names — avoids SetBool warnings on missing params
    private System.Collections.Generic.HashSet<string> _animParams =
        new System.Collections.Generic.HashSet<string>();

    private bool isGrounded = true;
    private bool inWater = false;

    private Vector3 moveDirection = Vector3.zero;
    private Quaternion targetRotation;
    private bool wantsMove = false;
    private bool jumpRequested = false;
    private float currentSpeed;

    void Start()
    {
        // ── Mirror: disable input on remote player objects ────────────────────
        var netId = GetComponent<NetworkIdentity>();
        bool isLocal = netId == null || netId.isLocalPlayer;
        if (!isLocal)
        {
            enabled = false;
            return;
        }

        anim   = GetComponentInChildren<Animator>();
        rb     = GetComponent<Rigidbody>();
        stats  = GetComponent<CharacterStats>();
        health = GetComponent<Health>();

        // Cache which parameters the controller actually has
        if (anim != null && anim.runtimeAnimatorController != null)
            foreach (var p in anim.parameters)
                _animParams.Add(p.name);

        // Auto-find camera and wire CameraFollow to this transform.
        if (cam == null) cam = Camera.main;
        if (cam == null && Camera.allCamerasCount > 0) cam = Camera.allCameras[0];
        if (cam != null)
        {
            var follow = cam.GetComponent<CameraFollow>() ?? cam.gameObject.AddComponent<CameraFollow>();
            follow.target = transform; // setter calls SnapToTarget() automatically
        }

        targetRotation = transform.rotation;
        currentSpeed   = moveSpeed;
        _dodgeCharges  = dodgeMaxCharges;

        SetAnimBool("isGrounded", true);
        SetAnimBool("inWater", false);
        SetAnimBool("isBackwards", false);
    }

    // Only call SetBool if the controller actually has that parameter
    void SetAnimBool(string param, bool value)
    {
        if (anim != null && _animParams.Contains(param))
            anim.SetBool(param, value);
    }

    void SetAnimTrigger(string param)
    {
        if (anim != null && _animParams.Contains(param))
            anim.SetTrigger(param);
    }

    // Returns true when the player is typing in any UI input field.
    // Also checks RodChatManager.IsOpen directly — the new Input System does NOT
    // consume key events when a TMP_InputField has focus, so WASD would still
    // move the character while typing without this second check.
    static bool IsTypingInUI()
    {
        var sel = EventSystem.current?.currentSelectedGameObject;
        return (sel != null && sel.GetComponent<TMP_InputField>() != null)
            || (RodChatManager.Instance != null && RodChatManager.Instance.IsOpen);
    }

    void Update()
    {
        // Yield all keyboard input to UI while player is typing
        if (IsTypingInUI())
        {
            wantsMove = false;
            return;
        }

        Vector2 input = Vector2.zero;

        bool pressingW = Keyboard.current.wKey.isPressed;
        bool pressingS = Keyboard.current.sKey.isPressed;
        bool pressingA = Keyboard.current.aKey.isPressed;
        bool pressingD = Keyboard.current.dKey.isPressed;

        if (pressingW) input.y += 1;
        if (pressingS) input.y -= 1;
        if (pressingA) input.x -= 1;
        if (pressingD) input.x += 1;

        bool bothMouseHeld = Mouse.current.leftButton.isPressed && Mouse.current.rightButton.isPressed;
        if (bothMouseHeld && input.sqrMagnitude == 0)
        {
            input.y = 1;
        }

        bool isSprinting = Keyboard.current.leftShiftKey.isPressed && input.sqrMagnitude > 0 && !pressingS;
        bool isMoving = input.sqrMagnitude > 0;
        bool isBackwards = pressingS && !bothMouseHeld;

        SetAnimBool("isMoving", isMoving);
        SetAnimBool("isSprinting", isSprinting);
        SetAnimBool("isBackwards", isBackwards);

        if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame && isGrounded)
        {
            jumpRequested = true;
        }

        wantsMove = isMoving;
        currentSpeed = isSprinting ? sprintSpeed : moveSpeed;
        if (stats != null) currentSpeed *= stats.MoveSpeedMultiplier;   // gear/attunement bonus

        // ── Dodge roll input (Left Alt or V) ─────────────────────
        bool dodgePressed = Keyboard.current.leftAltKey.wasPressedThisFrame
                         || Keyboard.current.vKey.wasPressedThisFrame;
        if (dodgePressed && _dodgeCharges > 0 && !_isDodging)
        {
            StartCoroutine(DodgeRoutine());
        }

        // Recharge one charge at a time
        if (_dodgeCharges < dodgeMaxCharges)
        {
            _dodgeRechargeTimer += Time.deltaTime;
            if (_dodgeRechargeTimer >= dodgeRecharge)
            {
                _dodgeRechargeTimer = 0f;
                _dodgeCharges++;
            }
        }

        if (cam != null)
        {
            Vector3 camForward = cam.transform.forward;
            Vector3 camRight   = cam.transform.right;
            camForward.y = 0; camForward.Normalize();
            camRight.y   = 0; camRight.Normalize();

            var camFollow    = cam.GetComponent<CameraFollow>();
            bool rightMouse  = camFollow != null && camFollow.RightMouseHeld;

            if (isMoving)
            {
                moveDirection = (camForward * input.y + camRight * input.x).normalized;

                // Face camera direction when right-mouse is held (WoW strafe feel)
                // Face movement direction otherwise
                Vector3 faceDir = rightMouse
                    ? Quaternion.Euler(0, camFollow.Yaw, 0) * Vector3.forward
                    : (pressingS ? -moveDirection : moveDirection);

                if (faceDir.sqrMagnitude > 0.001f)
                    targetRotation = Quaternion.LookRotation(faceDir);
            }
            else if (rightMouse && camFollow != null)
            {
                // Standing still + right mouse: character turns to face camera yaw
                targetRotation = Quaternion.Euler(0, camFollow.Yaw, 0);
            }
        }
    }

    void FixedUpdate()
    {
        if (jumpRequested)
        {
            jumpRequested = false;
            isGrounded = false;
            SetAnimBool("isGrounded", false);
            SetAnimTrigger("Jump");

            rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
        }

        if (wantsMove)
        {
            Vector3 targetPosition = rb.position + moveDirection * currentSpeed * Time.fixedDeltaTime;
            rb.MovePosition(targetPosition);
        }

        rb.MoveRotation(Quaternion.Slerp(rb.rotation, targetRotation, rotationSpeed * Time.fixedDeltaTime));
    }

    // ── Dodge Roll ────────────────────────────────────────────────
    // Returns the number of charges remaining — used by UI to draw stamina pips.
    public int DodgeCharges  => _dodgeCharges;
    public int DodgeMaxCharges => dodgeMaxCharges;

    private IEnumerator DodgeRoutine()
    {
        _isDodging = true;
        _dodgeCharges--;

        // Direction: current move direction, or backward if standing still
        Vector3 dir = moveDirection.sqrMagnitude > 0.01f ? moveDirection : -transform.forward;
        dir.y = 0;
        dir.Normalize();

        // Grant i-frames
        if (health != null) health.isInvulnerable = true;

        SetAnimTrigger("dodge");

        // Spawn entry VFX at feet
        if (dodgeVFX != null)
        {
            GameObject fx = Instantiate(dodgeVFX, transform.position, Quaternion.identity);
            Destroy(fx, 1.5f);
        }

        // Burst movement over the dodge window
        float elapsed = 0f;
        while (elapsed < dodgeDuration)
        {
            rb.MovePosition(rb.position + dir * dodgeForce * Time.fixedDeltaTime);
            elapsed += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }

        // Remove i-frames
        if (health != null) health.isInvulnerable = false;

        _isDodging = false;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Ground"))
        {
            isGrounded = true;
            SetAnimBool("isGrounded", true);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Water"))
        {
            inWater = true;
            SetAnimBool("inWater", true);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Water"))
        {
            inWater = false;
            SetAnimBool("inWater", false);
        }
    }
}