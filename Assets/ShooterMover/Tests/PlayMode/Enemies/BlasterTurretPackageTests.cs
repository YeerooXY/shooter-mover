#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using NUnit.Framework;
using ShooterMover.ContentPackages.Enemies.BlasterTurret;
using ShooterMover.ContentPackages.Enemies.Stage1;
using ShooterMover.ContentPackages.Weapons.BlasterMachineGun;
using ShooterMover.ContentPackages.Weapons.Shared.Runtime;
using ShooterMover.Contracts.Combat;
using ShooterMover.Contracts.Content;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Enemies;
using ShooterMover.UnityAdapters.Enemies;
using UnityEngine;

namespace ShooterMover.Tests.PlayMode.Enemies
{
    public sealed class BlasterTurretPackageTests
    {
        private readonly List<UnityEngine.Object> cleanup = new List<UnityEngine.Object>();

        [TearDown]
        public void TearDown()
        {
            for (int index = cleanup.Count - 1; index >= 0; index--)
            {
                UnityEngine.Object value = cleanup[index];
                if (value != null)
                {
                    UnityEngine.Object.DestroyImmediate(value);
                }
            }

            cleanup.Clear();
        }

        [Test]
        public void Descriptor_IsExactStationaryImmovableBlasterInput()
        {
            BlasterTurretDefinition definition = CreateDefinition();
            Stage1EnemyPackageDescriptor descriptor = definition.CreatePackageDescriptor();

            Assert.That(descriptor.DefinitionId, Is.EqualTo(Stage1EnemyPackageDescriptor.BlasterTurretId));
            Assert.That(descriptor.Classification, Is.EqualTo(Stage1EnemyPackageClassification.Ordinary));
            Assert.That(descriptor.DamageChannel, Is.EqualTo(CombatChannel.Kinetic));
            Assert.That(descriptor.WeightClass, Is.EqualTo(CombatWeightClass.Immovable));
            Assert.That(descriptor.AttackReference.DefinitionId, Is.EqualTo(BlasterMachineGunPackage.WeaponId));
            Assert.That(descriptor.AttackReference.ExpectedKind, Is.EqualTo(ContentDefinitionKind.Weapon));
            Assert.That(
                descriptor.Capabilities,
                Is.EqualTo(
                    Stage1EnemyCapability.StationaryPositioning
                    | Stage1EnemyCapability.BlasterProjectile
                    | Stage1EnemyCapability.SafeRecoveryWindow
                    | Stage1EnemyCapability.LineOfFireTelegraph));
            Assert.That(
                descriptor.ContentDefinition.References,
                Does.Contain(descriptor.MovementReference));
            Assert.That(
                descriptor.ContentDefinition.References,
                Does.Contain(descriptor.AttackReference));
            Assert.That(
                descriptor.ContentDefinition.References,
                Does.Contain(descriptor.TelegraphReference));
        }

        [Test]
        public void Stationary_FixedStepsRestoreAnchorAndEmitOnlyZeroVelocityDecisions()
        {
            Fixture fixture = CreateFixture();
            Vector2 anchor = fixture.Package.AnchorPosition;

            fixture.Package.transform.position = new Vector3(9f, -4f, 0f);
            fixture.Package.EnemyBody.position = new Vector2(9f, -4f);
            BlasterTurretStepResult first = fixture.Package.ExecuteFixedStep(0.1d);

            Assert.That(first.Status, Is.EqualTo(BlasterTurretStepStatus.Warning));
            Assert.That((Vector2)fixture.Package.transform.position, Is.EqualTo(anchor));
            Assert.That(fixture.Package.EnemyBody.position, Is.EqualTo(anchor));
            Assert.That(fixture.Package.EnemyBody.linearVelocity, Is.EqualTo(Vector2.zero));
            Assert.That(fixture.Package.EnemyBody.constraints, Is.EqualTo(RigidbodyConstraints2D.FreezeAll));

            EnemyActor2DDecision decision;
            Assert.That(
                fixture.Package.StationaryDecisionSource.TryDecide(
                    fixture.Package.Authority.CurrentState,
                    new EnemyTarget2DObservation(fixture.TargetId, 100d, -100d),
                    0.02d,
                    out decision),
                Is.True);
            Assert.That(decision.VelocityX, Is.Zero);
            Assert.That(decision.VelocityY, Is.Zero);

            TestContext.WriteLine(
                "stationary anchor=" + anchor
                + " fixed-steps=" + fixture.Package.FixedStepCount
                + " projected-velocity=(0,0) constraints=FreezeAll");
        }

