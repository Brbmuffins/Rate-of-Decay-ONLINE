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

    [Header("Shield")]
    public float shieldAbsorb   = 0f;
    public float shieldDuration = 0f;

    [Header("Heal")]
    public float healAmount = 0f;          // Field Repair single-target heal

    [Header("Timed Effects")]
    public float activeDuration = 0f;      // Phase Cloak, Siege Mode, Iron Tether, Transfer Protocol

    [Header("Chain Lightning")]
    public int   chainTargets       = 0;   // Arc Lance: 4
    public float chainDamageFalloff = 5f;  // damage lost per jump

    [Header("Pull / Zone")]
    public float pullRadius   = 0f;        // Magnetize, Singularity, Event Horizon
    public float pullDuration = 0f;        // Singularity pull phase

    [Header("Deployable Scene Prefab")]
    // The runtime object spawned in the world by this ability (mine, wall, zone, etc.)
    public GameObject deployablePrefab;
}

public class AbilityCaster : MonoBehaviour
{
    public Camera cam;
    public Inventory inventory;
    public CastAnimator castAnimator;
    public float castDelay = 0.3f;

    [Header("Class")]
    [Tooltip("Assign the chosen class's ClassAbilityPool asset before play starts.")]
    public ClassAbilityPool classPool;

    [Header("Ability Handlers — assign if your class uses these abilities")]
    public KineticReversalHandler kineticReversalHandler;
    public SiegeModeHandler       siegeModeHandler;
    public DashHandler            dashHandler;
    public StealthHandler         stealthHandler;
    public TransferProtocolHandler transferProtocolHandler;
    public IronTetherHandler      ironTetherHandler;

    [Header("Deployable Prefabs — assign the matching runtime prefabs")]
    [Tooltip("ShockMineBehaviour prefab (Runic Snare) — rune burst trap")]
    public GameObject shockMinePrefab;
    [Tooltip("NaniteSwarmBehaviour prefab (Spirit Wisps) — healing orb cloud")]
    public GameObject naniteSwarmPrefab;
    [Tooltip("SingularityBehaviour prefab (Void Maw ability)")]
    public GameObject singularityPrefab;
    [Tooltip("SingularityBehaviour prefab with applyExposed=true (Collapsing Void)")]
    public GameObject eventHorizonPrefab;
    [Tooltip("LastBastionWall prefab (Iron Rampart) — stone rune wall")]
    public GameObject lastBastionPrefab;
    [Tooltip("NullFieldZone prefab (Silence Ward) — curse fog")]
    public GameObject nullFieldPrefab;

    [Header("Class Deployables")]
    [Tooltip("RestorationBeacon (Cleric) or BastionNode (Ironclad)")]
    public GameObject beaconPrefab;
    [Tooltip("PhaseRelayDeployable (Arcanist)")]
    public GameObject phaseRelayPrefab;
    [Tooltip("ShadowRelayDeployable (Shadowblade)")]
    public GameObject shadowRelayPrefab;

    [Header("Mouse Aim")]
    public float minimumAimDistance = 1f;

