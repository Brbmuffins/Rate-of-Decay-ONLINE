using System;
using Mirror;
using UnityEngine;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

// ═══════════════════════════════════════════════════════════════════════════════
//  CharacterSelectManager
//  Attach to an empty GameObject in CharacterSelect.unity.
//  Builds the entire character selection screen in code — no prefabs needed.
//
//  Layout:
//    Left  280px — class list buttons
//    Center       — 3D model preview via RenderTexture
//    Right 430px  — class name / blurb / abilities / ENTER WORLD
//
//  Preview: a secondary camera renders only layer 31 to a RenderTexture that
//  is displayed in a RawImage. The main camera excludes layer 31.
//
//  VFX load with AssetDatabase in Editor play mode. Builds need addressables.
// ═══════════════════════════════════════════════════════════════════════════════

[AddComponentMenu("RoD/UI/Character Select Manager")]
public class CharacterSelectManager : MonoBehaviour
{
    // ── Class Definitions ────────────────────────────────────────────────────

    struct ClassDef
    {
        public int    idx;
        public string name, role, blurb, passive;
        public string[] abilities;
        public Color  accent;
        public string vfxA, vfxB;
    }

    static readonly ClassDef[] Classes =
    {
        new ClassDef
        {
            idx = 0, name = "ENGINEER", role = "Damage  ·  Control",
            blurb = "Builds the killzone before enemies arrive. Turrets, shock mines, and arc cannon work in concert. Set up before the fight — if you're reacting, you're losing.",
            abilities = new[] { "Sentinel Drop", "Shock Mine", "Arc Cannon", "Dark Blast" },
            passive = "Overengineered — deployables stack output in overlapping zones",
            accent = new Color(1f, 0.55f, 0.05f),
            vfxA = "Assets/brbmuffins Studio/brbmuffins Magic Pack/Prefabs/Character auras/Plexus.prefab",
            vfxB = "Assets/brbmuffins Technologies/brbmuffins Particle Pack/EffectExamples/Misc Effects/Prefabs/ElectricalSparks.prefab",
        },
        new ClassDef
        {
            idx = 1, name = "GUARDIAN", role = "Tank  ·  Crowd Control",
            blurb = "Takes hits for the team, punishes enemies who focus them. Iron Tether locks one target for 5 full seconds. Breach Slam staggers everything nearby. The anvil everything else is smashed against.",
            abilities = new[] { "Breach Slam", "Iron Tether", "Barrier Wall", "Shield Bash" },
            passive = "Threat Protocol — damage stacks DR bonus and redirects enemy aggro",
            accent = new Color(0.15f, 0.65f, 1f),
            vfxA = "Assets/brbmuffins Studio/brbmuffins Magic Pack/Prefabs/Character auras/Lightning aura.prefab",
            vfxB = "Assets/brbmuffins Dark Arts/brbmuffins Fantasy Pack/Prefabs/Glowing orbs.prefab",
        },
        new ClassDef
        {
            idx = 2, name = "WRAITH", role = "Stealth  ·  Burst",
            blurb = "Null fields, shadow relays, backstab burst. Invisible until it's too late. A Wraith who knows when to detonate beats button-mashers every single time.",
            abilities = new[] { "Shadow Step", "Null Field", "Phase Strike", "Shadow Relay" },
            passive = "Bounty System — elite kills instantly reset all cooldowns",
            accent = new Color(0.6f, 0.1f, 1f),
            vfxA = "Assets/brbmuffins Dark Arts/brbmuffins Fantasy Pack/Prefabs/Effects normal/Death magic circle.prefab",
            vfxB = "Assets/brbmuffins Dark Arts/brbmuffins Fantasy Pack/Prefabs/Feathers buff.prefab",
        },
        new ClassDef
        {
            idx = 3, name = "MEDIC", role = "Support  ·  Sustain",
            blurb = "Emergency revivals, heal shields, and sustained team uptime. The difference between a wipe and a clutch win. No one notices the Medic — until they're down and need one.",
            abilities = new[] { "Mend", "Trauma Shield", "Mass Revive", "Triage Pulse" },
            passive = "Triage Loop — each ally healed triggers 8% self-heal on the Medic",
            accent = new Color(0.1f, 1f, 0.45f),
            vfxA = "Assets/brbmuffins Studio/brbmuffins Magic Pack/Prefabs/Magic circles/Healing circle.prefab",
            vfxB = "Assets/brbmuffins Studio/brbmuffins Magic Pack/Prefabs/Character auras/Healing.prefab",
        },
        new ClassDef
        {
            idx = 4, name = "PHASER", role = "Assassin  ·  Mobility",
            blurb = "Phase shifts and singularity pulls. Appears where least expected — kills before detection. Every 6 casts triggers a ×1.4 damage window. Count your casts or play badly.",
            abilities = new[] { "Phase Shift", "Singularity", "Blink Strike", "Void Pull" },
            passive = "Phase Charge — every 6 ability casts triggers ×1.4 damage multiplier",
            accent = new Color(0.88f, 0.88f, 1f),
            vfxA = "Assets/brbmuffins Dark Arts/brbmuffins Fantasy Pack/Prefabs/Effects normal/Magic circle.prefab",
            vfxB = "Assets/brbmuffins Studio/brbmuffins Magic Pack/Prefabs/Character auras/Star aura.prefab",
        },
    };