        [Test]
        public void Cadence_EveryShotHasACompleteWarningStepAndDeterministicRecovery()
        {
            Fixture fixture = CreateFixture();

            BlasterTurretStepResult warningOne = fixture.Package.ExecuteFixedStep(0.2d);
            BlasterTurretStepResult shotOne = fixture.Package.ExecuteFixedStep(0.2d);
            BlasterTurretStepResult recovery = fixture.Package.ExecuteFixedStep(0.15d);
            BlasterTurretStepResult warningTwo = fixture.Package.ExecuteFixedStep(0.15d);
            BlasterTurretStepResult shotTwo = fixture.Package.ExecuteFixedStep(0.2d);

            Assert.That(warningOne.Status, Is.EqualTo(BlasterTurretStepStatus.Warning));
            Assert.That(warningOne.WarningVisible, Is.True);
            Assert.That(warningOne.Plan, Is.Null);
            Assert.That(shotOne.ShotExecuted, Is.True);
            Assert.That(shotOne.ShotSequence, Is.Zero);
            Assert.That(recovery.Status, Is.EqualTo(BlasterTurretStepStatus.Recovery));
            Assert.That(recovery.WarningVisible, Is.False);
            Assert.That(warningTwo.Status, Is.EqualTo(BlasterTurretStepStatus.Warning));
            Assert.That(warningTwo.Plan, Is.Null);
            Assert.That(shotTwo.ShotExecuted, Is.True);
            Assert.That(shotTwo.ShotSequence, Is.EqualTo(1L));

            TestContext.WriteLine(
                "cadence trace=warning,shot-0,recovery,warning,shot-1"
                + " warning-seconds=0.2 recovery-seconds=0.3");
        }

        [Test]
        public void AcceptedBlaster_ShotUsesWp003PlanAndWp002BoundedProjectile()
        {
            Fixture fixture = CreateFixture();
            fixture.Package.ExecuteFixedStep(0.2d);
            BlasterTurretStepResult shot = fixture.Package.ExecuteFixedStep(0.2d);

            Assert.That(shot.ShotExecuted, Is.True);
            Assert.That(shot.Execution, Is.Not.Null);
            Assert.That(shot.Execution.Succeeded, Is.True);
            Assert.That(shot.Plan, Is.Not.Null);
            Assert.That(shot.Plan.WeaponId, Is.EqualTo(BlasterMachineGunPackage.WeaponId));
            Assert.That(shot.Plan.Input.IsEmpowered, Is.False);
            Assert.That(
                shot.Plan.Input.RuntimeProfile,
                Is.EqualTo(BlasterMachineGunPackage.GetNormalRuntimeProfile()));
            Assert.That(shot.Plan.FaultCount, Is.Zero);
            Assert.That(shot.Plan.OperationCount, Is.EqualTo(1));
            Assert.That(
                shot.Plan.GetOperation(0).OperationKindId,
                Is.EqualTo(BlasterMachineGunPackage.OperationKindId));
            Assert.That(
                shot.Plan.GetOperation(0).Operation,
                Is.TypeOf<BoundedProjectileExecutionOperation>());
            BoundedProjectileExecutionOperation operation =
                (BoundedProjectileExecutionOperation)shot.Plan.GetOperation(0).Operation;
            Assert.That(operation.Channel, Is.EqualTo(CombatChannel.Kinetic));
            Assert.That(fixture.Package.ProjectileAdapter.ActiveProjectileCount, Is.EqualTo(1));
            TrackSpawnedProjectile(fixture);
        }

