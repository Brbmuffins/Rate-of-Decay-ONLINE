using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class ObjectUI : MonoBehaviour
{
    public GameObject uiRoot;
    public TMP_Text nameText;
    public Image healthFill;

    private Stats stats;

    void Start()
    {
        stats = GetComponentInParent<Stats>();

        if (nameText == null && uiRoot != null)
            nameText = uiRoot.GetComponentInChildren<TMP_Text>(true);

        if (stats != null && nameText != null)
            nameText.text = stats.characterName;

        UpdateHealthBar();

        if (uiRoot != null)
            uiRoot.SetActive(false);
    }

    void Update()
    {
        UpdateHealthBar();
    }

    void UpdateHealthBar()
    {
        if (stats == null || healthFill == null)
            return;

        healthFill.fillAmount = (float)stats.currentHealth / stats.maxHealth;
    }

    public void ShowUI()
    {
        if (uiRoot != null)
            uiRoot.SetActive(true);
    }

    public void HideUI()
    {
        if (uiRoot != null)
            uiRoot.SetActive(false);
    }
}