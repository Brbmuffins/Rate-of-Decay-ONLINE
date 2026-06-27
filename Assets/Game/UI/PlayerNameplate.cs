using Mirror;
using TMPro;
using UnityEngine;

// ═══════════════════════════════════════════════════════════════════════════
//  PlayerNameplate
//
//  Automatically added by PlayerIdentity.OnStartClient() — no prefab needed.
//  Spawns a world-space Canvas above the player that always faces the camera.
//
//  Layout (top to bottom):
//    [PlayerName]   ← white, bold, larger
//    [ClassName]    ← class-colour, smaller, slightly translucent
//
//  Rules:
//    • Local player's nameplate is hidden (you don't need to see your own)
//    • Fades with distance (fully visible 0–20 u, fades out by 40 u)
//    • Reacts to SyncVar changes via OnEnable/SyncVar hook
// ═══════════════════════════════════════════════════════════════════════════

[RequireComponent(typeof(PlayerIdentity))]
public class PlayerNameplate : MonoBehaviour
{
    // ── Config ────────────────────────────────────────────────────────────
    const float HeightOffset    = 2.4f;   // world units above transform.position
    const float FadeStartDist   = 20f;
    const float FadeEndDist     = 40f;
    const float NameFontSize    = 3.2f;
    const float ClassFontSize   = 2.2f;

    static readonly Color[] ClassColors =
    {
        new Color(0.35f, 0.75f, 1.00f),  // 0 Engineer   — blue
        new Color(1.00f, 0.80f, 0.20f),  // 1 Guardian   — gold
        new Color(0.70f, 0.40f, 1.00f),  // 2 Wraith     — purple
        new Color(0.35f, 1.00f, 0.55f),  // 3 Medic      — green
    };

    // ── State ─────────────────────────────────────────────────────────────
    PlayerIdentity  _id;
    Canvas          _canvas;
    CanvasGroup     _cg;
    TextMeshProUGUI _nameText;
    TextMeshProUGUI _classText;
    Transform       _cam;

    // ── Lifecycle ─────────────────────────────────────────────────────────

    void Awake()
    {
        _id = GetComponent<PlayerIdentity>();
        BuildCanvas();
    }

    void Start()
    {
        _cam = Camera.main?.transform;
        Refresh();
    }

    void LateUpdate()
    {
        if (_canvas == null) return;

        // Keep above the player
        _canvas.transform.position = transform.position + Vector3.up * HeightOffset;

        // Billboard — face the camera
        if (_cam != null)
            _canvas.transform.rotation = Quaternion.LookRotation(
                _canvas.transform.position - _cam.position);

        // Distance fade
        if (_cam != null && _cg != null)
        {
            float dist = Vector3.Distance(_cam.position, transform.position);
            float alpha = 1f - Mathf.InverseLerp(FadeStartDist, FadeEndDist, dist);
            _cg.alpha = alpha;
        }
    }

    // ── Public API ────────────────────────────────────────────────────────

    /// <summary>Call after SyncVars are populated to update displayed text.</summary>
    public void Refresh()
    {
        if (_id == null) return;

        // Hide on local player — you don't need to see your own name
        bool isLocal = _id.isLocalPlayer;
        _canvas.gameObject.SetActive(!isLocal);
        if (isLocal) return;

        _nameText.text = _id.playerName;
        _classText.text = _id.ClassName;

        int ci = Mathf.Clamp(_id.classIndex, 0, ClassColors.Length - 1);
        _classText.color = ClassColors[ci];
    }

    // ── Canvas builder ────────────────────────────────────────────────────

    void BuildCanvas()
    {
        // Nameplate lives in a separate GO so we can move it independently
        var cgo = new GameObject("Nameplate_" + gameObject.name,
            typeof(Canvas), typeof(CanvasGroup));
        // NOT a child of the player — we position it manually in LateUpdate
        // so the canvas doesn't inherit the player's rotation.
        cgo.transform.position = transform.position + Vector3.up * HeightOffset;

        _canvas = cgo.GetComponent<Canvas>();
        _canvas.renderMode   = RenderMode.WorldSpace;
        _canvas.sortingOrder = 10;

        // Scale the world-space canvas down to a readable but not giant size
        var rt = _canvas.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(200f, 60f);
        cgo.transform.localScale = Vector3.one * 0.01f; // 1 pixel = 0.01 world unit

        _cg = cgo.GetComponent<CanvasGroup>();
        _cg.blocksRaycasts = false; // nameplates never intercept mouse input
        _cg.interactable   = false;

        // Name label
        _nameText = MakeLabel(rt, "NameLabel",
            anchorMin: new Vector2(0f, 0.5f),
            anchorMax: new Vector2(1f, 1f),
            fontSize:  NameFontSize,
            color:     Color.white,
            bold:      true);

        // Class label
        _classText = MakeLabel(rt, "ClassLabel",
            anchorMin: new Vector2(0f, 0f),
            anchorMax: new Vector2(1f, 0.5f),
            fontSize:  ClassFontSize,
            color:     new Color(1f, 1f, 1f, 0.75f),
            bold:      false);

        // Destroy nameplate canvas when player is destroyed
        // (MonoBehaviour OnDestroy won't fire on the separate GO automatically)
        var watcher = gameObject.AddComponent<NameplateWatcher>();
        watcher.target = cgo;
    }

    static TextMeshProUGUI MakeLabel(RectTransform parent, string name,
        Vector2 anchorMin, Vector2 anchorMax,
        float fontSize, Color color, bool bold)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = rt.offsetMax = Vector2.zero;

        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text      = "";
        tmp.fontSize  = fontSize;
        tmp.color     = color;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;

        // Outline for readability against any background
        tmp.outlineWidth = 0.2f;
        tmp.outlineColor = new Color32(0, 0, 0, 200);

        return tmp;
    }
}

// ─────────────────────────────────────────────────────────────────────────────
//  NameplateWatcher  (tiny helper — destroys the canvas GO with the player)
// ─────────────────────────────────────────────────────────────────────────────

public class NameplateWatcher : MonoBehaviour
{
    public GameObject target;
    void OnDestroy() { if (target != null) Destroy(target); }
}
