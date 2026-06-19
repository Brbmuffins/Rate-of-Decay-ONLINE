using UnityEngine;

// Guardian — Iron Tether
// Locks an enemy within maxDistance of the Guardian for duration seconds.
// VFX: brbmuffins Dark Arts/Fantasy Pack/Prefabs/Leaves shield.prefab
//      (drop it on the enemy's feet for the tether anchor visual)
//      Uses a LineRenderer for the chain line.
[RequireComponent(typeof(LineRenderer))]
public class IronTetherHandler : MonoBehaviour
{
    [Header("Settings")]
    public float maxDistance       = 8f;
    public float duration          = 5f;

    [Header("VFX")]
    // Assign: brbmuffins Dark Arts/.../Leaves shield.prefab (tint blue/grey)
    public GameObject anchorVFX;

    private LineRenderer  _line;
    private Health        _selfHealth;
    private Transform     _target;
    private Health        _targetHealth;
    private GameObject    _anchorInstance;
    private float         _expiry;
    private bool          _active;

    void Awake()
    {
        _line       = GetComponent<LineRenderer>();
        _selfHealth = GetComponent<Health>();

        _line.positionCount = 2;
        _line.startWidth    = 0.06f;
        _line.endWidth      = 0.06f;
        _line.startColor    = new Color(0.3f, 0.6f, 1f, 0.9f);
        _line.endColor      = new Color(0.3f, 0.6f, 1f, 0.3f);
        _line.enabled       = false;
        _line.material      = new Material(Shader.Find("Sprites/Default"));
    }

    public void Activate(GameObject targetGO)
    {
        if (targetGO == null) return;
        Deactivate();

        _target       = targetGO.transform;
        _targetHealth = targetGO.GetComponent<Health>();
        _expiry       = Time.time + duration;
        _active       = true;
        _line.enabled = true;

        // Apply Tethered status effect so EnemyAI knows not to move freely
        targetGO.GetComponent<StatusEffectManager>()
            ?.AddEffect(new StatusEffect(StatusEffectType.Tethered, duration));

        if (anchorVFX != null)
            _anchorInstance = Instantiate(anchorVFX, targetGO.transform.position, Quaternion.identity,
                                          targetGO.transform);
    }

    public void Deactivate()
    {
        _active       = false;
        _line.enabled = false;
        if (_anchorInstance != null) { Destroy(_anchorInstance); _anchorInstance = null; }
        _target       = null;
        _targetHealth = null;
    }

    void Update()
    {
        if (!_active || _target == null) return;

        if (Time.time >= _expiry)
        {
            Deactivate();
            return;
        }

        // Enforce max distance — pull target back if too far
        float dist = Vector3.Distance(transform.position, _target.position);
        if (dist > maxDistance)
        {
            Vector3 clamped = transform.position +
                              (_target.position - transform.position).normalized * maxDistance;
            Rigidbody rb = _target.GetComponent<Rigidbody>();
            if (rb != null) rb.MovePosition(clamped);
            else            _target.position = clamped;
        }

        // Draw chain
        _line.SetPosition(0, transform.position + Vector3.up * 1f);
        _line.SetPosition(1, _target.position    + Vector3.up * 0.5f);
    }
}
