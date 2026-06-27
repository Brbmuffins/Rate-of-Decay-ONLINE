using System.Collections.Generic;
using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// ═══════════════════════════════════════════════════════════════════════════
//  PlayerListUI  — "Who's Online" panel
//
//  Self-bootstrapping — no scene object needed.
//  Press P to toggle open/closed.
//  Auto-refreshes when players join or leave.
//
//  Position: top-right corner, always on top of world but below EscMenu.
//
//  How it works:
//    PlayerIdentity registers itself in OnStartClient / unregisters in
//    OnStopClient via the static PlayerIdentity.All list.
//    This panel polls that list every 2s and on every P-keypress.
// ═══════════════════════════════════════════════════════════════════════════

public class PlayerListUI : MonoBehaviour
{
    // ── Auto-bootstrap ────────────────────────────────────────────────────
    static PlayerListUI _instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoCreate()
    {
        if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
            return;
        if (_instance != null) return;

        var go = new GameObject("PlayerListUI");
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<PlayerListUI>();
    }

    // ── Class colours — match PlayerNameplate ────────────────────────────
    static readonly string[] ClassColors =
    {
        "#59BFFF", // Engineer  — blue
        "#FFCC33", // Guardian  — gold
        "#B366FF", // Wraith    — purple
        "#59FF8C", // Medic     — green
    };

    // ── UI ────────────────────────────────────────────────────────────────
    Canvas          _canvas;
    CanvasGroup     _cg;
    GameObject      _panel;
    TextMeshProUGUI _headerText;
    Transform       _rowContainer;

    bool  _open    = true;   // visible by default so it's obvious on first connect
    float _refreshTimer;
    const float RefreshInterval = 2f;

    // ── Lifecycle ─────────────────────────────────────────────────────────

    void Awake()
    {
        if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
        { enabled = false; return; }

        BuildUI();
        SetOpen(_open);
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        // Don't intercept P while typing
        var sel = UnityEngine.EventSystems.EventSystem.current?.currentSelectedGameObject;
        bool typing = sel != null && sel.GetComponent<TMPro.TMP_InputField>() != null;

        if (!typing && kb.pKey.wasPressedThisFrame)
            SetOpen(!_open);

        if (!_open) return;

        _refreshTimer -= Time.unscaledDeltaTime;
        if (_refreshTimer <= 0f)
        {
            Refresh();
            _refreshTimer = RefreshInterval;
        }
    }

    // ── Public ────────────────────────────────────────────────────────────

    /// <summary>Call from PlayerIdentity.OnStartClient / OnStopClient to trigger an immediate refresh.</summary>
    public static void RequestRefresh() => _instance?.Refresh();

    // ── Core ──────────────────────────────────────────────────────────────

    void Refresh()
    {
        // Clear old rows
        foreach (Transform child in _rowContainer)
            Destroy(child.gameObject);

        var identities = Object.FindObjectsByType<PlayerIdentity>(FindObjectsSortMode.None);

        _headerText.text = $"ONLINE  <size=10><color=#64748b>{identities.Length} player{(identities.Length == 1 ? "" : "s")}</color></size>";

        if (identities.Length == 0)
        {
            AddRow("—", "#475569", "");
            return;
        }

        // Sort: local player first, then alphabetical
        var sorted = new List<PlayerIdentity>(identities);
        sorted.Sort((a, b) =>
        {
            if (a.isLocalPlayer) return -1;
            if (b.isLocalPlayer) return  1;
            return string.Compare(a.playerName, b.playerName,
                System.StringComparison.OrdinalIgnoreCase);
        });

        foreach (var id in sorted)
        {
            int ci   = Mathf.Clamp(id.classIndex, 0, ClassColors.Length - 1);
            string nameCol  = id.isLocalPlayer ? "#FFFFFF" : "#CBD5E1";
            string classCol = ClassColors[ci];
            string you      = id.isLocalPlayer ? " <color=#fbbf24>★</color>" : "";

            AddRow(id.playerName + you, nameCol, id.ClassName, classCol);
        }
    }

    void AddRow(string name, string nameColor, string className, string classColor = "#64748b")
    {
        // Row background
        var rowGO = new GameObject("Row", typeof(RectTransform), typeof(Image));
        rowGO.transform.SetParent(_rowContainer, false);
        rowGO.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.03f);

        var rowRt = rowGO.GetComponent<RectTransform>();
        rowRt.sizeDelta = new Vector2(0f, 22f);

        // Name label
        var nameLbl = MakeLabel(rowRt, "Name",
            new Vector2(0.04f, 0f), new Vector2(0.62f, 1f),
            $"<color={nameColor}>{name}</color>", 11f, TextAlignmentOptions.MidlineLeft);

