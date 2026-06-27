using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

// ═══════════════════════════════════════════════════════════════════════════
//  ShadowbladeAbilities
//  Attach to the Shadowblade player prefab alongside Health and StatusEffectManager.
//
//  DESIGN PILLARS
//  ─────────────
//  • Curse-first: every ability applies or amplifies Cursed (DoT) stacks.
//  • Dark Harvest detonates all debuffs on a target for burst — the core loop is
//    "curse → harvest."
//  • No aim required for most abilities — the Shadowblade is the accessibility class.
//    Corruption is passive, Silence Ward is placed, Dark Harvest is targeted by proximity.
//  • Shadow Veil (ultimate) makes the Shadowblade temporarily untouchable and
//    curses everything nearby on exit.
//
//  ABILITY SUMMARY
//  ───────────────
//  [PASSIVE]  Corruption       — every ability hit adds a Cursed (DoT) stack.
//  [Q]        Void Bolt        — cone burst; applies Cursed + Slow.
//  [W]        Silence Ward     — places a zone; Silences + Curses enemies inside.
//  [E]        Dark Mark        — single target; applies Weakened (dmg +25%) + Cursed.
//  [R]        Dark Harvest     — detonates ALL debuffs on nearby enemies for burst dmg.
//  [F / Ult]  Shadow Veil      — 2s invulnerable stealth; exit applies Cursed to all nearby.
//
//  WIRING
//  ──────
//  AbilityCaster on the same GameObject fires the cone/circle/rect visuals.
//  This script handles the damage math and status application.
//  Wire AbilityCaster.onAbilityUsed → WraithAbilities.OnAbilityFired(int slot)
//  if you want automatic VFX sync; otherwise call the public methods directly.
// ═══════════════════════════════════════════════════════════════════════════

[RequireComponent(typeof(Health))]
[RequireComponent(typeof(StatusEffectManager))]
public class WraithAbilities : MonoBehaviour
{
    // ── Corruption (passive) ──────────────────────────────────────────────
    [Header("Corruption (Passive DoT)")]
    public float corruptionDPS      = 8f;    // damage per second of each stack
    public float corruptionDuration = 4f;    // how long each stack lasts

    // ── Dark Blast [Q] ────────────────────────────────────────────────────
    [Header("Dark Blast [Q] — Cone")]
    public float darkBlastDamage    = 25f;
    public float darkBlastRange     = 7f;
    public float darkBlastAngle     = 60f;   // full cone angle in degrees
    public float darkBlastSlow      = 0.45f; // 45% slow
    public float darkBlastSlowDur   = 2f;
    public float darkBlastCooldown  = 4f;

    // ── Null Field [W] ────────────────────────────────────────────────────
    [Header("Null Field [W] — Placed Zone")]
    public float nullFieldRadius    = 4f;
    public float nullFieldDuration  = 5f;    // how long the zone lasts
    public float nullFieldSuppressDur = 1.5f;// suppress duration applied per tick
    public float nullFieldTickRate  = 0.5f;
    public float nullFieldCooldown  = 10f;
    public GameObject nullFieldVFX;          // drag in a Dark Arts "Death magic circle" prefab

    // ── Event Horizon [E] ─────────────────────────────────────────────────
    [Header("Event Horizon [E] — Single Target")]
    public float eventHorizonRange   = 12f;
    public float eventHorizonDamage  = 15f;
    public float exposedDuration     = 6f;
    public float eventHorizonCooldown = 7f;

    // ── Collapse [R] ──────────────────────────────────────────────────────
    [Header("Collapse [R] — Debuff Detonation")]
    public float collapseRange       = 8f;
    public float collapseDmgPerStack = 20f;  // per consumed debuff stack
    public float collapseCooldown    = 12f;
    public GameObject collapseVFX;           // "EnergyExplosion" or similar

    // ── Phase Shift [F] ──────────────────────────────────────────────────
    [Header("Phase Shift [F] — Ultimate")]
    public float phaseShiftDuration  = 2f;
    public float phaseShiftExitRadius = 6f;
    public float phaseShiftExitDoTDPS = 20f;
    public float phaseShiftExitDur   = 5f;
    public float phaseShiftCooldown  = 30f;
    public GameObject phaseShiftVFX;         // dissolve / stealth shimmer VFX

    // ── Internal state ────────────────────────────────────────────────────
    Health              _health;
    StatusEffectManager _status;

    float _cdDarkBlast;
    float _cdNullField;
    float _cdEventHorizon;
    float _cdCollapse;
    float _cdPhaseShift;

    bool  _phaseActive;

    // ── Lifecycle ─────────────────────────────────────────────────────────

