#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using ShooterMover.Contracts.Combat;
using ShooterMover.Contracts.Content;
using ShooterMover.Domain.Combat;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Enemies;
using ShooterMover.UnityAdapters.Enemies;
using UnityEngine;

namespace ShooterMover.Tests.PlayMode.Enemies
{
    public sealed class MobileBlasterDroidPackageTests
    {
        private const string PackageRoot =
            "Assets/ShooterMover/ContentPackages/Enemies/MobileBlasterDroid/";
        private const string DefinitionSourcePath =
            PackageRoot + "MobileBlasterDroidDefinition.cs";
        private const string RuntimeSourcePath =
            PackageRoot + "MobileBlasterDroidRuntime2D.cs";
        private const string PresentationSourcePath =
            PackageRoot + "MobileBlasterDroidTemporaryPresentation.cs";
        private const string DefinitionAssetPath =
            PackageRoot + "MobileBlasterDroidDefinition.asset";
        private const string PrefabPath =
            PackageRoot + "MobileBlasterDroid.prefab";
        private const string PackageNotePath =
            PackageRoot + "MOBILE_BLASTER_DROID_PACKAGE.md";
        private const string AcceptedProjectilePrefabGuid =
            "aa50b87e561c43e69f37921fd937a8fd";

        private static readonly StableId EnemyId =
            StableId.Parse("actor.mobile-blaster-droid-test");
        private static readonly StableId PlayerId =
            StableId.Parse("actor.player-one");
        private static readonly StableId TestWeaponSourceId =
            StableId.Parse("actor.test-weapon");
        private static readonly StableId ExpectedEnemyPackageId =
            StableId.Parse("enemy.mobile-blaster-droid");
        private static readonly StableId ExpectedWeaponId =
            StableId.Parse("weapon.blaster-machine-gun");
        private static readonly StableId ExpectedOperationKindId =
            StableId.Parse("operation-kind.bounded-projectile-2d");

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
        public void Movement_UsesDeterministicTowardAwayAndPreferredBandPositioning()
        {
            DroidFixture fixture = CreateFixture(new Vector2(10f, 0f));

            EnemyActor2DFixedStepResult approach = Execute(fixture, 0.02d);
            Assert.That(approach.Applied, Is.True);
            Assert.That(approach.AppliedVelocityX, Is.EqualTo(2.5d).Within(0.000001d));
            Assert.That(approach.AppliedVelocityY, Is.Zero.Within(0.000001d));

            fixture.Player.transform.position = new Vector3(1f, 0f, 0f);
            EnemyActor2DFixedStepResult retreat = Execute(fixture, 0.02d);
            Assert.That(retreat.Applied, Is.True);
            Assert.That(retreat.AppliedVelocityX, Is.EqualTo(-2.5d).Within(0.000001d));
            Assert.That(retreat.AppliedVelocityY, Is.Zero.Within(0.000001d));

            fixture.Player.transform.position = new Vector3(5f, 0f, 0f);
            EnemyActor2DFixedStepResult hold = Execute(fixture, 0.02d);
            Assert.That(hold.Applied, Is.True);
            Assert.That(hold.AppliedVelocityX, Is.Zero.Within(0.000001d));
            Assert.That(hold.AppliedVelocityY, Is.Zero.Within(0.000001d));
            Assert.That(fixture.EnemyBody.linearVelocity, Is.EqualTo(Vector2.zero));

            TestContext.WriteLine(
                "movement approach=2.5 retreat=-2.5 preferred-distance=5 tolerance=0.5 strafing=false predictive=false");
        }

