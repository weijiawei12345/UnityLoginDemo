using UnityEngine;

namespace ARPG.Player.Movement.Core
{
    /// <summary>Pure calculations used by the Fusion player motor.</summary>
    public static class MovementRules
    {
        /// <summary>Advances movement state by one simulation tick.</summary>
        public static void Step(
            ref PlayerMovementState state,
            Vector3 moveDirection,
            bool isSprinting,
            bool jumpPressed,
            bool isGrounded,
            float deltaTime,
            int tick,
            PlayerMovementConfigData config)
        {
            state.JumpedThisTick = false;
            state.LandedThisTick = false;

            Vector3 horizontalDirection = new Vector3(moveDirection.x, 0f, moveDirection.z);
            horizontalDirection = Vector3.ClampMagnitude(horizontalDirection, 1f);
            float speed = isSprinting
                ? config.RunSpeed
                : config.RunSpeed * Mathf.Clamp01(config.WalkSpeedMultiplier);

            state.Velocity.x = horizontalDirection.x * speed;
            state.Velocity.z = horizontalDirection.z * speed;

            if (jumpPressed && isGrounded && !state.JumpInProgress)
            {
                state.Velocity.y = config.JumpSpeed;
                state.IsGrounded = false;
                state.JumpInProgress = true;
                state.JumpedThisTick = true;
                state.JumpStartedTick = tick;
                return;
            }

            // CharacterController can report grounded for a tick after takeoff. Do not let
            // that stale contact erase the upward velocity before an airborne tick is seen.
            if (state.JumpInProgress && !state.WasAirborne)
            {
                state.IsGrounded = false;
                state.Velocity.y += config.Gravity * deltaTime;
                if (!isGrounded)
                {
                    state.WasAirborne = true;
                    return;
                }

                if (state.Velocity.y > 0f)
                {
                    return;
                }

                state.JumpInProgress = false;
            }

            if (isGrounded)
            {
                state.LandedThisTick = state.WasAirborne;
                state.JumpInProgress = false;
                state.WasAirborne = false;
                state.IsGrounded = true;
                state.Velocity.y = config.GroundStickVelocity;
                return;
            }

            state.IsGrounded = false;
            state.WasAirborne = true;
            state.Velocity.y += config.Gravity * deltaTime;
        }

        /// <summary>Reconciles ground contact reported by the movement executed this tick.</summary>
        public static void ResolveGroundContactAfterMove(
            ref PlayerMovementState state,
            bool contactedGround,
            PlayerMovementConfigData config)
        {
            // CharacterController.isGrounded describes the previous Move. CollisionFlags lets
            // the authoritative state publish a landing during the Move that actually caused it.
            if (!contactedGround || !state.WasAirborne || state.Velocity.y > 0f)
            {
                return;
            }

            state.JumpInProgress = false;
            state.WasAirborne = false;
            state.IsGrounded = true;
            state.LandedThisTick = true;
            state.Velocity.y = config.GroundStickVelocity;
        }
    }
}