    // ── Constants ────────────────────────────────────────────────────────────

    const string MODEL_PATH = "Assets/Game/Characters/Engineer/Model/Idle.fbx";
    const int    PREV_LAYER = 31;       // "CharacterPreview" — no layer name needed
    const float  LEFT_W     = 280f;
    const float  RIGHT_W    = 430f;
    const float  HEADER_H   = 40f;
    const float  FOOTER_H   = 34f;
    const float  BTN_H      = 84f;

    // ── Runtime state ────────────────────────────────────────────────────────

    int           _sel = 0;
    Button[]      _classBtns;
    Text          _nameText, _roleText, _blurbText, _passiveText;
    Text[]        _abilityTexts = new Text[4];
    Image[]       _slotHighlights = new Image[4];
    Button        _enterBtn;
    Text          _enterLabel;
    RawImage      _previewImg;
    Camera        _previewCam;
    RenderTexture _rt;
    GameObject    _previewRoot, _vfxA, _vfxB;
    float         _rotY;

    // ── Lifecycle ────────────────────────────────────────────────────────────

    void Awake()
    {
        // Main camera must not see preview layer objects
        var main = Camera.main;
        if (main != null) main.cullingMask &= ~(1 << PREV_LAYER);
    }

    void Start()
    {
        BuildUI();
        SetupPreview();
        SelectClass(PlayerPrefs.GetInt("SelectedCharacter", 0));
    }

    void Update()
    {
        _rotY += 18f * Time.deltaTime;
        if (_previewRoot != null)
            _previewRoot.transform.rotation = Quaternion.Euler(0f, _rotY, 0f);
    }

    void OnDestroy()
    {
        if (_rt != null) { _rt.Release(); Destroy(_rt); }
    }

    // ── 3D Preview ───────────────────────────────────────────────────────────