        [Test]
        public void Cadence_WindUpDirectionAndRecoveryAreReadableAndBounded()
        {
            DroidFixture fixture = CreateFixture(new Vector2(10f, 2f));

            Execute(fixture, 0.02d);
            Assert.That(GetPhase(fixture), Is.EqualTo("WindUp"));
            Assert.That(GetProperty<long>(fixture.Package, "FireAttemptCount"), Is.Zero);
            Assert.That(
                GetProperty<bool>(fixture.Presentation, "IsWindUpVisible"),
                Is.True);
            Vector2 lockedDirection = GetProperty<Vector2>(fixture.Package, "LockedDirection");
            Assert.That(lockedDirection.sqrMagnitude, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(lockedDirection.x, Is.GreaterThan(0f));

            Execute(fixture, 0.29d);
            Assert.That(GetPhase(fixture), Is.EqualTo("WindUp"));
            Assert.That(GetProperty<long>(fixture.Package, "FireAttemptCount"), Is.Zero);

            Execute(fixture, 0.02d);
            Assert.That(GetPhase(fixture), Is.EqualTo("Recovery"));
            Assert.That(GetProperty<long>(fixture.Package, "FireAttemptCount"), Is.EqualTo(1L));
            Assert.That(GetProperty<long>(fixture.Package, "SuccessfulShotCount"), Is.EqualTo(1L));
            Assert.That(GetProperty<int>(fixture.Package, "ActiveProjectileCount"), Is.EqualTo(1));
            Assert.That(
                GetProperty<bool>(fixture.Presentation, "IsWindUpVisible"),
                Is.False);

            Execute(fixture, 0.79d);
            Assert.That(GetPhase(fixture), Is.EqualTo("Recovery"));
            Assert.That(GetProperty<long>(fixture.Package, "FireAttemptCount"), Is.EqualTo(1L));

            Execute(fixture, 0.02d);
            Assert.That(GetPhase(fixture), Is.EqualTo("Ready"));
            Execute(fixture, 0.01d);
            Assert.That(GetPhase(fixture), Is.EqualTo("WindUp"));
            Assert.That(GetProperty<long>(fixture.Package, "FireAttemptCount"), Is.EqualTo(1L));

            TestContext.WriteLine(
                "readability wind-up=0.3 recovery=0.8 direction-locked=true line-cue=true recovery-shape-cue=true max-one-attempt-per-step=true");
        }

        [Test]
        public void ProjectileReuse_ExecutesTheAcceptedNormalBlasterPlanAndSharedShell()
        {
            DroidFixture fixture = CreateFixture(new Vector2(12f, 0f));
            AdvanceToFirstShot(fixture);

            WeaponFireExecutionPlan plan =
                GetProperty<WeaponFireExecutionPlan>(fixture.Package, "LastExecutionPlan");
            Assert.That(plan, Is.Not.Null);
            Assert.That(plan.FaultCount, Is.Zero);
            Assert.That(plan.WeaponId, Is.EqualTo(ExpectedWeaponId));
            Assert.That(plan.Input.IsEmpowered, Is.False);
            Assert.That(plan.OperationCount, Is.EqualTo(1));
            Assert.That(
                plan.GetOperation(0).OperationKindId,
                Is.EqualTo(ExpectedOperationKindId));

            object operation = plan.GetOperation(0).Operation;
            Assert.That(
                operation.GetType().FullName,
                Is.EqualTo(
                    "ShooterMover.ContentPackages.Weapons.Shared.Runtime.BoundedProjectileExecutionOperation"));
            Assert.That(GetProperty<double>(operation, "ProjectileSpeed"), Is.EqualTo(20d));
            Assert.That(
                GetProperty<double>(operation, "ProjectileLifetimeSeconds"),
                Is.EqualTo(2d));
            Assert.That(GetProperty<double>(operation, "ProjectileRadius"), Is.EqualTo(0.1d));
            Assert.That(
                GetProperty<CombatChannel>(operation, "Channel"),
                Is.EqualTo(CombatChannel.Kinetic));

            object spawned = GetProperty<object>(fixture.Package, "LastSpawnedProjectile");
            Assert.That(spawned, Is.Not.Null);
            Assert.That(spawned.GetType(), Is.EqualTo(RuntimeTypes.BoundedProjectile));
            Assert.That(GetProperty<bool>(spawned, "IsInitialized"), Is.True);

            TestContext.WriteLine(
                "reuse weapon=weapon.blaster-machine-gun mode=normal operation=bounded-projectile-2d speed=20 lifetime=2 radius=0.1 channel=kinetic");
        }

        [Test]
        public void TargetLoss_CancelsWindUpAndRequiresAFreshReadableCycle()
        {
            DroidFixture fixture = CreateFixture(new Vector2(10f, 0f));
            Execute(fixture, 0.02d);
            Assert.That(GetPhase(fixture), Is.EqualTo("WindUp"));

            fixture.PlayerTarget.enabled = false;
            EnemyActor2DFixedStepResult lost = Execute(fixture, 0.5d);

            Assert.That(
                lost.Status,
                Is.EqualTo(EnemyActor2DFixedStepStatus.TargetUnavailable));
            Assert.That(GetPhase(fixture), Is.EqualTo("Ready"));
            Assert.That(GetProperty<bool>(fixture.Package, "HasLockedDirection"), Is.False);
            Assert.That(GetProperty<long>(fixture.Package, "FireAttemptCount"), Is.Zero);
            Assert.That(GetProperty<int>(fixture.Package, "ActiveProjectileCount"), Is.Zero);
            Assert.That(
                GetProperty<bool>(fixture.Presentation, "IsWindUpVisible"),
                Is.False);

            fixture.PlayerTarget.enabled = true;
            Execute(fixture, 0.01d);
            Assert.That(GetPhase(fixture), Is.EqualTo("WindUp"));
            Assert.That(GetProperty<long>(fixture.Package, "FireAttemptCount"), Is.Zero);
            Execute(fixture, 0.31d);
            Assert.That(GetProperty<long>(fixture.Package, "SuccessfulShotCount"), Is.EqualTo(1L));
        }

        [Test]
        public void Death_CancelsPendingAndAlreadySpawnedProjectileWork()
        {
            DroidFixture fixture = CreateFixture(new Vector2(10f, 0f));
            AdvanceToFirstShot(fixture);
            Assert.That(GetProperty<int>(fixture.Package, "ActiveProjectileCount"), Is.EqualTo(1));

            HitMessage lethalMessage = new HitMessage(
                StableId.Create("event", "en006-lethal-hit"),
                TestWeaponSourceId,
                EnemyId,
                CombatChannel.Kinetic,
                HitResult.Confirmed);
            EnemyTarget2DHitApplication lethal = fixture.EnemyTarget.ApplyHit(
                lethalMessage,
                1000d,
                0L);

            Assert.That(lethal.Status, Is.EqualTo(EnemyTarget2DHitStatus.Applied));
            Assert.That(
                GetProperty<EnemyActorState>(fixture.Package, "CurrentState").IsDestroyed,
                Is.True);
            Assert.That(GetPhase(fixture), Is.EqualTo("Ready"));
            Assert.That(GetProperty<int>(fixture.Package, "ActiveProjectileCount"), Is.Zero);
            Assert.That(GetProperty<bool>(fixture.Package, "HasLockedDirection"), Is.False);

            EnemyActor2DFixedStepResult stopped = Execute(fixture, 0.5d);
            Assert.That(
                stopped.Status,
                Is.EqualTo(EnemyActor2DFixedStepStatus.ActorInactive));
            Assert.That(fixture.EnemyBody.linearVelocity, Is.EqualTo(Vector2.zero));
            Assert.That(GetProperty<long>(fixture.Package, "FireAttemptCount"), Is.EqualTo(1L));
        }

        [Test]
        public void Restart_RestoresHealthMovementAndFreshFireWithoutStaleShots()
        {
            DroidFixture fixture = CreateFixture(new Vector2(10f, 0f));
            AdvanceToFirstShot(fixture);
            HitMessage damageMessage = new HitMessage(
                StableId.Create("event", "en006-restart-damage"),
                TestWeaponSourceId,
                EnemyId,
                CombatChannel.Kinetic,
                HitResult.Confirmed);
            Assert.That(
                fixture.EnemyTarget.ApplyHit(damageMessage, 4d, 1L).Status,
                Is.EqualTo(EnemyTarget2DHitStatus.Applied));
            Assert.That(
                GetProperty<EnemyActorState>(fixture.Package, "CurrentState").Health,
                Is.EqualTo(12d));
            long generation = GetProperty<long>(fixture.Package, "Generation");

            Assert.That((bool)InvokeInstance(fixture.Package, "RestartSession"), Is.True);

            EnemyActorState restarted =
                GetProperty<EnemyActorState>(fixture.Package, "CurrentState");
            Assert.That(restarted.IsActive, Is.True);
            Assert.That(restarted.Health, Is.EqualTo(16d));
            Assert.That(restarted.ProcessedEventIds, Is.Empty);
            Assert.That(GetPhase(fixture), Is.EqualTo("Ready"));
            Assert.That(GetProperty<long>(fixture.Package, "FireAttemptCount"), Is.Zero);
            Assert.That(GetProperty<long>(fixture.Package, "SuccessfulShotCount"), Is.Zero);
            Assert.That(GetProperty<long>(fixture.Package, "DecisionSequence"), Is.Zero);
            Assert.That(GetProperty<int>(fixture.Package, "ActiveProjectileCount"), Is.Zero);
            Assert.That(
                GetProperty<WeaponFireExecutionPlan>(fixture.Package, "LastExecutionPlan"),
                Is.Null);
            Assert.That(
                GetProperty<long>(fixture.Package, "Generation"),
                Is.EqualTo(generation + 1L));
            Assert.That(fixture.EnemyBody.linearVelocity, Is.EqualTo(Vector2.zero));

            Execute(fixture, 0.01d);
            Execute(fixture, 0.31d);
            Assert.That(GetProperty<long>(fixture.Package, "SuccessfulShotCount"), Is.EqualTo(1L));
            Assert.That(GetProperty<int>(fixture.Package, "ActiveProjectileCount"), Is.EqualTo(1));
        }

        [Test]
        public void PackageBoundary_DeclaresExactEnemyCapabilitiesAndNoClonedWeaponLogic()
        {
            DroidFixture fixture = CreateFixture(new Vector2(10f, 0f));
            object descriptor = InvokeInstance(fixture.Definition, "CreatePackageDescriptor");
            ulong capabilities = Convert.ToUInt64(
                GetProperty<object>(descriptor, "Capabilities"));
            ContentReference attack = GetProperty<ContentReference>(
                descriptor,
                "AttackReference");

            Assert.That(
                GetProperty<StableId>(descriptor, "DefinitionId"),
                Is.EqualTo(ExpectedEnemyPackageId));
            Assert.That(
                GetProperty<CombatChannel>(descriptor, "DamageChannel"),
                Is.EqualTo(CombatChannel.Kinetic));
            Assert.That(
                GetProperty<CombatWeightClass>(descriptor, "WeightClass"),
                Is.EqualTo(CombatWeightClass.Standard));
            Assert.That(capabilities, Is.EqualTo(296UL));
            Assert.That(attack.DefinitionId, Is.EqualTo(ExpectedWeaponId));
            Assert.That(attack.ExpectedKind, Is.EqualTo(ContentDefinitionKind.Weapon));

            Assert.That(fixture.Package, Is.InstanceOf<IEnemyActor2DAuthority>());
            Assert.That(fixture.Package, Is.InstanceOf<IEnemyActor2DDecisionSource>());
            Assert.That(fixture.Package.GetComponent<EnemyActor2DAdapter>(), Is.Not.Null);
            Assert.That(fixture.Package.GetComponent<EnemyTarget2DAdapter>(), Is.Not.Null);
            Assert.That(fixture.Package.GetComponent<EnemyContact2DAdapter>(), Is.Not.Null);

            string definitionSource = ReadProjectFile(DefinitionSourcePath);
            string runtimeSource = ReadProjectFile(RuntimeSourcePath);
            string presentationSource = ReadProjectFile(PresentationSourcePath);
            string prefab = ReadProjectFile(PrefabPath);
            string packageNote = ReadProjectFile(PackageNotePath);
            string allSource = definitionSource + "\n" + runtimeSource + "\n" + presentationSource;

            string[] forbiddenTokens =
            {
                "NavMesh",
                "UnityEngine.Physics.",
                "Physics.Raycast",
                "RaycastHit",
                "GameObject.Find",
                "FindObject",
                "FindWithTag",
                "Camera.main",
                "Random.",
                "AddForce(",
                "MovePosition(",
                "predictive aim",
                "class MobileBlasterProjectile",
                "class BlasterProjectile",
            };
            foreach (string token in forbiddenTokens)
            {
                Assert.That(allSource, Does.Not.Contain(token), "Forbidden token: " + token);
            }

            Assert.That(
                Regex.IsMatch(allSource, @"\bCollider\s+[A-Za-z_]"),
                Is.False,
                "Forbidden 3D Collider type declaration.");
            Assert.That(runtimeSource, Does.Contain("BlasterMachineGunPackage.CreateBehaviorModule"));
            Assert.That(runtimeSource, Does.Contain("BlasterMachineGunPackage.GetNormalRuntimeProfile"));
            Assert.That(runtimeSource, Does.Contain("ProjectileExecutionPlanAdapter"));
            Assert.That(runtimeSource, Does.Contain("WeaponMount2DAdapter"));
            Assert.That(runtimeSource, Does.Contain("EnemyActorStepper.Step"));
            Assert.That(
                Regex.Matches(runtimeSource, "new ProjectileExecutionPlanAdapter").Count,
                Is.EqualTo(1));
            Assert.That(prefab, Does.Contain(AcceptedProjectilePrefabGuid));
            Assert.That(prefab, Does.Contain("MobileBlasterDroidDefinition"));
            Assert.That(packageNote, Does.Contain("does not declare projectile speed"));
            Assert.That(packageNote, Does.Contain("growing directional wind-up"));
            Assert.That(File.Exists(ProjectPath(DefinitionAssetPath)), Is.True);
            Assert.That(File.Exists(ProjectPath(PrefabPath)), Is.True);

            TestContext.WriteLine(
                "dependency-trace EN-002->EN-003->WP-003 Blaster plan->CB-009 mount->WP-002 shared projectile");
            TestContext.WriteLine(
                "manual-readability compare-with=enemy.blaster-turret cues=range-correction,growing-direction-line,compressed-recovery color-independent=true capture=pending-human");
        }

        private DroidFixture CreateFixture(Vector2 playerPosition)
        {
            GameObject player = Track(new GameObject("EN-006 Player Target"));
            player.transform.position = playerPosition;
            Rigidbody2D playerBody = player.AddComponent<Rigidbody2D>();
            playerBody.bodyType = RigidbodyType2D.Kinematic;
            playerBody.gravityScale = 0f;
            CircleCollider2D playerCollider = player.AddComponent<CircleCollider2D>();
            playerCollider.radius = 0.5f;
            EnemyTarget2DAdapter playerTarget = player.AddComponent<EnemyTarget2DAdapter>();
            playerTarget.Configure(PlayerId, player.transform, playerCollider);

            GameObject projectilePrefab = Track(new GameObject("Accepted Blaster Projectile Prefab"));
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
                0.3d,
                0.8d,
                0.65d,
                4,
                0.55d,
                4d,
                0.2d);
            Track(definition);

            GameObject enemy = Track(new GameObject("Mobile Blaster Droid Test"));
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

            return new DroidFixture(
                player,
                playerTarget,
                playerCollider,
                enemy,
                package,
                definition,
                enemy.GetComponent<Rigidbody2D>(),
                enemy.GetComponent<EnemyTarget2DAdapter>(),
                (Component)GetProperty<object>(package, "Presentation"));
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

        private static void AdvanceToFirstShot(DroidFixture fixture)
        {
            Execute(fixture, 0.01d);
            Execute(fixture, 0.31d);
            Assert.That(GetProperty<long>(fixture.Package, "SuccessfulShotCount"), Is.EqualTo(1L));
        }

        private static string GetPhase(DroidFixture fixture)
        {
            object phase = GetProperty<object>(fixture.Package, "FirePhase");
            return phase.ToString();
        }

        private T Track<T>(T value)
            where T : UnityEngine.Object
        {
            createdObjects.Add(value);
            return value;
        }

        private static T GetProperty<T>(object instance, string propertyName)
        {
            Assert.That(instance, Is.Not.Null, propertyName);
            PropertyInfo property = instance.GetType().GetProperty(
                propertyName,
                BindingFlags.Public | BindingFlags.Instance);
            Assert.That(property, Is.Not.Null, instance.GetType().FullName + "." + propertyName);
            return (T)property.GetValue(instance, null);
        }

        private static object InvokeStatic(Type type, string methodName, params object[] arguments)
        {
            MethodInfo method = type.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .SingleOrDefault(
                    candidate => candidate.Name == methodName
                        && candidate.GetParameters().Length == arguments.Length);
            Assert.That(method, Is.Not.Null, type.FullName + "." + methodName);
            return Invoke(method, null, arguments);
        }

        private static object InvokeInstance(
            object instance,
            string methodName,
            params object[] arguments)
        {
            Assert.That(instance, Is.Not.Null, methodName);
            MethodInfo method = instance.GetType()
                .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .SingleOrDefault(
                    candidate => candidate.Name == methodName
                        && candidate.GetParameters().Length == arguments.Length);
            Assert.That(method, Is.Not.Null, instance.GetType().FullName + "." + methodName);
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

        private static string ReadProjectFile(string projectPath)
        {
            return File.ReadAllText(ProjectPath(projectPath));
        }

        private static string ProjectPath(string projectPath)
        {
            string root = Directory.GetParent(Application.dataPath).FullName;
            string localPath = projectPath.Replace('/', Path.DirectorySeparatorChar);
            return Path.Combine(root, localPath);
        }

        private sealed class DroidFixture
        {
            public DroidFixture(
                GameObject player,
                EnemyTarget2DAdapter playerTarget,
                Collider2D playerCollider,
                GameObject enemy,
                Component package,
                ScriptableObject definition,
                Rigidbody2D enemyBody,
                EnemyTarget2DAdapter enemyTarget,
                Component presentation)
            {
                Player = player;
                PlayerTarget = playerTarget;
                PlayerCollider = playerCollider;
                Enemy = enemy;
                Package = package;
                Definition = definition;
                EnemyBody = enemyBody;
                EnemyTarget = enemyTarget;
                Presentation = presentation;
            }

            public GameObject Player { get; }

            public EnemyTarget2DAdapter PlayerTarget { get; }

            public Collider2D PlayerCollider { get; }

            public GameObject Enemy { get; }

            public Component Package { get; }

            public ScriptableObject Definition { get; }

            public Rigidbody2D EnemyBody { get; }

            public EnemyTarget2DAdapter EnemyTarget { get; }

            public Component Presentation { get; }
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
