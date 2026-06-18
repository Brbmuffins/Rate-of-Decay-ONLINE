using UnityEngine;

// Wraith deployable — Shadow Relay
// Extends Phase Cloak duration +3s while Wraith is within 6 units.
// Reduces Phase Cloak cooldown by 4s per kill while active.
// VFX: brbmuffins Technologies/Particle Pack/Smoke & Steam Effects/Prefabs/GroundFog.prefab (dark)
//      brbmuffins Technologies/Particle Pack/Misc Effects/Prefabs/DustMotesEffect.prefab
public class ShadowRelayDeployable : MonoBehaviour
{
    public float influenceRadius  = 6f;
    public float cloakExtension   = 3f;    // bonus seconds on Phase Cloak
    public float killCDR          = 4f;    // CDR on kill while relay is active
    public float lifetime         = 30f;

    [Header("VFX")]
    // Assign: brbmuffins Technologies/.../GroundFog.prefab (set start color very dark)
    public GameObject idleVFX;

    [HideInInspector] public int ownerID;
    [HideInInspector] public Transform ownerTransform;

    private float _timer;

    void Start()
    {
        if (idleVFX != null)
            Instantiate(idleVFX, transform.position, Quaternion.identity, transform);

        if (DeployableManager.Instance != null)
            DeployableManager.Instance.Register(gameObject, ownerID, 1);
    }

    void Update()
    {
        _timer += Time.deltaTime;
        if (_timer >= lifetime) Destroy(gameObject);
    }

    public bool IsOwnerInRange()
    {
        if (ownerTransform == null) return false;
        return Vector3.Distance(ownerTransform.position, transform.position) <= influenceRadius;
    }

    void OnDestroy()
    {
        DeployableManager.Instance?.Unregister(gameObject);
    }
}
