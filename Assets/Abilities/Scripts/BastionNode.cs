using UnityEngine;

// Guardian deployable — Bastion Node
// A dome that pre-shields every ally inside on entry (30 absorb each).
// VFX: brbmuffins Dark Arts/Fantasy Pack/Prefabs/Shield buff.prefab (tint blue)
//      brbmuffins Dark Arts/Fantasy Pack/Prefabs/Effects normal/Magic circle.prefab (base ring)
[RequireComponent(typeof(SphereCollider))]
public class BastionNode : MonoBehaviour
{
    public float shieldPerAlly  = 30f;
    public float radius         = 6f;
    public float lifetime       = 20f;
    public string playerTag     = "Player";

    [Header("VFX")]
    // Assign: brbmuffins Dark Arts/.../Shield buff.prefab
    public GameObject domeVFX;

    [HideInInspector] public int ownerID;

    private float _timer;

    void Start()
    {
        SphereCollider col = GetComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius    = radius;

        if (domeVFX != null)
            Instantiate(domeVFX, transform.position, Quaternion.identity, transform);

        if (DeployableManager.Instance != null)
            DeployableManager.Instance.Register(gameObject, ownerID, 1);
    }

    void Update()
    {
        _timer += Time.deltaTime;
        if (_timer >= lifetime) Destroy(gameObject);
    }

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(playerTag)) return;
        other.GetComponent<Health>()?.ApplyShield(shieldPerAlly);
    }

    void OnDestroy()
    {
        DeployableManager.Instance?.Unregister(gameObject);
    }
}