        [Test]
        public void Obstruction_CancelsWarningAndActiveProjectileWithoutStaleRelease()
        {
            Fixture fixture = CreateFixture();
            fixture.Package.ExecuteFixedStep(0.2d);
            fixture.LineOfFire.Clear = false;

            BlasterTurretStepResult blocked = fixture.Package.ExecuteFixedStep(0.2d);
            Assert.That(blocked.Status, Is.EqualTo(BlasterTurretStepStatus.Obstructed));
            Assert.That(blocked.Plan, Is.Null);
            Assert.That(fixture.Package.Cadence.Phase, Is.EqualTo(BlasterTurretCadencePhase.Idle));

            fixture.LineOfFire.Clear = true;
            BlasterTurretStepResult warningAgain = fixture.Package.ExecuteFixedStep(0.2d);
            Assert.That(warningAgain.Status, Is.EqualTo(BlasterTurretStepStatus.Warning));
            Assert.That(warningAgain.Plan, Is.Null);
            BlasterTurretStepResult shot = fixture.Package.ExecuteFixedStep(0.2d);
            Assert.That(shot.ShotExecuted, Is.True);
            TrackSpawnedProjectile(fixture);

            fixture.LineOfFire.Clear = false;
            BoundedProjectile2D active = fixture.Package.ProjectileAdapter.LastSpawnedProjectile;
            BlasterTurretStepResult blockedAfterShot = fixture.Package.ExecuteFixedStep(0.01d);
            Assert.That(blockedAfterShot.Status, Is.EqualTo(BlasterTurretStepStatus.Obstructed));
            Assert.That(fixture.Package.ProjectileAdapter.ActiveProjectileCount, Is.Zero);
            Assert.That(active.CompletionReason, Is.EqualTo(BoundedProjectile2DCompletionReason.Cancelled));
        }

        [Test]
        public void TargetLoss_CancelsPendingWarningAndRequiresAFullNewWarning()
        {
            Fixture fixture = CreateFixture();
            fixture.Package.ExecuteFixedStep(0.2d);
            fixture.TargetSource.Available = false;

            BlasterTurretStepResult lost = fixture.Package.ExecuteFixedStep(0.2d);
            Assert.That(lost.Status, Is.EqualTo(BlasterTurretStepStatus.TargetUnavailable));
            Assert.That(lost.Plan, Is.Null);
            Assert.That(fixture.Package.Presentation.IsWarningVisible, Is.False);

            fixture.TargetSource.Available = true;
            BlasterTurretStepResult reacquired = fixture.Package.ExecuteFixedStep(1d);
            Assert.That(reacquired.Status, Is.EqualTo(BlasterTurretStepStatus.Warning));
            Assert.That(reacquired.Plan, Is.Null, "Reacquisition cannot release a stale warned shot.");
            BlasterTurretStepResult shot = fixture.Package.ExecuteFixedStep(0.2d);
            Assert.That(shot.ShotExecuted, Is.True);
            TrackSpawnedProjectile(fixture);
        }

        [Test]
        public void Death_CancelsWarningAndAnyProjectileInFlight()
        {
            Fixture fixture = CreateFixture();
            fixture.Package.ExecuteFixedStep(0.2d);
            BlasterTurretStepResult shot = fixture.Package.ExecuteFixedStep(0.2d);
            Assert.That(shot.ShotExecuted, Is.True);
            TrackSpawnedProjectile(fixture);
            BoundedProjectile2D projectile = fixture.Package.ProjectileAdapter.LastSpawnedProjectile;

            fixture.Package.Authority.Apply(
                EnemyActorCommand.Damage(
                    0L,
                    StableId.Create("combat-event", "destroy-blaster-turret"),
                    StableId.Create("actor", "test-damage-source"),
                    (int)CombatChannel.Kinetic,
                    fixture.Definition.MaximumHealth));
            BlasterTurretStepResult destroyed = fixture.Package.ExecuteFixedStep(0.02d);

            Assert.That(destroyed.Status, Is.EqualTo(BlasterTurretStepStatus.ActorDestroyed));
            Assert.That(fixture.Package.Authority.CurrentState.IsDestroyed, Is.True);
            Assert.That(fixture.Package.ProjectileAdapter.ActiveProjectileCount, Is.Zero);
            Assert.That(projectile.CompletionReason, Is.EqualTo(BoundedProjectile2DCompletionReason.Cancelled));
            Assert.That(fixture.Package.Presentation.IsWarningVisible, Is.False);
        }

