using UnityEngine;

[System.Serializable]
public class TraitPill
{
    public Sprite icon;
    public string label;
}

[System.Serializable]
public class ClassStat
{
    public string label;
    [Range(0, 5)] public int value;
}

[System.Serializable]
public class AbilityPreview
{
    public Sprite icon;
    public string abilityName;
    [TextArea(1, 3)] public string description;
}

[CreateAssetMenu(fileName = "CharacterData", menuName = "RoD/Character Data")]
public class CharacterData : ScriptableObject
{
    [Header("Identity")]
    public string className;
    public string roleTagline;              // e.g. "Ranged DPS  ·  Blaster  ·  Controller"
    [TextArea(3, 6)] public string loreDescription;

    [Header("Visuals")]
    public Color classColor = Color.white;  // accent — used for glow, pills, stat bars
    public Color classColorDark = Color.gray; // panel background tint
    public Sprite portrait;                 // optional 2D fallback if no 3D preview

    [Header("Prefabs")]
    public GameObject prefab;              // spawned in-game
    public GameObject previewPrefab;       // spinning model in character select

    [Header("Trait Pills  (4–6 recommended)")]
    public TraitPill[] traits;

    [Header("Stats  (0–5 each)")]
    public ClassStat[] stats;

    [Header("Core Abilities  (show 3 on select screen)")]
    public AbilityPreview[] coreAbilities;

    [Header("Signature Deployable")]
    public string deployableName;
    [TextArea(1, 3)] public string deployableDescription;
    public Sprite deployableIcon;
}
