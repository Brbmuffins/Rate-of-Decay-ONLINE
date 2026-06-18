using UnityEngine;
using UnityEngine.UI;

public class TooltipUI : MonoBehaviour
{
    public static TooltipUI Instance;

    private RectTransform panelRect;
    private Text label;

    void Awake()
    {
        Instance = this;

        GameObject panel = new GameObject("TooltipPanel", typeof(RectTransform));
        panel.transform.SetParent(transform, false);
        panelRect = panel.GetComponent<RectTransform>();
        panelRect.sizeDelta = new Vector2(160, 30);
        panelRect.pivot = new Vector2(0, 1);

        Image background = panel.AddComponent<Image>();
        background.color = new Color(0, 0, 0, 0.85f);

        GameObject textObj = new GameObject("TooltipText", typeof(RectTransform));
        textObj.transform.SetParent(panel.transform, false);
        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(6, 4);
        textRect.offsetMax = new Vector2(-6, -4);

        label = textObj.AddComponent<Text>();
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize = 14;
        label.color = Color.white;
        label.alignment = TextAnchor.UpperLeft;
        label.supportRichText = true;

        panel.SetActive(false);
    }

    public void Show(ItemData item, Vector2 screenPosition, string hintOverride = null)
    {
        if (item == null) return;

        string colorHex = ColorUtility.ToHtmlStringRGB(item.RarityColor);
        string text = "<b><color=#" + colorHex + ">" + item.itemName + "</color></b>";

        if (!string.IsNullOrEmpty(item.description))
        {
            text += "\n" + item.description;
        }

        string hint = hintOverride ?? GetHint(item);
        if (!string.IsNullOrEmpty(hint))
        {
            text += "\n<color=#AAAAAA><i>" + hint + "</i></color>";
        }

        label.text = text;
        panelRect.sizeDelta = MeasureSize(text);
        panelRect.position = screenPosition;
        panelRect.gameObject.SetActive(true);
    }

    public void Show(string text, Vector2 screenPosition)
    {
        label.text = text;
        panelRect.sizeDelta = MeasureSize(text);
        panelRect.position = screenPosition;
        panelRect.gameObject.SetActive(true);
    }

    public void Hide()
    {
        panelRect.gameObject.SetActive(false);
    }

    string GetHint(ItemData item)
    {
        if (item.itemType == ItemType.Consumable) return "Click to use";
        if (item.equippable) return "Click to equip";
        return "";
    }

    Vector2 MeasureSize(string text)
    {
        string[] lines = text.Split('\n');
        int longestLine = 0;

        foreach (string line in lines)
        {
            string stripped = System.Text.RegularExpressions.Regex.Replace(line, "<.*?>", "");
            if (stripped.Length > longestLine) longestLine = stripped.Length;
        }

        float width = Mathf.Clamp(longestLine * 7f + 16f, 120f, 260f);
        float height = lines.Length * 18f + 8f;

        return new Vector2(width, height);
    }
}
