#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShooterMover.Contracts.Combat;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Enemies;
using ShooterMover.GameplayEntities.Enemies;
using ShooterMover.UnityAdapters.Enemies;
using UnityEngine;

namespace ShooterMover.Tests.PlayMode.Enemies
{
    public sealed class MobileBlasterDroidLiveDecisionTests
    {
        private const string RuntimeSourcePath =
            "Assets/ShooterMover/ContentPackages/Enemies/MobileBlasterDroid/MobileBlasterDroidRuntime2D.cs";
        private static readonly StableId EnemyId =
            StableId.Parse("actor.mobile-blaster-droid-live-test");
        private static readonly StableId PlayerId =
            StableId.Parse("actor.player-live-test");
        private static readonly StableId TestWeaponSourceId =
            StableId.Parse("actor.test-weapon-live");

        private readonly List<UnityEngine.Object> createdObjects =
            new List<UnityEngine.Object>();

        [TearDown]
        public void TearDown()
        {
            for (int index = createdObjects.Count - 1; index >= 0; index--)
            {
                if (createdObjects[index] != null)
                {
                    UnityEngine.Object.DestroyImmediate(createdObjects[index]);
                }
            }

            createdObjects.Clear();
            UnityEngine.Object[] projectiles =
                Resources.FindObjectsOfTypeAll(RuntimeTypes.BoundedProjectile);
            for (int index = 0; index < projectiles.Length; index++)
            {
                Component component = projectiles[index] as Component;
                if (component != null && component.gameObject != null)
                {
                    UnityEngine.Object.DestroyImmediate(component.gameObject);
                }
            }
        }

        [Test]
        public void LiveAdapter_UsesSharedPerceptionAndDecisionPolicy()
        {
            DroidFixture fixture = CreateFixture(new Vector2(5f, 0f));

            Execute(fixture, 0.01d);

            EnemyDecisionEvaluation evaluation = GetProperty<EnemyDecisionEvaluation>(
                fixture.Package,
                "LastDecisionEvaluation");
            EnemyDebugSnapshot debug = GetProperty<EnemyDebugSnapshot>(
                fixture.Package,
                "LiveDebugSnapshot");
            Assert.That(evaluation, Is.Not.Null);
            Assert.That(debug, Is.SameAs(evaluation.Debug));
            Assert.That(evaluation.Decision.RequestedAttack, Is.Not.Null);

            string source = File.ReadAllText(ProjectPath(RuntimeSourcePath));
            Assert.That(source, Does.Contain("EnemyPerceptionBuilder.Build"));
            Assert.That(source, Does.Contain("EnemyDecisionPolicy.Evaluate"));
        }

        [Test]
        public void OutOfDetectionTarget_IsIgnored()
        {
            DroidFixture fixture = CreateFixture(new Vector2(10f, 0f));

            Execute(fixture, 0.01d);

            EnemyDebugSnapshot debug = GetProperty<EnemyDebugSnapshot>(
                fixture.Package,
                "LiveDebugSnapshot");
            Assert.That(debug.SelectedTargetId, Is.Null);
            Assert.That(debug.SelectedTargetWithinDetectionRange, Is.False);
            Assert.That(debug.RequestedAttack, Is.Null);
            Assert.That(GetPhase(fixture), Is.EqualTo("Ready"));
            Assert.That(fixture.EnemyBody.linearVelocity, Is.EqualTo(Vector2.zero));
        }

        [Test]
        public void VisibleTargetOutsideAttackArc_DoesNotFire()
        {
            DroidFixture fixture = CreateFixture(new Vector2(-5f, 0f));

            Execute(fixture, 0.01d);

            EnemyDebugSnapshot debug = GetProperty<EnemyDebugSnapshot>(
                fixture.Package,
                "LiveDebugSnapshot");
            Assert.That(debug.SelectedTargetId, Is.EqualTo(PlayerId));
            Assert.That(debug.SelectedTargetWithinVisionArc, Is.True);
            Assert.That(debug.SelectedTargetWithinAttackArc, Is.False);
            Assert.That(debug.RequestedAttack, Is.Null);
            Assert.That(GetProperty<long>(fixture.Package, "FireAttemptCount"), Is.Zero);
        }

