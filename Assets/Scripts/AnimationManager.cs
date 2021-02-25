using UnityEngine;

[RequireComponent(typeof(Animator))]
public class AnimationManager : MonoBehaviour
{
    public AnimationClip animationClip;

    private Animator animator;


    void Start()
    {
        animator = GetComponent<Animator>();
        AnimatorOverrideController animatorOverrideController = new AnimatorOverrideController();
        animatorOverrideController.runtimeAnimatorController = animator.runtimeAnimatorController;
        animatorOverrideController["temp"] = animationClip;
        animator.runtimeAnimatorController = animatorOverrideController;
    }
}
