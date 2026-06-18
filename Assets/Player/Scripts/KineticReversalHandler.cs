using System.Collections;
using UnityEngine;

// Guardian — Kinetic Reversal
// Absorb incoming damage for absorbWindow seconds, then release as a cone burst.
// VFX: brbmuffins Dark Arts/Fantasy Pack/Prefabs/Shield buff.prefab  (absorb phase, tint blue)
//      brbmuffins Technologies/Particle Pack/Fire & Explosion Effects/EnergyExplosion.prefab (release)
//      brbmuffins Dark Arts/Fantasy Pack/Prefabs/Effects normal/Lightning strike skill.prefab (cone)
public class KineticReversalHandler : MonoBehaviour
{
    [Header("Settings")]
    public float absorbWindow  = 3f;
    public float coneRange     = 8f;
    public float coneAngle     = 70f;
    public float minDamage     = 20f;
    public float maxDamage     = 60f;
    public string enemyTag     = "Enemy";

    [Header("VFX")]
    // Assign: brbmuffins Dark Arts/.../Shield buff.prefab
    public GameObject absorbVFX;
    // Assign: brbmuffins Technologies/.../EnergyExplosion.prefab
    public GameObject releaseVFX;

    private Health  _health;
    private bool    _active = false;
    private GameObject _absorbInstance;

    void Awake()
    {
        _health = GetComponent<Health>();
    }

    public void Activate()
    {
        if (_active) return;
        StartCoroutine(AbsorbRoutine());
    }

    private IEnumerator AbsorbRoutine()
    {
        _active = true;
        _health.BeginAbsorption(absorbWindow);

        if (absorbVFX != null)
            _absorbInstance = Instantiate(absorbVFX, transform.position, Quaternion.identity, transform);

        yield return new WaitForSeconds(absorbWindow);

        Release();
    }

    private void Release()
    {
        _active = false;
        float absorbed = _health.AbsorbedAmount;
        _health.EndAbsorption();

        if (_absorbInstance != null) Destroy(_absorbInstance);

        // Scale damage: 0 absorbed → minDamage, 100+ absorbed → maxDamage
        float t      = Mathf.Clamp01(absorbed / 100f);
        float damage = Mathf.Lerp(minDamage, maxDamage, t);

        // Cone damage
        Collider[] hits = Physics.OverlapSphere(transform.position, coneRange);
        foreach (var col in hits)
        {
            if (!col.CompareTag(enemyTag)) continue;
            Vector3 dir = col.transform.position - transform.position;
            dir.y = 0;
            if (dir.sqrMagnitude < 0.0001f) continue;
            float angle = Vector3.Angle(transform.forward, dir);
            if (angle > coneAngle / 2f) continue;
            col.GetComponent<Health>()?.TakeDamage(damage, gameObject);
        }

        if (releaseVFX != null)
        {
            GameObject fx = Instantiate(releaseVFX, transform.position + transform.forward, transform.rotation);
            Destroy(fx, 3f);
        }
    }
}
