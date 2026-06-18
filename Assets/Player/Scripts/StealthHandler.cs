using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Handles Phase Cloak for the Wraith.
// Fades all renderers on this character. Disables enemy targeting.
// Breaking stealth with Neural Spike triggers +50% damage (one time).
// VFX: brbmuffins Technologies/Particle Pack/Misc Effects/Prefabs/DissolveSolidHorizontal.prefab
//      — spawn on cloak start and again on break.
public class StealthHandler : MonoBehaviour
{
    [Header("Cloak Settings")]
    public float cloakAlpha       = 0.08f;   // nearly invisible
    public float fadeSpeed        = 4f;
    public string enemyTag        = "Enemy";

    [Header("Damage Bonus")]
    public float backstabMultiplier = 1.5f;   // +50% on Neural Spike from stealth

    [Header("VFX")]
    // Assign: brbmuffins Technologies/.../DissolveSolidHorizontal.prefab or Dissolve.prefab
    public GameObject cloakFX;
    public GameObject breakFX;

    private List<Renderer>  _renderers = new List<Renderer>();
    private float           _targetAlpha = 1f;
    private bool            _cloaked = false;
    private bool            _backstabReady = false;
    private Coroutine       _cloakRoutine;

    public bool IsCloaked      => _cloaked;
    public bool BackstabReady  => _backstabReady;

    void Awake()
    {
        // Collect all renderers on this character (body, hair, equipment, etc.)
        GetComponentsInChildren(true, _renderers);
    }

    public void BeginCloak(float duration)
    {
        if (_cloakRoutine != null) StopCoroutine(_cloakRoutine);
        _cloakRoutine = StartCoroutine(CloakRoutine(duration));
    }

    public void BreakCloak()
    {
        if (!_cloaked) return;
        _cloaked      = false;
        _targetAlpha  = 1f;

        RestoreEnemyTargeting();

        if (breakFX != null)
        {
            GameObject fx = Instantiate(breakFX, transform.position + Vector3.up, Quaternion.identity);
            Destroy(fx, 2f);
        }
    }

    // Called by AbilityCaster before applying Neural Spike damage from stealth.
    public float ConsumeCloakBonus()
    {
        if (!_backstabReady) return 1f;
        _backstabReady = false;
        BreakCloak();
        return backstabMultiplier;
    }

    void Update()
    {
        // Smooth alpha fade
        foreach (var rend in _renderers)
        {
            if (rend == null) continue;
            foreach (var mat in rend.materials)
            {
                Color c = mat.color;
                c.a = Mathf.MoveTowards(c.a, _targetAlpha, fadeSpeed * Time.deltaTime);
                mat.color = c;
            }
        }
    }

    private IEnumerator CloakRoutine(float duration)
    {
        // Check for Shadow Relay bonus
        ShadowRelayDeployable relay = FindNearestRelay();
        float bonus = (relay != null && relay.IsOwnerInRange()) ? relay.cloakExtension : 0f;
        float totalDuration = duration + bonus;

        _cloaked       = true;
        _targetAlpha   = cloakAlpha;
        _backstabReady = true;

        SuppressEnemyTargeting();

        if (cloakFX != null)
        {
            GameObject fx = Instantiate(cloakFX, transform.position + Vector3.up, Quaternion.identity);
            Destroy(fx, 2f);
        }

        yield return new WaitForSeconds(totalDuration);

        if (_cloaked) BreakCloak();
    }

    void SuppressEnemyTargeting()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, 50f);
        foreach (var col in hits)
        {
            if (!col.CompareTag(enemyTag)) continue;
            EnemyAI ai = col.GetComponent<EnemyAI>();
            if (ai != null && ai.aggroTarget == transform)
                ai.SetAggroTarget(null);
        }
    }

    void RestoreEnemyTargeting()
    {
        // Enemies will naturally re-acquire on next FindNearestPlayer() call
    }

    ShadowRelayDeployable FindNearestRelay()
    {
        ShadowRelayDeployable[] relays = FindObjectsByType<ShadowRelayDeployable>(FindObjectsSortMode.None);
        foreach (var r in relays)
            if (r.ownerTransform == transform) return r;
        return null;
    }
}
