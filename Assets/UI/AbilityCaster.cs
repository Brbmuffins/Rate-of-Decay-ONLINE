using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

public enum AbilityShape { Circle, Cone, Rectangle }
public enum AbilityCategory { Damage, Heal, Support }

[System.Serializable]
public class AbilityDef
{
    public string abilityName = "Ability";
    public AbilityShape shape = AbilityShape.Circle;
    public AbilityCategory category = AbilityCategory.Damage;
    public float range = 4f;
    public float coneAngle = 60f;
    public float rectWidth = 1.5f;
    public float indicatorSize = 1.5f;
    public bool spawnTurret = false;
    public GameObject turretPrefab;
    public ItemData turretItem;
    public float cooldown = 3f;
    public Sprite icon;

    [Header("Charge")]
    public bool chargeable = false;
    public float maxChargeTime = 1.5f;
    public float damage = 10f;
    public float maxChargeDamage = 10f;
    public float maxChargeSizeMultiplier = 1.8f;
    public string targetTag = "Enemy";

    public Color chargedTint = new Color(0f, 0f, 0f, 0f);
    public bool fireVisual = false;

    [Header("VFX Prefabs")]
    public GameObject castVFX;
    public GameObject hitVFX;

    [Header("Shield (Magic Shield ability)")]
    public float shieldAbsorb = 0f;
    public float shieldDuration = 0f;
}

public class AbilityCaster : MonoBehaviour
{
    public Camera cam;
    public Inventory inventory;
    public float castDelay = 0.3f;

    [Header("Mouse Aim")]
    public float minimumAimDistance = 1f;

    [Header("Spellbook — all available spells")]
    public AbilityDef[] spellbook = new AbilityDef[]
    {
        new AbilityDef { abilityName = "Deploy Turret",   shape = AbilityShape.Circle,    category = AbilityCategory.Support, range = 10f, indicatorSize = 1.5f, spawnTurret = true, cooldown = 6f },
        new AbilityDef { abilityName = "Dark Blast",      shape = AbilityShape.Cone,      category = AbilityCategory.Damage,  range = 8f, coneAngle = 60f, cooldown = 3f, chargeable = true, maxChargeTime = 1.5f, damage = 10f, maxChargeDamage = 30f, maxChargeSizeMultiplier = 1.6f, targetTag = "Enemy", chargedTint = new Color(0.4f, 0.1f, 0.8f, 0.9f) },
        new AbilityDef { abilityName = "Healing Pulse",   shape = AbilityShape.Circle,    category = AbilityCategory.Heal,    range = 6f, indicatorSize = 3f, cooldown = 5f },
        new AbilityDef { abilityName = "Holy Beam",       shape = AbilityShape.Rectangle, category = AbilityCategory.Damage,  range = 10f, rectWidth = 1.5f, cooldown = 4f, chargeable = true, maxChargeTime = 1.5f, damage = 15f, maxChargeDamage = 50f, maxChargeSizeMultiplier = 1.8f, targetTag = "Enemy" },
        new AbilityDef { abilityName = "Fireball",        shape = AbilityShape.Circle,    category = AbilityCategory.Damage,  range = 12f, indicatorSize = 2f, cooldown = 4f, chargeable = true, maxChargeTime = 1.5f, damage = 20f, maxChargeDamage = 45f, maxChargeSizeMultiplier = 2f, targetTag = "Enemy", chargedTint = new Color(1f, 0.4f, 0.05f, 0.9f) },
        new AbilityDef { abilityName = "Lightning Strike", shape = AbilityShape.Circle,   category = AbilityCategory.Damage,  range = 10f, indicatorSize = 2.5f, cooldown = 5f, damage = 35f, targetTag = "Enemy" },
        new AbilityDef { abilityName = "Ice Nova",        shape = AbilityShape.Circle,    category = AbilityCategory.Damage,  range = 5f, indicatorSize = 5f, cooldown = 6f, damage = 15f, targetTag = "Enemy" },
        new AbilityDef { abilityName = "Magic Shield",    shape = AbilityShape.Circle,    category = AbilityCategory.Support, range = 0f, indicatorSize = 1f, cooldown = 8f, shieldAbsorb = 50f, shieldDuration = 5f },
    };

    [Header("Equipped slots (indices into spellbook)")]
    public int[] equippedIndices = new int[] { 0, 1, 2, 3 };

