using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

// Ability bar — bottom HUD strip (4 equipped slots) + Tab spellbook overlay (all 8 spells).
//
// Inspector setup:
//   1. Assign caster.
//   2. Assign ability1–4 (the icon Images in your bottom bar).
//   3. Assign cooldown1–4 (fill-type Images overlaying each icon).
//   4. Assign spellbookPanel — a UI Panel that will auto-populate with spell cards.
//   5. (Optional) assign keyLabels1–4 (TextMeshProUGUI showing "1"–"4" on slots).
//
// Spellbook cards are created at runtime inside spellbookPanel.
// Tab opens/closes the spellbook. Click a card to select it, then press 1–4 to equip.

public class AbilityBar : MonoBehaviour
{
    public AbilityCaster caster;

    [Header("Bottom bar — 4 equipped slots")]
    public Image ability1;
    public Image ability2;
    public Image ability3;
    public Image ability4;

    public Image cooldown1;
    public Image cooldown2;
    public Image cooldown3;
    public Image cooldown4;

    public TextMeshProUGUI keyLabel1;
    public TextMeshProUGUI keyLabel2;
    public TextMeshProUGUI keyLabel3;
    public TextMeshProUGUI keyLabel4;

    [Header("Spellbook panel — assign a UI Panel; cards are created at runtime")]
    public GameObject spellbookPanel;

    // Runtime state
    private Image[] icons;
    private Image[] cooldownOverlays;
    private Color[] baseColors;
    private int selectedSlot = 0;

    private int pendingSpellbookIndex = -1;    // which spellbook spell the player clicked
    private Image[] spellbookCards;            // generated card backgrounds
    private TextMeshProUGUI[] spellbookLabels; // generated name labels
    private bool spellbookOpen = false;

    static readonly Color ColorSelected    = new Color(1f, 0.85f, 0.2f);
    static readonly Color ColorPendingCard = new Color(0.3f, 0.8f, 1f);
    static readonly Color ColorEquipped    = new Color(0.5f, 1f, 0.5f);
    static readonly Color ColorNormal      = new Color(0.15f, 0.15f, 0.2f, 0.85f);
    static readonly Color ColorDamage      = new Color(1f, 0.35f, 0.2f);
    static readonly Color ColorHeal        = new Color(0.2f, 0.9f, 0.4f);
    static readonly Color ColorSupport     = new Color(0.3f, 0.6f, 1f);

    void Start()
    {
        icons          = new Image[] { ability1, ability2, ability3, ability4 };
        cooldownOverlays = new Image[] { cooldown1, cooldown2, cooldown3, cooldown4 };
        baseColors     = new Color[4];

        RefreshBottomBar();

        if (spellbookPanel != null)
        {
            BuildSpellbookCards();
            spellbookPanel.SetActive(false);
        }

        HighlightSlot(0);
    }

    void Update()
    {
        HandleSlotInput();
        HandleSpellbookToggle();
        HandlePendingEquip();
        RefreshCooldowns();
        RefreshHeldTint();
    }

    // ── Bottom bar ──────────────────────────────────────────────────────────

    void RefreshBottomBar()
    {
        for (int i = 0; i < 4; i++)
        {
            if (icons[i] == null) continue;

            AbilityDef ab = (caster != null && i < caster.abilities.Length) ? caster.abilities[i] : null;

            if (ab != null)
            {
                icons[i].sprite = ab.icon;
                baseColors[i] = ab.icon != null ? Color.white : CategoryColor(ab.category);
            }
            else
            {
                icons[i].sprite = null;
                baseColors[i] = new Color(0.3f, 0.3f, 0.35f);
            }

            icons[i].color = baseColors[i];

            if (cooldownOverlays[i] != null)
                cooldownOverlays[i].fillAmount = 0f;
        }

        TextMeshProUGUI[] labels = { keyLabel1, keyLabel2, keyLabel3, keyLabel4 };
        string[] keys = { "1", "2", "3", "4" };
        for (int i = 0; i < 4; i++)
            if (labels[i] != null) labels[i].text = keys[i];
    }

