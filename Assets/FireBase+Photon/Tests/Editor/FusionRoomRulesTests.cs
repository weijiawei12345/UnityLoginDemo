using System;
using System.Reflection;
using NUnit.Framework;

namespace ARPG.Tests
{
    public sealed class FusionRoomRulesTests
    {
        private const string RulesTypeName =
            "ARPG.Networking.Lobby.FusionRoomRules, ARPG.Networking.Lobby.Core";

        [Test]
        public void NormalizeRoomName_TrimsValidName()
        {
            Type rulesType = RequireRulesType();
            MethodInfo normalize = rulesType.GetMethod("NormalizeRoomName", BindingFlags.Public | BindingFlags.Static);

            Assert.That(normalize, Is.Not.Null);
            Assert.That(normalize.Invoke(null, new object[] { "  team-01  " }), Is.EqualTo("team-01"));
        }

        [TestCase("")]
        [TestCase("room with spaces")]
        [TestCase("room/01")]
        [TestCase("1234567890123456789012345")]
        public void TryValidateRoomName_RejectsUnsafeNames(string roomName)
        {
            Type rulesType = RequireRulesType();
            MethodInfo validate = rulesType.GetMethod("TryValidateRoomName", BindingFlags.Public | BindingFlags.Static);
            object[] arguments = { roomName, null };

            Assert.That(validate, Is.Not.Null);
            Assert.That(validate.Invoke(null, arguments), Is.False);
            Assert.That(arguments[1], Is.Not.Null.And.Not.Empty);
        }

        private static Type RequireRulesType()
        {
            Type type = Type.GetType(RulesTypeName);
            Assert.That(type, Is.Not.Null, "FusionRoomRules has not been implemented yet.");
            return type;
        }
    }
}