        [Test]
        public void InArcTarget_ProducesAcceptedAttackIntent()
        {
            DroidFixture fixture = CreateFixture(new Vector2(5f, 0f));

            Execute(fixture, 0.01d);

            EnemyAttackIntent accepted = GetProperty<EnemyAttackIntent>(
                fixture.Package,
                "LastAcceptedAttackIntent");
            Assert.That(accepted, Is.Not.Null);
            Assert.That(accepted.AttackerEntityId, Is.EqualTo(EnemyId));
            Assert.That(accepted.TargetEntityId, Is.EqualTo(PlayerId));
            Assert.That(GetPhase(fixture), Is.EqualTo("WindUp"));

            object[] arguments = { null };
            Assert.That(
                InvokeWithArguments(fixture.Package, "TryDequeueAttackIntent", arguments),
                Is.EqualTo(true));
            Assert.That(arguments[0], Is.SameAs(accepted));
            object[] emptyArguments = { null };
            Assert.That(
                InvokeWithArguments(fixture.Package, "TryDequeueAttackIntent", emptyArguments),
                Is.EqualTo(false));
        }

        [Test]
        public void LineOfSightObstacle_RejectsAttack()
        {
            DroidFixture fixture = CreateFixture(new Vector2(5f, 0f));
            GameObject obstacle = Track(new GameObject("Live Decision LOS Obstacle"));
            obstacle.transform.position = new Vector3(2.5f, 0f, 0f);
            BoxCollider2D obstacleCollider = obstacle.AddComponent<BoxCollider2D>();
            obstacleCollider.size = new Vector2(0.5f, 2f);
            Physics2D.SyncTransforms();

            Execute(fixture, 0.01d);

            EnemyDebugSnapshot debug = GetProperty<EnemyDebugSnapshot>(
                fixture.Package,
                "LiveDebugSnapshot");
            Assert.That(debug.SelectedTargetHasLineOfSight, Is.False);
            Assert.That(debug.RequestedAttack, Is.Null);
            Assert.That(
                GetProperty<EnemyAttackIntent>(fixture.Package, "LastAcceptedAttackIntent"),
                Is.Null);
        }

        [Test]
        public void WindUpPhase_RejectsCadenceReplay()
        {
            DroidFixture fixture = CreateFixture(new Vector2(5f, 0f));
            Execute(fixture, 0.01d);
            EnemyAttackIntent accepted = GetProperty<EnemyAttackIntent>(
                fixture.Package,
                "LastAcceptedAttackIntent");

            Execute(fixture, 0.1d);

            EnemyDebugSnapshot debug = GetProperty<EnemyDebugSnapshot>(
                fixture.Package,
                "LiveDebugSnapshot");
            Assert.That(debug.RequestedAttack, Is.Null);
            Assert.That(debug.BehaviorPhaseId.ToString(), Does.Contain("wind-up"));
            Assert.That(debug.DecisionReasonCode.ToString(), Does.Contain("cadence-not-ready"));
            Assert.That(
                GetProperty<EnemyAttackIntent>(fixture.Package, "LastAcceptedAttackIntent"),
                Is.SameAs(accepted));
            Assert.That(GetProperty<int>(fixture.Package, "PendingAttackIntentCount"), Is.EqualTo(1));
        }

