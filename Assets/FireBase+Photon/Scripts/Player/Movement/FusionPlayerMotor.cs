using ARPG.Player.Input;
using ARPG.Player.Movement.Core;
using Fusion;
using UnityEngine;

namespace ARPG.Player.Movement
{
    /// <summary>Consumes Fusion tick input and moves the authoritative player CharacterController.</summary>
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(PlayerMovementConfig))]
    public sealed class FusionPlayerMotor : NetworkBehaviour
    {
        private CharacterController _controller;
        private PlayerMovementConfig _config;
        private PlayerMovementState _state;

        [Networked] public float HorizontalSpeed { get; private set; }
        [Networked] public float VerticalSpeed { get; private set; }
        [Networked] public NetworkBool IsGrounded { get; private set; }
        [Networked] public NetworkBool IsJumping { get; private set; }

        public float RunSpeed => _config != null ? _config.RunSpeed : 0f;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _config = GetComponent<PlayerMovementConfig>();
        }

        public override void FixedUpdateNetwork()
        {
            // Shared Mode grants this player State Authority locally. Only that peer may move
            // the CharacterController or publish the resulting network state.
            if (!HasStateAuthority || _controller == null || _config == null)
            {
                return;
            }

            PlayerInputFrame input = default;
            GetInput(out input);

            Vector3 moveDirection = ToWorldDirection(input.Move, input.CameraForward);
            bool sprint = input.Buttons.IsSet(PlayerInputButton.Sprint);
            bool jump = input.Buttons.IsSet(PlayerInputButton.Jump);

            PlayerMovementConfigData config = _config.ToData();
            MovementRules.Step(
                ref _state,
                moveDirection,
                sprint,
                jump,
                _controller.isGrounded,
                Runner.DeltaTime,
                Runner.Tick,
                config);

            Vector3 displacement = _state.Velocity * Runner.DeltaTime;
            CollisionFlags collisionFlags = _controller.Move(displacement);
            bool contactedGround = (collisionFlags & CollisionFlags.Below) != 0;
            MovementRules.ResolveGroundContactAfterMove(ref _state, contactedGround, config);

            Vector3 horizontalVelocity = new Vector3(_state.Velocity.x, 0f, _state.Velocity.z);
            if (horizontalVelocity.sqrMagnitude > 0.0001f)
            {
                transform.forward = horizontalVelocity.normalized;
            }

            HorizontalSpeed = horizontalVelocity.magnitude;
            VerticalSpeed = _state.Velocity.y;
            IsGrounded = _state.IsGrounded;
            IsJumping = _state.JumpInProgress;
        }

        private static Vector3 ToWorldDirection(Vector2 move, Vector3 cameraForward)
        {
            Vector3 forward = Vector3.ProjectOnPlane(cameraForward, Vector3.up);
            if (forward.sqrMagnitude <= 0.0001f)
            {
                forward = Vector3.forward;
            }

            forward.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            return Vector3.ClampMagnitude(right * move.x + forward * move.y, 1f);
        }
    }
}
