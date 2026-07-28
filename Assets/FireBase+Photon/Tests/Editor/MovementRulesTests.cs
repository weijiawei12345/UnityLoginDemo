using System.Reflection;
using ARPG.Player.Movement.Core;
using NUnit.Framework;
using UnityEngine;

namespace ARPG.Tests
{
    public sealed class MovementRulesTests
    {
        private static readonly PlayerMovementConfigData Config =
            new PlayerMovementConfigData(5f, 0.5f, -9.81f, -1f, 5f);

        [Test]
        public void Step_ClampsDiagonalInputAndUsesWalkSpeed()
        {
            var state = new PlayerMovementState();

            MovementRules.Step(ref state, new Vector3(1f, 0f, 1f), false, false, true, 0.02f, 10, Config);

            Assert.That(new Vector2(state.Velocity.x, state.Velocity.z).magnitude, Is.EqualTo(2.5f).Within(0.001f));
        }

        [Test]
        public void Step_UsesRunSpeedWhenSprinting()
        {
            var state = new PlayerMovementState();

            MovementRules.Step(ref state, Vector3.forward, true, false, true, 0.02f, 10, Config);

            Assert.That(state.Velocity.z, Is.EqualTo(5f).Within(0.001f));
        }

        [Test]
        public void Step_AppliesGroundStickVelocityWhileGrounded()
        {
            var state = new PlayerMovementState { Velocity = new Vector3(0f, -20f, 0f) };

            MovementRules.Step(ref state, Vector3.zero, false, false, true, 0.02f, 10, Config);

            Assert.That(state.Velocity.y, Is.EqualTo(-1f));
            Assert.That(state.IsGrounded, Is.True);
        }

        [Test]
        public void Step_JumpTickDoesNotLandImmediately()
        {
            var state = new PlayerMovementState();

            MovementRules.Step(ref state, Vector3.zero, false, true, true, 0.02f, 10, Config);

            Assert.That(state.Velocity.y, Is.EqualTo(5f));
            Assert.That(state.JumpedThisTick, Is.True);
            Assert.That(state.LandedThisTick, Is.False);
            Assert.That(state.IsGrounded, Is.False);
        }

        [Test]
        public void Step_PreservesTakeoffWhenControllerStillReportsGrounded()
        {
            var state = new PlayerMovementState();
            MovementRules.Step(ref state, Vector3.zero, false, true, true, 0.02f, 10, Config);

            MovementRules.Step(ref state, Vector3.zero, false, false, true, 0.02f, 11, Config);

            Assert.That(state.Velocity.y, Is.GreaterThan(0f));
            Assert.That(state.IsGrounded, Is.False);
            Assert.That(state.LandedThisTick, Is.False);
        }

        [Test]
        public void Step_CancelsTakeoffIfGroundContactOutlastsUpwardVelocity()
        {
            var state = new PlayerMovementState
            {
                Velocity = new Vector3(0f, 0.01f, 0f),
                JumpInProgress = true,
                JumpStartedTick = 10
            };

            MovementRules.Step(ref state, Vector3.zero, false, false, true, 0.1f, 11, Config);

            Assert.That(state.JumpInProgress, Is.False);
            Assert.That(state.IsGrounded, Is.True);
            Assert.That(state.Velocity.y, Is.EqualTo(Config.GroundStickVelocity));
        }

        [Test]
        public void Step_IgnoresJumpWhileAirborne()
        {
            var state = new PlayerMovementState { Velocity = new Vector3(0f, 2f, 0f), WasAirborne = true };

            MovementRules.Step(ref state, Vector3.zero, false, true, false, 0.1f, 11, Config);

            Assert.That(state.Velocity.y, Is.EqualTo(2f + Config.Gravity * 0.1f).Within(0.001f));
            Assert.That(state.JumpedThisTick, Is.False);
        }

        [Test]
        public void Step_EmitsLandingOnceAfterAirborneState()
        {
            var state = new PlayerMovementState { Velocity = new Vector3(0f, -2f, 0f), WasAirborne = true };

            MovementRules.Step(ref state, Vector3.zero, false, false, true, 0.02f, 20, Config);
            Assert.That(state.LandedThisTick, Is.True);

            MovementRules.Step(ref state, Vector3.zero, false, false, true, 0.02f, 21, Config);
            Assert.That(state.LandedThisTick, Is.False);
        }

        [Test]
        public void ResolveGroundContactAfterMove_EndsJumpOnLandingTick()
        {
            var state = new PlayerMovementState
            {
                Velocity = new Vector3(0f, -2f, 0f),
                JumpInProgress = true,
                WasAirborne = true
            };
            MethodInfo method = typeof(MovementRules).GetMethod("ResolveGroundContactAfterMove");

            Assert.That(method, Is.Not.Null, "Movement rules must reconcile CharacterController.Move landing results.");

            object[] arguments = { state, true, Config };
            method.Invoke(null, arguments);
            state = (PlayerMovementState)arguments[0];

            Assert.That(state.JumpInProgress, Is.False);
            Assert.That(state.IsGrounded, Is.True);
            Assert.That(state.LandedThisTick, Is.True);
            Assert.That(state.Velocity.y, Is.EqualTo(Config.GroundStickVelocity));
        }
    }
}
