using UnityEngine;

public class CastAnimator : MonoBehaviour
{
    public AnimationClip castClip;
    public float crossfadeTime = 0.1f;

    private Animator anim;
    private AnimatorOverrideController overrideController;

    void Start()
    {
        anim = GetComponentInChildren<Animator>();
        if (anim == null) return;

        overrideController = new AnimatorOverrideController(anim.runtimeAnimatorController);
        anim.runtimeAnimatorController = overrideController;
    }

    public void PlayCast()
    {
        if (anim == null || castClip == null) return;
        anim.CrossFadeInFixedTime(castClip.name, crossfadeTime);
    }
}
