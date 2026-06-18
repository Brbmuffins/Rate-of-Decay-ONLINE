using UnityEngine;

// Phaser deployable — Phase Relay
// Passive field: extends Singularity pull duration and increases AoE radius
// for the Phaser who owns it, while they are within influence range.
// VFX: brbmuffins Dark Arts/Fantasy Pack/Prefabs/Magic buff.prefab (tint purple)
//      brbmuffins Dark Arts/Fantasy Pack/Prefabs/Effects normal/Magic circle.prefab
public class PhaseRelayDeployable : MonoBehaviour
{
    public float influenceRadius     = 10f;
    public float singularityBonus    = 2f;   // seconds added to pull phase
    public float lifetime            = 30f;

    [Header("VFX")]
    // Assign: brbmuffins Dark Arts/.../Magic buff.prefab
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

    // Called by SingularityBehaviour at spawn to check for bonus.
    public static float GetBonusNearPoint(Vector3 point, int ownerID)
    {
        PhaseRelayDeployable[] relays = FindObjectsByType<PhaseRelayDeployable>(FindObjectsSortMode.None);
        foreach (var r in relays)
        {
            if (r.ownerID != ownerID) continue;
            if (Vector3.Distance(point, r.transform.position) <= r.influenceRadius)
                return r.singularityBonus;
        }
        return 0f;
    }

    void OnDestroy()
    {
        DeployableManager.Instance?.Unregister(gameObject);
    }
}
