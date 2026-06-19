using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class ObjectUI : MonoBehaviour
{
    public GameObject uiRoot;
    public TMP_Text nameText;
    public Image healthFill;

    private Health health;

    void Start()
    {
        health = GetComponentInParent<Health>();

        if (nameText == null && uiRoot != null)
            nameText = uiRoot.GetComponentInChildren<TMP_Text>(true);

        if (health != null && nameText != null)
            nameText.text = health.gameObject.name;

        // Event-driven instead of polling every frame.
        if (health != null)
            health.onHealthChanged.AddListener(OnHealthChanged);

        UpdateHealthBar();

        if (uiRoot != null)
            uiRoot.SetActive(false);
    }

    void OnDestroy()
    {
        if (health != null)
            health.onHealthChanged.RemoveListener(OnHealthChanged);
    }

    void OnHealthChanged(float current, float max) => UpdateHealthBar();

    void UpdateHealthBar()
    {
        if (health == null || healthFill == null)
            return;

        healthFill.fillAmount = health.Fraction;
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