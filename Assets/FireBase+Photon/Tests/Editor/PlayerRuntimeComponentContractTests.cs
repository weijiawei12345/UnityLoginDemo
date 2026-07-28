using System;
using System.Reflection;
using Fusion;
using NUnit.Framework;
using UnityEngine;

namespace ARPG.Tests
{
    public sealed class PlayerRuntimeComponentContractTests
    {
        [TestCase("ARPG.Player.Movement.PlayerMovementConfig", typeof(MonoBehaviour))]
        [TestCase("ARPG.Player.Movement.FusionPlayerMotor", typeof(NetworkBehaviour))]
        [TestCase("ARPG.Player.Animation.FusionPlayerAnimationPresenter", typeof(NetworkBehaviour))]
        [TestCase("ARPG.Player.Camera.PlayerCameraBinder", typeof(NetworkBehaviour))]
        public void RuntimeComponent_HasExpectedBoundary(string fullTypeName, Type baseType)
        {
            Type type = Type.GetType($"{fullTypeName}, Assembly-CSharp");

            Assert.That(type, Is.Not.Null);
            Assert.That(baseType.IsAssignableFrom(type), Is.True);
        }

        [TestCase("FirstPersonCamera")]
        [TestCase("ThirdPersonCamera")]
        public void CameraController_ExposesExplicitBindingApi(string typeName)
        {
            Type type = Type.GetType($"{typeName}, Assembly-CSharp");

            Assert.That(type.GetMethod("Bind", new[] { typeof(Transform) }), Is.Not.Null);
            Assert.That(type.GetMethod("Unbind", new[] { typeof(Transform) }), Is.Not.Null);
        }

        [Test]
        public void AnimationState_ReleasesJumpBeforeFreeFall()
        {
            Type type = Type.GetType("ARPG.Player.Animation.PlayerAnimationState, Assembly-CSharp");
            MethodInfo factory = type?.GetMethod("FromMovement", BindingFlags.Public | BindingFlags.Static);

            Assert.That(factory, Is.Not.Null, "Animation state must derive the Jump lifetime from movement phase.");

            object ascending = factory.Invoke(null, new object[] { 0f, false, 2f, true });
            object descending = factory.Invoke(null, new object[] { 0f, false, -2f, true });
            PropertyInfo jumping = type.GetProperty("Jumping");

            Assert.That(jumping.GetValue(ascending), Is.True);
            Assert.That(jumping.GetValue(descending), Is.False);
        }
    }
}