        [Test]
        public void RecoveryCompletion_EvaluatesReadyBeforeTheFollowingWindUp()
        {
            DroidFixture fixture = CreateFixture(new Vector2(5f, 0f));
            Execute(fixture, 0.01d);
            Execute(fixture, 0.31d);
            Assert.That(GetPhase(fixture), Is.EqualTo("Recovery"));

            Execute(fixture, 0.79d);
            Assert.That(GetPhase(fixture), Is.EqualTo("Recovery"));
            Execute(fixture, 0.02d);

            EnemyDebugSnapshot readyDebug = GetProperty<EnemyDebugSnapshot>(
                fixture.Package,
                "LiveDebugSnapshot");
            Assert.That(GetPhase(fixture), Is.EqualTo("Ready"));
            Assert.That(
                readyDebug.BehaviorPhaseId,
                Is.EqualTo(GetProperty<object>(fixture.Definition, "ReadyPhaseId")));
            Assert.That(
                readyDebug.DecisionReasonCode.ToString(),
                Does.Not.Contain("cadence-not-ready"));

            Execute(fixture, 0.01d);
            Assert.That(GetPhase(fixture), Is.EqualTo("WindUp"));
        }

        [Test]
        public void DamageDestructionAndTerminalStop_StillUseActorAuthority()
        {
            DroidFixture fixture = CreateFixture(new Vector2(5f, 0f));
            Execute(fixture, 0.01d);
            long attemptsBeforeDeath = GetProperty<long>(fixture.Package, "FireAttemptCount");

            EnemyTarget2DHitApplication damage = fixture.EnemyTarget.ApplyHit(
                Hit("live-damage"),
                4d,
                0L);
            Assert.That(damage.Status, Is.EqualTo(EnemyTarget2DHitStatus.Applied));
            Assert.That(
                GetProperty<EnemyActorState>(fixture.Package, "CurrentState").Health,
                Is.EqualTo(12d));

            EnemyTarget2DHitApplication lethal = fixture.EnemyTarget.ApplyHit(
                Hit("live-lethal"),
                1000d,
                1L);
            Assert.That(lethal.Status, Is.EqualTo(EnemyTarget2DHitStatus.Applied));
            Assert.That(
                GetProperty<EnemyActorState>(fixture.Package, "CurrentState").IsDestroyed,
                Is.True);
            Assert.That(
                GetProperty<EnemyDestroyedNotification>(fixture.Package, "LastDestroyedNotification"),
                Is.Not.Null);

            EnemyActor2DFixedStepResult stopped = Execute(fixture, 0.5d);
            Assert.That(stopped.Status, Is.EqualTo(EnemyActor2DFixedStepStatus.ActorInactive));
            Assert.That(fixture.EnemyBody.linearVelocity, Is.EqualTo(Vector2.zero));
            Assert.That(
                GetProperty<long>(fixture.Package, "FireAttemptCount"),
                Is.EqualTo(attemptsBeforeDeath));
            Assert.That(GetProperty<int>(fixture.Package, "ActiveProjectileCount"), Is.Zero);
        }

        [Test]
        public void Restart_RestoresConfiguredStateAndFreshDecisionCadence()
        {
            DroidFixture fixture = CreateFixture(new Vector2(5f, 0f));
            fixture.EnemyTarget.ApplyHit(Hit("restart-lethal"), 1000d, 0L);
            long generation = GetProperty<long>(fixture.Package, "Generation");

            Assert.That((bool)InvokeInstance(fixture.Package, "RestartSession"), Is.True);

            EnemyActorState restarted =
                GetProperty<EnemyActorState>(fixture.Package, "CurrentState");
            Assert.That(restarted.IsActive, Is.True);
            Assert.That(restarted.Health, Is.EqualTo(16d));
            Assert.That(
                GetProperty<long>(fixture.Package, "Generation"),
                Is.EqualTo(generation + 1L));
            Assert.That(GetPhase(fixture), Is.EqualTo("Ready"));
            Assert.That(
                GetProperty<EnemyDestroyedNotification>(fixture.Package, "LastDestroyedNotification"),
                Is.Null);
            Assert.That(GetProperty<bool>(fixture.Package, "BlocksRoomClear"), Is.True);

            Execute(fixture, 0.01d);
            Assert.That(
                GetProperty<EnemyAttackIntent>(fixture.Package, "LastAcceptedAttackIntent"),
                Is.Not.Null);
        }

