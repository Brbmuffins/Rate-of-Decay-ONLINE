using UnityEngine;
using UnityEngine.Events;

// Extended Health — replaces the original.
// Backward compatible: TakeDamage / Heal / ApplyShield / currentHealth / maxHealth
// all work identically. New features are additive.
public class Health : MonoBehaviour
{
    [Header("Settings")]
    public float maxHealth = 100f;
    public float currentHealth;
    public bool isPlayer  = false;  // players go downed instead of dying outright
    public bool isRobotic = false;  // Defibrillator deals 60 burst dmg to robotics

    // ── Events ────────────────────────────────────────────────────
    public UnityEvent<float, float> onHealthChanged;    // (current, max)
    public UnityEvent               onDeath;
    public UnityEvent<float>        onDamageTaken;      // raw final damage (after shield, after redirect)
    public UnityEvent<float>        onHealApplied;      // heal amount (for Triage Loop)
    public UnityEvent<bool>         onDownedChanged;    // true = just went down, false = revived
    public UnityEvent<GameObject>   onKilledBy;         // who dealt the killing blow (for BountySystem)

    // ── Shield ────────────────────────────────────────────────────
    private float _shieldRemaining = 0f;
    public  bool  HasShield      => _shieldRemaining > 0f;
    public  float ShieldRemaining => _shieldRemaining;

    // ── Down State (players only) ──────────────────────────────────
    private bool _isDowned = false;
    public  bool IsDowned  => _isDowned;
    public  bool IsAlive   => !_isDowned && currentHealth > 0f;

    // ── Damage Redirect (Transfer Protocol) ───────────────────────
    // When set, a fraction of incoming damage is sent to redirectTarget instead.
    private Health _redirectTarget      = null;
    private float  _redirectFraction    = 0f;
    private float  _redirectExpiry      = 0f;

    // ── Damage Absorption (Kinetic Reversal) ──────────────────────
    // When active, absorbed damage accumulates instead of hitting HP.
    private bool  _absorbing        = false;
    private float _absorptionExpiry = 0f;
    private float _absorbedAmount   = 0f;
    public  float AbsorbedAmount    => _absorbedAmount;

    // ── DR modifier (Siege Mode, Threat Protocol) ─────────────────
    private float _damageReductionBonus = 0f; // 0.4 = 40% reduction
    public  float DamageReductionBonus  => _damageReductionBonus;

    // ── Gear / Attunement channels (driven by CharacterStats) ─────
    private float _baseMaxHealth       = 0f;   // captured at Awake, before gear
    private float _gearMaxHealthBonus  = 0f;
    private float _gearDamageReduction = 0f;   // stacks with ability DR
    public  float BaseMaxHealth        => _baseMaxHealth;

    public float Fraction => maxHealth > 0f ? currentHealth / maxHealth : 0f;

    // ── StatusEffect integration ───────────────────────────────────
    private StatusEffectManager _statusEffects;

    void Awake()
    {
        _baseMaxHealth = maxHealth;
        currentHealth  = maxHealth;
        _statusEffects = GetComponent<StatusEffectManager>();
    }

    void Update()
    {
        // Clear expired redirect
        if (_redirectTarget != null && Time.time >= _redirectExpiry)
            ClearRedirect();

        // Clear expired absorption
        if (_absorbing && Time.time >= _absorptionExpiry)
            _absorbing = false;
    }

    // ── Public API ────────────────────────────────────────────────

    public void ApplyShield(float amount)
    {
        _shieldRemaining = Mathf.Max(_shieldRemaining, amount);
    }

    // ── Invulnerability (Dodge Roll i-frames) ────────────────────
    // Set to true by PlayerMovement during a dodge. While true, all damage is ignored.
    [HideInInspector] public bool isInvulnerable = false;

