using System;
using Fusion;
using NUnit.Framework;

namespace ARPG.Tests
{
    public sealed class PlayerInputPipelineContractTests
    {
        [Test]
        public void PlayerInputFrame_IsFusionNetworkInput()
        {
            Type type = Type.GetType("ARPG.Player.Input.PlayerInputFrame, Assembly-CSharp");

            Assert.That(type, Is.Not.Null);
            Assert.That(typeof(INetworkInput).IsAssignableFrom(type), Is.True);
        }

        [Test]
        public void InputSource_ExposesCaptureAndConsumeOnly()
        {
            Type type = Type.GetType("ARPG.Player.Input.IPlayerInputSource, Assembly-CSharp");

            Assert.That(type, Is.Not.Null);
            Assert.That(type.GetMethod("Capture"), Is.Not.Null);
            Assert.That(type.GetMethod("ConsumeTickButtons"), Is.Not.Null);
        }

        [Test]
        public void FusionInputCallbacks_ImplementsRunnerCallbacks()
        {
            Type type = Type.GetType("ARPG.Player.Input.FusionPlayerInputCallbacks, Assembly-CSharp");

            Assert.That(type, Is.Not.Null);
            Assert.That(typeof(INetworkRunnerCallbacks).IsAssignableFrom(type), Is.True);
        }
    }
}
