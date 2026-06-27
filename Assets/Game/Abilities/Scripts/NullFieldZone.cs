using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Shadowblade — Silence Ward (curse field)
// Silences enemies inside (they cannot use abilities — see EnemyAI) AND applies a
// Cursed damage-over-time while they remain affected.
// VFX: brbmuffins Technologies/Particle Pack/Smoke & Steam Effects/Prefabs/GroundFog.prefab
//      tinted purple/grey. Drop as child of this object.
[RequireComponent(typeof(SphereCollider))]
public class NullFieldZone : MonoBehaviour
{
    [Header("Settings")]
    public float duration    = 5f;
    public float radius      = 5f;
    public string enemyTag   = "Enemy";

    [Tooltip("Decay damage per second dealt to enemies in the field.")]
    public float decayDamagePerSecond = 4f;

    [Header("VFX")]
    // Assign: brbmuffins Technologies/.../GroundFog.prefab
    public GameObject zoneVFX;

    private readonly List<StatusEffectManager> _silenced = new List<StatusEffectManager>();
    private GameObject _vfxInstance;

    void Start()
    {
        SphereCollider col = GetComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius    = radius;

        if (zoneVFX != null)
            _vfxInstance = Instantiate(zoneVFX, transform.position, Quaternion.identity, transform);

        StartCoroutine(Expire());
    }

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(enemyTag)) return;
        var sem = other.GetComponent<StatusEffectManager>();
        if (sem == null) return;
        sem.AddEffect(new StatusEffect(StatusEffectType.Silenced, duration));
        if (decayDamagePerSecond > 0f)
            sem.AddEffect(new StatusEffect(StatusEffectType.Cursed, duration, decayDamagePerSecond));
        _silenced.Add(sem);
    }

    void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag(enemyTag)) return;
        // Suppress will expire naturally via StatusEffectManager.Update;
        // we just remove from tracking list.
        var sem = other.GetComponent<StatusEffectManager>();
        if (sem != null) _silenced.Remove(sem);
    }

    IEnumerator Expire()
    {
        yield return new WaitForSeconds(duration);
        Destroy(gameObject);
    }

    void OnDestroy()
    {
        if (_vfxInstance != null) Destroy(_vfxInstance);
    }
}