    public void TakeDamage(float amount, GameObject source = null)
    {
        if (_isDowned) return;
        if (currentHealth <= 0f) return;
        if (isInvulnerable) return;   // dodge roll i-frames

        // Weakened: +25% incoming (Collapsing Void)
        if (_statusEffects != null && _statusEffects.IsWeakened)
            amount *= 1.25f;

        // Damage reduction (Siege Mode, Threat Protocol)
        amount *= (1f - Mathf.Clamp01(_damageReductionBonus));

        // Gear damage reduction (attunement system) — stacks multiplicatively
        amount *= (1f - _gearDamageReduction);

        // Damage redirect (Transfer Protocol) — redirect sends a portion to the medic
        if (_redirectTarget != null && _redirectTarget.IsAlive && Time.time < _redirectExpiry)
        {
            float redirected = amount * _redirectFraction;
            amount -= redirected;
            _redirectTarget.TakeDamage(redirected); // no source override for the redirect
        }

        // Absorption (Kinetic Reversal) — intercept damage before shield/HP
        if (_absorbing && Time.time < _absorptionExpiry)
        {
            _absorbedAmount += amount;
            onDamageTaken?.Invoke(amount);
            return; // absorbed — no HP lost
        }

        // Shield
        if (_shieldRemaining > 0f)
        {
            float absorbed = Mathf.Min(_shieldRemaining, amount);
            _shieldRemaining -= absorbed;
            amount -= absorbed;
        }

        if (amount <= 0f) return;

        currentHealth = Mathf.Max(0f, currentHealth - amount);
        onHealthChanged?.Invoke(currentHealth, maxHealth);
        onDamageTaken?.Invoke(amount);

        if (currentHealth <= 0f)
            HandleDeath(source);
    }

    public void Heal(float amount)
    {
        if (_isDowned || currentHealth <= 0f) return;
        float actual = Mathf.Min(amount, maxHealth - currentHealth);
        if (actual <= 0f) return;
        currentHealth += actual;
        onHealthChanged?.Invoke(currentHealth, maxHealth);
        onHealApplied?.Invoke(actual);
    }

    // Defibrillator: revive a downed player at hpPercent (e.g. 0.3 = 30%)
    public void Revive(float hpPercent)
    {
        if (!_isDowned) return;
        _isDowned     = false;
        currentHealth = maxHealth * Mathf.Clamp01(hpPercent);
        onDownedChanged?.Invoke(false);
        onHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    // ── Transfer Protocol ──────────────────────────────────────────
    // Redirects `fraction` (0–1) of incoming damage to `target` for `duration` seconds.
    public void SetDamageRedirect(Health target, float fraction, float duration)
    {
        _redirectTarget   = target;
        _redirectFraction = fraction;
        _redirectExpiry   = Time.time + duration;
    }

    public void ClearRedirect()
    {
        _redirectTarget   = null;
        _redirectFraction = 0f;
    }

    // ── Kinetic Reversal ──────────────────────────────────────────
    public void BeginAbsorption(float duration)
    {
        _absorbing        = true;
        _absorbedAmount   = 0f;
        _absorptionExpiry = Time.time + duration;
    }

    public void EndAbsorption()
    {
        _absorbing = false;
    }

    // ── Siege Mode / Threat Protocol ─────────────────────────────
    public void SetDamageReduction(float fraction) => _damageReductionBonus = Mathf.Clamp01(fraction);
    public void ClearDamageReduction()             => _damageReductionBonus = 0f;

    // ── Gear / Attunement channels (called by CharacterStats) ─────
    // Adjusts max HP by the gear bonus, preserving current HP (and granting
    // the added HP on equip; clamping on unequip).
    public void SetGearMaxHealthBonus(float bonus)
    {
        if (_baseMaxHealth <= 0f) _baseMaxHealth = maxHealth; // safety if called pre-Awake
        float delta = bonus - _gearMaxHealthBonus;
        _gearMaxHealthBonus = bonus;
        maxHealth = _baseMaxHealth + _gearMaxHealthBonus;
        currentHealth = Mathf.Clamp(currentHealth + Mathf.Max(0f, delta), 0f, maxHealth);
        onHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    public void SetGearDamageReduction(float fraction)
        => _gearDamageReduction = Mathf.Clamp01(fraction);

    // ── Adaptive Shield ───────────────────────────────────────────
    // Called by AdaptiveShieldHandler each time the target takes a hit.
    public void GrowShield(float amount)
    {
        _shieldRemaining = Mathf.Min(_shieldRemaining + amount, 80f);
    }

    // ── Private ───────────────────────────────────────────────────
    private void HandleDeath(GameObject source)
    {
        if (isPlayer)
        {
            _isDowned = true;
            onDownedChanged?.Invoke(true);
            // Do NOT invoke onDeath for players — they are downed, not dead.
        }
        else
        {
            onDeath?.Invoke();
            onKilledBy?.Invoke(source);
        }
    }
}