    // The 4 active ability slots — derived from spellbook via equippedIndices at runtime.
    // Shown read-only in Inspector for debugging; do not edit here, edit spellbook above.
    [SerializeField, HideInInspector] private AbilityDef[] _equippedAbilities = new AbilityDef[4];
    public AbilityDef[] abilities => _equippedAbilities;

    private int heldAbilityIndex = -1;
    private GameObject activeIndicator;
    private float aimTimer = 0f;
    private float[] cooldownTimers = new float[4];

    private GameObject activeShieldVFX;
    private float shieldVFXTimer = 0f;

    public int HeldAbilityIndex => heldAbilityIndex;

    void Awake()
    {
        SyncEquippedFromSpellbook();
    }

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public void SyncEquippedFromSpellbook()
    {
        _equippedAbilities = new AbilityDef[4];
        for (int i = 0; i < 4; i++)
        {
            int idx = (i < equippedIndices.Length) ? equippedIndices[i] : -1;
            _equippedAbilities[i] = (idx >= 0 && idx < spellbook.Length) ? spellbook[idx] : null;
        }
    }

    public void EquipSpell(int spellbookIndex, int slot)
    {
        if (slot < 0 || slot >= 4) return;
        if (spellbookIndex < 0 || spellbookIndex >= spellbook.Length) return;

        if (heldAbilityIndex == slot)
            CancelAim();

        equippedIndices[slot] = spellbookIndex;
        _equippedAbilities[slot] = spellbook[spellbookIndex];
        cooldownTimers[slot] = 0f;
    }

    public bool IsEquipped(int spellbookIndex, out int slot)
    {
        for (int i = 0; i < equippedIndices.Length; i++)
        {
            if (equippedIndices[i] == spellbookIndex)
            {
                slot = i;
                return true;
            }
        }
        slot = -1;
        return false;
    }

    void Update()
    {
        if (cam == null) cam = Camera.main;

        for (int i = 0; i < cooldownTimers.Length; i++)
        {
            if (cooldownTimers[i] > 0f)
                cooldownTimers[i] -= Time.deltaTime;
        }

        if (activeShieldVFX != null)
        {
            shieldVFXTimer -= Time.deltaTime;
            if (shieldVFXTimer <= 0f)
            {
                Destroy(activeShieldVFX);
                activeShieldVFX = null;
            }
        }

        for (int i = 0; i < 4; i++)
        {
            if (abilities[i] == null) continue;

            KeyControl key = GetDigitKey(i);
            if (key == null) continue;

            bool hasTurretAvailable =
                !abilities[i].spawnTurret ||
                abilities[i].turretItem == null ||
                inventory == null ||
                inventory.HasItem(abilities[i].turretItem);

            if (key.wasPressedThisFrame && cooldownTimers[i] <= 0f && hasTurretAvailable)
            {
                // Instant-cast abilities (shield absorb, range 0) fire on keypress with no aiming
                if (abilities[i].shieldAbsorb > 0f && abilities[i].range <= 0f)
                {
                    if (heldAbilityIndex != -1) CancelAim();
                    FinalizeCast(abilities[i], null, 0f);
                    cooldownTimers[i] = abilities[i].cooldown;
                }
                else if (heldAbilityIndex == i)
                {
                    CancelAim();
                }
                else
                {
                    if (heldAbilityIndex != -1)
                        CancelAim();

                    heldAbilityIndex = i;
                    aimTimer = 0f;
                    activeIndicator = CreateIndicator(abilities[i]);

                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
                }
            }
        }

        if (heldAbilityIndex != -1)
        {
            aimTimer += Time.deltaTime;

            if (activeIndicator != null)
                UpdateIndicatorTransform(abilities[heldAbilityIndex], activeIndicator, aimTimer);

            if (Keyboard.current.escapeKey.wasPressedThisFrame || Mouse.current.rightButton.wasPressedThisFrame)
            {
                CancelAim();
            }
            else if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                FinalizeCast(abilities[heldAbilityIndex], activeIndicator, aimTimer);

                cooldownTimers[heldAbilityIndex] = abilities[heldAbilityIndex].cooldown;

                heldAbilityIndex = -1;
                activeIndicator = null;

                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }
    }

