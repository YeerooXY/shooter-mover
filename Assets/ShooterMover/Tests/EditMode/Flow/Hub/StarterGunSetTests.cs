using System.Linq;
using NUnit.Framework;
using ShooterMover.Application.Flow.Game;
using ShooterMover.Domain.Common;

namespace ShooterMover.Tests.EditMode.Flow.Hub
{
    public sealed class StarterGunSetTests
    {
        [Test]
        public void FreshCharacterUsesOnlyCurrentAuthoredStarterGuns()
        {
            StarterInventory starter = StarterLoadout.CreateStarter(
                StableId.Parse("character-instance.authored-starter-guns"),
                StableId.Parse(GunMountPolicy.AggressiveLoadoutProfileId));

            string[] actual = starter.GunInventory.Instances
                .Select(item => item.GunDefinitionId.Value)
                .Distinct()
                .OrderBy(value => value)
                .ToArray();
            string[] expected =
            {
                "gun_arc_rifle_mk1_01",
                "gun_blaster_mk1_01",
                "gun_rattler_mk1_01",
                "gun_shotgun_mk1_01",
                "gun_sniper_mk1_01",
                "gun_teknova_singularity_mk1_01",
            };

            Assert.That(actual, Is.EqualTo(expected));
            Assert.That(
                starter.EquippedGuns.Bindings
                    .Where(binding => binding.InstanceId != null)
                    .All(binding => starter.GunInventory.Instances.Any(item =>
                        item.InstanceId == binding.InstanceId
                        && item.GunDefinitionId.Value
                            == StarterLoadout.DefaultGunId)),
                Is.True);
            Assert.That(actual, Does.Not.Contain("rattler.mk1"));
            Assert.That(actual, Does.Not.Contain("sweeper.mk1"));
            Assert.That(actual, Does.Not.Contain("voltspike.mk1"));
            Assert.That(actual, Does.Not.Contain("prismata.mk1"));
            Assert.That(actual, Does.Not.Contain("crownfall.mk1"));
            Assert.That(actual, Does.Not.Contain("nullstar.mk1"));
        }
    }
}
