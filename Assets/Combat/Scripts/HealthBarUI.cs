using UnityEngine;
using UnityEngine.UI;

public class HealthBarUI : MonoBehaviour
{
    public Health health;
    public Image fillImage;

    void Start()
    {
        if (health != null)
        {
            health.onHealthChanged.AddListener(UpdateBar);
            UpdateBar(health.currentHealth, health.maxHealth);
        }
    }

    void UpdateBar(float current, float max)
    {
        if (fillImage != null)
        {
            fillImage.fillAmount = max > 0f ? current / max : 0f;
        }
    }
}
