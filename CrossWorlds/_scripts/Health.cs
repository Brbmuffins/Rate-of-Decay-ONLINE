using UnityEngine;
using UnityEngine.Events;
using Mirror;

/// <summary>
/// Health — Shared HP component used by players, enemies, and the boss.
/// Server-authoritative via SyncVar. Fires UnityEvents for damage and death.
///
/// Copy to: Assets/Game/Combat/Health.cs
///
/// Consumers:
///   WorldBossController  — boss damage, phases, reflect
///   EnemyController      — enemy death, aggro confirm
///   WaveSpawner          — enemy death tracking
///   WorldItem            — not used directly; players have Health too
/// </summary>
[System.Serializable]
public class DamageEvent : UnityEvent<float, GameObject> { }

public class Health : NetworkBehaviour
{
    // ─── Inspector ────────────────────────────────────────────────────────────
    [Header("Stats")]
    public float maxHp = 100f;
    public bool isInvulnerable = false;

    // ─── Synced State ─────────────────────────────────────────────────────────
    [SyncVar(hook = nameof(OnHpSynced))]
    public float currentHp;

    [SyncVar]
    public bool isDead = false;

    // ─── Events (server only — subscribe in OnStartServer) ───────────────────
    /// Fired server-side on every hit: (damage, source GameObject)
    public DamageEvent onDamageReceived = new DamageEvent();
    /// Fired server-side once, when HP reaches 0
    public UnityEvent onDeath = new UnityEvent();
    /// Fired on all clients whenever HP changes — (normalized 0–1)
    public UnityEvent<float> onHpChanged = new UnityEvent<float>();

    // ─── Lifecycle ────────────────────────────────────────────────────────────
    void Awake()
    {
        currentHp = maxHp;
    }

    // ─── Server API ───────────────────────────────────────────────────────────
    /// <summary>Deal damage. source = the attacking GameObject, or null.</summary>
    [Server]
    public void TakeDamage(float amount, GameObject source)
    {
        if (isDead || isInvulnerable || amount <= 0f) return;

        currentHp = Mathf.Max(0f, currentHp - amount);
        onDamageReceived.Invoke(amount, source);

        if (currentHp <= 0f && !isDead)
        {
            isDead = true;
            onDeath.Invoke();
        }
    }

    /// <summary>Restore HP. Capped at maxHp. No-ops if dead.</summary>
    [Server]
    public void Heal(float amount)
    {
        if (isDead || amount <= 0f) return;
        currentHp = Mathf.Min(maxHp, currentHp + amount);
    }

    /// <summary>Instantly kill — bypasses invulnerability. Use for void/instakill mechanics.</summary>
    [Server]
    public void InstantKill(GameObject source)
    {
        if (isDead) return;
        currentHp = 0f;
        isDead = true;
        onDamageReceived.Invoke(maxHp, source);
        onDeath.Invoke();
    }

    // ─── SyncVar Hook (all clients) ───────────────────────────────────────────
    void OnHpSynced(float oldVal, float newVal)
    {
        float pct = maxHp > 0f ? Mathf.Clamp01(newVal / maxHp) : 0f;
        onHpChanged.Invoke(pct);
    }

    // ─── Convenience ─────────────────────────────────────────────────────────
    public float HpPercent => maxHp > 0f ? Mathf.Clamp01(currentHp / maxHp) : 0f;
}
