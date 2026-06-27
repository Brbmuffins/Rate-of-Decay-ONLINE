using System.Collections;
using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

// ═══════════════════════════════════════════════════════════════════════════
//  LoginManager
//  Builds the entire login UI in code — no manual scene wiring needed.
//  Attach to a GameObject in the Login scene.
//
//  Creates:
//    • Full-screen canvas with dark vignette overlay
//    • Top: animated game title + subtitle
//    • Center: dark glassmorphism login panel with glowing border
//    • Bottom: server status pill + version label
//
//  Scene Setup:
//    1. Create a new scene "Login"
//    2. Add a Camera: pos (0,2,-10), rot (8,0,0)
//    3. Set skybox to FS013_Night.mat (Lighting → Environment)
//    4. Create empty GameObject "LoginManager" → attach this script
//    5. Create empty GameObject "LoginScreenVFX" → attach LoginScreenVFX.cs
//    6. Fill the VFX prefab slots in LoginScreenVFX Inspector
//    7. Ambient audio: drag AmbientNatureNightRainy.wav into LoginScreenVFX.ambientLoop
// ═══════════════════════════════════════════════════════════════════════════

public class LoginManager : MonoBehaviour
{
    [Header("Server")]
    public string authServerURL  = "http://15.204.243.36:3000";
    public string gameScene      = "GameWorld"; // fallback only — Mirror loads this via NetworkManager.Online Scene

    [Header("Optional — wire if you want button-click VFX")]
    public LoginScreenVFX sceneVFX;

    // ── Color palette ──────────────────────────────────────────────────────
    static readonly Color BG           = new Color(0.03f, 0.02f, 0.07f, 0.92f);
    static readonly Color PanelColor   = new Color(0.06f, 0.04f, 0.12f, 0.88f);
    static readonly Color AccentCyan   = new Color(0.20f, 0.75f, 1.00f, 1.00f);
    static readonly Color AccentGlow   = new Color(0.15f, 0.55f, 0.85f, 0.35f);
    static readonly Color TextPrimary  = new Color(0.92f, 0.90f, 0.95f, 1.00f);
    static readonly Color TextDim      = new Color(0.55f, 0.53f, 0.62f, 1.00f);
    static readonly Color TextSuccess  = new Color(0.30f, 1.00f, 0.55f, 1.00f);
    static readonly Color TextError    = new Color(1.00f, 0.32f, 0.32f, 1.00f);
    static readonly Color BorderColor  = new Color(0.20f, 0.65f, 1.00f, 0.60f);
    static readonly Color InputBG      = new Color(0.08f, 0.06f, 0.16f, 0.95f);
    static readonly Color BtnLogin     = new Color(0.10f, 0.45f, 0.80f, 1.00f);
    static readonly Color BtnRegister  = new Color(0.10f, 0.18f, 0.35f, 1.00f);

    // ── Runtime refs ───────────────────────────────────────────────────────
    private Canvas          _canvas;
    private TMP_InputField  _userInput, _passInput;
    private TMP_InputField  _regUserInput, _regEmailInput, _regPassInput;
    private TMP_InputField  _serverInput;
    private TMP_Text        _statusText, _regStatusText;
    private TextMeshProUGUI _titleText;
    private GameObject      _loginPanel, _registerPanel;
    private Image           _borderTop, _borderBot;
    private bool            _busy;
    private float           _titlePulseT;

    // ── Unity ──────────────────────────────────────────────────────────────

    void Awake()
    {
        BuildCanvas();
    }

    void Update()
    {
        // Pulse the title glow
        _titlePulseT += Time.deltaTime * 1.1f;
        if (_titleText != null)
        {
            float pulse = 0.85f + Mathf.Sin(_titlePulseT) * 0.15f;
            _titleText.color = new Color(AccentCyan.r, AccentCyan.g, AccentCyan.b, pulse);
        }

        // Animate the cyan border lines (subtle brightness breathe)
        if (_borderTop != null)
        {
            float b = 0.4f + Mathf.Sin(_titlePulseT * 1.3f) * 0.15f;
            _borderTop.color = new Color(BorderColor.r, BorderColor.g, BorderColor.b, b);
            _borderBot.color = new Color(BorderColor.r, BorderColor.g, BorderColor.b, b * 0.6f);
        }

        // Enter key support
        if (!_busy && Keyboard.current != null && Keyboard.current.enterKey.wasPressedThisFrame)
        {
            if (_loginPanel.activeSelf)    OnLoginClicked();
            else if (_registerPanel.activeSelf) OnRegisterClicked();
        }
    }

