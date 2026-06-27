using UnityEngine;

// ─────────────────────────────────────────────────────────────────────────────
//  RodBillboard
//  Makes a GameObject always face the main camera.
//  Used on zone indicator text labels placed by RodCombatWorldBuilder.
// ─────────────────────────────────────────────────────────────────────────────

[DisallowMultipleComponent]
public class RodBillboard : MonoBehaviour
{
    void LateUpdate()
    {
        var cam = Camera.main;
        if (cam == null) return;
        transform.rotation = Quaternion.LookRotation(
            transform.position - cam.transform.position);
    }
}
