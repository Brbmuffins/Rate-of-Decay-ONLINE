using UnityEngine;

// MEDIC passive — Triage Loop
// Each ally you heal returns 8% of the heal amount back to your own HP.
// Hooks into every Health.onHealApplied event on tracked allies.
public class PassiveTriageLoop : ClassPassive
{
    public float feedbackPercent = 0.08f;
    public float allyTrackRadius = 20f;     // range to auto-detect and hook allies
    public string playerTag      = "Player";

    protected override void Awake()
    {
        base.Awake();
    }

    void Start()
    {
        // Find all ally Health components and subscribe to their heal events.
        // Re-call if new players join mid-session.
        HookAllAllies();
    }

    public void HookAllAllies()
    {
        GameObject[] players = GameObject.FindGameObjectsWithTag(playerTag);
        foreach (var p in players)
        {
            if (p == gameObject) continue;
            Health h = p.GetComponent<Health>();
            if (h != null)
            {
                // Remove first to avoid double-subscribe
                h.onHealApplied.RemoveListener(OnAllyHealed);
                h.onHealApplied.AddListener(OnAllyHealed);
            }
        }
    }

    void OnAllyHealed(float healAmount)
    {
        float feedback = healAmount * feedbackPercent;
        health?.Heal(feedback);
    }

    public override void OnAbilityCast(AbilityDef ability) { }
}