    // ── UI Build ───────────────────────────────────────────────────────────

    void BuildCanvas()
    {
        // Root canvas
        GameObject cgo = new GameObject("LoginCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        _canvas = cgo.GetComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 10;
        var scaler = cgo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        RectTransform root = _canvas.GetComponent<RectTransform>();

        // Dark vignette overlay — adds atmospheric depth over the 3D scene
        MakeImage(root, "Vignette", new Color(0f, 0f, 0.04f, 0.55f), Stretch());

        // ── Top: Game Title ──
        BuildTitle(root);

        // ── Center-Left: Login Panel ──
        _loginPanel = BuildLoginPanel(root);

        // ── Center-Left: Register Panel (hidden by default) ──
        _registerPanel = BuildRegisterPanel(root);
        _registerPanel.SetActive(false);

        // ── Bottom: Status bar ──
        BuildBottomBar(root);
    }

    void BuildTitle(RectTransform root)
    {
        // Game title — large, glowing, letter-spaced
        GameObject titleGO = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI));
        titleGO.transform.SetParent(root, false);
        _titleText = titleGO.GetComponent<TextMeshProUGUI>();
        _titleText.text       = "RATE OF DECAY";
        _titleText.fontSize   = 72f;
        _titleText.fontStyle  = FontStyles.Bold;
        _titleText.color      = AccentCyan;
        _titleText.alignment  = TextAlignmentOptions.Center;
        _titleText.characterSpacing = 18f;
        var rt = titleGO.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.1f, 0.78f);
        rt.anchorMax = new Vector2(0.9f, 0.96f);
        rt.offsetMin = rt.offsetMax = Vector2.zero;

        // "ONLINE" subtitle
        GameObject subGO = new GameObject("Subtitle", typeof(RectTransform), typeof(TextMeshProUGUI));
        subGO.transform.SetParent(root, false);
        var sub = subGO.GetComponent<TextMeshProUGUI>();
        sub.text      = "O N L I N E";
        sub.fontSize  = 22f;
        sub.color     = new Color(TextDim.r, TextDim.g, TextDim.b, 0.85f);
        sub.alignment = TextAlignmentOptions.Center;
        sub.characterSpacing = 14f;
        var subRt = subGO.GetComponent<RectTransform>();
        subRt.anchorMin = new Vector2(0.1f, 0.72f);
        subRt.anchorMax = new Vector2(0.9f, 0.80f);
        subRt.offsetMin = subRt.offsetMax = Vector2.zero;

        // Thin cyan divider line under subtitle
        GameObject line = new GameObject("TitleLine", typeof(RectTransform), typeof(Image));
        line.transform.SetParent(root, false);
        line.GetComponent<Image>().color = new Color(AccentCyan.r, AccentCyan.g, AccentCyan.b, 0.4f);
        var lineRt = line.GetComponent<RectTransform>();
        lineRt.anchorMin = new Vector2(0.30f, 0.715f);
        lineRt.anchorMax = new Vector2(0.70f, 0.718f);
        lineRt.offsetMin = lineRt.offsetMax = Vector2.zero;
    }

    GameObject BuildLoginPanel(RectTransform root)
    {
        // Panel sits center-left — gives breathing room for the 3D scene
        GameObject panel = new GameObject("LoginPanel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(root, false);
        panel.GetComponent<Image>().color = PanelColor;
        var rt = panel.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.30f, 0.24f);
        rt.anchorMax = new Vector2(0.70f, 0.72f);
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        RectTransform panelRt = rt;

        // Glowing top border
        _borderTop = MakeImage(panelRt, "BorderTop", BorderColor,
            new Vector2(0f, 0.975f), new Vector2(1f, 1f));

        // Glowing bottom border (dimmer)
        _borderBot = MakeImage(panelRt, "BorderBot", new Color(BorderColor.r, BorderColor.g, BorderColor.b, 0.3f),
            new Vector2(0f, 0f), new Vector2(1f, 0.025f));

        // Left accent bar
        MakeImage(panelRt, "BorderLeft", new Color(AccentCyan.r, AccentCyan.g, AccentCyan.b, 0.25f),
            new Vector2(0f, 0f), new Vector2(0.004f, 1f));

        // Panel header label
        var header = MakeLabel(panelRt, "Header", "AUTHENTICATION", 13f, FontStyles.Bold, TextDim);
        header.rectTransform.anchorMin = new Vector2(0.05f, 0.84f);
        header.rectTransform.anchorMax = new Vector2(0.95f, 0.93f);
        header.rectTransform.offsetMin = header.rectTransform.offsetMax = Vector2.zero;
        header.alignment = TextAlignmentOptions.MidlineLeft;
        header.characterSpacing = 6f;

        // Thin separator
        MakeImage(panelRt, "Sep", new Color(AccentCyan.r, AccentCyan.g, AccentCyan.b, 0.18f),
            new Vector2(0.04f, 0.825f), new Vector2(0.96f, 0.83f));

        // Username field
        _userInput = BuildInputField(panelRt, "USERNAME", new Vector2(0.05f, 0.62f), new Vector2(0.95f, 0.78f), false);

        // Password field
        _passInput = BuildInputField(panelRt, "PASSWORD", new Vector2(0.05f, 0.40f), new Vector2(0.95f, 0.56f), true);

        // Server IP field — small, below password
        var serverLabel = MakeLabel(panelRt, "ServerLabel", "GAME SERVER", 9f, FontStyles.Bold,
            new Color(AccentCyan.r, AccentCyan.g, AccentCyan.b, 0.55f));
        serverLabel.rectTransform.anchorMin = new Vector2(0.05f, 0.305f);
        serverLabel.rectTransform.anchorMax = new Vector2(0.55f, 0.365f);
        serverLabel.rectTransform.offsetMin = serverLabel.rectTransform.offsetMax = Vector2.zero;
        serverLabel.characterSpacing = 4f;

        _serverInput = BuildInputField(panelRt, "IP", new Vector2(0.05f, 0.235f), new Vector2(0.95f, 0.31f), false);
        _serverInput.text = PlayerPrefs.GetString("game_server_ip", "15.204.243.36");

        // Status text (shifted up slightly to make room)
        _statusText = MakeLabel(panelRt, "Status", "", 12f, FontStyles.Normal, TextDim);
        _statusText.rectTransform.anchorMin = new Vector2(0.05f, 0.16f);
        _statusText.rectTransform.anchorMax = new Vector2(0.95f, 0.235f);
        _statusText.rectTransform.offsetMin = _statusText.rectTransform.offsetMax = Vector2.zero;
        _statusText.alignment = TextAlignmentOptions.Center;
        _statusText.textWrappingMode = TextWrappingModes.Normal;

        // Login button
        BuildButton(panelRt, "ENTER WORLD", BtnLogin,
            new Vector2(0.05f, 0.02f), new Vector2(0.95f, 0.155f), OnLoginClicked);

        // Register link
        var regLink = BuildFlatButton(panelRt, "No account? REGISTER",
            new Vector2(0.05f, -0.06f), new Vector2(0.95f, 0.02f), ShowRegister);
        regLink.color = TextDim;
        regLink.fontSize = 11f;

#if UNITY_EDITOR
        // Dev HOST button — editor only, bypasses auth for local testing
        BuildButton(panelRt, "▶ HOST (DEV)", new Color(0.08f, 0.35f, 0.12f, 1f),
            new Vector2(0.05f, -0.22f), new Vector2(0.95f, -0.07f), OnHostClicked);
#endif

        return panel;
    }

    GameObject BuildRegisterPanel(RectTransform root)
    {
        GameObject panel = new GameObject("RegisterPanel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(root, false);
        panel.GetComponent<Image>().color = PanelColor;
        var rt = panel.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.30f, 0.18f);
        rt.anchorMax = new Vector2(0.70f, 0.77f);
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        RectTransform panelRt = rt;

        // Borders
        MakeImage(panelRt, "BorderTop", BorderColor, new Vector2(0f, 0.975f), new Vector2(1f, 1f));
        MakeImage(panelRt, "BorderLeft", new Color(AccentCyan.r, AccentCyan.g, AccentCyan.b, 0.25f),
            new Vector2(0f, 0f), new Vector2(0.004f, 1f));

        var header = MakeLabel(panelRt, "Header", "CREATE ACCOUNT", 13f, FontStyles.Bold, TextDim);
        header.rectTransform.anchorMin = new Vector2(0.05f, 0.87f);
        header.rectTransform.anchorMax = new Vector2(0.95f, 0.95f);
        header.rectTransform.offsetMin = header.rectTransform.offsetMax = Vector2.zero;
        header.alignment = TextAlignmentOptions.MidlineLeft;
        header.characterSpacing = 6f;

        MakeImage(panelRt, "Sep", new Color(AccentCyan.r, AccentCyan.g, AccentCyan.b, 0.18f),
            new Vector2(0.04f, 0.855f), new Vector2(0.96f, 0.86f));

        _regUserInput  = BuildInputField(panelRt, "USERNAME", new Vector2(0.05f, 0.70f), new Vector2(0.95f, 0.83f), false);
        _regEmailInput = BuildInputField(panelRt, "EMAIL",    new Vector2(0.05f, 0.51f), new Vector2(0.95f, 0.64f), false);
        _regPassInput  = BuildInputField(panelRt, "PASSWORD", new Vector2(0.05f, 0.32f), new Vector2(0.95f, 0.45f), true);

        _regStatusText = MakeLabel(panelRt, "Status", "", 12f, FontStyles.Normal, TextDim);
        _regStatusText.rectTransform.anchorMin = new Vector2(0.05f, 0.20f);
        _regStatusText.rectTransform.anchorMax = new Vector2(0.95f, 0.31f);
        _regStatusText.rectTransform.offsetMin = _regStatusText.rectTransform.offsetMax = Vector2.zero;
        _regStatusText.alignment = TextAlignmentOptions.Center;
        _regStatusText.textWrappingMode = TextWrappingModes.Normal;

        BuildButton(panelRt, "CREATE ACCOUNT", BtnLogin,
            new Vector2(0.05f, 0.09f), new Vector2(0.95f, 0.19f), OnRegisterClicked);

        var backLink = BuildFlatButton(panelRt, "← Back to login",
            new Vector2(0.05f, 0.02f), new Vector2(0.95f, 0.09f), ShowLogin);
        backLink.color = TextDim;
        backLink.fontSize = 11f;

        return panel;
    }

    void BuildBottomBar(RectTransform root)
    {
        // Server status pill
        GameObject pill = new GameObject("ServerPill", typeof(RectTransform), typeof(Image));
        pill.transform.SetParent(root, false);
        pill.GetComponent<Image>().color = new Color(0.05f, 0.18f, 0.06f, 0.85f);
        var pillRt = pill.GetComponent<RectTransform>();
        pillRt.anchorMin = new Vector2(0.02f, 0.02f);
        pillRt.anchorMax = new Vector2(0.22f, 0.07f);
        pillRt.offsetMin = pillRt.offsetMax = Vector2.zero;

        var dot = MakeLabel(pill.GetComponent<RectTransform>(), "Dot", "●", 10f, FontStyles.Normal,
            new Color(0.3f, 1f, 0.4f, 1f));
        dot.rectTransform.anchorMin = new Vector2(0.04f, 0f);
        dot.rectTransform.anchorMax = new Vector2(0.15f, 1f);
        dot.rectTransform.offsetMin = dot.rectTransform.offsetMax = Vector2.zero;
        dot.alignment = TextAlignmentOptions.MidlineLeft;

        var status = MakeLabel(pill.GetComponent<RectTransform>(), "StatusLabel", "SERVER ONLINE", 10f, FontStyles.Bold,
            new Color(0.4f, 0.9f, 0.5f, 1f));
        status.rectTransform.anchorMin = new Vector2(0.15f, 0f);
        status.rectTransform.anchorMax = new Vector2(0.96f, 1f);
        status.rectTransform.offsetMin = status.rectTransform.offsetMax = Vector2.zero;
        status.alignment = TextAlignmentOptions.MidlineLeft;
        status.characterSpacing = 3f;

        // Version label
        var ver = MakeLabel(root, "Version", "v0.1.0-alpha", 10f, FontStyles.Normal, TextDim);
        ver.rectTransform.anchorMin = new Vector2(0.78f, 0.02f);
        ver.rectTransform.anchorMax = new Vector2(0.98f, 0.06f);
        ver.rectTransform.offsetMin = ver.rectTransform.offsetMax = Vector2.zero;
        ver.alignment = TextAlignmentOptions.MidlineRight;
    }

    // ── Input fields ───────────────────────────────────────────────────────

    TMP_InputField BuildInputField(RectTransform parent, string placeholder, Vector2 anchorMin, Vector2 anchorMax, bool password)
    {
        GameObject go = new GameObject("Input_" + placeholder, typeof(RectTransform), typeof(Image), typeof(TMP_InputField));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = InputBG;
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = rt.offsetMax = Vector2.zero;

        // Thin cyan bottom underline — not a box, just a line
        GameObject underline = new GameObject("Underline", typeof(RectTransform), typeof(Image));
        underline.transform.SetParent(go.transform, false);
        underline.GetComponent<Image>().color = new Color(AccentCyan.r, AccentCyan.g, AccentCyan.b, 0.5f);
        var ul = underline.GetComponent<RectTransform>();
        ul.anchorMin = new Vector2(0f, 0f);
        ul.anchorMax = new Vector2(1f, 0.04f);
        ul.offsetMin = ul.offsetMax = Vector2.zero;

        // Label above
        GameObject label = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        label.transform.SetParent(go.transform, false);
        var lbl = label.GetComponent<TextMeshProUGUI>();
        lbl.text           = placeholder;
        lbl.fontSize       = 9f;
        lbl.color          = new Color(AccentCyan.r, AccentCyan.g, AccentCyan.b, 0.7f);
        lbl.characterSpacing = 4f;
        lbl.fontStyle      = FontStyles.Bold;
        var lrt = label.GetComponent<RectTransform>();
        lrt.anchorMin = new Vector2(0.03f, 0.7f);
        lrt.anchorMax = new Vector2(0.97f, 1.0f);
        lrt.offsetMin = lrt.offsetMax = Vector2.zero;

        // Placeholder text
        GameObject ph = new GameObject("Placeholder", typeof(RectTransform), typeof(TextMeshProUGUI));
        ph.transform.SetParent(go.transform, false);
        var phT = ph.GetComponent<TextMeshProUGUI>();
        phT.text      = password ? "••••••••" : "Enter " + placeholder.ToLower() + "...";
        phT.fontSize  = 14f;
        phT.color     = new Color(0.35f, 0.33f, 0.42f, 1f);
        phT.fontStyle = FontStyles.Italic;
        var phRt = ph.GetComponent<RectTransform>();
        phRt.anchorMin = new Vector2(0.03f, 0.05f);
        phRt.anchorMax = new Vector2(0.97f, 0.65f);
        phRt.offsetMin = phRt.offsetMax = Vector2.zero;

        // Input text
        GameObject txt = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        txt.transform.SetParent(go.transform, false);
        var txtT = txt.GetComponent<TextMeshProUGUI>();
        txtT.fontSize = 14f;
        txtT.color    = TextPrimary;
        var txtRt = txt.GetComponent<RectTransform>();
        txtRt.anchorMin = new Vector2(0.03f, 0.05f);
        txtRt.anchorMax = new Vector2(0.97f, 0.65f);
        txtRt.offsetMin = txtRt.offsetMax = Vector2.zero;

        // Wire the TMP_InputField
        var field = go.GetComponent<TMP_InputField>();
        field.textViewport   = txtRt;
        field.textComponent  = txtT;
        field.placeholder    = phT;
        field.caretColor     = AccentCyan;
        field.selectionColor = new Color(AccentCyan.r, AccentCyan.g, AccentCyan.b, 0.3f);
        if (password)
            field.contentType = TMP_InputField.ContentType.Password;

        return field;
    }

    // ── Buttons ─────────────────────────────────────────────────────────────

    void BuildButton(RectTransform parent, string label, Color color,
        Vector2 anchorMin, Vector2 anchorMax, UnityEngine.Events.UnityAction callback)
    {
        GameObject go = new GameObject("Btn_" + label, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        go.GetComponent<Image>().color = color;
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = rt.offsetMax = Vector2.zero;

        // Thin top border on button for depth
        GameObject border = new GameObject("TopBorder", typeof(RectTransform), typeof(Image));
        border.transform.SetParent(go.transform, false);
        border.GetComponent<Image>().color = new Color(AccentCyan.r, AccentCyan.g, AccentCyan.b, 0.5f);
        var brt = border.GetComponent<RectTransform>();
        brt.anchorMin = new Vector2(0f, 0.9f);
        brt.anchorMax = new Vector2(1f, 1f);
        brt.offsetMin = brt.offsetMax = Vector2.zero;

        GameObject txt = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        txt.transform.SetParent(go.transform, false);
        var t = txt.GetComponent<TextMeshProUGUI>();
        t.text           = label;
        t.fontSize       = 13f;
        t.fontStyle      = FontStyles.Bold;
        t.color          = TextPrimary;
        t.alignment      = TextAlignmentOptions.Center;
        t.characterSpacing = 4f;
        var trt = txt.GetComponent<RectTransform>();
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = trt.offsetMax = Vector2.zero;

        var btn = go.GetComponent<Button>();
        var colors = btn.colors;
        colors.normalColor      = Color.white;
        colors.highlightedColor = new Color(1.3f, 1.3f, 1.3f, 1f);
        colors.pressedColor     = new Color(0.7f, 0.7f, 0.8f, 1f);
        btn.colors = colors;
        btn.onClick.AddListener(callback);
    }

    TextMeshProUGUI BuildFlatButton(RectTransform parent, string label,
        Vector2 anchorMin, Vector2 anchorMax, UnityEngine.Events.UnityAction callback)
    {
        GameObject go = new GameObject("FlatBtn_" + label, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        go.GetComponent<Image>().color = Color.clear;
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        go.GetComponent<Button>().onClick.AddListener(callback);

        GameObject txt = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        txt.transform.SetParent(go.transform, false);
        var t = txt.GetComponent<TextMeshProUGUI>();
        t.text      = label;
        t.fontSize  = 12f;
        t.color     = TextDim;
        t.alignment = TextAlignmentOptions.Center;
        var trt = txt.GetComponent<RectTransform>();
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = trt.offsetMax = Vector2.zero;
        return t;
    }

    // ── Button handlers ────────────────────────────────────────────────────

    public void OnLoginClicked()
    {
        if (_busy) return;
        string user = _userInput.text.Trim();
        string pass = _passInput.text;
        if (string.IsNullOrEmpty(user) || string.IsNullOrEmpty(pass))
        {
            SetStatus(_statusText, "Enter your username and password.", false);
            return;
        }

        // Save the chosen server IP so CharacterSelectManager can apply it before StartClient()
        string ip = _serverInput != null ? _serverInput.text.Trim() : "15.204.243.36";
        if (string.IsNullOrEmpty(ip)) ip = "15.204.243.36";
        PlayerPrefs.SetString("game_server_ip", ip);
        PlayerPrefs.Save();

        StartCoroutine(LoginRoutine(user, pass));
    }

#if UNITY_EDITOR
    public void OnHostClicked()
    {
        if (_busy) return;

        // Set dev credentials — CharacterSelectManager reads these to call StartHost()
        PlayerPrefs.SetString("username", "DevPlayer");
        PlayerPrefs.SetString("jwt_token", "dev");
        PlayerPrefs.SetInt("SelectedCharacter", 0);
        PlayerPrefs.Save();

        SetStatus(_statusText, "Opening character select...", true);
        SceneManager.LoadScene("CharacterSelect");
    }
#endif

    public void OnRegisterClicked()
    {
        if (_busy) return;
        string user  = _regUserInput.text.Trim();
        string email = _regEmailInput.text.Trim();
        string pass  = _regPassInput.text;
        if (string.IsNullOrEmpty(user) || string.IsNullOrEmpty(email) || string.IsNullOrEmpty(pass))
        {
            SetStatus(_regStatusText, "All fields are required.", false);
            return;
        }
        StartCoroutine(RegisterRoutine(user, email, pass));
    }

    public void ShowLogin()
    {
        _loginPanel.SetActive(true);
        _registerPanel.SetActive(false);
        _statusText.text = "";
    }

    public void ShowRegister()
    {
        _loginPanel.SetActive(false);
        _registerPanel.SetActive(true);
        _regStatusText.text = "";
    }

    // ── Auth routines ──────────────────────────────────────────────────────

    IEnumerator LoginRoutine(string username, string password)
    {
        _busy = true;
        SetStatus(_statusText, "Authenticating...", true);

        string json = $"{{\"username\":\"{username}\",\"password\":\"{password}\"}}";
        using UnityWebRequest req = new UnityWebRequest(authServerURL + "/login", "POST");
        req.uploadHandler   = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(json));
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");

        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            string msg = TryParseError(req.downloadHandler.text) ?? "Could not reach server.";
            SetStatus(_statusText, msg, false);
            if (sceneVFX != null) sceneVFX.OnLoginFail();
            _busy = false;
            yield break;
        }

        TokenResponse res = JsonUtility.FromJson<TokenResponse>(req.downloadHandler.text);
        if (string.IsNullOrEmpty(res?.token))
        {
            SetStatus(_statusText, "Invalid response from server.", false);
            _busy = false;
            yield break;
        }

        PlayerPrefs.SetString("jwt_token", res.token);
        PlayerPrefs.SetString("username",  username);
        PlayerPrefs.Save();

        SetStatus(_statusText, "Authenticated. Loading character select...", true);
        if (sceneVFX != null) sceneVFX.OnLoginSuccess();
        yield return new WaitForSeconds(0.6f);

        // Route through CharacterSelect so the player can pick their class.
        // CharacterSelectManager will call NetworkManager.StartClient() after selection.
        SceneManager.LoadScene("CharacterSelect");
    }

    IEnumerator RegisterRoutine(string username, string email, string password)
    {
        _busy = true;
        SetStatus(_regStatusText, "Creating account...", true);

        string json = $"{{\"username\":\"{username}\",\"email\":\"{email}\",\"password\":\"{password}\"}}";
        using UnityWebRequest req = new UnityWebRequest(authServerURL + "/register", "POST");
        req.uploadHandler   = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(json));
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");

        yield return req.SendWebRequest();

        _busy = false;

        if (req.result != UnityWebRequest.Result.Success)
        {
            string msg = TryParseError(req.downloadHandler.text) ?? "Registration failed.";
            SetStatus(_regStatusText, msg, false);
            yield break;
        }

        SetStatus(_regStatusText, "Account created! Returning to login...", true);
        yield return new WaitForSeconds(1.5f);
        _regUserInput.text = username;
        ShowLogin();
        _userInput.text = username;
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    void SetStatus(TMP_Text label, string msg, bool good)
    {
        if (label == null) return;
        label.text  = msg;
        label.color = good ? TextSuccess : TextError;
    }

    string TryParseError(string json)
    {
        try { return JsonUtility.FromJson<ErrorResponse>(json)?.message; }
        catch { return null; }
    }

    Image MakeImage(RectTransform parent, string name, Color color, Vector2 anchorMin, Vector2 anchorMax)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = color;
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        return img;
    }

    TextMeshProUGUI MakeLabel(RectTransform parent, string name, string text, float size,
        FontStyles style, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<TextMeshProUGUI>();
        t.text      = text;
        t.fontSize  = size;
        t.fontStyle = style;
        t.color     = color;
        return t;
    }

    RectTransform Stretch()
    {
        // returns dummy anchors — used inline
        return null;
    }

    Image MakeImage(RectTransform parent, string name, Color color, RectTransform _dummy)
    {
        // Overload for full-stretch vignette
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = color;
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        return img;
    }

    [System.Serializable] class TokenResponse { public string token; }
    [System.Serializable] class ErrorResponse  { public string message; }
}