        [Test]
        public void RoomClearProjection_StopsBlockingAfterDestruction()
        {
            DroidFixture fixture = CreateFixture(new Vector2(5f, 0f));
            Assert.That(GetProperty<bool>(fixture.Package, "BlocksRoomClear"), Is.True);

            fixture.EnemyTarget.ApplyHit(Hit("room-clear-lethal"), 1000d, 0L);

            EnemyRuntimeProjection projection = GetProperty<EnemyRuntimeProjection>(
                fixture.Package,
                "CurrentRuntimeProjection");
            Assert.That(
                projection.Definition.RoomClearRole,
                Is.EqualTo(EnemyRoomClearRole.RequiredEnemy));
            Assert.That(projection.BlocksRoomClear, Is.False);
            Assert.That(GetProperty<bool>(fixture.Package, "BlocksRoomClear"), Is.False);
        }

        [Test]
        public void DebugSnapshot_IsTheExactEvaluatedDecisionSnapshot()
        {
            DroidFixture fixture = CreateFixture(new Vector2(5f, 0f));

            Execute(fixture, 0.01d);

            EnemyDecisionEvaluation evaluation = GetProperty<EnemyDecisionEvaluation>(
                fixture.Package,
                "LastDecisionEvaluation");
            EnemyDebugSnapshot debug = GetProperty<EnemyDebugSnapshot>(
                fixture.Package,
                "LiveDebugSnapshot");
            Assert.That(debug, Is.SameAs(evaluation.Debug));
            Assert.That(debug.SelectedTargetId, Is.EqualTo(evaluation.Decision.SelectedTargetId));
            Assert.That(debug.RequestedAttack, Is.SameAs(evaluation.Decision.RequestedAttack));
            Assert.That(debug.DesiredMovement, Is.EqualTo(evaluation.Decision.DesiredMovement));
            Assert.That(debug.DesiredFacing, Is.EqualTo(evaluation.Decision.DesiredFacing));
            Assert.That(debug.DecisionReasonCode, Is.EqualTo(evaluation.Decision.ReasonCode));
        }

        private DroidFixture CreateFixture(Vector2 playerPosition)
        {
            GameObject player = Track(new GameObject("Live Decision Player Target"));
            player.transform.position = playerPosition;
            Rigidbody2D playerBody = player.AddComponent<Rigidbody2D>();
            playerBody.bodyType = RigidbodyType2D.Kinematic;
            playerBody.gravityScale = 0f;
            CircleCollider2D playerCollider = player.AddComponent<CircleCollider2D>();
            playerCollider.radius = 0.5f;
            EnemyTarget2DAdapter playerTarget = player.AddComponent<EnemyTarget2DAdapter>();
            playerTarget.Configure(PlayerId, player.transform, playerCollider);

            GameObject projectilePrefab = Track(new GameObject("Live Decision Projectile Prefab"));
            projectilePrefab.SetActive(false);
            Rigidbody2D projectileBody = projectilePrefab.AddComponent<Rigidbody2D>();
            projectileBody.gravityScale = 0f;
            CircleCollider2D projectileCollider = projectilePrefab.AddComponent<CircleCollider2D>();
            projectileCollider.isTrigger = true;
            Component projectile = projectilePrefab.AddComponent(RuntimeTypes.BoundedProjectile);

            ScriptableObject definition = (ScriptableObject)InvokeStatic(
                RuntimeTypes.Definition,
                "CreateRuntime",
                16d,
                2.5d,
                5d,
                0.5d,
                8d,
                360d,
                90d,
                0d,
                6d,
                0.3d,
                0.8d,
                0.65d,
                4,
                0.55d,
                4d,
                0.2d);
            Track(definition);

            GameObject enemy = Track(new GameObject("Mobile Blaster Droid Live Decision Test"));
            Component package = enemy.AddComponent(RuntimeTypes.Runtime);
            InvokeInstance(
                package,
                "ConfigureSession",
                definition,
                EnemyId,
                playerTarget,
                new Collider2D[] { playerCollider },
                PlayerId,
                CombatWeightClass.Standard,
                projectile);
            Physics2D.SyncTransforms();

            return new DroidFixture(
                player,
                package,
                definition,
                enemy.GetComponent<Rigidbody2D>(),
                enemy.GetComponent<EnemyTarget2DAdapter>());
        }

