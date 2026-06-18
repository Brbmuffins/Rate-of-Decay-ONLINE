using System.Collections;
using UnityEngine;

// Guardian — Siege Mode
// Locks movement, applies 40% DR, and triples Threat generation for 6 seconds.
// VFX: brbmuffins Technologies/Particle Pack/Smoke & Steam Effects/Prefabs/Steam.prefab (on anchor)
//      brbmuffins Dark Arts/Fantasy Pack/Prefabs/Shield buff.prefab (defense aura)
public class SiegeModeHandler : MonoBehaviour
{
    [Header("Settings")]
    public float duration         = 6f;
    public float damageReduction  = 0.40f;

    [Header("VFX")]
    // Assign: brbmuffins Technologies/.../Steam.prefab
    public GameObject anchorVFX;
    // Assign: brbmuffins Dark Arts/.../Shield buff.prefab
    public GameObject auraVFX;

    private Health               _health;
    private PlayerMovement       _movement;
    private PassiveThreatProtocol _threat;
    private bool                 _active = false;

    void Awake()
    {
        _health   = GetComponent<Health>();
        _movement = GetComponent<PlayerMovement>();
        _threat   = GetComponent<PassiveThreatProtocol>();
    }

    public void Activate()
    {
        if (_active) return;
        StartCoroutine(SiegeRoutine());
    }

    private IEnumerator SiegeRoutine()
    {
        _active = true;

        // Disable movement
        float savedSpeed = 0f;
        if (_movement != null) { savedSpeed = _movement.moveSpeed; _movement.moveSpeed = 0f; }

        // Apply DR
        _health?.SetDamageReduction(damageReduction);

        // Threat gen is handled by ThreatProtocol's stack listener;
        // triple rate by adding extra stacks on each damage event.
        // We do this by forcing 3 stacks on each hit via a temporary listener.
        bool boosting = true;
        void OnHit(float _) { if (boosting) _threat?.AddStacks(2); } // +2 on top of the normal +1
        if (_health != null) _health.onDamageTaken.AddListener(OnHit);

        GameObject anchor = null, aura = null;
        if (anchorVFX != null)
        {
            anchor = Instantiate(anchorVFX, transform.position, Quaternion.identity, transform);
        }
        if (auraVFX != null)
        {
            aura = Instantiate(auraVFX, transform.position, Quaternion.identity, transform);
        }

        yield return new WaitForSeconds(duration);

        boosting = false;
        if (_health != null) _health.onDamageTaken.RemoveListener(OnHit);

        // Restore
        if (_movement != null) _movement.moveSpeed = savedSpeed;
        _health?.ClearDamageReduction();

        if (anchor != null) Destroy(anchor);
        if (aura   != null) Destroy(aura);

        _active = false;
    }
}
