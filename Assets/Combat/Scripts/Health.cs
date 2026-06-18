using UnityEngine;
using UnityEngine.Events;

public class Health : MonoBehaviour
{
    public float maxHealth = 100f;
    public float currentHealth;

    public UnityEvent<float, float> onHealthChanged;
    public UnityEvent onDeath;

    public float Fraction => maxHealth > 0f ? currentHealth / maxHealth : 0f;

    private float shieldRemaining = 0f;
    public bool HasShield => shieldRemaining > 0f;
    public float ShieldRemaining => shieldRemaining;

    void Awake()
    {
        currentHealth = maxHealth;
    }

    public void ApplyShield(float amount)
    {
        shieldRemaining = Mathf.Max(shieldRemaining, amount);
    }

    public void TakeDamage(float amount)
    {
        if (currentHealth <= 0f) return;

        if (shieldRemaining > 0f)
        {
            float absorbed = Mathf.Min(shieldRemaining, amount);
            shieldRemaining -= absorbed;
            amount -= absorbed;
        }

        if (amount <= 0f) return;

        currentHealth = Mathf.Max(0f, currentHealth - amount);
        onHealthChanged?.Invoke(currentHealth, maxHealth);

        if (currentHealth <= 0f)
        {
            onDeath?.Invoke();
        }
    }

    public void Heal(float amount)
    {
        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
        onHealthChanged?.Invoke(currentHealth, maxHealth);
    }
}
