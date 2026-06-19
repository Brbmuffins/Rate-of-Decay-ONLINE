using UnityEngine;

// An attunement is a gear UPGRADE socketed into an equippable item.
// This is the game's progression vector — there is no XP and no levels.
// Create via: Assets > Create > Inventory > Attunement
[CreateAssetMenu(fileName = "New Attunement", menuName = "Inventory/Attunement")]
public class Attunement : ScriptableObject
{
    public string itemName = "Attunement";
    public Sprite icon;
    [TextArea] public string description;

    [Tooltip("1 = minor, 3 = major. Used for UI color/sorting only.")]
    [Range(1, 3)] public int tier = 1;

    [Tooltip("Stat changes this attunement grants while socketed into gear.")]
    public StatModifier[] modifiers;

    public Color TierColor
    {
        get
        {
            switch (tier)
            {
                case 2:  return new Color(0.3f, 0.6f, 1f);   // blue
                case 3:  return new Color(0.7f, 0.3f, 1f);   // purple
                default: return new Color(0.3f, 0.9f, 0.3f); // green
            }
        }
    }
}