    void SetupPreview()
    {
        // RenderTexture — portrait ratio for character display
        _rt = new RenderTexture(480, 768, 24) { antiAliasing = 4 };
        if (_previewImg != null) _previewImg.texture = _rt;

        // Preview camera — only renders PREV_LAYER, outputs to RT
        var camGO = new GameObject("_PreviewCam");
        _previewCam = camGO.AddComponent<Camera>();
        _previewCam.cullingMask     = 1 << PREV_LAYER;
        _previewCam.clearFlags      = CameraClearFlags.SolidColor;
        _previewCam.backgroundColor = new Color(0.03f, 0.02f, 0.08f);
        _previewCam.targetTexture   = _rt;
        _previewCam.fieldOfView     = 42f;
        _previewCam.nearClipPlane   = 0.1f;
        _previewCam.farClipPlane    = 20f;
        _previewCam.aspect          = 480f / 768f;
        camGO.transform.SetPositionAndRotation(
            new Vector3(0f, 1.35f, -3.2f), Quaternion.Euler(5f, 0f, 0f));

        // Key light
        var keyLight = new GameObject("_PreviewKeyLight");
        SetLayer(keyLight, PREV_LAYER);
        var kl = keyLight.AddComponent<Light>();
        kl.type = LightType.Directional; kl.intensity = 1.8f;
        kl.color = new Color(0.92f, 0.94f, 1f);
        keyLight.transform.SetPositionAndRotation(
            new Vector3(2f, 5f, -3f), Quaternion.Euler(45f, -30f, 0f));

        // Fill light (dim, opposite side)
        var fillLight = new GameObject("_PreviewFillLight");
        SetLayer(fillLight, PREV_LAYER);
        var fl = fillLight.AddComponent<Light>();
        fl.type = LightType.Directional; fl.intensity = 0.5f;
        fl.color = new Color(0.4f, 0.5f, 0.8f);
        fillLight.transform.SetPositionAndRotation(
            new Vector3(-3f, 2f, 2f), Quaternion.Euler(20f, 150f, 0f));

        // Platform disc
        var plat = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        plat.name = "_Platform";
        Destroy(plat.GetComponent<Collider>());
        plat.transform.SetPositionAndRotation(new Vector3(0f, -0.05f, 0f), Quaternion.identity);
        plat.transform.localScale = new Vector3(1.6f, 0.04f, 1.6f);
        plat.GetComponent<Renderer>().material.color = new Color(0.06f, 0.06f, 0.12f);
        SetLayer(plat, PREV_LAYER);

        // Platform ring (slightly larger, darker)
        var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        ring.name = "_PlatformRing";
        Destroy(ring.GetComponent<Collider>());
        ring.transform.SetPositionAndRotation(new Vector3(0f, -0.06f, 0f), Quaternion.identity);
        ring.transform.localScale = new Vector3(1.85f, 0.02f, 1.85f);
        ring.GetComponent<Renderer>().material.color = new Color(0.08f, 0.08f, 0.15f);
        SetLayer(ring, PREV_LAYER);

        // Model root (this is what rotates — VFX are children of this)
        _previewRoot = new GameObject("_PreviewRoot");
        _previewRoot.transform.position = Vector3.zero;

#if UNITY_EDITOR
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(MODEL_PATH);
        if (asset != null)
        {
            var model = Instantiate(asset, _previewRoot.transform);
            model.name = "Model";
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            model.transform.localScale    = Vector3.one;
            SetLayer(model, PREV_LAYER);
        }
        else
        {
            FallbackCapsule();
        }
#else
        FallbackCapsule();
#endif
    }

    void FallbackCapsule()
    {
        var cap = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        cap.transform.SetParent(_previewRoot.transform);
        cap.transform.localPosition = new Vector3(0f, 1f, 0f);
        Destroy(cap.GetComponent<Collider>());
        SetLayer(cap, PREV_LAYER);
    }

    void SwapVFX(ClassDef def)
    {
        if (_vfxA != null) Destroy(_vfxA);
        if (_vfxB != null) Destroy(_vfxB);
        _vfxA = _vfxB = null;

#if UNITY_EDITOR
        _vfxA = SpawnVFX(def.vfxA, new Vector3(0f, 0.05f, 0f), 0.9f);
        _vfxB = SpawnVFX(def.vfxB, new Vector3(0f, 0.6f,  0f), 0.5f);
#endif

        // Tint platform accent color
        var plat = GameObject.Find("_Platform");
        if (plat != null)
            plat.GetComponent<Renderer>().material.color =
                Color.Lerp(new Color(0.05f, 0.05f, 0.1f), def.accent, 0.22f);
    }

