using UnityEngine;
using UnityEngine.UI;
using Mirror;
using TMPro;

/// <summary>
/// WorldBossHealthBar — Shared boss HP bar, visible to all clients.
/// Self-bootstrapping via RuntimeInitializeOnLoadMethod — no scene object required.
/// Phase markers drawn at 60% and 30%.
/// </summary>
public class WorldBossHealthBar : MonoBehaviour
{
    private static WorldBossHealthBar _instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (_instance != null) return;
        var go = new GameObject("[WorldBossHealthBar]");
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<WorldBossHealthBar>();
    }

    private Canvas         _canvas;
    private Slider         _hpSlider;
    private TextMeshProUGUI _bossNameText;
    private TextMeshProUGUI _hpText;
    private Image          _fillImage;
    private GameObject     _root;

    private WorldBossController _trackedBoss;

    static readonly Color Phase1Color = new Color(0.2f, 0.8f, 1f);
    static readonly Color Phase2Color = new Color(1f, 0.6f, 0f);
    static readonly Color Phase3Color = new Color(1f, 0.15f, 0.15f);

    void Awake() { BuildUI(); HideBar(); }

    void Update()
    {
        if (_trackedBoss == null)
        {
            _trackedBoss = FindObjectOfType<WorldBossController>();
            if (_trackedBoss != null &&
                _trackedBoss.currentPhase != WorldBossController.BossPhase.Idle &&
                _trackedBoss.currentPhase != WorldBossController.BossPhase.Dead)
                ShowBar(_trackedBoss.gameObject.name);
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

        float pct    = health.Fraction;
        _hpSlider.value = pct;
        _hpText.text    = $"{Mathf.CeilToInt(health.currentHealth)} / {Mathf.CeilToInt(health.maxHealth)}";

        if (_trackedBoss.isImmune)
            _fillImage.color = Color.Lerp(_fillImage.color, Color.white, Time.deltaTime * 4f);
    }

    public void OnPhaseChanged(WorldBossController.BossPhase phase)
    {
        switch (phase)
        {
            case WorldBossController.BossPhase.Phase1:    _fillImage.color = Phase1Color; break;
            case WorldBossController.BossPhase.Phase2:    _fillImage.color = Phase2Color; _bossNameText.text = "NULL ARCHITECT — Shard Fracture"; break;
            case WorldBossController.BossPhase.Phase3:    _fillImage.color = Phase3Color; _bossNameText.text = "NULL ARCHITECT — CRITICAL"; break;
            case WorldBossController.BossPhase.Transition: _fillImage.color = Color.white; _bossNameText.text = "NULL ARCHITECT — IMMUNE"; break;
        }
    }

    void ShowBar(string bossName) { _root.SetActive(true); _bossNameText.text = bossName.ToUpper(); _fillImage.color = Phase1Color; }
    void HideBar()                { _root.SetActive(false); }

    System.Collections.IEnumerator HideAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        HideBar();
    }

    void BuildUI()
    {
        _canvas = gameObject.AddComponent<Canvas>();
        _canvas.renderMode    = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder  = 150;
        gameObject.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        gameObject.AddComponent<GraphicRaycaster>();

        _root = new GameObject("BossBarRoot");
        _root.transform.SetParent(transform, false);
        var rootRect = _root.AddComponent<RectTransform>();
        rootRect.anchorMin = new Vector2(0.2f, 0.92f);
        rootRect.anchorMax = new Vector2(0.8f, 0.99f);
        rootRect.offsetMin = rootRect.offsetMax = Vector2.zero;

        var bg    = new GameObject("BG", typeof(RectTransform));
        bg.transform.SetParent(_root.transform, false);
        StretchFull(bg);
        bg.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.85f);

        var nameObj = new GameObject("BossName");
        nameObj.transform.SetParent(_root.transform, false);
        var nameRect = nameObj.AddComponent<RectTransform>();
        nameRect.anchorMin = new Vector2(0f, 0.55f);
        nameRect.anchorMax = Vector2.one;
        nameRect.offsetMin = new Vector2(8f, 0f);
        nameRect.offsetMax = new Vector2(-8f, -2f);
        _bossNameText = nameObj.AddComponent<TextMeshProUGUI>();
        _bossNameText.text      = "NULL ARCHITECT";
        _bossNameText.fontSize  = 13;
        _bossNameText.fontStyle = FontStyles.Bold;
        _bossNameText.color     = Color.white;
        _bossNameText.alignment = TextAlignmentOptions.Center;

        var sliderObj = new GameObject("HPSlider");
        sliderObj.transform.SetParent(_root.transform, false);
        var sliderRect = sliderObj.AddComponent<RectTransform>();
        sliderRect.anchorMin = new Vector2(0f, 0.05f);
        sliderRect.anchorMax = new Vector2(1f, 0.52f);
        sliderRect.offsetMin = new Vector2(8f, 0f);
        sliderRect.offsetMax = new Vector2(-8f, 0f);
        _hpSlider = sliderObj.AddComponent<Slider>();
        _hpSlider.minValue   = 0f;
        _hpSlider.maxValue   = 1f;
        _hpSlider.value      = 1f;
        _hpSlider.interactable = false;

        var sliderBg = new GameObject("SliderBG", typeof(RectTransform));
        sliderBg.transform.SetParent(sliderObj.transform, false);
        StretchFull(sliderBg);
        _hpSlider.targetGraphic = sliderBg.AddComponent<Image>();
        ((Image)_hpSlider.targetGraphic).color = new Color(0.1f, 0.1f, 0.1f);

        var fillArea = new GameObject("FillArea", typeof(RectTransform));
        fillArea.transform.SetParent(sliderObj.transform, false);
        StretchFull(fillArea);
        var fill = new GameObject("Fill", typeof(RectTransform));
        fill.transform.SetParent(fillArea.transform, false);
        StretchFull(fill);
        _fillImage        = fill.AddComponent<Image>();
        _fillImage.color  = Phase1Color;
        _hpSlider.fillRect = fill.GetComponent<RectTransform>();

        BuildPhaseMarker(sliderObj.transform, 0.60f, new Color(1f, 1f, 0f, 0.8f));
        BuildPhaseMarker(sliderObj.transform, 0.30f, new Color(1f, 0.3f, 0.1f, 0.8f));

        var hpTextObj = new GameObject("HPText", typeof(RectTransform));
        hpTextObj.transform.SetParent(sliderObj.transform, false);
        StretchFull(hpTextObj);
        _hpText = hpTextObj.AddComponent<TextMeshProUGUI>();
        _hpText.fontSize  = 10;
        _hpText.color     = Color.white;
        _hpText.alignment = TextAlignmentOptions.Center;
    }

    void BuildPhaseMarker(Transform parent, float xAnchor, Color color)
    {
        var marker = new GameObject($"Marker_{xAnchor}");
        marker.transform.SetParent(parent, false);
        var rect = marker.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(xAnchor - 0.002f, 0f);
        rect.anchorMax = new Vector2(xAnchor + 0.002f, 1f);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        marker.AddComponent<Image>().color = color;
    }

    void StretchFull(GameObject go)
    {
        var rect = go.GetComponent<RectTransform>() ?? go.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }
}