    [Header("Spellbook — all available spells")]
    public AbilityDef[] spellbook = new AbilityDef[]
    {
        // ── SHARED / CROSS-CLASS (indices 0–7) ─────────────────────────────────────────
        new AbilityDef { abilityName = "Runic Sentinel",   shape = AbilityShape.Circle,    category = AbilityCategory.Support, range = 10f, indicatorSize = 1.5f, spawnTurret = true, cooldown = 6f },
        new AbilityDef { abilityName = "Void Bolt",        shape = AbilityShape.Cone,      category = AbilityCategory.Damage,  range = 8f, coneAngle = 60f, cooldown = 3f, chargeable = true, maxChargeTime = 1.5f, damage = 10f, maxChargeDamage = 30f, maxChargeSizeMultiplier = 1.6f, targetTag = "Enemy", chargedTint = new Color(0.4f, 0.1f, 0.8f, 0.9f) },
        new AbilityDef { abilityName = "Mending Circle",   shape = AbilityShape.Circle,    category = AbilityCategory.Heal,    range = 6f, indicatorSize = 3f, cooldown = 5f },
        new AbilityDef { abilityName = "Storm Lash",       shape = AbilityShape.Rectangle, category = AbilityCategory.Damage,  range = 10f, rectWidth = 1.5f, cooldown = 4f, chargeable = true, maxChargeTime = 1.5f, damage = 15f, maxChargeDamage = 50f, maxChargeSizeMultiplier = 1.8f, targetTag = "Enemy" },
        new AbilityDef { abilityName = "Ember Surge",      shape = AbilityShape.Circle,    category = AbilityCategory.Damage,  range = 12f, indicatorSize = 2f, cooldown = 4f, chargeable = true, maxChargeTime = 1.5f, damage = 20f, maxChargeDamage = 45f, maxChargeSizeMultiplier = 2f, targetTag = "Enemy", chargedTint = new Color(1f, 0.4f, 0.05f, 0.9f) },
        new AbilityDef { abilityName = "Mind Spike",       shape = AbilityShape.Circle,    category = AbilityCategory.Damage,  range = 10f, indicatorSize = 2.5f, cooldown = 5f, damage = 35f, targetTag = "Enemy" },
        new AbilityDef { abilityName = "Binding Wave",     shape = AbilityShape.Circle,    category = AbilityCategory.Damage,  range = 5f, indicatorSize = 5f, cooldown = 6f, damage = 15f, targetTag = "Enemy" },
        new AbilityDef { abilityName = "Arcane Ward",      shape = AbilityShape.Circle,    category = AbilityCategory.Support, range = 0f, indicatorSize = 1f, cooldown = 8f, shieldAbsorb = 50f, shieldDuration = 5f },

        // ── WARDEN (indices 8–12) ───────────────────────────────────────────────────────
        // [8]  Runic Snare — proximity burst rune trap; Warden and Shadowblade
        new AbilityDef { abilityName = "Runic Snare",      shape = AbilityShape.Circle,    category = AbilityCategory.Damage,  range = 8f, indicatorSize = 1f, cooldown = 5f, damage = 40f, targetTag = "Enemy" },
        // [9]  Battle Hymn — team CDR aura; instant self-cast
        new AbilityDef { abilityName = "Battle Hymn",      shape = AbilityShape.Circle,    category = AbilityCategory.Support, range = 0f, indicatorSize = 6f, cooldown = 12f },
        // [10] Spirit Redirect — redirect active Runic Sentinel onto focus target
        new AbilityDef { abilityName = "Spirit Redirect",  shape = AbilityShape.Circle,    category = AbilityCategory.Support, range = 12f, indicatorSize = 1f, cooldown = 8f },
        // [11] Mend — single-target direct heal + debuff cleanse
        new AbilityDef { abilityName = "Mend",             shape = AbilityShape.Circle,    category = AbilityCategory.Heal,    range = 6f, indicatorSize = 1f, cooldown = 6f },
        // [12] Conjurer's Surge (Warden Ultimate) — all constructs activate at full power simultaneously
        new AbilityDef { abilityName = "Conjurer's Surge", shape = AbilityShape.Circle,    category = AbilityCategory.Support, range = 0f, indicatorSize = 1f, cooldown = 45f },

        // ── IRONCLAD (indices 13–18) ───────────────────────────────────────────────────
        // [13] Counter Blow — absorb damage for 3s, release as cone burst up to 60 dmg
        new AbilityDef { abilityName = "Counter Blow",     shape = AbilityShape.Cone,      category = AbilityCategory.Support, range = 8f, coneAngle = 70f, cooldown = 10f, damage = 60f, targetTag = "Enemy" },
        // [14] Gravity Slam — pull all enemies in radius to anchor point, no damage
        new AbilityDef { abilityName = "Gravity Slam",     shape = AbilityShape.Circle,    category = AbilityCategory.Support, range = 10f, indicatorSize = 4f, cooldown = 7f },
        // [15] Shieldwall Charge — charge forward 6 units, 25 dmg, stagger + 3 Threat stacks
        new AbilityDef { abilityName = "Shieldwall Charge",shape = AbilityShape.Rectangle, category = AbilityCategory.Damage,  range = 6f, rectWidth = 2f, cooldown = 6f, damage = 25f, targetTag = "Enemy" },
        // [16] Stalwart Stance — stationary stance: 40% DR + 3x Threat generation for 6s
        new AbilityDef { abilityName = "Stalwart Stance",  shape = AbilityShape.Circle,    category = AbilityCategory.Support, range = 0f, indicatorSize = 1f, cooldown = 14f },
        // [17] Rune Chain — leash one enemy within 8 units for 5s; absorb 15% of their attacks on allies
        new AbilityDef { abilityName = "Rune Chain",       shape = AbilityShape.Circle,    category = AbilityCategory.Support, range = 8f, indicatorSize = 1f, cooldown = 9f },
        // [18] Iron Rampart (Ironclad Ultimate) — full-width stone rune wall, blocks projectiles 10s
        new AbilityDef { abilityName = "Iron Rampart",     shape = AbilityShape.Rectangle, category = AbilityCategory.Support, range = 8f, rectWidth = 8f, cooldown = 50f },

        // ── ARCANIST (indices 19–22) ───────────────────────────────────────────────────
        // [19] Arcane Step — teleport up to 10 units in aimed direction
        new AbilityDef { abilityName = "Arcane Step",      shape = AbilityShape.Circle,    category = AbilityCategory.Support, range = 10f, indicatorSize = 0.5f, cooldown = 4f },
        // [20] Void Maw — pull enemies to center for 3s then 20 AoE burst
        new AbilityDef { abilityName = "Void Maw",         shape = AbilityShape.Circle,    category = AbilityCategory.Damage,  range = 10f, indicatorSize = 8f, cooldown = 9f, damage = 20f, targetTag = "Enemy" },
        // [21] Forked Lightning — chain lightning, jumps up to 4 enemies (30/25/20/15 dmg)
        new AbilityDef { abilityName = "Forked Lightning",  shape = AbilityShape.Circle,    category = AbilityCategory.Damage,  range = 10f, indicatorSize = 1.5f, cooldown = 7f, damage = 30f, targetTag = "Enemy" },
        // [22] Collapsing Void (Arcanist Ultimate) — 12-unit pull, 3s collapse, 60 AoE + Weakened window
        new AbilityDef { abilityName = "Collapsing Void",  shape = AbilityShape.Circle,    category = AbilityCategory.Damage,  range = 14f, indicatorSize = 12f, cooldown = 50f, damage = 60f, targetTag = "Enemy" },

        // ── CLERIC (indices 23–28) ─────────────────────────────────────────────────────
        // [23] Soul Bond — tether ally: their incoming damage reroutes to you for 5s
        new AbilityDef { abilityName = "Soul Bond",        shape = AbilityShape.Circle,    category = AbilityCategory.Support, range = 8f, indicatorSize = 1f, cooldown = 9f },
        // [24] Spirit Wisps — mobile healing orbs, drift toward ally, chip enemies they pass through
        new AbilityDef { abilityName = "Spirit Wisps",     shape = AbilityShape.Circle,    category = AbilityCategory.Heal,    range = 10f, indicatorSize = 2f, cooldown = 7f },
        // [25] Divine Spark — revive downed ally at 30% HP OR 60 burst dmg to undead enemies
        new AbilityDef { abilityName = "Divine Spark",     shape = AbilityShape.Circle,    category = AbilityCategory.Heal,    range = 6f, indicatorSize = 1.5f, cooldown = 14f, damage = 60f, targetTag = "Enemy" },
        // [26] Sacred Aegis — shield on ally that scales 20→80 absorb as they take hits over 8s
        new AbilityDef { abilityName = "Sacred Aegis",     shape = AbilityShape.Circle,    category = AbilityCategory.Support, range = 8f, indicatorSize = 1f, cooldown = 10f, shieldAbsorb = 20f, shieldDuration = 8f },
        // [27] Dispel — instant cleanse all debuffs from target ally
        new AbilityDef { abilityName = "Dispel",           shape = AbilityShape.Circle,    category = AbilityCategory.Support, range = 8f, indicatorSize = 1f, cooldown = 7f },
        // [28] Temporal Grace (Cleric Ultimate) — rewind entire team 5 seconds: HP, position, debuffs
        new AbilityDef { abilityName = "Temporal Grace",   shape = AbilityShape.Circle,    category = AbilityCategory.Heal,    range = 0f, indicatorSize = 1f, cooldown = 60f },

        // ── SHADOWBLADE (indices 29–31) ────────────────────────────────────────────────
        // [29] Shadow Veil — full invisibility for 4s; breaking with Mind Spike = +50% damage
        new AbilityDef { abilityName = "Shadow Veil",      shape = AbilityShape.Circle,    category = AbilityCategory.Support, range = 0f, indicatorSize = 1f, cooldown = 10f },
        // [30] Silence Ward — silence all enemy abilities in radius for 5s
        new AbilityDef { abilityName = "Silence Ward",     shape = AbilityShape.Circle,    category = AbilityCategory.Support, range = 10f, indicatorSize = 5f, cooldown = 12f },
        // [31] Dark Harvest (Shadowblade Ultimate) — consume all active debuffs on enemies in range: 20 dmg per stack
        new AbilityDef { abilityName = "Dark Harvest",     shape = AbilityShape.Circle,    category = AbilityCategory.Damage,  range = 8f, indicatorSize = 8f, cooldown = 40f, damage = 20f, targetTag = "Enemy" },
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

    // ── Cached component refs ──────────────────────────────────────
    private ClassPassive         _passive;
    private PassivePhaseCharge   _phaseCharge;
    private PassiveBountySystem  _bounty;
    private Health               _health;
    private CharacterStats       _characterStats;  // gear/attunement bonuses

    public int HeldAbilityIndex => heldAbilityIndex;

    void Awake()
    {
        SyncEquippedFromSpellbook();

        _passive        = GetComponent<ClassPassive>();
        _phaseCharge    = GetComponent<PassivePhaseCharge>();
        _bounty         = GetComponent<PassiveBountySystem>();
        _health         = GetComponent<Health>();
        _characterStats = GetComponent<CharacterStats>();

        // Register this player with SnapshotSystem
        SnapshotSystem.Instance?.Track(gameObject);
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
        if (!IsAllowedByClass(spellbookIndex)) return;

        if (heldAbilityIndex == slot)
            CancelAim();

        equippedIndices[slot] = spellbookIndex;
        _equippedAbilities[slot] = spellbook[spellbookIndex];
        cooldownTimers[slot] = 0f;
    }

    // Returns true if this spellbook index is permitted for the current class.
    // Always returns true when no classPool is assigned (editor / testing).
    public bool IsAllowedByClass(int spellbookIndex)
    {
        if (classPool == null) return true;
        foreach (int idx in classPool.availableIndices)
            if (idx == spellbookIndex) return true;
        return false;
    }

    // Apply a class pool and reset to its default loadout.
    public void ApplyClass(ClassAbilityPool pool)
    {
        classPool = pool;
        if (pool == null) return;

        for (int i = 0; i < 4; i++)
            equippedIndices[i] = (i < pool.defaultEquipped.Length) ? pool.defaultEquipped[i] : -1;

        SyncEquippedFromSpellbook();
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
                    cooldownTimers[i] = CooldownFor(abilities[i]);
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

                cooldownTimers[heldAbilityIndex] = CooldownFor(abilities[heldAbilityIndex]);

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

    // Called by BountySystem passive when a kill is registered.
    public void ReduceAllCooldowns(float seconds)
    {
        for (int i = 0; i < cooldownTimers.Length; i++)
            cooldownTimers[i] = Mathf.Max(0f, cooldownTimers[i] - seconds);
    }

    // Cooldown after gear/attunement Cooldown Reduction is applied.
    float CooldownFor(AbilityDef ability)
    {
        float cd = ability.cooldown;
        if (_characterStats != null)
            cd *= (1f - _characterStats.CooldownReduction);
        return cd;
    }

    void FinalizeCast(AbilityDef ability, GameObject indicator, float aimTime)
    {
        Debug.Log("Cast ability: " + ability.abilityName);

        // Notify passive (Phase Charge meter, etc.)
        _passive?.OnAbilityCast(ability);

        // Phase Charge: scale next damage ability
        float damageMultiplier = _phaseCharge != null
            ? _phaseCharge.ConsumeBonusIfCharged(ability)
            : 1f;

        // Gear + attunement damage bonus (CharacterStats) — applies to every
        // shape and every dispatched ability since they all read this value.
        if (_characterStats != null)
            damageMultiplier *= _characterStats.DamageMultiplier;

        castAnimator?.PlayCast(ability.category);

        if (ability.castVFX != null)
            SpawnVFX(ability.castVFX, transform.position + Vector3.up * 1f, transform.rotation);

        if (ability.shape == AbilityShape.Rectangle && ability.damage > 0f && indicator != null)
        {
            float chargeFraction = GetChargeFraction(ability, aimTime);
            float damage = Mathf.Lerp(ability.damage, ability.maxChargeDamage, chargeFraction) * damageMultiplier;
            ApplyRectangleDamage(ability, indicator, damage);
        }

        if (ability.shape == AbilityShape.Cone && ability.damage > 0f && indicator != null)
        {
            float chargeFraction = GetChargeFraction(ability, aimTime);
            float damage = Mathf.Lerp(ability.damage, ability.maxChargeDamage, chargeFraction) * damageMultiplier;
            float coneRange = ability.range * indicator.transform.localScale.x;
            ApplyConeDamage(ability, indicator, damage, coneRange);

            if (ability.fireVisual)
                SpawnFireBurst(transform.position + Vector3.up * 0.5f, indicator.transform.rotation, coneRange, ability.coneAngle);
        }

        if (ability.shape == AbilityShape.Circle && ability.damage > 0f)
        {
            ApplyCircleDamage(ability, indicator, damageMultiplier);
        }

        if (ability.shieldAbsorb > 0f)
            CastMagicShield(ability);

        if (ability.spawnTurret && indicator != null)
            SpawnTurret(ability, indicator.transform.position);

        // ── Route to ability-specific behaviours ──────────────────
        Vector3 castPoint = indicator != null ? indicator.transform.position : transform.position;
        DispatchAbility(ability, castPoint, damageMultiplier);

        if (indicator != null)
            Destroy(indicator, castDelay);
    }

    // ── Ability dispatch ─────────────────────────────────────────
    void DispatchAbility(AbilityDef ability, Vector3 castPoint, float dmgMult)
    {
        switch (ability.abilityName)
        {
            // ─ Warden ────────────────────────────────────────────
            case "Runic Snare":
                SpawnDeployableAt(shockMinePrefab ?? ability.deployablePrefab, castPoint,
                    go => { var m = go.GetComponent<ShockMineBehaviour>(); if (m) m.owner = gameObject; });
                break;

            case "Battle Hymn":
                CastOverdrive(ability);
                break;

            case "Spirit Redirect":
                CastDroneCommand(castPoint);
                break;

            case "Mend":
                CastFieldRepair(ability, castPoint);
                break;

            case "Conjurer's Surge":
                CastSystemOverload();
                break;

            // ─ Ironclad ──────────────────────────────────────────
            case "Counter Blow":
                kineticReversalHandler?.Activate();
                break;

            case "Gravity Slam":
                CastMagnetize(ability, castPoint);
                break;

            case "Shieldwall Charge":
                dashHandler?.BreachSlam(GetComponent<PassiveThreatProtocol>());
                break;

            case "Stalwart Stance":
                siegeModeHandler?.Activate();
                break;

            case "Rune Chain":
                CastIronTether(castPoint);
                break;

            case "Iron Rampart":
                SpawnDeployableAt(lastBastionPrefab ?? ability.deployablePrefab, castPoint, null, transform.rotation);
                break;

            // ─ Arcanist ──────────────────────────────────────────
            case "Arcane Step":
                dashHandler?.PhaseShift(castPoint);
                break;

            case "Void Maw":
                CastSingularity(ability, castPoint, false, dmgMult);
                break;

            case "Forked Lightning":
                CastArcLance(ability, castPoint, dmgMult);
                break;

            case "Collapsing Void":
                CastSingularity(ability, castPoint, true, dmgMult);
                break;

            // ─ Cleric ────────────────────────────────────────────
            case "Soul Bond":
                CastTransferProtocol(castPoint);
                break;

            case "Spirit Wisps":
                CastNaniteSwarm(ability, castPoint);
                break;

            case "Divine Spark":
                CastDefibrillator(ability, castPoint, dmgMult);
                break;

            case "Sacred Aegis":
                CastAdaptiveShield(castPoint);
                break;

            case "Dispel":
                CastPurgeProtocol(castPoint);
                break;

            case "Temporal Grace":
                SnapshotSystem.Instance?.Rollback(5f);
                break;

            // ─ Shadowblade ───────────────────────────────────────
            case "Shadow Veil":
            {
                float dur = ability.activeDuration > 0f ? ability.activeDuration : 4f;
                stealthHandler?.BeginCloak(dur);
                break;
            }

            case "Silence Ward":
                SpawnDeployableAt(nullFieldPrefab ?? ability.deployablePrefab, castPoint, null);
                break;

            case "Dark Harvest":
                CastCollapse(ability, castPoint, dmgMult);
                break;
        }
    }

    // ── New ability methods ──────────────────────────────────────

    void CastOverdrive(AbilityDef ability)
    {
        float duration  = ability.activeDuration > 0f ? ability.activeDuration : 8f;
        float auraRange = ability.indicatorSize  > 0f ? ability.indicatorSize  : 12f;

        // Apply a temporary +30% CDR buff to all allies in range (including self).
        // CDR is tracked in CharacterStats — AddTemporaryCDR clamps total CDR to 0.6 (60% max).
        Collider[] hits = Physics.OverlapSphere(transform.position, auraRange);
        foreach (var col in hits)
        {
            if (!col.CompareTag("Player")) continue;

            CharacterStats cs = col.GetComponent<CharacterStats>();
            if (cs != null)
                StartCoroutine(OverdriveCDRBuff(cs, duration));

            // Buff VFX on each ally
            if (ability.castVFX != null)
            {
                GameObject fx = Instantiate(ability.castVFX,
                    col.transform.position + Vector3.up * 0.5f, Quaternion.identity);
                Destroy(fx, duration + 0.5f);
            }
        }
    }

    private System.Collections.IEnumerator OverdriveCDRBuff(CharacterStats cs, float duration)
    {
        const float bonusCDR = 0.30f;   // +30% cooldown reduction for the duration
        cs.AddTemporaryCDR(bonusCDR);
        yield return new UnityEngine.WaitForSeconds(duration);
        cs.AddTemporaryCDR(-bonusCDR);  // remove the buff when expired
    }

    void CastDroneCommand(Vector3 castPoint)
    {
        // Find the nearest enemy to the cast point and redirect all active turrets to it.
        Collider[] hits = Physics.OverlapSphere(castPoint, 2f);
        Transform focusTarget = null;
        float best = Mathf.Infinity;
        foreach (var col in hits)
        {
            if (!col.CompareTag("Enemy")) continue;
            float d = Vector3.Distance(castPoint, col.transform.position);
            if (d < best) { best = d; focusTarget = col.transform; }
        }

        if (focusTarget == null) return;

        // Find this player's deployed turrets and set their focus target.
        if (DeployableManager.Instance != null)
        {
            foreach (var dep in DeployableManager.Instance.GetAll(gameObject.GetInstanceID()))
            {
                if (dep == null) continue;
                var tc = dep.GetComponent<TurretController>();
                if (tc != null) tc.SetFocusTarget(focusTarget, 6f);
            }
        }
    }

    void CastFieldRepair(AbilityDef ability, Vector3 castPoint)
    {
        float healAmt = ability.healAmount > 0f ? ability.healAmount : 40f;
        // Find nearest ally at cast point
        Collider[] hits = Physics.OverlapSphere(castPoint, 1.5f);
        foreach (var col in hits)
        {
            if (!col.CompareTag("Player")) continue;
            Health h = col.GetComponent<Health>();
            if (h == null || h == _health) continue;
            h.Heal(healAmt);
            col.GetComponent<StatusEffectManager>()?.RemoveAll();   // clears 1 debuff
            if (ability.hitVFX != null)
                SpawnVFX(ability.hitVFX, col.transform.position + Vector3.up, Quaternion.identity);
            break;
        }
    }

    void CastSystemOverload()
    {
        if (DeployableManager.Instance == null) return;
        DeployableManager.Instance.SystemOverload(gameObject.GetInstanceID(), 8f);

        // Force all turrets to rapid-fire mode for 8 seconds
        foreach (var dep in DeployableManager.Instance.GetAll(gameObject.GetInstanceID()))
        {
            if (dep == null) continue;
            var tc = dep.GetComponent<TurretController>();
            if (tc != null) tc.SetOverloadMode(8f);
        }
    }

    void CastMagnetize(AbilityDef ability, Vector3 castPoint)
    {
        float radius   = ability.pullRadius > 0f ? ability.pullRadius : 4f;
        float duration = ability.pullDuration > 0f ? ability.pullDuration : 2f;

        Collider[] hits = Physics.OverlapSphere(castPoint, radius);
        foreach (var col in hits)
        {
            if (!col.CompareTag(ability.targetTag)) continue;
            StartCoroutine(PullToPoint(col, castPoint, duration));
        }

        if (ability.castVFX != null)
            SpawnVFX(ability.castVFX, castPoint, Quaternion.identity);
    }

    System.Collections.IEnumerator PullToPoint(Collider col, Vector3 center, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration && col != null)
        {
            elapsed += Time.fixedDeltaTime;
            Rigidbody rb = col.GetComponent<Rigidbody>();
            Vector3 dir = (center - col.transform.position).normalized;
            if (rb != null) rb.AddForce(dir * 14f, ForceMode.Acceleration);
            else            col.transform.position += dir * 5f * Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }
    }

    void CastIronTether(Vector3 castPoint)
    {
        if (ironTetherHandler == null) return;
        // Find nearest enemy near the cast point
        Collider[] hits = Physics.OverlapSphere(castPoint, 2f);
        foreach (var col in hits)
        {
            if (!col.CompareTag("Enemy")) continue;
            ironTetherHandler.Activate(col.gameObject);
            return;
        }
    }

    void CastSingularity(AbilityDef ability, Vector3 castPoint, bool isEventHorizon, float dmgMult)
    {
        GameObject prefab = isEventHorizon
            ? (eventHorizonPrefab ?? ability.deployablePrefab)
            : (singularityPrefab  ?? ability.deployablePrefab);

        if (prefab == null) return;

        SpawnDeployableAt(prefab, castPoint, go =>
        {
            var s = go.GetComponent<SingularityBehaviour>();
            if (s == null) return;
            s.burstDamage     *= dmgMult;
            s.applyExposed     = isEventHorizon;
            s.owner            = gameObject;
            // Check for Phase Relay bonus
            float bonus = PhaseRelayDeployable.GetBonusNearPoint(castPoint, gameObject.GetInstanceID());
            s.pullDurationBonus = bonus;
        });
    }

    void CastArcLance(AbilityDef ability, Vector3 startPoint, float dmgMult)
    {
        int   maxChain   = ability.chainTargets > 0 ? ability.chainTargets : 4;
        float dmg        = ability.damage * dmgMult;
        float falloff    = ability.chainDamageFalloff;
        float jumpRadius = 6f;
        string tag       = ability.targetTag;

        Transform last = null;
        Collider   nearest = FindNearestInRadius(startPoint, jumpRadius, tag, null);

        for (int i = 0; i < maxChain && nearest != null; i++)
        {
            Health h = nearest.GetComponent<Health>();
            h?.TakeDamage(Mathf.Max(1f, dmg), gameObject);

            if (ability.hitVFX != null)
                SpawnVFX(ability.hitVFX, nearest.transform.position + Vector3.up * 0.5f, Quaternion.identity);

            // Draw lightning between jumps (quick LineRenderer)
            Vector3 from = last != null ? last.position + Vector3.up * 0.8f
                                        : startPoint    + Vector3.up * 0.8f;
            DrawLightningLine(from, nearest.transform.position + Vector3.up * 0.8f, 0.15f);

            last  = nearest.transform;
            dmg   = Mathf.Max(1f, dmg - falloff);
            nearest = FindNearestInRadius(last.position, jumpRadius, tag, last);
        }
    }

    Collider FindNearestInRadius(Vector3 center, float radius, string tag, Transform exclude)
    {
        Collider[] hits = Physics.OverlapSphere(center, radius);
        float best = Mathf.Infinity;
        Collider found = null;
        foreach (var col in hits)
        {
            if (!col.CompareTag(tag)) continue;
            if (exclude != null && col.transform == exclude) continue;
            float d = Vector3.Distance(center, col.transform.position);
            if (d < best) { best = d; found = col; }
        }
        return found;
    }

    void DrawLightningLine(Vector3 from, Vector3 to, float duration)
    {
        GameObject go   = new GameObject("ArcLance");
        LineRenderer lr = go.AddComponent<LineRenderer>();
        lr.positionCount = 8;
        lr.startWidth    = 0.05f;
        lr.endWidth      = 0.01f;
        lr.material      = new Material(Shader.Find("Sprites/Default"));
        lr.startColor    = new Color(0.8f, 0.4f, 1f, 0.9f);
        lr.endColor      = new Color(0.5f, 0.2f, 1f, 0.3f);

        for (int i = 0; i < 8; i++)
        {
            float t   = i / 7f;
            Vector3 p = Vector3.Lerp(from, to, t);
            if (i > 0 && i < 7)
            {
                Vector3 perp = Vector3.Cross((to - from).normalized, Vector3.up);
                p += perp * (Random.Range(-0.3f, 0.3f));
                p += Vector3.up * Random.Range(-0.15f, 0.15f);
            }
            lr.SetPosition(i, p);
        }

        Destroy(go, duration);
    }

    void CastTransferProtocol(Vector3 castPoint)
    {
        if (transferProtocolHandler == null) return;
        Collider[] hits = Physics.OverlapSphere(castPoint, 1.5f);
        foreach (var col in hits)
        {
            if (!col.CompareTag("Player")) continue;
            if (col.gameObject == gameObject) continue;
            transferProtocolHandler.Activate(col.gameObject);
            return;
        }
    }

    void CastNaniteSwarm(AbilityDef ability, Vector3 castPoint)
    {
        GameObject prefab = naniteSwarmPrefab ?? ability.deployablePrefab;
        if (prefab == null) return;

        // Find nearest ally to target
        Collider[] hits = Physics.OverlapSphere(castPoint, 3f);
        Health targetH = null; Transform targetT = null;
        foreach (var col in hits)
        {
            if (!col.CompareTag("Player")) continue;
            targetH = col.GetComponent<Health>();
            targetT = col.transform;
            break;
        }
        if (targetH == null) { targetH = _health; targetT = transform; }

        SpawnDeployableAt(prefab, transform.position + Vector3.up, go =>
        {
            var s = go.GetComponent<NaniteSwarmBehaviour>();
            if (s == null) return;
            s.targetHealth = targetH;
            s.target       = targetT;
            if (ability.healAmount > 0f) s.healAmount = ability.healAmount;
        });
    }

    void CastDefibrillator(AbilityDef ability, Vector3 castPoint, float dmgMult)
    {
        // Priority 1: revive a downed ally nearby
        Collider[] allies = Physics.OverlapSphere(castPoint, 2f);
        foreach (var col in allies)
        {
            if (!col.CompareTag("Player")) continue;
            Health h = col.GetComponent<Health>();
            if (h != null && h.IsDowned)
            {
                h.Revive(0.30f);
                if (ability.hitVFX != null)
                    SpawnVFX(ability.hitVFX, col.transform.position + Vector3.up, Quaternion.identity);
                return;
            }
        }

        // Priority 2: deal burst damage to robotic enemies in range
        Collider[] enemies = Physics.OverlapSphere(castPoint, 2f);
        foreach (var col in enemies)
        {
            if (!col.CompareTag("Enemy")) continue;
            Health h = col.GetComponent<Health>();
            if (h == null || !h.isRobotic) continue;
            float dmg = (ability.damage > 0f ? ability.damage : 60f) * dmgMult;
            h.TakeDamage(dmg, gameObject);
            if (ability.hitVFX != null)
                SpawnVFX(ability.hitVFX, col.transform.position + Vector3.up, Quaternion.identity);
        }
    }

    void CastAdaptiveShield(Vector3 castPoint)
    {
        // Apply a 20-absorb shield to nearest ally that grows as they take hits.
        Collider[] hits = Physics.OverlapSphere(castPoint, 2f);
        foreach (var col in hits)
        {
            if (!col.CompareTag("Player")) continue;
            Health h = col.GetComponent<Health>();
            if (h == null) continue;
            h.ApplyShield(20f);
            // Subscribe to grow shield on each hit for 8s
            StartCoroutine(AdaptiveShieldRoutine(h, 8f));
            return;
        }
    }

    System.Collections.IEnumerator AdaptiveShieldRoutine(Health target, float duration)
    {
        float expiry = Time.time + duration;
        void OnHit(float _) { target.GrowShield(10f); }
        target.onDamageTaken.AddListener(OnHit);
        while (Time.time < expiry) yield return null;
        target.onDamageTaken.RemoveListener(OnHit);
    }

    void CastPurgeProtocol(Vector3 castPoint)
    {
        Collider[] hits = Physics.OverlapSphere(castPoint, 1.5f);
        foreach (var col in hits)
        {
            if (!col.CompareTag("Player")) continue;
            col.GetComponent<StatusEffectManager>()?.RemoveAll();
            return;
        }
    }

    void CastCollapse(AbilityDef ability, Vector3 castPoint, float dmgMult)
    {
        float baseDmg = ability.damage > 0f ? ability.damage : 20f;
        float radius  = ability.indicatorSize > 0f ? ability.indicatorSize / 2f : 4f;

        Collider[] hits = Physics.OverlapSphere(castPoint, radius);
        foreach (var col in hits)
        {
            if (!col.CompareTag(ability.targetTag)) continue;
            var sem = col.GetComponent<StatusEffectManager>();
            if (sem == null) continue;
            int stacks = sem.ConsumeDebuffStacks();
            if (stacks > 0)
            {
                float dmg = baseDmg * stacks * dmgMult;
                col.GetComponent<Health>()?.TakeDamage(dmg, gameObject);
                if (ability.hitVFX != null)
                    SpawnVFX(ability.hitVFX, col.transform.position + Vector3.up * 0.5f, Quaternion.identity);
            }
        }
    }

    // Generic helper: instantiate a deployable prefab, run optional init, register it.
    void SpawnDeployableAt(GameObject prefab, Vector3 pos, System.Action<GameObject> init,
                            Quaternion? rot = null)
    {
        if (prefab == null) return;
        GameObject go = Instantiate(prefab, pos, rot ?? Quaternion.identity);
        init?.Invoke(go);
        DeployableManager.Instance?.Register(go, gameObject.GetInstanceID(),
            classPool != null ? GetClassDeployableLimit() : 1);
    }

    int GetClassDeployableLimit()
    {
        if (classPool == null) return 1;
        return classPool.className == "Warden" ? 3 : 1;
    }

    void ApplyCircleDamage(AbilityDef ability, GameObject indicator, float damageMultiplier = 1f)
    {
        Vector3 center = indicator != null ? indicator.transform.position : transform.position;
        Collider[] hits = Physics.OverlapSphere(center, ability.indicatorSize / 2f);

        foreach (Collider hit in hits)
        {
            if (!hit.CompareTag(ability.targetTag)) continue;

            Health health = hit.GetComponent<Health>();
            if (health != null)
            {
                health.TakeDamage(ability.damage * damageMultiplier);
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
