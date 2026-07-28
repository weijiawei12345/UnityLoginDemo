using System;
using System.Reflection;
using NUnit.Framework;

namespace ARPG.Tests
{
    public sealed class PlayerSpawnerLifecycleTests
    {
        [Test]
        public void PlayerSpawner_ExposesLateSceneSpawnEntryPoint()
        {
            Type spawnerType = Type.GetType("PlayerSpawner, Assembly-CSharp");

            Assert.That(spawnerType, Is.Not.Null);
            Assert.That(
                spawnerType.GetMethod("EnsureLocalPlayerSpawned", BindingFlags.Instance | BindingFlags.Public),
                Is.Not.Null,
                "A PlayerSpawner loaded after PlayerJoined needs an explicit local-player spawn entry point.");
        }
    }
}