    void Awake()
    {
        _health = GetComponent<Health>();
        _status = GetComponent<StatusEffectManager>();
    }

    void Update()
    {
        _cdDarkBlast    = Mathf.Max(0f, _cdDarkBlast    - Time.deltaTime);
        _cdNullField    = Mathf.Max(0f, _cdNullField    - Time.deltaTime);
        _cdEventHorizon = Mathf.Max(0f, _cdEventHorizon - Time.deltaTime);
        _cdCollapse     = Mathf.Max(0f, _cdCollapse     - Time.deltaTime);
        _cdPhaseShift   = Mathf.Max(0f, _cdPhaseShift   - Time.deltaTime);

        HandleInput();
    }

    void HandleInput()
    {
        var kb = Keyboard.current;
        if (kb == null) return;
        if (kb.qKey.wasPressedThisFrame) UseDarkBlast();
        if (kb.wKey.wasPressedThisFrame) UseNullField();
        if (kb.eKey.wasPressedThisFrame) UseEventHorizon();
        if (kb.rKey.wasPressedThisFrame) UseCollapse();
        if (kb.fKey.wasPressedThisFrame) UsePhaseShift();
    }

    // ── Public Cooldown Queries (for AbilityBar UI) ───────────────────────

    public float DarkBlastCooldownFraction    => _cdDarkBlast    / darkBlastCooldown;
    public float NullFieldCooldownFraction    => _cdNullField    / nullFieldCooldown;
    public float EventHorizonCooldownFraction => _cdEventHorizon / eventHorizonCooldown;
    public float CollapseCooldownFraction     => _cdCollapse     / collapseCooldown;
    public float PhaseShiftCooldownFraction   => _cdPhaseShift   / phaseShiftCooldown;

    // ═════════════════════════════════════════════════════════════════════
    //  ABILITIES
    // ═════════════════════════════════════════════════════════════════════

    // ── [Q] Dark Blast ────────────────────────────────────────────────────
    //  Cone in front of the Wraith. Instant damage + Slow + Corruption stack.

    public void UseDarkBlast()
    {
        if (_cdDarkBlast > 0f || _phaseActive) return;
        _cdDarkBlast = darkBlastCooldown;

        int hits = 0;
        foreach (var col in Physics.OverlapSphere(transform.position, darkBlastRange))
        {
            if (!IsEnemy(col)) continue;
            Vector3 dir = (col.transform.position - transform.position).normalized;
            float angle = Vector3.Angle(transform.forward, dir);
            if (angle > darkBlastAngle * 0.5f) continue;

            var h = col.GetComponent<Health>();
            var s = col.GetComponent<StatusEffectManager>();
            if (h == null) continue;

            h.TakeDamage(darkBlastDamage, gameObject);
            s?.AddEffect(new StatusEffect(StatusEffectType.Slow, darkBlastSlowDur, darkBlastSlow, gameObject));
            ApplyCorruption(s);
            hits++;
        }

        Debug.Log($"[Shadowblade] Dark Blast hit {hits} enemies.");
    }

    // ── [W] Null Field ────────────────────────────────────────────────────
    //  Places a zone at the Wraith's position. Enemies inside are Suppressed
    //  and receive a Corruption tick every 0.5s.

    public void UseNullField()
    {
        if (_cdNullField > 0f || _phaseActive) return;
        _cdNullField = nullFieldCooldown;

        Vector3 pos = transform.position;

        if (nullFieldVFX != null)
        {
            var vfx = Instantiate(nullFieldVFX, pos, Quaternion.identity);
            Destroy(vfx, nullFieldDuration + 0.5f);
        }

        StartCoroutine(NullFieldRoutine(pos));
    }

    IEnumerator NullFieldRoutine(Vector3 center)
    {
        float elapsed = 0f;
        while (elapsed < nullFieldDuration)
        {
            foreach (var col in Physics.OverlapSphere(center, nullFieldRadius))
            {
                if (!IsEnemy(col)) continue;
                var s = col.GetComponent<StatusEffectManager>();
                s?.AddEffect(new StatusEffect(StatusEffectType.Silenced, nullFieldSuppressDur, 0f, gameObject));
                ApplyCorruption(s);
            }
            yield return new WaitForSeconds(nullFieldTickRate);
            elapsed += nullFieldTickRate;
        }
    }

    // ── [E] Event Horizon ─────────────────────────────────────────────────
    //  Hits the nearest enemy within range. Applies Exposed (all damage +25%)
    //  and a Corruption stack. Pairs with any teammate's damage.