        [Test]
        public void Restart_ClearsShotsCadenceAndIdentityBeforeReplayingDeterministically()
        {
            Fixture fixture = CreateFixture();
            fixture.Package.ExecuteFixedStep(0.2d);
            BlasterTurretStepResult firstShot = fixture.Package.ExecuteFixedStep(0.2d);
            Assert.That(firstShot.ShotExecuted, Is.True);
            TrackSpawnedProjectile(fixture);
            long generation = fixture.Package.Generation;

            Assert.That(fixture.Package.RestartSession(), Is.True);
            Assert.That(fixture.Package.Generation, Is.EqualTo(generation + 1L));
            Assert.That(fixture.Package.FixedStepCount, Is.Zero);
            Assert.That(fixture.Package.Cadence.NextShotSequence, Is.Zero);
            Assert.That(fixture.Package.Cadence.Phase, Is.EqualTo(BlasterTurretCadencePhase.Idle));
            Assert.That(fixture.Package.ProjectileAdapter.ActiveProjectileCount, Is.Zero);
            Assert.That(fixture.Package.StationaryDecisionSource.Sequence, Is.Zero);
            Assert.That(fixture.Package.Authority.CurrentState.IsActive, Is.True);

            BlasterTurretStepResult warning = fixture.Package.ExecuteFixedStep(0.2d);
            BlasterTurretStepResult replayedShot = fixture.Package.ExecuteFixedStep(0.2d);
            Assert.That(warning.Status, Is.EqualTo(BlasterTurretStepStatus.Warning));
            Assert.That(replayedShot.ShotExecuted, Is.True);
            Assert.That(replayedShot.ShotSequence, Is.Zero);
            Assert.That(
                replayedShot.Plan.CombatEventId,
                Is.Not.EqualTo(firstShot.Plan.CombatEventId));
            TrackSpawnedProjectile(fixture);

            TestContext.WriteLine(
                "restart generation=" + fixture.Package.Generation
                + " cadence-reset=true active-projectiles-before-replay=0 replay-shot=0");
        }

        [Test]
        public void WarningPresentation_UsesRailAndRepeatedShapeTicksWithoutColorDependence()
        {
            GameObject owner = Track(new GameObject("WarningPresentation"));
            BlasterTurretPresentation2D presentation =
                owner.AddComponent<BlasterTurretPresentation2D>();
            presentation.Configure(0.08d);
            presentation.ShowWarning(Vector2.zero, new Vector2(6f, 2f));

            Assert.That(presentation.IsWarningVisible, Is.True);
            Assert.That(presentation.UsesColorIndependentPattern, Is.True);
            Assert.That(presentation.PatternTickCount, Is.EqualTo(4));
            Assert.That(presentation.WarningRail.enabled, Is.True);
            Assert.That(presentation.WarningRail.positionCount, Is.EqualTo(2));

            presentation.SetDestroyed(true);
            Assert.That(presentation.IsWarningVisible, Is.False);
            Assert.That(presentation.IsDestroyed, Is.True);

            TestContext.WriteLine(
                "warning-capture rail=1 perpendicular-ticks=4 hue-required=false destroyed-hidden=true");
        }

        [Test]
        public void PhysicsLineOfFire_ObstacleWinsBeforeTargetAndTargetBehindRemainsValid()
        {
            GameObject owner = Track(new GameObject("TurretOwner"));
            BoxCollider2D ownerCollider = owner.AddComponent<BoxCollider2D>();
            owner.transform.position = Vector3.zero;

            GameObject target = Track(new GameObject("TargetBehind"));
            CircleCollider2D targetCollider = target.AddComponent<CircleCollider2D>();
            target.transform.position = new Vector3(-5f, 0f, 0f);

            GameObject obstacle = Track(new GameObject("Obstacle"));
            BoxCollider2D obstacleCollider = obstacle.AddComponent<BoxCollider2D>();
            obstacle.transform.position = new Vector3(-2f, 0f, 0f);
            Physics2D.SyncTransforms();

            BlasterTurretPhysicsLineOfFireSource source =
                new BlasterTurretPhysicsLineOfFireSource();
            Assert.That(
                source.HasClearLine(
                    new Vector2(-0.7f, 0f),
                    target.transform.position,
                    targetCollider,
                    ownerCollider),
                Is.False);

            obstacleCollider.enabled = false;
            Physics2D.SyncTransforms();
            Assert.That(
                source.HasClearLine(
                    new Vector2(-0.7f, 0f),
                    target.transform.position,
                    targetCollider,
                    ownerCollider),
                Is.True,
                "A target behind the initial facing is valid when its 2D line is clear.");
        }

