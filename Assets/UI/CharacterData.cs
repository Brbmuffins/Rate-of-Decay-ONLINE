using UnityEngine;

[CreateAssetMenu(fileName = "CharacterData", menuName = "RoD/Character Data")]
public class CharacterData : ScriptableObject
{
    public string className;
    [TextArea] public string description;
    public GameObject prefab;        // the actual player prefab spawned in-game
    public GameObject previewPrefab; // model shown spinning in character select (can be same as prefab)
    public Color classColor = Color.white;
    public Sprite portrait;
}