    void HandleSlotInput()
    {
        if (Keyboard.current.digit1Key.wasPressedThisFrame) SelectSlot(0);
        if (Keyboard.current.digit2Key.wasPressedThisFrame) SelectSlot(1);
        if (Keyboard.current.digit3Key.wasPressedThisFrame) SelectSlot(2);
        if (Keyboard.current.digit4Key.wasPressedThisFrame) SelectSlot(3);
    }

    void SelectSlot(int slot)
    {
        if (spellbookOpen && pendingSpellbookIndex >= 0)
        {
            // Equip the pending spell into this slot
            caster.EquipSpell(pendingSpellbookIndex, slot);
            pendingSpellbookIndex = -1;
            RefreshBottomBar();
            RefreshSpellbookCards();
            HighlightSlot(slot);
            return;
        }

        selectedSlot = slot;
        HighlightSlot(slot);
    }

    void HighlightSlot(int slot)
    {
        for (int i = 0; i < icons.Length; i++)
        {
            if (icons[i] == null) continue;
            icons[i].transform.localScale = (i == slot) ? Vector3.one * 1.15f : Vector3.one;
        }
    }

    void RefreshCooldowns()
    {
        if (caster == null) return;
        for (int i = 0; i < 4; i++)
        {
            if (cooldownOverlays[i] != null)
                cooldownOverlays[i].fillAmount = caster.GetCooldownFraction(i);
        }
    }

    void RefreshHeldTint()
    {
        if (caster == null) return;
        for (int i = 0; i < 4; i++)
        {
            if (icons[i] == null) continue;
            icons[i].color = caster.HeldAbilityIndex == i ? Color.yellow : baseColors[i];
        }
    }

    // ── Spellbook panel ─────────────────────────────────────────────────────

    void HandleSpellbookToggle()
    {
        if (Keyboard.current.tabKey.wasPressedThisFrame)
        {
            spellbookOpen = !spellbookOpen;
            if (spellbookPanel != null)
                spellbookPanel.SetActive(spellbookOpen);

            if (!spellbookOpen)
                pendingSpellbookIndex = -1;

            // Unlock/relock cursor
            Cursor.lockState = spellbookOpen ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible   = spellbookOpen;
        }
    }

    void HandlePendingEquip()
    {
        if (!spellbookOpen || pendingSpellbookIndex < 0) return;

        // ESC or RMB cancels pending selection
        if (Keyboard.current.escapeKey.wasPressedThisFrame || Mouse.current.rightButton.wasPressedThisFrame)
        {
            pendingSpellbookIndex = -1;
            RefreshSpellbookCards();
        }
    }

