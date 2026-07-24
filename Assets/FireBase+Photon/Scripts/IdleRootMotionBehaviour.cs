using UnityEngine;

/// <summary>
/// 挂在「Idle Walk Run Blend」上：Speed≈0（待机）时开启 Apply Root Motion，
/// 有移动速度或离开该状态时关闭，避免与 CharacterController 位移冲突。
/// </summary>
public class IdleRootMotionBehaviour : StateMachineBehaviour
{
    [SerializeField] private string speedParameter = "Speed";
    [SerializeField] private float idleSpeedThreshold = 0.01f;

    public override void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        bool applyRootMotion = animator.GetFloat(speedParameter) <= idleSpeedThreshold;
        if (animator.applyRootMotion != applyRootMotion)
            animator.applyRootMotion = applyRootMotion;
    }

    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if (animator.applyRootMotion)
            animator.applyRootMotion = false;
    }
}
