using UnityEngine;

// ─────────────────────────────────────────────────────────────────────────────
//  EnemyDeathVFX
//  Placed on enemy GameObjects by RodCombatWorldBuilder.
//  Stores the AssetDatabase path of a death VFX prefab; loads and spawns it
//  when Health fires onDeath.
//
//  Uses Resources.Load as a runtime fallback when AssetDatabase is unavailable
//  (i.e. in a build). For builds, copy the VFX prefabs into a Resources/ folder.
// ─────────────────────────────────────────────────────────────────────────────

[DisallowMultipleComponent]
public class EnemyDeathVFX : MonoBehaviour
{
    // Set by RodCombatWorldBuilder at edit time (full asset path).
    [HideInInspector] public string vfxPath;

    // Optional direct reference for builds (drag in Inspector).
    public GameObject vfxPrefabOverride;

    void Start()
    {
        var health = GetComponent<Health>();
        if (health != null)
            health.onDeath.AddListener(OnDied);
    }

    void OnDied()
    {
        SpawnFX();
        Destroy(gameObject, 0.1f);
    }

    void SpawnFX()
    {
        GameObject prefab = vfxPrefabOverride;

#if UNITY_EDITOR
        if (prefab == null && !string.IsNullOrEmpty(vfxPath))
            prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(vfxPath);
#endif

        if (prefab == null) return;

        var fx = Instantiate(prefab, transform.position + Vector3.up, Quaternion.identity);
        Destroy(fx, 3f);
    }
}