    GameObject SpawnVFX(string path, Vector3 localPos, float scale)
    {
#if UNITY_EDITOR
        if (string.IsNullOrEmpty(path)) return null;
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null) { Debug.LogWarning($"[CharSel] VFX not found: {path}"); return null; }
        var go = Instantiate(prefab, _previewRoot.transform);
        go.transform.localPosition = localPos;
        go.transform.localScale    = Vector3.one * scale;
        SetLayer(go, PREV_LAYER);
        return go;
#else
        return null;
#endif
    }

    // ── Selection ─────────────────────────────────────────────────────────────

    void SelectClass(int idx)
    {
        idx  = Mathf.Clamp(idx, 0, Classes.Length - 1);
        _sel = idx;
        var d = Classes[idx];

        // Info panel
        _nameText.text    = d.name;
        _nameText.color   = d.accent;
        _roleText.text    = d.role;
        _blurbText.text   = d.blurb;
        _passiveText.text = d.passive;

        for (int i = 0; i < 4; i++)
            _abilityTexts[i].text = $"[{i + 1}]   {d.abilities[i]}";

        // Class list buttons
        for (int i = 0; i < _classBtns.Length; i++)
        {
            bool sel = i == idx;
            _classBtns[i].GetComponent<Image>().color =
                sel ? new Color(d.accent.r, d.accent.g, d.accent.b, 0.18f)
                    : new Color(1f, 1f, 1f, 0.04f);

            // Find name label (first child text)
            var lbl = _classBtns[i].GetComponentInChildren<Text>();
            if (lbl != null) lbl.color = sel ? d.accent : new Color(0.65f, 0.65f, 0.72f);
        }

        // Enter button accent
        var cb = _enterBtn.colors;
        cb.normalColor      = Color.Lerp(Color.black, d.accent, 0.55f);
        cb.highlightedColor = d.accent;
        cb.pressedColor     = Color.white;
        _enterBtn.colors    = cb;

        // Phaser = not yet playable indicator
        if (_enterLabel != null)
            _enterLabel.text = (idx == 4) ? "COMING SOON" : "ENTER WORLD";

        SwapVFX(d);
    }

    // ── Enter World ───────────────────────────────────────────────────────────

    void OnEnterWorld()
    {
        if (_sel == 4) return; // Phaser not yet playable

        int mirrorIdx = Mathf.Clamp(_sel, 0, 3);
        PlayerPrefs.SetInt("SelectedCharacter", mirrorIdx);
        PlayerPrefs.Save();

        if (NetworkManager.singleton == null)
        {
            Debug.LogError("[CharSel] No NetworkManager — load LoginScene first.");
            return;
        }

        bool dev = PlayerPrefs.GetString("jwt_token", "") == "dev";
        if (dev) NetworkManager.singleton.StartHost();
        else     NetworkManager.singleton.StartClient();
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  UI Construction
    // ═══════════════════════════════════════════════════════════════════════════

    void BuildUI()
    {
        var cvs = new GameObject("Canvas").AddComponent<Canvas>();
        cvs.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = cvs.gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight  = 0.5f;
        cvs.gameObject.AddComponent<GraphicRaycaster>();

        // Background
        Img(cvs.gameObject, "BG", new Color(0.025f, 0.018f, 0.06f), Stretch());

        // Subtle center gradient overlay (makes edges darker)
        var grad = Img(cvs.gameObject, "Vignette", new Color(0f, 0f, 0f, 0.35f), Stretch());

        BuildLeft(cvs.gameObject);
        BuildCenter(cvs.gameObject);
        BuildRight(cvs.gameObject);
    }

    // ── Left panel ────────────────────────────────────────────────────────────

    void BuildLeft(GameObject cvs)
    {
        var p = Img(cvs, "Left", new Color(0.03f, 0.03f, 0.09f, 0.9f), PanelLeft(LEFT_W));

        // Header
        Txt(p, "CHOOSE CLASS", 10, TextAnchor.MiddleCenter,
            new Color(0.4f, 0.4f, 0.52f), TL(0, 0, LEFT_W, HEADER_H));
        HRule(p, HEADER_H, LEFT_W);

        _classBtns = new Button[Classes.Length];
        for (int i = 0; i < Classes.Length; i++)
        {
            int ci = i;
            float yOff = HEADER_H + 6f + i * (BTN_H + 4f);
            _classBtns[i] = ClassBtn(p, Classes[i], yOff);
            _classBtns[i].onClick.AddListener(() => SelectClass(ci));
        }

        // Bottom version tag
        Txt(p, "v0.1 ALPHA", 9, TextAnchor.LowerCenter,
            new Color(0.25f, 0.25f, 0.32f), Bottom(LEFT_W, 28f));
    }

    Button ClassBtn(GameObject parent, ClassDef d, float topY)
    {
        var bg  = Img(parent, d.name, new Color(1f, 1f, 1f, 0.04f),
            TL(6, topY, LEFT_W - 12, BTN_H));
        var btn = bg.AddComponent<Button>();
        var cb  = ColorBlock.defaultColorBlock;
        cb.normalColor      = new Color(1f, 1f, 1f, 0f);
        cb.highlightedColor = new Color(1f, 1f, 1f, 0.06f);
        cb.pressedColor     = new Color(1f, 1f, 1f, 0.15f);
        btn.colors = cb; btn.targetGraphic = bg.GetComponent<Image>();

        // Left accent stripe
        Img(bg, "Stripe", d.accent, TL(0, 0, 3, BTN_H));

        // Class name
        Txt(bg, d.name, 14, TextAnchor.UpperLeft, Color.white, TL(14, 12, LEFT_W - 30, 26));

        // Role
        Txt(bg, d.role, 10, TextAnchor.UpperLeft,
            new Color(0.5f, 0.5f, 0.6f), TL(14, 40, LEFT_W - 30, 18));

        // Coming soon badge for Phaser
        if (d.idx == 4)
            Txt(bg, "SOON", 8, TextAnchor.MiddleRight,
                new Color(0.5f, 0.5f, 0.6f), TL(0, 28, LEFT_W - 16, 18));

        return btn;
    }

    // ── Center panel ──────────────────────────────────────────────────────────

    void BuildCenter(GameObject cvs)
    {
        var p = Img(cvs, "Center", Color.clear, PanelCenter(LEFT_W, RIGHT_W));

        // Header title
        Txt(p, "RATE OF DECAY ONLINE  ·  SURVIVOR SELECT", 11,
            TextAnchor.MiddleCenter, new Color(0.35f, 0.35f, 0.45f),
            StretchTop(HEADER_H));
        HRule(p, HEADER_H, 0f, stretchH: true);

        // 3D Preview — RawImage fills center minus header/footer
        var previewGO = new GameObject("Preview3D");
        previewGO.transform.SetParent(p.transform, false);
        previewGO.layer = 5;
        var prt = previewGO.AddComponent<RectTransform>();
        prt.anchorMin = Vector2.zero; prt.anchorMax = Vector2.one;
        prt.offsetMin = new Vector2(0f,  FOOTER_H + 4f);
        prt.offsetMax = new Vector2(0f, -(HEADER_H + 4f));
        _previewImg = previewGO.AddComponent<RawImage>();
        _previewImg.color = Color.white;

        // Aspect ratio fitter so portrait model doesn't stretch
        var arf = previewGO.AddComponent<AspectRatioFitter>();
        arf.aspectMode  = AspectRatioFitter.AspectMode.FitInParent;
        arf.aspectRatio = 480f / 768f;

        // Footer
        HRule(p, FOOTER_H, 0f, stretchH: true, fromBottom: true);
        Txt(p, "SELECT YOUR SURVIVOR  ·  CHOOSE WISELY", 9,
            TextAnchor.MiddleCenter, new Color(0.3f, 0.3f, 0.4f),
            StretchBottom(FOOTER_H));
    }

    // ── Right panel ───────────────────────────────────────────────────────────

    void BuildRight(GameObject cvs)
    {
        var p = Img(cvs, "Right", new Color(0.03f, 0.03f, 0.09f, 0.9f), PanelRight(RIGHT_W));

        float y = HEADER_H + 20f;

        // Class name (large)
        var nmGO = Txt(p, "ENGINEER", 32, TextAnchor.UpperLeft,
            new Color(1f, 0.55f, 0.05f), TL(24, y, RIGHT_W - 48, 48));
        _nameText = nmGO.GetComponent<Text>();
        y += 52f;

        // Role
        var rlGO = Txt(p, "Damage · Control", 11, TextAnchor.UpperLeft,
            new Color(0.5f, 0.5f, 0.62f), TL(24, y, RIGHT_W - 48, 22));
        _roleText = rlGO.GetComponent<Text>();
        y += 30f;

        HRule(p, y, RIGHT_W - 40f, centered: true);
        y += 18f;

        // Blurb
        var blGO = Txt(p, "...", 11, TextAnchor.UpperLeft,
            new Color(0.78f, 0.78f, 0.84f), TL(24, y, RIGHT_W - 48, 100), wrap: true);
        _blurbText = blGO.GetComponent<Text>();
        y += 110f;

        HRule(p, y, RIGHT_W - 40f, centered: true);
        y += 18f;

        // Abilities header
        Txt(p, "ABILITIES", 9, TextAnchor.UpperLeft,
            new Color(0.42f, 0.42f, 0.54f), TL(24, y, 100, 18));
        y += 24f;

        for (int i = 0; i < 4; i++)
        {
            // Slot number badge
            var badge = Img(p, $"Slot{i+1}", new Color(0.1f, 0.1f, 0.18f), TL(24, y, 24, 22));
            Txt(badge, $"{i+1}", 10, TextAnchor.MiddleCenter,
                new Color(0.5f, 0.5f, 0.62f), TL(0, 0, 24, 22));

            var abGO = Txt(p, $"—", 11, TextAnchor.UpperLeft,
                new Color(0.85f, 0.85f, 0.9f), TL(56, y + 2f, RIGHT_W - 88, 20));
            _abilityTexts[i] = abGO.GetComponent<Text>();
            y += 30f;
        }

        y += 10f;
        HRule(p, y, RIGHT_W - 40f, centered: true);
        y += 16f;

        // Passive
        Txt(p, "PASSIVE", 9, TextAnchor.UpperLeft,
            new Color(0.42f, 0.42f, 0.54f), TL(24, y, 80, 16));
        y += 20f;

        var psGO = Txt(p, "...", 10, TextAnchor.UpperLeft,
            new Color(0.72f, 0.88f, 0.72f), TL(24, y, RIGHT_W - 48, 52), wrap: true);
        _passiveText = psGO.GetComponent<Text>();
        y += 64f;

        // ENTER WORLD button
        var eBg = Img(p, "EnterBtn", new Color(0.55f, 0.32f, 0.02f), TL(24, y, RIGHT_W - 48, 56));
        _enterBtn = eBg.AddComponent<Button>();
        _enterBtn.targetGraphic = eBg.GetComponent<Image>();
        var eCB = ColorBlock.defaultColorBlock;
        eCB.normalColor      = new Color(0.5f, 0.28f, 0.02f);
        eCB.highlightedColor = new Color(1f, 0.65f, 0.08f);
        eCB.pressedColor     = Color.white;
        _enterBtn.colors     = eCB;
        _enterBtn.onClick.AddListener(OnEnterWorld);
        var eLbl = Txt(eBg, "ENTER WORLD", 16, TextAnchor.MiddleCenter,
            Color.white, TL(0, 0, RIGHT_W - 48, 56));
        _enterLabel = eLbl.GetComponent<Text>();
        _enterLabel.fontStyle = FontStyle.Bold;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  UI Helpers
    // ═══════════════════════════════════════════════════════════════════════════

    static Font _font;
    static Font GetFont()
    {
        if (_font != null) return _font;
        _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (_font == null) _font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        return _font;
    }

    static GameObject Img(GameObject parent, string name, Color col,
        Action<RectTransform> layout)
    {
        var go = new GameObject(name); go.layer = 5;
        go.transform.SetParent(parent.transform, false);
        go.AddComponent<Image>().color = col;
        layout(go.GetComponent<RectTransform>());
        return go;
    }

    static GameObject Txt(GameObject parent, string text, int size,
        TextAnchor align, Color col, Action<RectTransform> layout,
        bool wrap = false)
    {
        var go = new GameObject("T_" + text); go.layer = 5;
        go.transform.SetParent(parent.transform, false);
        var t = go.AddComponent<Text>();
        t.text                 = text;
        t.fontSize             = size;
        t.alignment            = align;
        t.color                = col;
        t.font                 = GetFont();
        t.horizontalOverflow   = wrap ? HorizontalWrapMode.Wrap : HorizontalWrapMode.Overflow;
        t.verticalOverflow     = VerticalWrapMode.Overflow;
        t.resizeTextForBestFit = false;
        layout(go.GetComponent<RectTransform>());
        return go;
    }

    static void HRule(GameObject parent, float topY, float w = 0f,
        bool stretchH = false, bool centered = false, bool fromBottom = false)
    {
        var go = new GameObject("Rule"); go.layer = 5;
        go.transform.SetParent(parent.transform, false);
        go.AddComponent<Image>().color = new Color(0.22f, 0.22f, 0.32f, 0.7f);
        var rt = go.GetComponent<RectTransform>();

        if (stretchH)
        {
            rt.anchorMin = fromBottom ? new Vector2(0, 0) : new Vector2(0, 1);
            rt.anchorMax = fromBottom ? new Vector2(1, 0) : new Vector2(1, 1);
            float edge   = fromBottom ? topY : -topY;
            rt.offsetMin = new Vector2(0, edge - 1);
            rt.offsetMax = new Vector2(0, edge + 1);
        }
        else if (centered)
        {
            rt.anchorMin = new Vector2(0.5f, 1f); rt.anchorMax = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -topY);
            rt.sizeDelta = new Vector2(w, 1f);
        }
        else
        {
            rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(w * 0.5f, -topY);
            rt.sizeDelta = new Vector2(w, 1f);
        }
    }

    // ── Layout lambdas ────────────────────────────────────────────────────────

    // Top-left anchored rect (x, topY offset, width, height)
    static Action<RectTransform> TL(float x, float y, float w, float h) => rt =>
    {
        rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(0, 1);
        rt.anchoredPosition = new Vector2(x + w * 0.5f, -(y + h * 0.5f));
        rt.sizeDelta = new Vector2(w, h);
    };

    // Stretch to fill
    static Action<RectTransform> Stretch() => rt =>
    { rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero; };

    // Left panel strip
    static Action<RectTransform> PanelLeft(float w) => rt =>
    { rt.anchorMin = new Vector2(0,0); rt.anchorMax = new Vector2(0,1); rt.offsetMin = Vector2.zero; rt.offsetMax = new Vector2(w,0); };

    // Right panel strip
    static Action<RectTransform> PanelRight(float w) => rt =>
    { rt.anchorMin = new Vector2(1,0); rt.anchorMax = new Vector2(1,1); rt.offsetMin = new Vector2(-w,0); rt.offsetMax = Vector2.zero; };

    // Center panel (between left and right)
    static Action<RectTransform> PanelCenter(float l, float r) => rt =>
    { rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = new Vector2(l,0); rt.offsetMax = new Vector2(-r,0); };

    // Stretch width, fixed height at top
    static Action<RectTransform> StretchTop(float h) => rt =>
    { rt.anchorMin = new Vector2(0,1); rt.anchorMax = new Vector2(1,1); rt.offsetMin = new Vector2(0,-h); rt.offsetMax = Vector2.zero; };

    // Stretch width, fixed height at bottom
    static Action<RectTransform> StretchBottom(float h) => rt =>
    { rt.anchorMin = Vector2.zero; rt.anchorMax = new Vector2(1,0); rt.offsetMin = Vector2.zero; rt.offsetMax = new Vector2(0,h); };

    // Bottom strip (for version label in left panel)
    static Action<RectTransform> Bottom(float w, float h) => rt =>
    { rt.anchorMin = new Vector2(0,0); rt.anchorMax = new Vector2(1,0); rt.offsetMin = Vector2.zero; rt.offsetMax = new Vector2(0,h); };

    // ── Layer helper ──────────────────────────────────────────────────────────

    static void SetLayer(GameObject go, int layer)
    {
        go.layer = layer;
        foreach (Transform c in go.transform) SetLayer(c.gameObject, layer);
    }
}