    void BuildSpellbookCards()
    {
        if (caster == null || spellbookPanel == null) return;

        int count = caster.spellbook.Length;
        spellbookCards  = new Image[count];
        spellbookLabels = new TextMeshProUGUI[count];

        RectTransform panelRect = spellbookPanel.GetComponent<RectTransform>();

        // Simple grid: 4 columns
        int cols     = 4;
        float cardW  = 110f;
        float cardH  = 80f;
        float gapX   = 12f;
        float gapY   = 12f;
        float startX = -(cols * (cardW + gapX) - gapX) / 2f + cardW / 2f;
        float startY = 60f;

        for (int i = 0; i < count; i++)
        {
            int capturedIndex = i;
            AbilityDef ab = caster.spellbook[i];

            // Card background
            GameObject cardGO = new GameObject("SpellCard_" + i, typeof(RectTransform), typeof(Image), typeof(Button));
            cardGO.transform.SetParent(spellbookPanel.transform, false);

            RectTransform rt = cardGO.GetComponent<RectTransform>();
            int col = i % cols;
            int row = i / cols;
            rt.sizeDelta        = new Vector2(cardW, cardH);
            rt.anchoredPosition = new Vector2(startX + col * (cardW + gapX), startY - row * (cardH + gapY));

            Image bg = cardGO.GetComponent<Image>();
            bg.color = ColorNormal;
            spellbookCards[i] = bg;

            // Category color bar (left strip)
            GameObject stripGO = new GameObject("Strip", typeof(RectTransform), typeof(Image));
            stripGO.transform.SetParent(cardGO.transform, false);
            RectTransform stripRt = stripGO.GetComponent<RectTransform>();
            stripRt.anchorMin   = new Vector2(0, 0);
            stripRt.anchorMax   = new Vector2(0, 1);
            stripRt.sizeDelta   = new Vector2(5f, 0);
            stripRt.anchoredPosition = Vector2.zero;
            stripGO.GetComponent<Image>().color = CategoryColor(ab.category);

            // Ability name label
            GameObject labelGO = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelGO.transform.SetParent(cardGO.transform, false);
            RectTransform labelRt = labelGO.GetComponent<RectTransform>();
            labelRt.anchorMin        = new Vector2(0.08f, 0.5f);
            labelRt.anchorMax        = new Vector2(1f, 1f);
            labelRt.offsetMin        = Vector2.zero;
            labelRt.offsetMax        = Vector2.zero;
            TextMeshProUGUI tmp      = labelGO.GetComponent<TextMeshProUGUI>();
            tmp.text                 = ab.abilityName;
            tmp.fontSize             = 11f;
            tmp.color                = Color.white;
            tmp.alignment            = TextAlignmentOptions.MidlineLeft;
            spellbookLabels[i]       = tmp;

            // Category sub-label
            GameObject subGO = new GameObject("Sub", typeof(RectTransform), typeof(TextMeshProUGUI));
            subGO.transform.SetParent(cardGO.transform, false);
            RectTransform subRt    = subGO.GetComponent<RectTransform>();
            subRt.anchorMin        = new Vector2(0.08f, 0f);
            subRt.anchorMax        = new Vector2(1f, 0.5f);
            subRt.offsetMin        = Vector2.zero;
            subRt.offsetMax        = Vector2.zero;
            TextMeshProUGUI subTmp = subGO.GetComponent<TextMeshProUGUI>();
            subTmp.text            = ab.category.ToString();
            subTmp.fontSize        = 9f;
            subTmp.color           = new Color(1f, 1f, 1f, 0.5f);
            subTmp.alignment       = TextAlignmentOptions.MidlineLeft;

            // Click handler
            Button btn = cardGO.GetComponent<Button>();
            btn.onClick.AddListener(() => OnSpellCardClicked(capturedIndex));
        }
    }

    void OnSpellCardClicked(int spellbookIndex)
    {
        pendingSpellbookIndex = spellbookIndex;
        RefreshSpellbookCards();
    }

    void RefreshSpellbookCards()
    {
        if (spellbookCards == null || caster == null) return;

        for (int i = 0; i < spellbookCards.Length; i++)
        {
            if (spellbookCards[i] == null) continue;

            if (i == pendingSpellbookIndex)
            {
                spellbookCards[i].color = ColorPendingCard;
            }
            else if (caster.IsEquipped(i, out int slot))
            {
                spellbookCards[i].color = ColorEquipped;
                if (spellbookLabels[i] != null)
                    spellbookLabels[i].text = caster.spellbook[i].abilityName + " [" + (slot + 1) + "]";
            }
            else
            {
                spellbookCards[i].color = ColorNormal;
                if (spellbookLabels[i] != null)
                    spellbookLabels[i].text = caster.spellbook[i].abilityName;
            }
        }
    }

    Color CategoryColor(AbilityCategory cat)
    {
        switch (cat)
        {
            case AbilityCategory.Heal:    return ColorHeal;
            case AbilityCategory.Support: return ColorSupport;
            default:                      return ColorDamage;
        }
    }
}