        [Test]
        public void PointBlankTarget_FailsSafeWithoutWarningOrProjectile()
        {
            Fixture fixture = CreateFixture();
            fixture.TargetSource.Position = fixture.Package.AnchorPosition;
            fixture.TargetObject.transform.position = fixture.Package.AnchorPosition;

            BlasterTurretStepResult result = fixture.Package.ExecuteFixedStep(1d);
            Assert.That(result.Status, Is.EqualTo(BlasterTurretStepStatus.PointBlankTarget));
            Assert.That(result.WarningVisible, Is.False);
            Assert.That(result.Plan, Is.Null);
            Assert.That(fixture.Package.ProjectileAdapter.ActiveProjectileCount, Is.Zero);
        }

        private Fixture CreateFixture()
        {
            BlasterTurretDefinition definition = CreateDefinition();
            StableId actorId = StableId.Create("actor", "blaster-turret-test");
            StableId targetId = StableId.Create("actor", "player-test-target");

            GameObject targetObject = Track(new GameObject("BlasterTurretTarget"));
            targetObject.transform.position = new Vector3(8f, 0f, 0f);
            CircleCollider2D targetCollider = targetObject.AddComponent<CircleCollider2D>();
            targetCollider.radius = 0.5f;
            MutableTargetSource targetSource = new MutableTargetSource(
                targetId,
                targetObject.transform.position);

            GameObject projectileObject = Track(new GameObject("BoundedProjectilePrefab"));
            projectileObject.SetActive(false);
            BoundedProjectile2D projectilePrefab =
                projectileObject.AddComponent<BoundedProjectile2D>();

            GameObject turretObject = Track(new GameObject("BlasterTurret"));
            turretObject.transform.position = new Vector3(2f, 1f, 0f);
            BlasterTurretPackage package = turretObject.AddComponent<BlasterTurretPackage>();
            MutableLineOfFireSource lineOfFire = new MutableLineOfFireSource();
            package.Configure(
                definition,
                targetSource,
                targetCollider,
                projectilePrefab,
                actorId,
                targetId,
                CombatWeightClass.Standard,
                lineOfFire);

            return new Fixture(
                definition,
                package,
                targetObject,
                targetSource,
                lineOfFire,
                targetId);
        }

        private BlasterTurretDefinition CreateDefinition()
        {
            BlasterTurretDefinition definition = BlasterTurretDefinition.CreateRuntime(
                30d,
                0.2d,
                0.3d,
                30d,
                0.7d,
                0.07d,
                0.5d,
                0.02d,
                4);
            cleanup.Add(definition);
            return definition;
        }

        private void TrackSpawnedProjectile(Fixture fixture)
        {
            BoundedProjectile2D projectile = fixture.Package.ProjectileAdapter.LastSpawnedProjectile;
            if (projectile != null && !cleanup.Contains(projectile.gameObject))
            {
                cleanup.Add(projectile.gameObject);
            }
        }

        private GameObject Track(GameObject value)
        {
            cleanup.Add(value);
            return value;
        }

        private sealed class MutableTargetSource : IEnemyTarget2DSource
        {
            public MutableTargetSource(StableId targetId, Vector2 position)
            {
                TargetId = targetId;
                Position = position;
                Available = true;
            }

            public StableId TargetId { get; }

            public Vector2 Position { get; set; }

            public bool Available { get; set; }

            public bool TryReadTarget(out EnemyTarget2DObservation target)
            {
                target = Available
                    ? new EnemyTarget2DObservation(TargetId, Position.x, Position.y)
                    : null;
                return target != null;
            }
        }

        private sealed class MutableLineOfFireSource : IBlasterTurretLineOfFireSource
        {
            public bool Clear { get; set; } = true;

            public bool HasClearLine(
                Vector2 origin,
                Vector2 targetPosition,
                Collider2D targetCollider,
                Collider2D ownerCollider)
            {
                return Clear;
            }
        }

        private sealed class Fixture
        {
            public Fixture(
                BlasterTurretDefinition definition,
                BlasterTurretPackage package,
                GameObject targetObject,
                MutableTargetSource targetSource,
                MutableLineOfFireSource lineOfFire,
                StableId targetId)
            {
                Definition = definition;
                Package = package;
                TargetObject = targetObject;
                TargetSource = targetSource;
                LineOfFire = lineOfFire;
                TargetId = targetId;
            }

            public BlasterTurretDefinition Definition { get; }

            public BlasterTurretPackage Package { get; }

            public GameObject TargetObject { get; }

            public MutableTargetSource TargetSource { get; }

            public MutableLineOfFireSource LineOfFire { get; }

            public StableId TargetId { get; }
        }
    }
}
#endif
