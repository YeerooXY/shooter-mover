using System.Collections;
using NUnit.Framework;
using ShooterMover.Content.Definitions.Objects;
using ShooterMover.ContentPackages.Environment.VoidHazards;
using ShooterMover.Domain.Common;
using ShooterMover.UnityAdapters.Authoring;
using UnityEngine.TestTools;

namespace ShooterMover.Tests.PlayMode.Environment.VoidHazards
{
    public sealed partial class VoidHazardAuthoring2DTests
    {
        [UnityTest]
        public IEnumerator IgnoredCategoryAndDuplicateContactSubmitNoAuthorityRequest()
        {
            ObjectFamilyDefinitionAsset family = CreateFamily();
            GameplaySceneScope2D scope = CreateScope("IgnoredScope");
            VoidHazardTestPorts ports = CreatePorts("IgnoredPorts");
            VoidHazardAuthoring2D hazard = CreateHazard(
                "IgnoredHazard",
                scope.transform,
                family,
                VoidPlayerResponseKind.Ignore,
                0d,
                "checkpoint.alpha",
                VoidEnemyResponseKind.Ignore,
                VoidProjectileResponseKind.Ignore,
                VoidPropResponseKind.Ignore,
                null,
                ports);
            VoidHazardTarget2D target = CreateTarget(
                "Player",
                VoidHazardTargetCategory.Player,
                false,
                ports);

            VoidHazardContactResult first = hazard.HandleContactEnter(target);
            VoidHazardContactResult duplicate = hazard.HandleContactEnter(target);

            Assert.That(first.Status, Is.EqualTo(VoidHazardContactStatus.IgnoredByPolicy));
            Assert.That(duplicate.Status, Is.EqualTo(VoidHazardContactStatus.DuplicateContactIgnored));
            Assert.That(ports.DamageRequestCount, Is.Zero);
            Assert.That(ports.InstantDeathRequestCount, Is.Zero);
            Assert.That(hazard.ActiveTargetCount, Is.EqualTo(1));
            Assert.That(ports.PresentationCount, Is.EqualTo(2));
            Assert.That(hazard.HandleContactExit(target), Is.True);
            Assert.That(hazard.HandleContactExit(target), Is.True);
            Assert.That(hazard.ActiveTargetCount, Is.Zero);
            yield return null;
        }

        [UnityTest]
        public IEnumerator DamageAndInstantDeathUseSeparateCombatRequests()
        {
            ObjectFamilyDefinitionAsset family = CreateFamily();
            GameplaySceneScope2D scope = CreateScope("CombatScope");
            VoidHazardTestPorts ports = CreatePorts("CombatPorts");
            VoidHazardTarget2D player = CreateTarget(
                "Player",
                VoidHazardTargetCategory.Player,
                false,
                ports);

            VoidHazardAuthoring2D damageHazard = CreateHazard(
                "DamageHazard",
                scope.transform,
                family,
                VoidPlayerResponseKind.Damage,
                12d,
                "checkpoint.alpha",
                VoidEnemyResponseKind.Ignore,
                VoidProjectileResponseKind.Ignore,
                VoidPropResponseKind.Ignore,
                null,
                ports);
            VoidHazardAuthoring2D deathHazard = CreateHazard(
                "DeathHazard",
                scope.transform,
                family,
                VoidPlayerResponseKind.InstantDeath,
                0d,
                "checkpoint.alpha",
                VoidEnemyResponseKind.Ignore,
                VoidProjectileResponseKind.Ignore,
                VoidPropResponseKind.Ignore,
                null,
                ports);

            Assert.That(damageHazard.HandleContactEnter(player).Status,
                Is.EqualTo(VoidHazardContactStatus.Applied));
            Assert.That(deathHazard.HandleContactEnter(player).Status,
                Is.EqualTo(VoidHazardContactStatus.Applied));
            Assert.That(ports.DamageRequestCount, Is.EqualTo(1));
            Assert.That(ports.InstantDeathRequestCount, Is.EqualTo(1));
            Assert.That(ports.LastDamageRequest.Amount, Is.EqualTo(12d));
            Assert.That(
                ports.LastDamageRequest.HazardId,
                Is.Not.EqualTo(ports.LastInstantDeathRequest.HazardId));
            yield return null;
        }

        [UnityTest]
        public IEnumerator RespawnUsesCheckpointAndMissingCheckpointFailsClosed()
        {
            ObjectFamilyDefinitionAsset family = CreateFamily();
            GameplaySceneScope2D scope = CreateScope("RespawnScope");
            VoidHazardTestPorts ports = CreatePorts("RespawnPorts");
            VoidHazardTarget2D player = CreateTarget(
                "Player",
                VoidHazardTargetCategory.Player,
                false,
                ports);
            VoidHazardAuthoring2D hazard = CreateHazard(
                "RespawnHazard",
                scope.transform,
                family,
                VoidPlayerResponseKind.Respawn,
                0d,
                "checkpoint.alpha",
                VoidEnemyResponseKind.Ignore,
                VoidProjectileResponseKind.Ignore,
                VoidPropResponseKind.Ignore,
                ports,
                ports);

            Assert.That(hazard.HandleContactEnter(player).Status,
                Is.EqualTo(VoidHazardContactStatus.Applied));
            Assert.That(ports.RespawnRequestCount, Is.EqualTo(1));
            Assert.That(
                ports.LastRespawnRequest.CheckpointId,
                Is.EqualTo(StableId.Parse("checkpoint.alpha")));
            Assert.That(
                ports.LastRespawnRequest.Destination.DestinationId,
                Is.EqualTo(StableId.Parse("destination.alpha")));

            hazard.HandleContactExit(player);
            ports.ResolveCheckpoint = false;
            Assert.That(hazard.HandleContactEnter(player).Status,
                Is.EqualTo(VoidHazardContactStatus.MissingCheckpoint));
            Assert.That(ports.RespawnRequestCount, Is.EqualTo(1));

            VoidHazardAuthoring2D invalid = CreateUnactivatedHazard(
                "InvalidRespawnHazard",
                scope.transform,
                family,
                VoidPlayerResponseKind.Respawn,
                0d,
                "checkpoint.alpha",
                null,
                ports);
            Assert.That(invalid.TryActivate(), Is.False);
            Assert.That(
                invalid.LastValidationResult.Status,
                Is.EqualTo(VoidHazardValidationStatus.MissingCheckpointPort));
            Assert.That(invalid.GetComponent<UnityEngine.Collider2D>().enabled, Is.False);
            yield return null;
        }
    }
}
