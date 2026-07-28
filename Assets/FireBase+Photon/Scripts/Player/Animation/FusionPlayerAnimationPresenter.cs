using ARPG.Player.Movement;
using Fusion;
using UnityEngine;

namespace ARPG.Player.Animation
{
    /// <summary>Maps authoritative movement results to the networked Animator.</summary>
    [RequireComponent(typeof(FusionPlayerMotor))]
    [RequireComponent(typeof(NetworkMecanimAnimator))]
    public sealed class FusionPlayerAnimationPresenter : NetworkBehaviour
    {
        private static readonly int SpeedHash = Animator.StringToHash("Speed");
        private static readonly int MotionSpeedHash = Animator.StringToHash("MotionSpeed");
        private static readonly int GroundedHash = Animator.StringToHash("Grounded");
        private static readonly int FreeFallHash = Animator.StringToHash("FreeFall");
        private static readonly int JumpHash = Animator.StringToHash("Jump");

        [SerializeField] private Animator _animator;
        [SerializeField, Min(0f)] private float _animatorSpeedMax = 15f;

        private FusionPlayerMotor _motor;

        private void Awake()
        {
            _motor = GetComponent<FusionPlayerMotor>();
            if (_animator == null)
            {
                _animator = GetComponentInChildren<Animator>();
            }
        }

        public override void Render()
        {
            // NetworkMecanimAnimator applies replicated parameters on proxies. Restrict this
            // presenter to State Authority so the Animator has one gameplay writer.
            if (!HasStateAuthority || _animator == null || _motor == null)
            {
                return;
            }

            float runSpeed = _motor.RunSpeed;
            float normalizedSpeed = runSpeed > 0f
                ? Mathf.Clamp01(_motor.HorizontalSpeed / runSpeed)
                : 0f;
            bool grounded = _motor.IsGrounded;

            PlayerAnimationState state = PlayerAnimationState.FromMovement(
                normalizedSpeed,
                grounded,
                _motor.VerticalSpeed,
                _motor.IsJumping);
            Apply(state);
        }

        private void Apply(PlayerAnimationState state)
        {
            _animator.SetFloat(SpeedHash, state.NormalizedSpeed * _animatorSpeedMax);
            _animator.SetFloat(MotionSpeedHash, 1f);
            _animator.SetBool(GroundedHash, state.Grounded);
            _animator.SetBool(FreeFallHash, state.FreeFall);
            _animator.SetBool(JumpHash, state.Jumping);
        }
    }
}