    void CancelAim()
    {
        if (activeIndicator != null)
            Destroy(activeIndicator);

        activeIndicator = null;
        heldAbilityIndex = -1;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public float GetCooldownFraction(int slot)
    {
        if (slot < 0 || slot >= cooldownTimers.Length) return 0f;
        if (abilities[slot] == null || abilities[slot].cooldown <= 0f) return 0f;

        return Mathf.Clamp01(cooldownTimers[slot] / abilities[slot].cooldown);
    }

    KeyControl GetDigitKey(int index)
    {
        switch (index)
        {
            case 0: return Keyboard.current.digit1Key;
            case 1: return Keyboard.current.digit2Key;
            case 2: return Keyboard.current.digit3Key;
            case 3: return Keyboard.current.digit4Key;
            default: return null;
        }
    }

    Vector3 GetCameraAimPoint()
    {
        Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

        RaycastHit[] hits = Physics.RaycastAll(ray, 100f);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (RaycastHit hit in hits)
        {
            if (hit.transform == transform || hit.transform.IsChildOf(transform))
                continue;
            return hit.point;
        }

        Plane groundPlane = new Plane(Vector3.up, transform.position);
        if (groundPlane.Raycast(ray, out float distance))
            return ray.GetPoint(distance);

        return transform.position + transform.forward * minimumAimDistance;
    }

    void GetAimData(AbilityDef ability, out Vector3 aimDir, out float aimDistance)
    {
        Vector3 targetPoint = GetCameraAimPoint();
        Vector3 toTarget = targetPoint - transform.position;
        toTarget.y = 0f;

        if (toTarget.sqrMagnitude < 0.0001f)
        {
            aimDir = transform.forward;
            aimDir.y = 0f;
            aimDir.Normalize();
            aimDistance = minimumAimDistance;
            return;
        }

        aimDistance = Mathf.Clamp(toTarget.magnitude, minimumAimDistance, ability.range);
        aimDir = toTarget.normalized;
    }

    GameObject CreateIndicator(AbilityDef ability)
    {
        GameObject indicator;

        if (ability.shape == AbilityShape.Circle)
        {
            indicator = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            indicator.transform.localScale = new Vector3(ability.indicatorSize, 0.02f, ability.indicatorSize);
        }
        else if (ability.shape == AbilityShape.Rectangle)
        {
            indicator = GameObject.CreatePrimitive(PrimitiveType.Cube);
            indicator.transform.localScale = new Vector3(ability.rectWidth, 0.02f, ability.range);
        }
        else
        {
            indicator = CreateConeIndicator(ability.range, ability.coneAngle);
        }

        Collider col = indicator.GetComponent<Collider>();
        if (col != null) Destroy(col);

        Renderer rend = indicator.GetComponent<Renderer>();
        Material mat = new Material(Shader.Find("Sprites/Default"));
        mat.color = GetCategoryColor(ability.category);
        rend.material = mat;

        return indicator;
    }

    Color GetCategoryColor(AbilityCategory category)
    {
        switch (category)
        {
            case AbilityCategory.Heal:    return new Color(0.2f, 1f, 0.3f, 0.45f);
            case AbilityCategory.Support: return new Color(0.2f, 0.6f, 1f, 0.45f);
            default:                      return new Color(1f, 0.5f, 0.1f, 0.45f);
        }
    }

    float GetChargeFraction(AbilityDef ability, float timer)
    {
        if (!ability.chargeable || ability.maxChargeTime <= 0f) return 0f;
        float t = timer % ability.maxChargeTime;
        return t / ability.maxChargeTime;
    }

    void UpdateIndicatorTransform(AbilityDef ability, GameObject indicator, float aimTime)
    {
        GetAimData(ability, out Vector3 aimDir, out float aimDistance);
        float chargeFraction = GetChargeFraction(ability, aimTime);

        if (ability.shape == AbilityShape.Circle)
        {
            indicator.transform.position = transform.position + aimDir * aimDistance + Vector3.up * 0.05f;
        }
        else if (ability.shape == AbilityShape.Rectangle)
        {
            float widthMul = Mathf.Lerp(1f, ability.maxChargeSizeMultiplier, chargeFraction);
            indicator.transform.position = transform.position + aimDir * (aimDistance / 2f) + Vector3.up * 0.05f;
            indicator.transform.rotation = Quaternion.LookRotation(aimDir);
            indicator.transform.localScale = new Vector3(ability.rectWidth * widthMul, 0.02f, aimDistance);
        }
        else if (ability.shape == AbilityShape.Cone)
        {
            float chargeMul = Mathf.Lerp(1f, ability.maxChargeSizeMultiplier, chargeFraction);
            float distanceMul = aimDistance / ability.range;
            indicator.transform.position = transform.position + Vector3.up * 0.05f;
            indicator.transform.rotation = Quaternion.LookRotation(aimDir);
            indicator.transform.localScale = Vector3.one * distanceMul * chargeMul;
        }

        if (ability.chargeable)
        {
            Renderer rend = indicator.GetComponent<Renderer>();
            Color baseColor = GetCategoryColor(ability.category);
            Color c;

            if (ability.chargedTint.a > 0f)
                c = Color.Lerp(baseColor, ability.chargedTint, chargeFraction);
            else
            {
                c = baseColor;
                c.a = Mathf.Lerp(c.a, 0.85f, chargeFraction);
            }

            rend.material.color = c;
        }
    }

    void SpawnVFX(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        if (prefab == null) return;
        GameObject fx = Instantiate(prefab, position, rotation);
        Destroy(fx, 4f);
    }

    void FinalizeCast(AbilityDef ability, GameObject indicator, float aimTime)
    {
        Debug.Log("Cast ability: " + ability.abilityName);

        if (ability.castVFX != null)
            SpawnVFX(ability.castVFX, transform.position + Vector3.up * 1f, transform.rotation);

        if (ability.shape == AbilityShape.Rectangle && ability.damage > 0f && indicator != null)
        {
            float chargeFraction = GetChargeFraction(ability, aimTime);
            float damage = Mathf.Lerp(ability.damage, ability.maxChargeDamage, chargeFraction);
            ApplyRectangleDamage(ability, indicator, damage);
        }

        if (ability.shape == AbilityShape.Cone && ability.damage > 0f && indicator != null)
        {
            float chargeFraction = GetChargeFraction(ability, aimTime);
            float damage = Mathf.Lerp(ability.damage, ability.maxChargeDamage, chargeFraction);
            float coneRange = ability.range * indicator.transform.localScale.x;
            ApplyConeDamage(ability, indicator, damage, coneRange);

            if (ability.fireVisual)
                SpawnFireBurst(transform.position + Vector3.up * 0.5f, indicator.transform.rotation, coneRange, ability.coneAngle);
        }

        if (ability.shape == AbilityShape.Circle && ability.damage > 0f)
        {
            ApplyCircleDamage(ability, indicator);
        }

        if (ability.shieldAbsorb > 0f)
            CastMagicShield(ability);

        if (ability.spawnTurret && indicator != null)
            SpawnTurret(ability, indicator.transform.position);

        if (indicator != null)
            Destroy(indicator, castDelay);
    }

    void ApplyCircleDamage(AbilityDef ability, GameObject indicator)
    {
        Vector3 center = indicator != null ? indicator.transform.position : transform.position;
        Collider[] hits = Physics.OverlapSphere(center, ability.indicatorSize / 2f);

        foreach (Collider hit in hits)
        {
            if (!hit.CompareTag(ability.targetTag)) continue;

            Health health = hit.GetComponent<Health>();
            if (health != null)
            {
                health.TakeDamage(ability.damage);
                SpawnVFX(ability.hitVFX, hit.transform.position + Vector3.up * 0.5f, Quaternion.identity);
            }
        }
    }

    void ApplyRectangleDamage(AbilityDef ability, GameObject indicator, float damage)
    {
        float rectangleLength = indicator.transform.localScale.z;
        Vector3 halfExtents = new Vector3(
            indicator.transform.localScale.x / 2f,
            1f,
            rectangleLength / 2f
        );

        Collider[] hits = Physics.OverlapBox(
            indicator.transform.position,
            halfExtents,
            indicator.transform.rotation
        );

        foreach (Collider hit in hits)
        {
            if (!hit.CompareTag(ability.targetTag)) continue;

            Health health = hit.GetComponent<Health>();
            if (health != null)
            {
                health.TakeDamage(damage);
                SpawnVFX(ability.hitVFX, hit.transform.position + Vector3.up * 0.5f, Quaternion.identity);
            }
        }
    }

    void ApplyConeDamage(AbilityDef ability, GameObject indicator, float damage, float coneRange)
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, coneRange);