        // Class label
        if (!string.IsNullOrEmpty(className))
            MakeLabel(rowRt, "Class",
                new Vector2(0.64f, 0f), new Vector2(0.98f, 1f),
                $"<color={classColor}>{className}</color>", 10f, TextAlignmentOptions.MidlineRight);
    }

    // ── UI helpers ────────────────────────────────────────────────────────

    void SetOpen(bool open)
    {
        _open = open;
        _cg.alpha          = open ? 1f : 0f;
        _cg.blocksRaycasts = false; // never block mouse input
        _cg.interactable   = false;
        if (open) Refresh();
    }

    void BuildUI()
    {
        // Canvas
        var cgo = new GameObject("PlayerListCanvas",
            typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
        DontDestroyOnLoad(cgo);

        _canvas = cgo.GetComponent<Canvas>();
        _canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 50; // below chat (100), EscMenu (200)

        var scaler = cgo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight  = 0.5f;

        _cg = cgo.GetComponent<CanvasGroup>();
        _cg.blocksRaycasts = false;
        _cg.interactable   = false;

        var root = _canvas.GetComponent<RectTransform>();

        // Panel — top-right corner, fixed width, grows downward
        _panel = new GameObject("PlayerListPanel", typeof(RectTransform), typeof(Image),
            typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        _panel.transform.SetParent(root, false);

        var panelImg = _panel.GetComponent<Image>();
        panelImg.color = new Color(0.04f, 0.03f, 0.12f, 0.88f);

        var panelRt = _panel.GetComponent<RectTransform>();
        panelRt.anchorMin = new Vector2(1f, 1f);
        panelRt.anchorMax = new Vector2(1f, 1f);
        panelRt.pivot     = new Vector2(1f, 1f);
        panelRt.anchoredPosition = new Vector2(-12f, -12f);
        panelRt.sizeDelta = new Vector2(200f, 0f); // width fixed, height auto

        var vlg = _panel.GetComponent<VerticalLayoutGroup>();
        vlg.padding           = new RectOffset(8, 8, 6, 8);
        vlg.spacing           = 2f;
        vlg.childAlignment    = TextAnchor.UpperLeft;
        vlg.childControlWidth = true;
        vlg.childForceExpandWidth = true;
        vlg.childControlHeight    = true;
        vlg.childForceExpandHeight = false;

        var csf = _panel.GetComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // Header
        var headerGO = new GameObject("Header", typeof(RectTransform), typeof(TextMeshProUGUI),
            typeof(LayoutElement));
        headerGO.transform.SetParent(_panel.transform, false);
        headerGO.GetComponent<LayoutElement>().minHeight = 20f;

        _headerText = headerGO.GetComponent<TextMeshProUGUI>();
        _headerText.text      = "ONLINE";
        _headerText.fontSize  = 11f;
        _headerText.color     = new Color(0.5f, 0.75f, 1f);
        _headerText.fontStyle = FontStyles.Bold;
        _headerText.richText  = true;
        _headerText.alignment = TextAlignmentOptions.MidlineLeft;

        // Thin divider
        var divGO = new GameObject("Divider", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        divGO.transform.SetParent(_panel.transform, false);
        divGO.GetComponent<Image>().color = new Color(0.2f, 0.2f, 0.35f, 1f);
        divGO.GetComponent<LayoutElement>().minHeight = 1f;

        // Row container
        var containerGO = new GameObject("Rows", typeof(RectTransform),
            typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        containerGO.transform.SetParent(_panel.transform, false);
        _rowContainer = containerGO.transform;

        var rvlg = containerGO.GetComponent<VerticalLayoutGroup>();
        rvlg.spacing              = 1f;
        rvlg.childAlignment       = TextAnchor.UpperLeft;
        rvlg.childControlWidth    = true;
        rvlg.childForceExpandWidth = true;
        rvlg.childControlHeight   = true;
        rvlg.childForceExpandHeight = false;

        var rcsf = containerGO.GetComponent<ContentSizeFitter>();
        rcsf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // Hint footer
        var hintGO = new GameObject("Hint", typeof(RectTransform), typeof(TextMeshProUGUI),
            typeof(LayoutElement));
        hintGO.transform.SetParent(_panel.transform, false);
        hintGO.GetComponent<LayoutElement>().minHeight = 14f;
        var hint = hintGO.GetComponent<TextMeshProUGUI>();
        hint.text      = "<color=#334155>[P] toggle</color>";
        hint.fontSize  = 9f;
        hint.color     = new Color(0.4f, 0.4f, 0.5f);
        hint.alignment = TextAlignmentOptions.MidlineRight;
        hint.richText  = true;
    }

    static TextMeshProUGUI MakeLabel(RectTransform parent, string name,
        Vector2 anchorMin, Vector2 anchorMax,
        string text, float fontSize, TextAlignmentOptions alignment)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        var tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text      = text;
        tmp.fontSize  = fontSize;
        tmp.richText  = true;
        tmp.alignment = alignment;
        return tmp;
    }
}
