using System;
using System.Reflection;
using NUnit.Framework;

namespace ARPG.Tests
{
    public sealed class LobbyRoomSnapshotTests
    {
        private const string SnapshotTypeName =
            "ARPG.Networking.Lobby.LobbyRoomSnapshot, ARPG.Networking.Lobby.Core";

        [TestCase(false, true, 1, 4, false)]
        [TestCase(true, false, 1, 4, false)]
        [TestCase(true, true, 4, 4, false)]
        [TestCase(true, true, 3, 4, true)]
        public void CanJoin_ReflectsVisibilityOpenAndCapacity(
            bool isVisible,
            bool isOpen,
            int playerCount,
            int maxPlayers,
            bool expected)
        {
            Type snapshotType = Type.GetType(SnapshotTypeName);
            Assert.That(snapshotType, Is.Not.Null, "LobbyRoomSnapshot has not been implemented yet.");

            object snapshot = Activator.CreateInstance(
                snapshotType,
                "room-01",
                playerCount,
                maxPlayers,
                isOpen,
                isVisible,
                "play",
                "normal",
                "waiting",
                "dev");
            PropertyInfo canJoin = snapshotType.GetProperty("CanJoin");

            Assert.That(canJoin, Is.Not.Null);
            Assert.That(canJoin.GetValue(snapshot), Is.EqualTo(expected));
        }
    }
}
