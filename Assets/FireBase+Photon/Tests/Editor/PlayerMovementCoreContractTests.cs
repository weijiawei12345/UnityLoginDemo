using System;
using NUnit.Framework;

namespace ARPG.Tests
{
    public sealed class PlayerMovementCoreContractTests
    {
        private const string CoreAssembly = "ARPG.Player.Movement.Core";

        [TestCase("ARPG.Player.Movement.Core.PlayerMovementConfigData")]
        [TestCase("ARPG.Player.Movement.Core.PlayerMovementState")]
        [TestCase("ARPG.Player.Movement.Core.MovementRules")]
        public void MovementCore_ExposesFocusedRuleTypes(string fullTypeName)
        {
            Type type = Type.GetType($"{fullTypeName}, {CoreAssembly}");

            Assert.That(type, Is.Not.Null, $"Missing movement core type: {fullTypeName}.");
        }
    }
}
