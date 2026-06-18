using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using TMPro;

// ═══════════════════════════════════════════════════════════════════════════
//  CHARACTER SELECT — builds the entire UI in code.
//  See RoD_CharacterSelect_Setup.txt for full Unity wiring instructions.
// ═══════════════════════════════════════════════════════════════════════════

public class CharacterSelectUI : MonoBehaviour
{
    [Header("Roster")]
    public CharacterData[] characters;

    [Header("Scene")]
    public string gameScene = "SampleScene";

    [Header("3D Preview")]
    public Camera previewCamera;
    public RenderTexture previewRenderTexture;
    public Transform previewSpawnPoint;
    public float rotationSpeed = 28f;

    // ── Runtime ──────────────────────────────────────────────────────────────
    private int currentIndex = 0;
    private GameObject previewInstance;
    private Canvas rootCanvas;

    // Layout panels
    private Image backgroundPanel;
    private RawImage previewDisplay;
    private TextMeshProUGUI classNameText;
    private TextMeshProUGUI roleTagText;
    private TextMeshProUGUI loreText;
    private Transform traitRow;
    private Transform statColumn;
    private Transform abilityRow;
    private Image deployablePanel;
    private TextMeshProUGUI deployableName;
    private TextMeshProUGUI deployableDesc;
    private Image deployableIcon;
    private Image leftArrow;
    private Image rightArrow;

    // Colors
    static readonly Color BG          = new Color(0.05f, 0.05f, 0.10f, 1f);
    static readonly Color PanelDark   = new Color(0.08f, 0.08f, 0.14f, 0.92f);
    static readonly Color PanelMid    = new Color(0.10f, 0.10f, 0.18f, 0.88f);
    static readonly Color TextPrimary = new Color(0.95f, 0.93f, 0.88f, 1f);
    static readonly Color TextDim     = new Color(0.65f, 0.63f, 0.60f, 1f);
    static readonly Color Transparent = new Color(0, 0, 0, 0);

    // ── Unity ────────────────────────────────────────────────────────────────

    void Start()
    {
        if (previewCamera != null)
            previewCamera.targetTexture = previewRenderTexture;

        BuildUI();
        ShowCharacter(0);
    }

    void Update()
    {
        if (previewInstance != null)
            previewInstance.transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime);

