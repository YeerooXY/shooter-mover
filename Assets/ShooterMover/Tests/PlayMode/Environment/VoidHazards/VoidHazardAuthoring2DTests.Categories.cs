using System.Collections;
using NUnit.Framework;
using ShooterMover.Content.Definitions.Objects;
using ShooterMover.ContentPackages.Environment.VoidHazards;
using ShooterMover.Domain.Common;
using ShooterMover.UnityAdapters.Authoring;
using UnityEngine;
using UnityEngine.TestTools;

namespace ShooterMover.Tests.PlayMode.Environment.VoidHazards
{
    public sealed partial class VoidHazardAuthoring2DTests
    {
        [UnityTest]
        public IEnumerator ProjectileEnemyAndPropPoliciesAreIndependentAndDuplicateSafe()
        {
            ObjectFamilyDefinitionAsset family = CreateFamily();
            GameplaySceneScope2D scope = CreateScope("CategoryScope");
            VoidHazardTestPorts ports = CreatePorts("CategoryPorts");
            VoidHazardAuthoring2D hazard = CreateHazard(
                "CategoryHazard",
                scope.transform,
                family,
                VoidPlayerResponseKind.Ignore,
                0d,
                "checkpoint.alpha",
                VoidEnemyResponseKind.RequestFall,
                VoidProjectileResponseKind.RemoveProjectile,
                VoidPropResponseKind.KeepSupported,
                null,
                ports);
            VoidHazardTarget2D enemy = CreateTarget(
                "Enemy", VoidHazardTargetCategory.Enemy, false, ports);
            VoidHazardTarget2D projectile = CreateTarget(
                "Projectile", VoidHazardTargetCategory.Projectile, false, ports);
            VoidHazardTarget2D supportedProp = CreateTarget(
                "SupportedProp", VoidHazardTargetCategory.Prop, true, ports);
            VoidHazardTarget2D unsupportedProp = CreateTarget(
                "UnsupportedProp", VoidHazardTargetCategory.Prop, false, ports);

            Assert.That(hazard.HandleContactEnter(enemy).Status,
                Is.EqualTo(VoidHazardContactStatus.Applied));
            Assert.That(hazard.HandleContactEnter(projectile).Status,
                Is.EqualTo(VoidHazardContactStatus.Applied));
            Assert.That(hazard.HandleContactEnter(projectile).Status,
                Is.EqualTo(VoidHazardContactStatus.DuplicateContactIgnored));
            Assert.That(hazard.HandleContactEnter(supportedProp).Status,
                Is.EqualTo(VoidHazardContactStatus.SupportedPropKept));
            Assert.That(hazard.HandleContactEnter(unsupportedProp).Status,
                Is.EqualTo(VoidHazardContactStatus.Applied));

            Assert.That(ports.EnemyFallRequestCount, Is.EqualTo(1));
            Assert.That(ports.ProjectileRemovalRequestCount, Is.EqualTo(1));
            Assert.That(ports.PropRemovalRequestCount, Is.EqualTo(1));
            Assert.That(ports.LastEnemyFallRequest.TargetId, Is.EqualTo(enemy.TargetId));
            Assert.That(ports.LastProjectileRemovalRequest.TargetId,
                Is.EqualTo(projectile.TargetId));
            Assert.That(ports.LastPropRemovalRequest.TargetId,
                Is.EqualTo(unsupportedProp.TargetId));
            yield return null;
        }

        [UnityTest]
        public IEnumerator RestartClearsContactsAndRestoresArbitraryHierarchyPlacement()
        {
            ObjectFamilyDefinitionAsset family = CreateFamily();
            GameplaySceneScope2D scope = CreateScope("RestartScope");
            GameObject levelOne = Track(new GameObject("LevelOne"));
            levelOne.transform.SetParent(scope.transform);
            GameObject levelTwo = Track(new GameObject("LevelTwo"));
            levelTwo.transform.SetParent(levelOne.transform);
            VoidHazardTestPorts ports = CreatePorts("RestartPorts");
            VoidHazardAuthoring2D hazard = CreateHazard(
                "NestedHazard",
                levelTwo.transform,
                family,
                VoidPlayerResponseKind.Ignore,
                0d,
                "checkpoint.alpha",
                VoidEnemyResponseKind.Ignore,
                VoidProjectileResponseKind.RemoveProjectile,
                VoidPropResponseKind.Ignore,
                null,
                ports);
            VoidHazardTarget2D projectile = CreateTarget(
                "Projectile", VoidHazardTargetCategory.Projectile, false, ports);
            StableId placedId = hazard.RestartParticipantId;

            Assert.That(hazard.HandleContactEnter(projectile).IsAccepted, Is.True);
            StableId firstEventId = ports.LastProjectileRemovalRequest.EventId;
            Assert.That(hazard.HandleContactEnter(projectile).Status,
                Is.EqualTo(VoidHazardContactStatus.DuplicateContactIgnored));
            Assert.That(ports.ProjectileRemovalRequestCount, Is.EqualTo(1));
            Assert.That(hazard.ActiveTargetCount, Is.EqualTo(1));
            Assert.That(scope.RegisteredRestartParticipantCount, Is.EqualTo(1));

            hazard.gameObject.name = "RenamedNestedHazard";
            levelTwo.transform.SetParent(scope.transform);
            scope.RunRestart(1L);

            Assert.That(hazard.IsReady, Is.True);
            Assert.That(hazard.AcceptsContacts, Is.True);
            Assert.That(hazard.ActiveTargetCount, Is.Zero);
            Assert.That(hazard.GetComponent<Collider2D>().enabled, Is.True);
            Assert.That(hazard.RestartParticipantId, Is.EqualTo(placedId));
            Assert.That(scope.RegisteredRestartParticipantCount, Is.EqualTo(1));
            Assert.That(hazard.HandleContactEnter(projectile).IsAccepted, Is.True);
            Assert.That(ports.ProjectileRemovalRequestCount, Is.EqualTo(2));
            Assert.That(ports.LastProjectileRemovalRequest.EventId,
                Is.Not.EqualTo(firstEventId));
            yield return null;
        }
    }
}
