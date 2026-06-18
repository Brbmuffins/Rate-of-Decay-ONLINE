using UnityEngine;

public class Stats : MonoBehaviour
{
    [Header("Basic Info")]
    public string characterName = "Enemy";

    [Header("Health")]
    public int maxHealth = 100;
    public int currentHealth = 100;

    [Header("Combat")]
    public int attackPower = 10;
    public int armor = 0;

    [Header("State")]
    public bool isDead = false;

    void Start()
    {
        currentHealth = maxHealth;
    }

    public void TakeDamage(int amount)
    {
        if (isDead)
            return;

        int damage = Mathf.Max(amount - armor, 1);

        currentHealth -= damage;

        Debug.Log(characterName + " takes " + damage + " damage.");

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    public void Heal(int amount)
    {
        if (isDead)
            return;

        currentHealth += amount;

        if (currentHealth > maxHealth)
            currentHealth = maxHealth;
    }

    void Die()
    {
        isDead = true;

        Debug.Log(characterName + " died.");

        // Optional:
        // Destroy(gameObject);
        // Play animation
        // Drop loot
    }
}