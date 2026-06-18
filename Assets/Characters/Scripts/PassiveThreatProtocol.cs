using UnityEngine;

// GUARDIAN passive — Threat Protocol
// Taking damage generates Threat stacks.
// At 5 stacks: nearby enemies redirect aggro to you + 20% DR for 6s.
// Death clears all stacks.
public class PassiveThreatProtocol : ClassPassive
{
    public int   stacksNeeded       = 5;
    public float aggroRadius        = 12f;
    public float damageReduction    = 0.20f;
    public float drDuration         = 6f;
    public string enemyTag          = "Enemy";

    private int   _stacks        = 0;
    private float _drExpiry      = 0f;
    private bool  _drActive      = false;

    public int   ThreatStacks   => _stacks;

    // Call this externally to add stacks (e.g. from Breach Slam, Siege Mode).
    public void AddStacks(int n)
    {
        _stacks += n;
        CheckActivation();
    }

    protected override void Awake()
    {
        base.Awake();
        if (health != null)
        {
            health.onDamageTaken.AddListener(OnDamageTaken);
            health.onDeath.AddListener(OnDeath);
        }
    }

    void Update()
    {
        if (_drActive && Time.time >= _drExpiry)
        {
            _drActive = false;
            health?.ClearDamageReduction();
        }
    }

    void OnDamageTaken(float _)
    {
        _stacks++;
        CheckActivation();
    }

    void OnDeath()
    {
        _stacks = 0;
        _drActive = false;
        health?.ClearDamageReduction();
    }

    void CheckActivation()
    {
        if (_stacks < stacksNeeded) return;
        _stacks = 0;
        ActivateThreatBurst();
    }

    void ActivateThreatBurst()
    {
        // Redirect all nearby enemy aggro to this player
        Collider[] hits = Physics.OverlapSphere(transform.position, aggroRadius);
        foreach (var col in hits)
        {
            if (!col.CompareTag(enemyTag)) continue;
            EnemyAI ai = col.GetComponent<EnemyAI>();
            ai?.SetAggroTarget(transform);
        }

        // Apply DR
        health?.SetDamageReduction(damageReduction);
        _drActive = true;
        _drExpiry = Time.time + drDuration;
    }

    public override void OnAbilityCast(AbilityDef ability) { }
}
