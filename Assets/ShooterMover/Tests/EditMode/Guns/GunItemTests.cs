using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Guns;
using ShooterMover.Domain.Guns.Execution;

namespace ShooterMover.Tests.EditMode.Guns
{
    public sealed class GunItemTests
    {
        [Test]
        public void PublicInstanceDataContainsOnlyCanonicalGunState()
        {
            string[] properties = typeof(GunItem)
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
                    "GunDefinitionId",
                }));
        }

        [Test]
        public void SameDefinitionCreatesTwoSeparateOwnedGuns()
        {
            var definitionId = new GunDefinitionId("rattler.mk1");
            GunItem first =
                GunItem.CreateUnmodified(
                    StableId.Parse("instance.11111111111111111111111111111111"),
                    definitionId);
            GunItem second =
                GunItem.CreateUnmodified(
                    StableId.Parse("instance.22222222222222222222222222222222"),
                    definitionId);

            Assert.That(first.InstanceId, Is.Not.EqualTo(second.InstanceId));
            Assert.That(
                first.GunDefinitionId,
                Is.EqualTo(second.GunDefinitionId));
            Assert.That(first, Is.Not.EqualTo(second));
        }

        [Test]
        public void SameDefinitionMayRetainDifferentAssignments()
        {
            var definitionId = new GunDefinitionId("rattler.mk1");
            GunItem plain = GunItem.Create(
                StableId.Parse("instance.33333333333333333333333333333333"),
                definitionId,
                Array.Empty<StableId>(),
                Array.Empty<StableId>());
            GunItem modified = GunItem.Create(
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

            Assert.That(plain.AugmentAssignments, Is.Not.Null);
            Assert.That(plain.OverclockAssignments, Is.Not.Null);
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
            var definitionId = new GunDefinitionId("rattler.mk1");
            GunItem original =
                GunItem.CreateUnmodified(
                    instanceId,
                    definitionId);

            GunItem updated = original.WithAssignments(
                new[]
                {
                    StableId.Parse("augment-instance.precision-feed"),
                },
                Array.Empty<StableId>());

            Assert.That(updated.InstanceId, Is.EqualTo(instanceId));
            Assert.That(updated.GunDefinitionId, Is.EqualTo(definitionId));
            Assert.That(updated.AugmentAssignments.Count, Is.EqualTo(1));
        }

        [Test]
        public void GeneratedIdentityIsOpaqueAndCarriesNoGunOrSourceData()
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
            Assert.That(canonical, Does.Not.Contain("gun"));
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
                GunItem.Create(
                    StableId.Parse(
                        "instance.66666666666666666666666666666666"),
                    new GunDefinitionId("rattler.mk1"),
                    new[] { duplicate, duplicate },
                    Array.Empty<StableId>()));
        }

        [Test]
        public void NullAssignmentEnumerablesAreRejected()
        {
            StableId instanceId =
                StableId.Parse("instance.77777777777777777777777777777777");
            var definitionId = new GunDefinitionId("rattler.mk1");

            Assert.Throws<ArgumentNullException>(() =>
                GunItem.Create(
                    instanceId,
                    definitionId,
                    null,
                    Array.Empty<StableId>()));
            Assert.Throws<ArgumentNullException>(() =>
                GunItem.Create(
                    instanceId,
                    definitionId,
                    Array.Empty<StableId>(),
                    null));
        }

        [Test]
        public void NullAssignmentEntriesAreRejected()
        {
            Assert.Throws<ArgumentException>(() =>
                GunItem.Create(
                    StableId.Parse(
                        "instance.88888888888888888888888888888888"),
                    new GunDefinitionId("rattler.mk1"),
                    new StableId[] { null },
                    Array.Empty<StableId>()));
        }

        [Test]
        public void AssignmentCollectionsAreSortedDeterministically()
        {
            StableId augmentA = StableId.Parse("augment-instance.alpha");
            StableId augmentZ = StableId.Parse("augment-instance.zulu");
            StableId overclockA = StableId.Parse("overclock-instance.alpha");
            StableId overclockZ = StableId.Parse("overclock-instance.zulu");

            GunItem instance = GunItem.Create(
                StableId.Parse("instance.99999999999999999999999999999999"),
                new GunDefinitionId("rattler.mk1"),
                new[] { augmentZ, augmentA },
                new[] { overclockZ, overclockA });

            Assert.That(
                instance.AugmentAssignments,
                Is.EqualTo(new[] { augmentA, augmentZ }));
            Assert.That(
                instance.OverclockAssignments,
                Is.EqualTo(new[] { overclockA, overclockZ }));
        }

        [Test]
        public void AssignmentCollectionsCopyInputsAndRejectMutation()
        {
            StableId original = StableId.Parse("augment-instance.original");
            StableId later = StableId.Parse("augment-instance.later");
            var source = new List<StableId> { original };
            GunItem instance = GunItem.Create(
                StableId.Parse("instance.aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"),
                new GunDefinitionId("rattler.mk1"),
                source,
                Array.Empty<StableId>());

            source.Add(later);

            Assert.That(
                instance.AugmentAssignments,
                Is.EqualTo(new[] { original }));
            Assert.Throws<NotSupportedException>(() =>
                ((IList<StableId>)instance.AugmentAssignments).Add(later));
            Assert.Throws<NotSupportedException>(() =>
                ((IList<StableId>)instance.OverclockAssignments).Add(
                    StableId.Parse("overclock-instance.later")));
        }
    }
}
