using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// ═══════════════════════════════════════════════════════════════════════════
//  EscMenu
//  Press Escape → pause overlay with Resume / Logout / Quit.
//  Attach to a persistent GameObject (e.g. the NetworkManager object).
//  Safe on dedicated server — bails out early if headless.
// ═══════════════════════════════════════════════════════════════════════════

public class EscMenu : MonoBehaviour
{
    // ── Auto-bootstrap ────────────────────────────────────────────────────
    // Creates itself once on game start — no manual prefab wiring needed.
    static EscMenu _instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoCreate()
    {
        if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
            return; // headless server — skip
        if (_instance != null) return;

        var go = new GameObject("EscMenu");
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<EscMenu>();
    }


    // ── UI ────────────────────────────────────────────────────────────────
    Canvas      _canvas;
    GameObject  _panel;
    bool        _open;

    // ── Lifecycle ─────────────────────────────────────────────────────────

    void Awake()
    {
        // Never run on headless dedicated server
        if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
        {
            enabled = false;
            return;
        }
        BuildUI();
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;
        if (!kb.escapeKey.wasPressedThisFrame) return;

        // If chat is open, let RodChatManager handle Escape (closes chat).
        // Don't open the ESC menu at the same time.
        if (RodChatManager.Instance != null && RodChatManager.Instance.IsOpen)
            return;

        // If a chat / input field is focused via EventSystem, first ESC defocuses it.
        var es  = UnityEngine.EventSystems.EventSystem.current;
        var sel = es?.currentSelectedGameObject;
        if (sel != null && sel.GetComponent<TMP_InputField>() != null)
        {
            es.SetSelectedGameObject(null);
            return;
        }

        SetOpen(!_open);
    }

    // ── Actions ───────────────────────────────────────────────────────────

    void Resume()   => SetOpen(false);

    void Logout()
    {
        SetOpen(false);

        // Re-enable cursor before scene transition so login screen isn't mouse-locked
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;

        if (NetworkServer.active && NetworkClient.isConnected)
            NetworkManager.singleton.StopHost();
        else if (NetworkClient.isConnected)
            NetworkManager.singleton.StopClient();
        else if (NetworkServer.active)
            NetworkManager.singleton.StopServer();
        // RodNetworkManager.Awake() sets offlineScene = LoginScene, so Mirror
        // navigates there automatically. Do NOT also call SceneManager.LoadScene()
        // here — that double-loads the scene and can hang the editor.
    }

    void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    void SetOpen(bool open)
    {
        _open = open;
        _panel.SetActive(open);

        // Enable/disable canvas raycasting with the panel
        var cg = _canvas.GetComponent<CanvasGroup>();
        if (cg != null) { cg.blocksRaycasts = open; cg.interactable = open; }

        if (open)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible   = true;
        }
    }

    // ── Procedural UI ─────────────────────────────────────────────────────

    void BuildUI()
    {
        // Canvas
        var cgo = new GameObject("EscMenuCanvas",
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
        DontDestroyOnLoad(cgo);

        _canvas = cgo.GetComponent<Canvas>();
        _canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 200; // on top of chat (100) and GM console (999 only when open)

        var scaler = cgo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight  = 0.5f;

        var root = _canvas.GetComponent<RectTransform>();

        // _panel is the full-screen container — hiding it removes EVERYTHING including
        // the overlay, so nothing blocks clicks when the menu is closed.
        _panel = MakeRect("EscPanel", root, Vector2.zero, Vector2.one);

        // Dark screen overlay (child of _panel, so hidden with it)
        var overlay = MakeRect("Overlay", _panel.GetComponent<RectTransform>(), Vector2.zero, Vector2.one);
        Img(overlay, new Color(0f, 0f, 0f, 0.6f));

        // Centred card (also child of _panel)
        var card = MakeRect("Card", _panel.GetComponent<RectTransform>(),
            new Vector2(0.35f, 0.3f), new Vector2(0.65f, 0.7f));
        Img(card, new Color(0.04f, 0.03f, 0.12f, 0.97f));
        var cardRt = card.GetComponent<RectTransform>();

        // Title
        var title = MakeTmp("Title", cardRt,
            new Vector2(0.05f, 0.78f), new Vector2(0.95f, 0.95f));
        title.text      = "MENU";
        title.fontSize  = 22f;
        title.color     = new Color(0.5f, 0.8f, 1f);
        title.fontStyle = FontStyles.Bold;
        title.alignment = TextAlignmentOptions.Center;

        // Divider
        var div = MakeRect("Divider", cardRt,
            new Vector2(0.05f, 0.76f), new Vector2(0.95f, 0.77f));
        Img(div, new Color(0.2f, 0.2f, 0.35f, 1f));

        // Buttons
        MakeButton("Resume",   cardRt, new Vector2(0.1f, 0.54f), new Vector2(0.9f, 0.68f),
            new Color(0.08f, 0.5f, 0.18f), Resume);

        MakeButton("Logout",   cardRt, new Vector2(0.1f, 0.35f), new Vector2(0.9f, 0.49f),
            new Color(0.45f, 0.35f, 0.05f), Logout);

        MakeButton("Quit Game", cardRt, new Vector2(0.1f, 0.15f), new Vector2(0.9f, 0.29f),
            new Color(0.5f, 0.05f, 0.05f), Quit);

        _panel.SetActive(false);

        // Belt-and-suspenders: also disable raycasts on the canvas itself when hidden
        var cg = cgo.GetComponent<CanvasGroup>();
        cg.blocksRaycasts = false;
        cg.interactable   = false;
    }

    // ── UI helpers ────────────────────────────────────────────────────────

    static GameObject MakeRect(string name, RectTransform parent,
        Vector2 anchorMin, Vector2 anchorMax)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        return go;
    }

    static void Img(GameObject go, Color col)
    {
        var img = go.GetComponent<Image>() ?? go.AddComponent<Image>();
        img.color = col;
    }

    static TextMeshProUGUI MakeTmp(string name, RectTransform parent,
        Vector2 anchorMin, Vector2 anchorMax)
    {
        var go = MakeRect(name, parent, anchorMin, anchorMax);
        return go.AddComponent<TextMeshProUGUI>();
    }

    void MakeButton(string label, RectTransform parent,
        Vector2 anchorMin, Vector2 anchorMax,
        Color bgColor, UnityEngine.Events.UnityAction onClick)
    {
        var go = MakeRect(label + "Btn", parent, anchorMin, anchorMax);
        Img(go, bgColor);

        var txt = MakeTmp(label + "Lbl", go.GetComponent<RectTransform>(),
            Vector2.zero, Vector2.one);
        txt.text      = label.ToUpper();
        txt.fontSize  = 14f;
        txt.color     = Color.white;
        txt.fontStyle = FontStyles.Bold;
        txt.alignment = TextAlignmentOptions.Center;

        var btn = go.AddComponent<Button>();
        var colors = btn.colors;
        colors.normalColor      = bgColor;
        colors.highlightedColor = bgColor * 1.35f;
        colors.pressedColor     = bgColor * 0.7f;
        btn.colors = colors;
        btn.onClick.AddListener(onClick);
    }
}