    public void UseEventHorizon()
    {
        if (_cdEventHorizon > 0f || _phaseActive) return;
        _cdEventHorizon = eventHorizonCooldown;

        GameObject target = FindNearestEnemy(eventHorizonRange);
        if (target == null) { Debug.Log("[Wraith] Event Horizon — no target in range."); return; }

        var h = target.GetComponent<Health>();
        var s = target.GetComponent<StatusEffectManager>();

        h?.TakeDamage(eventHorizonDamage, gameObject);
        s?.AddEffect(new StatusEffect(StatusEffectType.Weakened, exposedDuration, 0f, gameObject));
        ApplyCorruption(s);

        Debug.Log($"[Shadowblade] Dark Mark → {target.name} (Weakened + Cursed)");
    }

    // ── [R] Collapse ──────────────────────────────────────────────────────
    //  Detonates every debuff stack on every enemy within range.
    //  Damage = stacks × collapseDmgPerStack. Big finisher after stacking.

    public void UseCollapse()
    {
        if (_cdCollapse > 0f || _phaseActive) return;
        _cdCollapse = collapseCooldown;

        int totalStacks = 0;
        foreach (var col in Physics.OverlapSphere(transform.position, collapseRange))
        {
            if (!IsEnemy(col)) continue;
            var s = col.GetComponent<StatusEffectManager>();
            var h = col.GetComponent<Health>();
            if (s == null || h == null) continue;

            int stacks = s.ConsumeDebuffStacks();
            if (stacks <= 0) continue;

            float dmg = stacks * collapseDmgPerStack;
            h.TakeDamage(dmg, gameObject);
            totalStacks += stacks;

            // Spawn detonation VFX at each enemy
            if (collapseVFX != null)
            {
                var vfx = Instantiate(collapseVFX,
                    col.transform.position + Vector3.up, Quaternion.identity);
                Destroy(vfx, 2f);
            }
        }

        Debug.Log($"[Shadowblade] Collapse detonated {totalStacks} total stacks.");
    }

    // ── [F] Phase Shift ───────────────────────────────────────────────────
    //  2 seconds of invulnerability + stealth.
    //  On exit: every nearby enemy gets a heavy Corruption DoT.

    public void UsePhaseShift()
    {
        if (_cdPhaseShift > 0f || _phaseActive) return;
        _cdPhaseShift = phaseShiftCooldown;

        StartCoroutine(PhaseShiftRoutine());
    }

    IEnumerator PhaseShiftRoutine()
    {
        _phaseActive = true;
        _health.isInvulnerable = true;

        if (phaseShiftVFX != null)
        {
            var vfx = Instantiate(phaseShiftVFX, transform.position, Quaternion.identity);
            Destroy(vfx, phaseShiftDuration + 0.5f);
        }

        // Hide renderer briefly to signal stealth
        var renderers = GetComponentsInChildren<Renderer>();
        foreach (var r in renderers) r.enabled = false;

        yield return new WaitForSeconds(phaseShiftDuration);

        // Re-appear
        foreach (var r in renderers) r.enabled = true;
        _health.isInvulnerable = false;
        _phaseActive = false;

        // Exit burst — corrupt everything nearby
        foreach (var col in Physics.OverlapSphere(transform.position, phaseShiftExitRadius))
        {
            if (!IsEnemy(col)) continue;
            var s = col.GetComponent<StatusEffectManager>();
            s?.AddEffect(new StatusEffect(StatusEffectType.Cursed,
                phaseShiftExitDur, phaseShiftExitDoTDPS, gameObject));
        }

        Debug.Log("[Wraith] Phase Shift exit — nearby enemies corrupted.");
    }

    // ═════════════════════════════════════════════════════════════════════
    //  HELPERS
    // ═════════════════════════════════════════════════════════════════════

    // Corruption passive — applied after every ability hit.
    void ApplyCorruption(StatusEffectManager target)
    {
        if (target == null) return;
        // Each call refreshes/adds a DamageOverTime stack.
        target.AddEffect(new StatusEffect(
            StatusEffectType.Cursed,
            corruptionDuration,
            corruptionDPS,
            gameObject));
    }

    bool IsEnemy(Collider col)
    {
        return col.CompareTag("Enemy") && col.gameObject != gameObject;
    }

    GameObject FindNearestEnemy(float range)
    {
        GameObject best = null;
        float bestDist  = range;
        foreach (var col in Physics.OverlapSphere(transform.position, range))
        {
            if (!IsEnemy(col)) continue;
            float d = Vector3.Distance(transform.position, col.transform.position);
            if (d < bestDist) { bestDist = d; best = col.gameObject; }
        }
        return best;
    }
}