        if (Keyboard.current != null)
        {
            if (Keyboard.current.leftArrowKey.wasPressedThisFrame)  Previous();
            if (Keyboard.current.rightArrowKey.wasPressedThisFrame) Next();
        }
    }

    // ── Navigation ───────────────────────────────────────────────────────────

    public void Next()
    {
        currentIndex = (currentIndex + 1) % characters.Length;
        ShowCharacter(currentIndex);
    }

    public void Previous()
    {
        currentIndex = (currentIndex - 1 + characters.Length) % characters.Length;
        ShowCharacter(currentIndex);
    }

    public void Play()
    {
        PlayerPrefs.SetInt("SelectedCharacter", currentIndex);
        PlayerPrefs.Save();
        SceneManager.LoadScene(gameScene);
    }

    // ── Populate ─────────────────────────────────────────────────────────────

    void ShowCharacter(int index)
    {
        if (characters == null || characters.Length == 0) return;
        CharacterData d = characters[index];

        // Background tint
        backgroundPanel.color = Color.Lerp(BG, d.classColorDark, 0.18f);

        // Header
        classNameText.text = d.className.ToUpper();
        classNameText.color = d.classColor;
        roleTagText.text    = d.roleTagline;
        loreText.text       = d.loreDescription;

        // Trait pills
        foreach (Transform c in traitRow) Destroy(c.gameObject);
        if (d.traits != null)
            foreach (var t in d.traits)
                BuildTraitPill(traitRow, t, d.classColor);

        // Stat bars
        foreach (Transform c in statColumn) Destroy(c.gameObject);
        if (d.stats != null)
            foreach (var s in d.stats)
                BuildStatBar(statColumn, s, d.classColor);

        // Ability cards
        foreach (Transform c in abilityRow) Destroy(c.gameObject);
        if (d.coreAbilities != null)
            foreach (var a in d.coreAbilities)
                BuildAbilityCard(abilityRow, a, d.classColor);

        // Deployable
        deployablePanel.color = new Color(d.classColor.r, d.classColor.g, d.classColor.b, 0.12f);
        deployableName.text   = d.deployableName.ToUpper();
        deployableName.color  = d.classColor;
        deployableDesc.text   = d.deployableDescription;
        deployableIcon.sprite = d.deployableIcon;
        deployableIcon.color  = d.deployableIcon != null ? Color.white : Transparent;

        // Arrow tints
        leftArrow.color  = d.classColor;
        rightArrow.color = d.classColor;

        // 3D preview swap
        if (previewInstance != null) Destroy(previewInstance);
        GameObject prefab = d.previewPrefab != null ? d.previewPrefab : d.prefab;
        if (prefab != null && previewSpawnPoint != null)
        {
            previewInstance = Instantiate(prefab, previewSpawnPoint.position, previewSpawnPoint.rotation);
            Animator animator = previewInstance.GetComponentInChildren<Animator>();
            foreach (var mb in previewInstance.GetComponentsInChildren<MonoBehaviour>())
                if (mb != animator) mb.enabled = false;
            SetLayer(previewInstance, LayerMask.NameToLayer("CharacterPreview"));
        }
    }

    // ── UI Builder ───────────────────────────────────────────────────────────

    void BuildUI()
    {
        rootCanvas = GetComponentInParent<Canvas>();
        if (rootCanvas == null)
        {
            Debug.LogError("CharacterSelectUI must be a child of a Canvas.");
            return;
        }

        RectTransform root = rootCanvas.GetComponent<RectTransform>();

        // ── Full-screen background ──
        backgroundPanel = MakeImage(root, "Background", BG, Vector2.zero, Vector2.one);
        Stretch(backgroundPanel.rectTransform);

        // ── Left panel (40%) — preview ──
        RectTransform leftPanel = MakePanel(root, "LeftPanel", PanelDark,
            new Vector2(0, 0), new Vector2(0.42f, 1f));

        // Preview display
        GameObject rawGO = new GameObject("PreviewDisplay", typeof(RectTransform), typeof(RawImage));
        rawGO.transform.SetParent(leftPanel, false);
        previewDisplay = rawGO.GetComponent<RawImage>();
        previewDisplay.texture = previewRenderTexture;
        RectTransform rawRt = rawGO.GetComponent<RectTransform>();
        rawRt.anchorMin = new Vector2(0.05f, 0.22f);
        rawRt.anchorMax = new Vector2(0.95f, 0.95f);
        rawRt.offsetMin = rawRt.offsetMax = Vector2.zero;

        // Class name
        classNameText = MakeLabel(leftPanel, "ClassName", "", 36f, FontStyles.Bold);
        classNameText.rectTransform.anchorMin        = new Vector2(0.05f, 0.13f);
        classNameText.rectTransform.anchorMax        = new Vector2(0.95f, 0.21f);
        classNameText.rectTransform.offsetMin        = classNameText.rectTransform.offsetMax = Vector2.zero;
        classNameText.alignment                      = TextAlignmentOptions.Center;

        // Role tagline
        roleTagText = MakeLabel(leftPanel, "RoleTag", "", 13f, FontStyles.Normal);
        roleTagText.color                           = TextDim;
        roleTagText.rectTransform.anchorMin         = new Vector2(0.05f, 0.07f);
        roleTagText.rectTransform.anchorMax         = new Vector2(0.95f, 0.13f);
        roleTagText.rectTransform.offsetMin         = roleTagText.rectTransform.offsetMax = Vector2.zero;
        roleTagText.alignment                       = TextAlignmentOptions.Center;

        // Left/Right arrows
        leftArrow  = MakeArrowButton(leftPanel, "ArrowLeft",  "◀", new Vector2(0f,   0.45f), new Vector2(0.12f, 0.55f), Previous);
        rightArrow = MakeArrowButton(leftPanel, "ArrowRight", "▶", new Vector2(0.88f, 0.45f), new Vector2(1f,   0.55f), Next);

        // ── Right panel (58%) — details ──
        RectTransform rightPanel = MakePanel(root, "RightPanel", PanelMid,
            new Vector2(0.42f, 0f), new Vector2(1f, 1f));

        // Lore description
        loreText = MakeLabel(rightPanel, "Lore", "", 13f, FontStyles.Normal);
        loreText.color                       = TextDim;
        loreText.rectTransform.anchorMin     = new Vector2(0.05f, 0.77f);
        loreText.rectTransform.anchorMax     = new Vector2(0.95f, 0.96f);
        loreText.rectTransform.offsetMin     = loreText.rectTransform.offsetMax = Vector2.zero;
        loreText.textWrappingMode            = TextWrappingModes.Normal;
        loreText.alignment                   = TextAlignmentOptions.TopLeft;

        // Trait pills row
        GameObject traitGO = new GameObject("TraitRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        traitGO.transform.SetParent(rightPanel, false);
        traitRow = traitGO.transform;
        RectTransform traitRt = traitGO.GetComponent<RectTransform>();
        traitRt.anchorMin = new Vector2(0.04f, 0.68f);
        traitRt.anchorMax = new Vector2(0.96f, 0.76f);
        traitRt.offsetMin = traitRt.offsetMax = Vector2.zero;
        var traitLayout = traitGO.GetComponent<HorizontalLayoutGroup>();
        traitLayout.spacing            = 8f;
        traitLayout.childAlignment     = TextAnchor.MiddleLeft;
        traitLayout.childForceExpandWidth  = false;
        traitLayout.childForceExpandHeight = true;

        // Stat bars
        GameObject statGO = new GameObject("StatColumn", typeof(RectTransform), typeof(VerticalLayoutGroup));
        statGO.transform.SetParent(rightPanel, false);
        statColumn = statGO.transform;
        RectTransform statRt = statGO.GetComponent<RectTransform>();
        statRt.anchorMin = new Vector2(0.04f, 0.44f);
        statRt.anchorMax = new Vector2(0.70f, 0.67f);
        statRt.offsetMin = statRt.offsetMax = Vector2.zero;
        var statLayout = statGO.GetComponent<VerticalLayoutGroup>();
        statLayout.spacing            = 6f;
        statLayout.childAlignment     = TextAnchor.UpperLeft;
        statLayout.childForceExpandWidth  = true;
        statLayout.childForceExpandHeight = false;

        // Deployable panel
        GameObject depGO = new GameObject("DeployablePanel", typeof(RectTransform), typeof(Image));
        depGO.transform.SetParent(rightPanel, false);
        deployablePanel = depGO.GetComponent<Image>();
        deployablePanel.color = PanelDark;
        RectTransform depRt = depGO.GetComponent<RectTransform>();
        depRt.anchorMin = new Vector2(0.04f, 0.27f);
        depRt.anchorMax = new Vector2(0.96f, 0.43f);
        depRt.offsetMin = depRt.offsetMax = Vector2.zero;

        // Deployable icon
        GameObject depIconGO = new GameObject("DepIcon", typeof(RectTransform), typeof(Image));
        depIconGO.transform.SetParent(depGO.transform, false);
        deployableIcon = depIconGO.GetComponent<Image>();
        RectTransform depIconRt = depIconGO.GetComponent<RectTransform>();
        depIconRt.anchorMin = new Vector2(0.01f, 0.1f);
        depIconRt.anchorMax = new Vector2(0.14f, 0.9f);
        depIconRt.offsetMin = depIconRt.offsetMax = Vector2.zero;

        // Deployable name
        deployableName = MakeLabel(depGO.GetComponent<RectTransform>(), "DepName", "DEPLOYABLE", 13f, FontStyles.Bold);
        deployableName.rectTransform.anchorMin = new Vector2(0.16f, 0.5f);
        deployableName.rectTransform.anchorMax = new Vector2(0.96f, 0.95f);
        deployableName.rectTransform.offsetMin = deployableName.rectTransform.offsetMax = Vector2.zero;
        deployableName.alignment = TextAlignmentOptions.BottomLeft;

        // Deployable description
        deployableDesc = MakeLabel(depGO.GetComponent<RectTransform>(), "DepDesc", "", 11f, FontStyles.Normal);
        deployableDesc.color                       = TextDim;
        deployableDesc.rectTransform.anchorMin     = new Vector2(0.16f, 0.05f);
        deployableDesc.rectTransform.anchorMax     = new Vector2(0.96f, 0.52f);
        deployableDesc.rectTransform.offsetMin     = deployableDesc.rectTransform.offsetMax = Vector2.zero;
        deployableDesc.alignment                   = TextAlignmentOptions.TopLeft;
        deployableDesc.textWrappingMode            = TextWrappingModes.Normal;

        // Ability cards row
        GameObject abilityGO = new GameObject("AbilityRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        abilityGO.transform.SetParent(rightPanel, false);
        abilityRow = abilityGO.transform;
        RectTransform abilityRt = abilityGO.GetComponent<RectTransform>();
        abilityRt.anchorMin = new Vector2(0.04f, 0.05f);
        abilityRt.anchorMax = new Vector2(0.96f, 0.26f);
        abilityRt.offsetMin = abilityRt.offsetMax = Vector2.zero;
        var abilityLayout = abilityGO.GetComponent<HorizontalLayoutGroup>();
        abilityLayout.spacing                = 10f;
        abilityLayout.childAlignment         = TextAnchor.MiddleLeft;
        abilityLayout.childForceExpandWidth  = true;
        abilityLayout.childForceExpandHeight = true;

        // ── Play button ──
        BuildPlayButton(root);
    }

    // ── Component builders ───────────────────────────────────────────────────

    void BuildTraitPill(Transform parent, TraitPill trait, Color accent)
    {
        GameObject pill = new GameObject("Pill_" + trait.label, typeof(RectTransform), typeof(Image), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        pill.transform.SetParent(parent, false);

        Image bg = pill.GetComponent<Image>();
        bg.color = new Color(accent.r, accent.g, accent.b, 0.18f);

        var le = pill.GetComponent<LayoutElement>();
        le.preferredHeight = 28f;
        le.minWidth        = 80f;

        var hg = pill.GetComponent<HorizontalLayoutGroup>();
        hg.padding                   = new RectOffset(8, 8, 4, 4);
        hg.spacing                   = 6f;
        hg.childAlignment            = TextAnchor.MiddleLeft;
        hg.childForceExpandWidth     = false;
        hg.childForceExpandHeight    = true;

        // Icon
        if (trait.icon != null)
        {
            GameObject iconGO = new GameObject("Icon", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            iconGO.transform.SetParent(pill.transform, false);
            iconGO.GetComponent<Image>().sprite = trait.icon;
            var ile = iconGO.GetComponent<LayoutElement>();
            ile.preferredWidth  = 18f;
            ile.preferredHeight = 18f;
        }

        // Label
        GameObject labelGO = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
        labelGO.transform.SetParent(pill.transform, false);
        var tmp = labelGO.GetComponent<TextMeshProUGUI>();
        tmp.text      = trait.label.ToUpper();
        tmp.fontSize  = 10f;
        tmp.color     = accent;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.MidlineLeft;
        var lle = labelGO.GetComponent<LayoutElement>();
        lle.flexibleWidth = 1f;
    }

    void BuildStatBar(Transform parent, ClassStat stat, Color accent)
    {
        GameObject row = new GameObject("Stat_" + stat.label, typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        row.transform.SetParent(parent, false);
        var le = row.GetComponent<LayoutElement>();
        le.preferredHeight = 18f;
        var hg = row.GetComponent<HorizontalLayoutGroup>();
        hg.spacing             = 8f;
        hg.childAlignment      = TextAnchor.MiddleLeft;
        hg.childForceExpandWidth  = false;
        hg.childForceExpandHeight = true;

        // Stat label
        GameObject labelGO = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
        labelGO.transform.SetParent(row.transform, false);
        var tmp = labelGO.GetComponent<TextMeshProUGUI>();
        tmp.text     = stat.label.ToUpper();
        tmp.fontSize = 10f;
        tmp.color    = TextDim;
        tmp.alignment = TextAlignmentOptions.MidlineLeft;
        var lle = labelGO.GetComponent<LayoutElement>();
        lle.preferredWidth = 90f;

        // Pips (5 total)
        for (int i = 0; i < 5; i++)
        {
            GameObject pip = new GameObject("Pip" + i, typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            pip.transform.SetParent(row.transform, false);
            bool filled = i < stat.value;
            pip.GetComponent<Image>().color = filled
                ? new Color(accent.r, accent.g, accent.b, 0.9f)
                : new Color(0.25f, 0.25f, 0.30f, 0.7f);
            var ple = pip.GetComponent<LayoutElement>();
            ple.preferredWidth  = 18f;
            ple.preferredHeight = 10f;
        }
    }

    void BuildAbilityCard(Transform parent, AbilityPreview ability, Color accent)
    {
        GameObject card = new GameObject("Ability_" + ability.abilityName, typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(LayoutElement));
        card.transform.SetParent(parent, false);
        card.GetComponent<Image>().color = new Color(accent.r, accent.g, accent.b, 0.10f);

        var vg = card.GetComponent<VerticalLayoutGroup>();
        vg.padding                   = new RectOffset(8, 8, 8, 8);
        vg.spacing                   = 4f;
        vg.childAlignment            = TextAnchor.UpperCenter;
        vg.childForceExpandWidth     = true;
        vg.childForceExpandHeight    = false;

        // Icon
        GameObject iconGO = new GameObject("Icon", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        iconGO.transform.SetParent(card.transform, false);
        var iconImg = iconGO.GetComponent<Image>();
        iconImg.sprite = ability.icon;
        iconImg.color  = ability.icon != null ? Color.white : new Color(accent.r, accent.g, accent.b, 0.4f);
        var ile = iconGO.GetComponent<LayoutElement>();
        ile.preferredHeight = 44f;

        // Ability name
        GameObject nameGO = new GameObject("Name", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
        nameGO.transform.SetParent(card.transform, false);
        var nameT = nameGO.GetComponent<TextMeshProUGUI>();
        nameT.text      = ability.abilityName.ToUpper();
        nameT.fontSize  = 10f;
        nameT.color     = accent;
        nameT.fontStyle = FontStyles.Bold;
        nameT.alignment = TextAlignmentOptions.Center;
        nameGO.GetComponent<LayoutElement>().preferredHeight = 16f;

        // Description
        GameObject descGO = new GameObject("Desc", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
        descGO.transform.SetParent(card.transform, false);
        var descT = descGO.GetComponent<TextMeshProUGUI>();
        descT.text            = ability.description;
        descT.fontSize        = 9f;
        descT.color           = TextDim;
        descT.alignment       = TextAlignmentOptions.Top;
        descT.textWrappingMode = TextWrappingModes.Normal;
        descGO.GetComponent<LayoutElement>().preferredHeight = 36f;
    }

    Image MakeArrowButton(RectTransform parent, string name, string symbol, Vector2 anchorMin, Vector2 anchorMax, UnityEngine.Events.UnityAction callback)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = rt.offsetMax = Vector2.zero;

        Image img = go.GetComponent<Image>();
        img.color = Color.white;

        GameObject label = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        label.transform.SetParent(go.transform, false);
        var rt2 = label.GetComponent<RectTransform>();
        rt2.anchorMin = Vector2.zero;
        rt2.anchorMax = Vector2.one;
        rt2.offsetMin = rt2.offsetMax = Vector2.zero;
        var tmp = label.GetComponent<TextMeshProUGUI>();
        tmp.text      = symbol;
        tmp.fontSize  = 22f;
        tmp.color     = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;

        go.GetComponent<Button>().onClick.AddListener(callback);
        return img;
    }

    void BuildPlayButton(RectTransform root)
    {
        GameObject btnGO = new GameObject("PlayButton", typeof(RectTransform), typeof(Image), typeof(Button));
        btnGO.transform.SetParent(root, false);
        var rt = btnGO.GetComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0.38f, 0.02f);
        rt.anchorMax        = new Vector2(0.62f, 0.09f);
        rt.offsetMin        = rt.offsetMax = Vector2.zero;
        btnGO.GetComponent<Image>().color = new Color(0.15f, 0.55f, 0.35f, 1f);

        GameObject labelGO = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelGO.transform.SetParent(btnGO.transform, false);
        var lrt = labelGO.GetComponent<RectTransform>();
        lrt.anchorMin = Vector2.zero;
        lrt.anchorMax = Vector2.one;
        lrt.offsetMin = lrt.offsetMax = Vector2.zero;
        var tmp = labelGO.GetComponent<TextMeshProUGUI>();
        tmp.text      = "DEPLOY";
        tmp.fontSize  = 18f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color     = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;

        btnGO.GetComponent<Button>().onClick.AddListener(Play);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    Image MakeImage(RectTransform parent, string name, Color color, Vector2 anchorMin, Vector2 anchorMax)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        Image img = go.GetComponent<Image>();
        img.color = color;
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        return img;
    }

    RectTransform MakePanel(RectTransform parent, string name, Color color, Vector2 anchorMin, Vector2 anchorMax)
    {
        Image img = MakeImage(parent, name, color, anchorMin, anchorMax);
        return img.GetComponent<RectTransform>();
    }

    TextMeshProUGUI MakeLabel(RectTransform parent, string name, string text, float size, FontStyles style)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text      = text;
        tmp.fontSize  = size;
        tmp.fontStyle = style;
        tmp.color     = TextPrimary;
        return tmp;
    }

    void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    void SetLayer(GameObject go, int layer)
    {
        if (layer < 0) return;
        go.layer = layer;
        foreach (Transform child in go.transform)
            SetLayer(child.gameObject, layer);
    }
}
