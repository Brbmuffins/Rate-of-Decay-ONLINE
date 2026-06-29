using UnityEngine;
using UnityEngine.UI;
using Mirror;
using TMPro;

/// <summary>
/// WorldBossHealthBar — Shared boss HP bar, visible to all clients.
/// Syncs via SyncVar from WorldBossController. Phase markers drawn at 60% and 30%.
/// Self-bootstrapping via RuntimeInitializeOnLoadMethod — no scene object required.
///
/// Copy to: Assets/Game/UI/WorldBossHealthBar.cs
/// </summary>
public class WorldBossHealthBar : MonoBehaviour
{
    // ─── Singleton (self-bootstrapping) ───────────────────────────────────────
    private static WorldBossHealthBar _instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (_instance != null) return;
        var go = new GameObject("[WorldBossHealthBar]");
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<WorldBossHealthBar>();
    }

    // ─── UI References (built at runtime if not assigned) ─────────────────────
    private Canvas _canvas;
    private Slider _hpSlider;
    private TextMeshProUGUI _bossNameText;
    private TextMeshProUGUI _hpText;
    private Image _fillImage;
    private GameObject _root;

    // Phase marker images
    private Image _phase2Marker;
    private Image _phase3Marker;

    // ─── State ────────────────────────────────────────────────────────────────
    private WorldBossController _trackedBoss;
    private bool _visible = false;

    // Phase colors
    private static readonly Color Phase1Color = new Color(0.2f, 0.8f, 1f);   // blue
    private static readonly Color Phase2Color = new Color(1f, 0.6f, 0f);     // orange
    private static readonly Color Phase3Color = new Color(1f, 0.15f, 0.15f); // red

    // ─── Lifecycle ────────────────────────────────────────────────────────────
    void Awake()
    {
        BuildUI();
        HideBar();
    }

    void Update()
    {
        if (_trackedBoss == null)
        {
            // Search for a boss that's fighting
            _trackedBoss = FindObjectOfType<WorldBossController>();
            if (_trackedBoss != null &&
                _trackedBoss.currentPhase != WorldBossController.BossPhase.Idle &&
                _trackedBoss.currentPhase != WorldBossController.BossPhase.Dead)
            {
                ShowBar(_trackedBoss.gameObject.name);
            }
            return;
        }

        if (_trackedBoss.currentPhase == WorldBossController.BossPhase.Dead)
        {
            StartCoroutine(HideAfterDelay(3f));
            _trackedBoss = null;
            return;
        }

        var health = _trackedBoss.GetComponent<Health>();
        if (health == null) return;

        float pct = Mathf.Clamp01(health.currentHp / health.maxHp);
        _hpSlider.value = pct;
        _hpText.text = $"{Mathf.CeilToInt(health.currentHp)} / {Mathf.CeilToInt(health.maxHp)}";

        // Immune pulse
        if (_trackedBoss.isImmune)
            _fillImage.color = Color.Lerp(_fillImage.color, Color.white, Time.deltaTime * 4f);
    }

    // ─── Phase Events ─────────────────────────────────────────────────────────
    public void OnPhaseChanged(WorldBossController.BossPhase phase)
    {
        switch (phase)
        {
            case WorldBossController.BossPhase.Phase1:
                _fillImage.color = Phase1Color;
                break;
            case WorldBossController.BossPhase.Phase2:
                _fillImage.color = Phase2Color;
                _bossNameText.text = "NULL ARCHITECT — Shard Fracture";
                break;
            case WorldBossController.BossPhase.Phase3:
                _fillImage.color = Phase3Color;
                _bossNameText.text = "NULL ARCHITECT — CRITICAL";
                break;
            case WorldBossController.BossPhase.Transition:
                _fillImage.color = Color.white;
                _bossNameText.text = "NULL ARCHITECT — IMMUNE";
                break;
        }
    }

    // ─── Show / Hide ──────────────────────────────────────────────────────────
    void ShowBar(string bossName)
    {
        _visible = true;
        _root.SetActive(true);
        _bossNameText.text = bossName.ToUpper();
        _fillImage.color = Phase1Color;
    }

    void HideBar()
    {
        _visible = false;
        _root.SetActive(false);
    }

    System.Collections.IEnumerator HideAfterDelay(float delay)
    {
        yield return new UnityEngine.WaitForSeconds(delay);
        HideBar();
    }

    // ─── Build UI at Runtime ──────────────────────────────────────────────────
    void BuildUI()
    {
        // Canvas
        _canvas = gameObject.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 150;
        gameObject.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        gameObject.AddComponent<GraphicRaycaster>();

        // Root panel — top-center
        _root = new GameObject("BossBarRoot");
        _root.transform.SetParent(transform, false);
        var rootRect = _root.AddComponent<RectTransform>();
        rootRect.anchorMin = new Vector2(0.2f, 0.92f);
        rootRect.anchorMax = new Vector2(0.8f, 0.99f);
        rootRect.offsetMin = rootRect.offsetMax = Vector2.zero;

        // Dark background
        var bg = new GameObject("BG");
        bg.transform.SetParent(_root.transform, false);
        var bgRect = bg.AddComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = bgRect.offsetMax = Vector2.zero;
        var bgImg = bg.AddComponent<Image>();
        bgImg.color = new Color(0f, 0f, 0f, 0.85f);

        // Boss name
        var nameObj = new GameObject("BossName");
        nameObj.transform.SetParent(_root.transform, false);
        var nameRect = nameObj.AddComponent<RectTransform>();
        nameRect.anchorMin = new Vector2(0f, 0.55f);
        nameRect.anchorMax = Vector2.one;
        nameRect.offsetMin = new Vector2(8f, 0f);
        nameRect.offsetMax = new Vector2(-8f, -2f);
        _bossNameText = nameObj.AddComponent<TextMeshProUGUI>();
        _bossNameText.text = "NULL ARCHITECT";
        _bossNameText.fontSize = 13;
        _bossNameText.fontStyle = FontStyles.Bold;
        _bossNameText.color = Color.white;
        _bossNameText.alignment = TextAlignmentOptions.Center;

        // HP Slider
        var sliderObj = new GameObject("HPSlider");
        sliderObj.transform.SetParent(_root.transform, false);
        var sliderRect = sliderObj.AddComponent<RectTransform>();
        sliderRect.anchorMin = new Vector2(0f, 0.05f);
        sliderRect.anchorMax = new Vector2(1f, 0.52f);
        sliderRect.offsetMin = new Vector2(8f, 0f);
        sliderRect.offsetMax = new Vector2(-8f, 0f);
        _hpSlider = sliderObj.AddComponent<Slider>();
        _hpSlider.minValue = 0f;
        _hpSlider.maxValue = 1f;
        _hpSlider.value = 1f;
        _hpSlider.interactable = false;

        // Slider background
        var sliderBg = new GameObject("SliderBG");
        sliderBg.transform.SetParent(sliderObj.transform, false);
        StretchFull(sliderBg);
        var sliderBgImg = sliderBg.AddComponent<Image>();
        sliderBgImg.color = new Color(0.1f, 0.1f, 0.1f);
        _hpSlider.targetGraphic = sliderBgImg;

        // Fill area
        var fillArea = new GameObject("FillArea");
        fillArea.transform.SetParent(sliderObj.transform, false);
        StretchFull(fillArea);
        var fill = new GameObject("Fill");
        fill.transform.SetParent(fillArea.transform, false);
        StretchFull(fill);
        _fillImage = fill.AddComponent<Image>();
        _fillImage.color = Phase1Color;
        _hpSlider.fillRect = fill.GetComponent<RectTransform>();

        // Phase 2 marker at 60%
        _phase2Marker = BuildPhaseMarker(sliderObj.transform, 0.60f, new Color(1f, 1f, 0f, 0.8f));
        // Phase 3 marker at 30%
        _phase3Marker = BuildPhaseMarker(sliderObj.transform, 0.30f, new Color(1f, 0.3f, 0.1f, 0.8f));

        // HP text overlay
        var hpTextObj = new GameObject("HPText");
        hpTextObj.transform.SetParent(sliderObj.transform, false);
        StretchFull(hpTextObj);
        _hpText = hpTextObj.AddComponent<TextMeshProUGUI>();
        _hpText.text = "";
        _hpText.fontSize = 10;
        _hpText.color = Color.white;
        _hpText.alignment = TextAlignmentOptions.Center;
    }

    Image BuildPhaseMarker(Transform parent, float xAnchor, Color color)
    {
        var marker = new GameObject($"Marker_{xAnchor}");
        marker.transform.SetParent(parent, false);
        var rect = marker.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(xAnchor - 0.002f, 0f);
        rect.anchorMax = new Vector2(xAnchor + 0.002f, 1f);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        var img = marker.AddComponent<Image>();
        img.color = color;
        return img;
    }

    void StretchFull(GameObject go)
    {
        var rect = go.GetComponent<RectTransform>() ?? go.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }
}