        foreach (Collider hit in hits)
        {
            if (!hit.CompareTag(ability.targetTag)) continue;

            Vector3 toHit = hit.transform.position - transform.position;
            toHit.y = 0;

            if (toHit.sqrMagnitude < 0.0001f) continue;

            float angle = Vector3.Angle(indicator.transform.forward, toHit);
            if (angle > ability.coneAngle / 2f) continue;

            Health health = hit.GetComponent<Health>();
            if (health != null)
            {
                health.TakeDamage(damage);
                SpawnVFX(ability.hitVFX, hit.transform.position + Vector3.up * 0.5f, Quaternion.identity);
            }
        }
    }

    void CastMagicShield(AbilityDef ability)
    {
        Health health = GetComponent<Health>();
        if (health != null)
            health.ApplyShield(ability.shieldAbsorb);

        // Remove any existing shield VFX before spawning new one
        if (activeShieldVFX != null)
            Destroy(activeShieldVFX);

        if (ability.castVFX != null)
        {
            activeShieldVFX = Instantiate(ability.castVFX, transform.position, Quaternion.identity, transform);
            activeShieldVFX.transform.localPosition = Vector3.zero;
            shieldVFXTimer = ability.shieldDuration > 0f ? ability.shieldDuration : 5f;
        }
    }

    void SpawnTurret(AbilityDef ability, Vector3 position)
    {
        if (ability.turretPrefab != null)
        {
            GameObject turret = Instantiate(ability.turretPrefab, position, Quaternion.identity);
            turret.name = "Turret";

            if (turret.GetComponent<TurretController>() == null)
                turret.AddComponent<TurretController>();

            if (ability.turretItem != null && inventory != null)
            {
                inventory.RemoveItem(ability.turretItem);

                TurretPickup pickup = turret.GetComponent<TurretPickup>();
                if (pickup != null)
                {
                    pickup.item = ability.turretItem;
                    pickup.inventory = inventory;
                }
            }
        }
        else
        {
            GameObject turret = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            turret.name = "Turret (Placeholder)";
            turret.transform.position = position;
            turret.transform.localScale = new Vector3(0.6f, 1f, 0.6f);
            turret.AddComponent<TurretController>();
        }
    }

    void SpawnFireBurst(Vector3 position, Quaternion rotation, float coneRange, float coneAngle)
    {
        GameObject go = new GameObject("FireBurst");
        go.transform.position = position;
        go.transform.rotation = rotation;

        ParticleSystem ps = go.AddComponent<ParticleSystem>();

        var main = ps.main;
        main.duration = 0.3f;
        main.loop = false;
        main.playOnAwake = false;
        main.startLifetime = 0.4f;
        main.startSpeed = new ParticleSystem.MinMaxCurve(coneRange * 0.6f, coneRange * 1.2f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.4f, 1f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(1f, 0.55f, 0.05f),
            new Color(1f, 0.9f, 0.3f)
        );
        main.maxParticles = 60;

        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = 0f;
        emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 40) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = Mathf.Clamp(coneAngle / 2f, 1f, 89f);
        shape.radius = 0.15f;

        ParticleSystemRenderer psr = go.GetComponent<ParticleSystemRenderer>();
        Shader particleShader = Shader.Find("Sprites/Default");
        if (particleShader != null)
            psr.material = new Material(particleShader);

        ps.Play();
        Destroy(go, main.duration + main.startLifetime.constantMax + 0.5f);
    }

    GameObject CreateConeIndicator(float range, float angle)
    {
        GameObject go = new GameObject("ConeIndicator");
        MeshFilter mf = go.AddComponent<MeshFilter>();
        go.AddComponent<MeshRenderer>();

        int segments = 20;
        Vector3[] vertices = new Vector3[segments + 2];
        int[] triangles = new int[segments * 3];

        vertices[0] = Vector3.zero;
        float startAngle = -angle / 2f;
        float step = angle / segments;

        for (int i = 0; i <= segments; i++)
        {
            float a = (startAngle + step * i) * Mathf.Deg2Rad;
            vertices[i + 1] = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * range;
        }

        for (int i = 0; i < segments; i++)
        {
            triangles[i * 3] = 0;
            triangles[i * 3 + 1] = i + 1;
            triangles[i * 3 + 2] = i + 2;
        }

        Mesh mesh = new Mesh();
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mf.mesh = mesh;

        return go;
    }
}
