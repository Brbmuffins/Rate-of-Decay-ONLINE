using UnityEngine;

public class CastAnimator : MonoBehaviour
{
    // In your AnimatorController create 3 triggers: CastDamage, CastHeal, CastSupport
    // Each trigger transitions from Any State into its own animation state.

    private Animator anim;

    void Start()
    {
        anim = GetComponentInChildren<Animator>();
    }

    public void PlayCast(AbilityCategory category)
    {
        if (anim == null) return;

        switch (category)
        {
            case AbilityCategory.Heal:    anim.SetTrigger("CastHeal");    break;
            case AbilityCategory.Support: anim.SetTrigger("CastSupport"); break;
            default:                      anim.SetTrigger("CastDamage");  break;
        }
    }
}
