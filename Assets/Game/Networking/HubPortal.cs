using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using TMPro;

// ═══════════════════════════════════════════════════════════════════════════
//  HubPortal
//  Place on any portal GameObject in the Hub scene.
//  When a player walks within ActivateRadius, a floating prompt appears.
//  Press E to load the target scene.
//
//  Inspector fields:
//    targetScene   — exact scene name (must be in Build Settings)
//    portalLabel   — display name shown in the prompt, e.g. "Combat Zone"
//    activateRadius — how close the player needs to be (default 5u)
//
//  If the scene is not ready yet, set targetScene = "" and it will show
//  "Coming Soon" instead of attempting to load.
// ═══════════════════════════════════════════════════════════════════════════

[AddComponentMenu("BCE/Hub Portal")]
public class HubPortal : MonoBehaviour
{
    [Header("Destination")]
    [Tooltip("Exact scene name in Build Settings. Leave empty for 'Coming Soon'.")]
    public string targetScene  = "";
    public string portalLabel  = "Portal";

    [Header("Interaction")]
    public float activateRadius = 5f;

    // ── Runtime ───────────────────────────────────────────────────────────
    GameObject  _promptGO;
    TextMeshPro _promptText;
    bool        _playerNear;
    bool        _loading;

    static readonly Color ColorReady    = new Color(0.85f, 1.00f, 0.85f, 1f);
    static readonly Color ColorDisabled = new Color(0.65f, 0.65f, 0.70f, 1f);

    // ── Unity ──────────────────────────────────────────────────────────────

    void Start()
    {
        BuildPrompt();
        SetPromptVisible(false);
    }

    void Update()
    {
        if (_loading) return;

        // Distance check against local player
        var player = FindLocalPlayer();
        bool near  = player != null &&
                     Vector3.Distance(transform.position, player.position) <= activateRadius;

        if (near != _playerNear)
        {
            _playerNear = near;
            SetPromptVisible(near);
        }

        if (!near) return;

        // E key — consume only if not typing in a UI field
        var kb  = Keyboard.current;
        var sel = UnityEngine.EventSystems.EventSystem.current?.currentSelectedGameObject;
        bool typing = sel != null && sel.GetComponent<TMPro.TMP_InputField>() != null;

        if (kb != null && !typing && kb.eKey.wasPressedThisFrame)
            TryEnter();
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.25f);
        Gizmos.DrawSphere(transform.position, activateRadius);
    }

    // ── Core ───────────────────────────────────────────────────────────────

    void TryEnter()
    {
        if (string.IsNullOrEmpty(targetScene))
        {
            // Coming soon — flash message, no load
            SetPromptText("Coming Soon", ColorDisabled);
            return;
        }

        _loading = true;
        SetPromptText("Entering...", ColorReady);
        SceneManager.LoadScene(targetScene);
    }

    // ── Floating prompt ────────────────────────────────────────────────────

    void BuildPrompt()
    {
        _promptGO = new GameObject("PortalPrompt");
        _promptGO.transform.SetParent(transform, false);
        // Float above the portal centre — adjust Y to taste
        _promptGO.transform.localPosition = new Vector3(0f, 3.8f, 0f);

        _promptText = _promptGO.AddComponent<TextMeshPro>();
        _promptText.alignment       = TextAlignmentOptions.Center;
        _promptText.fontSize         = 3.2f;
        _promptText.richText         = true;
        _promptText.raycastTarget    = false;
        _promptText.enableWordWrapping = false;

        // Billboard — always face camera
        _promptGO.AddComponent<RodBillboard>();

        SetPromptText(null, ColorReady);
    }

    void SetPromptText(string overrideMsg, Color color)
    {
        if (_promptText == null) return;
        bool ready     = !string.IsNullOrEmpty(targetScene);
        string line1   = $"<size=3.6><b>{portalLabel}</b></size>";
        string line2   = overrideMsg ?? (ready ? "[E] Enter" : "Coming Soon");
        _promptText.text  = $"{line1}\n<size=2.8><color=#{ColorToHex(color)}>{line2}</color></size>";
    }

    void SetPromptVisible(bool visible)
    {
        if (_promptGO != null) _promptGO.SetActive(visible);
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    static Transform FindLocalPlayer()
    {
        // Local player's GameObject is tagged "Player" and carries a Mirror
        // NetworkIdentity marked isLocalPlayer. Fallback to any "Player" tag.
        var identities = Object.FindObjectsByType<Mirror.NetworkIdentity>(
            FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (var id in identities)
            if (id.isLocalPlayer) return id.transform;

        // Fallback for dev mode / no Mirror
        var go = GameObject.FindWithTag("Player");
        return go != null ? go.transform : null;
    }

    static string ColorToHex(Color c)
    {
        return ColorUtility.ToHtmlStringRGB(c);
    }
}
