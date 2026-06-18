using UnityEngine;

// Medic deployable — Restoration Beacon
// Pulsing nanite emitter heals all allies in range every pulseInterval seconds.
// VFX: brbmuffins Dark Arts/Fantasy Pack/Prefabs/Effects normal/Magic circle.prefab (tint green)
//      brbmuffins Dark Arts/Fantasy Pack/Prefabs/Healing buff.prefab (per-heal burst)
public class RestorationBeacon : MonoBehaviour
{
    [Header("Healing")]
    public float healPerPulse   = 12f;
    public float pulseInterval  = 3f;
    public float radius         = 8f;
    public string playerTag     = "Player";
    public float lifetime       = 30f;

    [Header("VFX")]
    // Assign: brbmuffins Dark Arts/.../Magic circle.prefab
    public GameObject idleVFX;
    // Assign: brbmuffins Dark Arts/.../Healing buff.prefab
    public GameObject pulseVFX;

    [HideInInspector] public int ownerID;
    [HideInInspector] public GameObject owner;

    private float _pulseTimer;
    private float _lifetimeTimer;

    void Start()
    {
        if (idleVFX != null)
            Instantiate(idleVFX, transform.position, Quaternion.identity, transform);

        if (DeployableManager.Instance != null)
            DeployableManager.Instance.Register(gameObject, ownerID, 1);
    }

    void Update()
    {
        _lifetimeTimer += Time.deltaTime;
        if (_lifetimeTimer >= lifetime) { Destroy(gameObject); return; }

        _pulseTimer += Time.deltaTime;
        if (_pulseTimer < pulseInterval) return;
        _pulseTimer = 0f;
        Pulse();
    }

    void Pulse()
    {
        float mult = DeployableManager.Instance != null
            ? DeployableManager.Instance.GetMultiplier(gameObject)
            : 1f;

        Collider[] hits = Physics.OverlapSphere(transform.position, radius);
        foreach (var col in hits)
        {
            if (!col.CompareTag(playerTag)) continue;
            col.GetComponent<Health>()?.Heal(healPerPulse * mult);
        }

        if (pulseVFX != null)
        {
            GameObject fx = Instantiate(pulseVFX, transform.position, Quaternion.identity);
            Destroy(fx, 2f);
        }
    }

    void OnDestroy()
    {
        DeployableManager.Instance?.Unregister(gameObject);
    }
}