        private static EnemyActor2DFixedStepResult Execute(
            DroidFixture fixture,
            double deltaTimeSeconds)
        {
            return (EnemyActor2DFixedStepResult)InvokeInstance(
                fixture.Package,
                "ExecuteFixedStep",
                deltaTimeSeconds);
        }

        private static string GetPhase(DroidFixture fixture)
        {
            return GetProperty<object>(fixture.Package, "FirePhase").ToString();
        }

        private static HitMessage Hit(string suffix)
        {
            return new HitMessage(
                StableId.Create("event", suffix),
                TestWeaponSourceId,
                EnemyId,
                CombatChannel.Kinetic,
                HitResult.Confirmed);
        }

        private T Track<T>(T value)
            where T : UnityEngine.Object
        {
            createdObjects.Add(value);
            return value;
        }

        private static T GetProperty<T>(object instance, string propertyName)
        {
            PropertyInfo property = instance.GetType().GetProperty(
                propertyName,
                BindingFlags.Public | BindingFlags.Instance);
            Assert.That(property, Is.Not.Null, instance.GetType().FullName + "." + propertyName);
            return (T)property.GetValue(instance, null);
        }

        private static object InvokeStatic(Type type, string methodName, params object[] arguments)
        {
            MethodInfo method = type.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Single(candidate => candidate.Name == methodName
                    && candidate.GetParameters().Length == arguments.Length);
            return Invoke(method, null, arguments);
        }

        private static object InvokeInstance(
            object instance,
            string methodName,
            params object[] arguments)
        {
            MethodInfo method = instance.GetType()
                .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Single(candidate => candidate.Name == methodName
                    && candidate.GetParameters().Length == arguments.Length);
            return Invoke(method, instance, arguments);
        }

        private static object InvokeWithArguments(
            object instance,
            string methodName,
            object[] arguments)
        {
            MethodInfo method = instance.GetType()
                .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Single(candidate => candidate.Name == methodName
                    && candidate.GetParameters().Length == arguments.Length);
            return Invoke(method, instance, arguments);
        }

        private static object Invoke(MethodInfo method, object instance, object[] arguments)
        {
            try
            {
                return method.Invoke(instance, arguments);
            }
            catch (TargetInvocationException exception)
            {
                if (exception.InnerException != null)
                {
                    throw exception.InnerException;
                }

                throw;
            }
        }

        private static string ProjectPath(string projectPath)
        {
            string root = Directory.GetParent(UnityEngine.Application.dataPath).FullName;
            return Path.Combine(root, projectPath.Replace('/', Path.DirectorySeparatorChar));
        }

        private sealed class DroidFixture
        {
            public DroidFixture(
                GameObject player,
                Component package,
                ScriptableObject definition,
                Rigidbody2D enemyBody,
                EnemyTarget2DAdapter enemyTarget)
            {
                Player = player;
                Package = package;
                Definition = definition;
                EnemyBody = enemyBody;
                EnemyTarget = enemyTarget;
            }

            public GameObject Player { get; }
            public Component Package { get; }
            public ScriptableObject Definition { get; }
            public Rigidbody2D EnemyBody { get; }
            public EnemyTarget2DAdapter EnemyTarget { get; }
        }

        private static class RuntimeTypes
        {
            public static readonly Type Definition = Type.GetType(
                "ShooterMover.ContentPackages.Enemies.MobileBlasterDroid.MobileBlasterDroidDefinition, Assembly-CSharp",
                true);
            public static readonly Type Runtime = Type.GetType(
                "ShooterMover.ContentPackages.Enemies.MobileBlasterDroid.MobileBlasterDroidRuntime2D, Assembly-CSharp",
                true);
            public static readonly Type BoundedProjectile = Type.GetType(
                "ShooterMover.ContentPackages.Weapons.Shared.Runtime.BoundedProjectile2D, Assembly-CSharp",
                true);
        }
    }
}
#endif
