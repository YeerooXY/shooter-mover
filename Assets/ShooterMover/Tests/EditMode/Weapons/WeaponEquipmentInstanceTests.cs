using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Weapons;

namespace ShooterMover.Tests.EditMode.Weapons
{
    public sealed class WeaponEquipmentInstanceTests
    {
        [Test]
        public void PublicInstanceDataContainsOnlyCanonicalWeaponState()
        {
            string[] properties = typeof(WeaponEquipmentInstance)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(value => value.Name)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();

            Assert.That(
                properties,
                Is.EqualTo(new[]
                {
                    "AugmentAssignments",
                    "InstanceId",
                    "OverclockAssignments",
                    "WeaponDefinitionId",
                }));
        }

        [Test]
        public void SameDefinitionCreatesTwoSeparateOwnedWeapons()
        {
            var definitionId = new WeaponDefinitionId("rattler.mk1");
            WeaponEquipmentInstance first =
                WeaponEquipmentInstance.CreateUnmodified(
                    StableId.Parse("instance.11111111111111111111111111111111"),
                    definitionId);
            WeaponEquipmentInstance second =
                WeaponEquipmentInstance.CreateUnmodified(
                    StableId.Parse("instance.22222222222222222222222222222222"),
                    definitionId);

            Assert.That(first.InstanceId, Is.Not.EqualTo(second.InstanceId));
            Assert.That(
                first.WeaponDefinitionId,
                Is.EqualTo(second.WeaponDefinitionId));
            Assert.That(first, Is.Not.EqualTo(second));
        }

        [Test]
        public void SameDefinitionMayRetainDifferentAssignments()
        {
            var definitionId = new WeaponDefinitionId("rattler.mk1");
            WeaponEquipmentInstance plain = WeaponEquipmentInstance.Create(
                StableId.Parse("instance.33333333333333333333333333333333"),
                definitionId,
                Array.Empty<StableId>(),
                Array.Empty<StableId>());
            WeaponEquipmentInstance modified = WeaponEquipmentInstance.Create(
                StableId.Parse("instance.44444444444444444444444444444444"),
                definitionId,
                new[]
                {
                    StableId.Parse("augment-instance.calibrated-feed"),
                },
                new[]
                {
                    StableId.Parse("overclock-instance.aggressive-cycle"),
                });

            Assert.That(plain.AugmentAssignments, Is.Empty);
            Assert.That(plain.OverclockAssignments, Is.Empty);
            Assert.That(modified.AugmentAssignments.Count, Is.EqualTo(1));
            Assert.That(modified.OverclockAssignments.Count, Is.EqualTo(1));
            Assert.That(plain, Is.Not.EqualTo(modified));
        }

        [Test]
        public void AssignmentChangesPreserveExactOwnedIdentity()
        {
            StableId instanceId =
                StableId.Parse("instance.55555555555555555555555555555555");
            var definitionId = new WeaponDefinitionId("rattler.mk1");
            WeaponEquipmentInstance original =
                WeaponEquipmentInstance.CreateUnmodified(
                    instanceId,
                    definitionId);

            WeaponEquipmentInstance updated = original.WithAssignments(
                new[]
                {
                    StableId.Parse("augment-instance.precision-feed"),
                },
                Array.Empty<StableId>());

            Assert.That(updated.InstanceId, Is.EqualTo(instanceId));
            Assert.That(updated.WeaponDefinitionId, Is.EqualTo(definitionId));
            Assert.That(updated.AugmentAssignments.Count, Is.EqualTo(1));
        }

        [Test]
        public void GeneratedIdentityIsOpaqueAndCarriesNoWeaponOrSourceData()
        {
            StableId generated = OwnedEquipmentInstanceIdFactory.Create();
            string canonical = generated.ToString();

            Assert.That(generated.Namespace, Is.EqualTo("instance"));
            Assert.That(generated.Value.Length, Is.EqualTo(32));
            Assert.That(
                generated.Value.All(value =>
                    (value >= '0' && value <= '9')
                    || (value >= 'a' && value <= 'f')),
                Is.True);
            Assert.That(canonical, Does.Not.Contain("rattler"));
            Assert.That(canonical, Does.Not.Contain("weapon"));
            Assert.That(canonical, Does.Not.Contain("starter"));
            Assert.That(canonical, Does.Not.Contain("onboarding"));
            Assert.That(canonical, Does.Not.Contain("slot"));
        }

        [Test]
        public void DuplicateAssignmentReferenceIsRejected()
        {
            StableId duplicate =
                StableId.Parse("augment-instance.repeated-reference");

            Assert.Throws<ArgumentException>(() =>
                WeaponEquipmentInstance.Create(
                    StableId.Parse(
                        "instance.66666666666666666666666666666666"),
                    new WeaponDefinitionId("rattler.mk1"),
                    new[] { duplicate, duplicate },
                    Array.Empty<StableId>()));
        }
    }
}
