using System.Collections;
using UnityEngine;

// Guardian — Last Bastion
// A wall that blocks incoming projectiles. Spawned facing the Guardian's forward direction.
// The collider blocks objects on the "Projectile" layer (set up in Physics settings).
// VFX: brbmuffins Dark Arts/Fantasy Pack/Prefabs/Effects normal/Mana wall.prefab — use as castVFX,
//      tint it blue in the material to read as hardlight rather than green magic.
public class LastBastionWall : MonoBehaviour
{
    [Header("Duration")]
    public float duration = 10f;

    [Header("VFX")]
    // Assign: brbmuffins Dark Arts/.../Mana wall.prefab as a child of this object
    // The prefab already has particle + mesh — just drop it in.
    public GameObject wallVFX;

    void Start()
    {
        if (wallVFX != null)
            Instantiate(wallVFX, transform.position, transform.rotation, transform);

        StartCoroutine(Expire());
    }

    private IEnumerator Expire()
    {
        yield return new WaitForSeconds(duration);
        Destroy(gameObject);
    }
}
