using System.Collections;
using UnityEngine;

public class TurretController : MonoBehaviour
{
    public float range = 8f;
    public float fireRate = 1f;
    public float damage = 10f;
    public string targetTag = "Enemy";

    [Header("Visuals (optional - auto-created if left empty)")]
    public Transform barrel;
    public Transform muzzlePoint;
    public ParticleSystem muzzleFlash;
    public float recoilDistance = 0.15f;
    public float recoilRecoverySpeed = 8f;

    // Runtime state
    private Transform currentTarget;
    private Transform _focusTarget;       // Drone Command override
    private float     _focusExpiry;
    private float     _outputMultiplier = 1f; // Overengineered / System Overload bonus
    private bool      _overloaded;
    private float     _baseFireRate;
    private float fireTimer = 0f;
    private LineRenderer tracer;
    private float tracerTimer = 0f;
    private Vector3 barrelRestLocalPos;

    void Awake()
    {
        _baseFireRate = fireRate;
        if (barrel == null) barrel = transform;
        barrelRestLocalPos = barrel.localPosition;

        if (muzzleFlash == null) muzzleFlash = CreateDefaultMuzzleFlash();

        tracer = gameObject.AddComponent<LineRenderer>();
        tracer.startWidth = 0.04f;
        tracer.endWidth = 0.02f;
        tracer.material = new Material(Shader.Find("Sprites/Default"));
        tracer.startColor = new Color(0.4f, 1f, 1f, 0.9f);
        tracer.endColor = new Color(0.4f, 1f, 1f, 0f);
        tracer.positionCount = 2;
        tracer.enabled = false;
    }

    // ── Public API ────────────────────────────────────────────────

    // Drone Command: pin this turret to a specific enemy for duration seconds.
    public void SetFocusTarget(Transform target, float duration)
    {
        _focusTarget = target;
        _focusExpiry = Time.time + duration;
    }

    // System Overload: triple fire rate and max output multiplier for duration seconds.
    public void SetOverloadMode(float duration)
    {
        if (!_overloaded) StartCoroutine(OverloadRoutine(duration));
    }

    // Called by DeployableManager to sync the Overengineered multiplier.
    public void SetOutputMultiplier(float mult) => _outputMultiplier = mult;

    // ── Internal ──────────────────────────────────────────────────

    private IEnumerator OverloadRoutine(float duration)
    {
        _overloaded   = true;
        fireRate      = _baseFireRate * 3f;
        _outputMultiplier = DeployableManager.Instance != null
            ? DeployableManager.Instance.GetMultiplier(gameObject)
            : 1f;
        yield return new WaitForSeconds(duration);
        fireRate      = _baseFireRate;
        _overloaded   = false;
    }

    void Update()
    {
        // Drone Command focus expires?
        if (_focusTarget != null && Time.time >= _focusExpiry) _focusTarget = null;

        FindTarget();

        barrel.localPosition = Vector3.Lerp(barrel.localPosition, barrelRestLocalPos, recoilRecoverySpeed * Time.deltaTime);

        if (tracer.enabled)
        {
            tracerTimer -= Time.deltaTime;
            if (tracerTimer <= 0f) tracer.enabled = false;
        }

        if (currentTarget == null) return;

        Vector3 lookPoint = currentTarget.position;
        lookPoint.y = transform.position.y;
        transform.LookAt(lookPoint);

        fireTimer += Time.deltaTime;
        if (fireTimer >= 1f / fireRate)
        {
            Fire();
            fireTimer = 0f;
        }
    }

    void FindTarget()
    {
        // Drone Command focus takes priority when valid and in range.
        if (_focusTarget != null)
        {
            float dist = Vector3.Distance(transform.position, _focusTarget.position);
            currentTarget = dist <= range * 1.5f ? _focusTarget : null;
            if (currentTarget != null) return;
        }

        if (currentTarget != null)
        {
            float dist = Vector3.Distance(transform.position, currentTarget.position);
            if (dist <= range) return;
            currentTarget = null;
        }

        GameObject[] candidates = GameObject.FindGameObjectsWithTag(targetTag);
        float closestDist = Mathf.Infinity;

        foreach (GameObject candidate in candidates)
        {
            float dist = Vector3.Distance(transform.position, candidate.transform.position);
            if (dist <= range && dist < closestDist)
            {
                closestDist = dist;
                currentTarget = candidate.transform;
            }
        }
    }

    void Fire()
    {
        Health targetHealth = currentTarget.GetComponent<Health>();
        if (targetHealth != null)
        {
            // Apply Overengineered output multiplier (stacks from DeployableManager)
            float mult = _outputMultiplier;
            if (!_overloaded && DeployableManager.Instance != null)
                mult = DeployableManager.Instance.GetMultiplier(gameObject);
            targetHealth.TakeDamage(damage * mult);
        }

        barrel.localPosition = barrelRestLocalPos - Vector3.forward * recoilDistance;

        if (muzzleFlash != null) muzzleFlash.Play();

        Vector3 origin = muzzlePoint != null ? muzzlePoint.position : barrel.position;
        Vector3 target = currentTarget.position + Vector3.up * 0.5f;
        tracer.SetPosition(0, origin);
        tracer.SetPosition(1, target);
        tracer.enabled = true;
        tracerTimer = 0.06f;
    }

    ParticleSystem CreateDefaultMuzzleFlash()
    {
        GameObject go = new GameObject("MuzzleFlash");
        go.transform.SetParent(barrel, false);
        go.transform.localPosition = Vector3.forward * 0.5f;

        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.loop = false;
        main.duration = 0.1f;
        main.startLifetime = 0.08f;
        main.startSpeed = 0.5f;
        main.startSize = 0.3f;
        main.startColor = new Color(1f, 0.8f, 0.2f);
        main.maxParticles = 10;
        main.playOnAwake = false;

        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = 0f;
        emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 6) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 15f;
        shape.radius = 0.05f;

        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        Shader particleShader = Shader.Find("Sprites/Default");
        if (particleShader != null) renderer.material = new Material(particleShader);

        return ps;
    }
}
