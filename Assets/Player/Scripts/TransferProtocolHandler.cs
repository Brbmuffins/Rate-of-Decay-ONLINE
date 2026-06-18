using UnityEngine;

// Medic — Transfer Protocol
// Tethers to an ally: 100% of their incoming damage routes to the Medic instead.
// VFX: brbmuffins Dark Arts/Fantasy Pack/Prefabs/Glowing orbs.prefab (tethered orb on ally)
//      Uses a LineRenderer to draw the tether.
[RequireComponent(typeof(LineRenderer))]
public class TransferProtocolHandler : MonoBehaviour
{
    [Header("Settings")]
    public float redirectFraction = 1.0f;   // 100% redirect
    public float duration         = 5f;

    [Header("VFX")]
    // Assign: brbmuffins Dark Arts/.../Glowing orbs.prefab (tint green)
    public GameObject tetherOrbVFX;

    private LineRenderer _line;
    private Health       _selfHealth;
    private Health       _targetHealth;
    private GameObject   _orbInstance;
    private float        _expiry;

    void Awake()
    {
        _line        = GetComponent<LineRenderer>();
        _selfHealth  = GetComponent<Health>();

        _line.positionCount = 2;
        _line.startWidth    = 0.05f;
        _line.endWidth      = 0.05f;
        _line.startColor    = new Color(0.2f, 1f, 0.4f, 0.8f);
        _line.endColor      = new Color(0.2f, 1f, 0.4f, 0.2f);
        _line.enabled       = false;

        Material mat = new Material(Shader.Find("Sprites/Default"));
        _line.material = mat;
    }

    // targetGO = the ally GameObject that was aimed at
    public void Activate(GameObject targetGO)
    {
        Health target = targetGO?.GetComponent<Health>();
        if (target == null || target == _selfHealth) return;

        // Clear any existing tether
        Deactivate();

        _targetHealth = target;
        _targetHealth.SetDamageRedirect(_selfHealth, redirectFraction, duration);
        _expiry = Time.time + duration;
        _line.enabled = true;

        if (tetherOrbVFX != null)
        {
            _orbInstance = Instantiate(tetherOrbVFX, targetGO.transform.position + Vector3.up * 1.2f,
                                       Quaternion.identity, targetGO.transform);
        }
    }

    public void Deactivate()
    {
        _targetHealth?.ClearRedirect();
        _targetHealth = null;
        _line.enabled = false;
        if (_orbInstance != null) { Destroy(_orbInstance); _orbInstance = null; }
    }

    void Update()
    {
        if (_targetHealth == null) return;

        if (Time.time >= _expiry)
        {
            Deactivate();
            return;
        }

        // Draw tether line
        _line.SetPosition(0, transform.position + Vector3.up * 1.2f);
        _line.SetPosition(1, _targetHealth.transform.position + Vector3.up * 1.2f);
    }
}
