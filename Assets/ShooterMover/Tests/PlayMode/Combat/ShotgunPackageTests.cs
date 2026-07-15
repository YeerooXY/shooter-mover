#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShooterMover.Contracts.Combat;
using ShooterMover.Domain.Combat;
using ShooterMover.Domain.Common;
using ShooterMover.UnityAdapters.Combat;
using UnityEngine;
using UnityEngine.TestTools;

namespace ShooterMover.Tests.PlayMode.Combat
{
    public sealed class ShotgunPackageTests
    {
        private const string TuningPath =
            "Assets/ShooterMover/ContentPackages/Weapons/Shotgun/ShotgunTuning.cs";
        private const string ModulePath =
            "Assets/ShooterMover/ContentPackages/Weapons/Shotgun/ShotgunSpreadBehaviorModule.cs";
        private const string HandlerPath =
            "Assets/ShooterMover/ContentPackages/Weapons/Shotgun/ShotgunPellet2DHandler.cs";
        private const string FixturePath =
            "Assets/ShooterMover/ContentPackages/Weapons/Shotgun/SHOTGUN_SPREAD_FIXTURE.json";
        private const string ReadabilityNotePath =
            "Assets/ShooterMover/ContentPackages/Weapons/Shotgun/SHOTGUN_READABILITY_NOTE.md";

        private static readonly StableId SourceId =
            StableId.Parse("actor.wp-004-shotgun-source");
        private static readonly StableId WeaponId =
            StableId.Parse("weapon.shotgun");
        private static readonly StableId MountId =
            StableId.Parse("weapon-mount.wp-004-shotgun");

        private readonly List<GameObject> createdObjects = new List<GameObject>();
        private readonly List<IDisposable> createdAdapters = new List<IDisposable>();

        [TearDown]
        public void TearDown()
        {
            for (int index = createdAdapters.Count - 1; index >= 0; index--)
            {
                createdAdapters[index].Dispose();
            }

            createdAdapters.Clear();

            for (int index = createdObjects.Count - 1; index >= 0; index--)
            {
                if (createdObjects[index] != null)
                {
                    UnityEngine.Object.DestroyImmediate(createdObjects[index]);
                }
            }

            createdObjects.Clear();
        }

        [Test]
        public void PelletOrderAndCount_AreDeterministicLeftToRight()
        {
            WeaponFireExecutionPlan first = BuildPlan(
                false,
                "pellet-order",
                1L,
                1d,
                0d);
            WeaponFireExecutionPlan replay = BuildPlan(
                false,
                "pellet-order",
                1L,
                1d,
                0d);
            double[] expectedOffsets = { -12d, -8d, -4d, 0d, 4d, 8d, 12d };

            Assert.That(first.OperationCount, Is.EqualTo(7));
            Assert.That(replay.OperationCount, Is.EqualTo(first.OperationCount));
            Assert.That(replay.DeterministicIdentity, Is.EqualTo(first.DeterministicIdentity));

            for (int index = 0; index < expectedOffsets.Length; index++)
            {
                object firstPellet = first.GetOperation(index).Operation;
                object replayPellet = replay.GetOperation(index).Operation;

                Assert.That(GetProperty<int>(firstPellet, "PelletIndex"), Is.EqualTo(index));
                Assert.That(GetProperty<int>(firstPellet, "PelletCount"), Is.EqualTo(7));
                Assert.That(
                    GetProperty<double>(firstPellet, "OffsetDegrees"),
                    Is.EqualTo(expectedOffsets[index]).Within(0.000000001d));
                Assert.That(
                    GetProperty<StableId>(replayPellet, "OperationId"),
                    Is.EqualTo(GetProperty<StableId>(firstPellet, "OperationId")));
            }

            Assert.That(
                Enumerable.Range(0, first.OperationCount)
                    .Select(
                        index => GetProperty<StableId>(
                            first.GetOperation(index).Operation,
                            "OperationId"))
                    .Distinct()
                    .Count(),
                Is.EqualTo(7));

            TestContext.WriteLine(
                "spread-fixture pellet-count=7 ordered-offsets=[-12,-8,-4,0,4,8,12] deterministic-replay=true");
        }

        [Test]
        public void SpreadBounds_RejectInvalidCountsAndAngles()
        {
            AssertTuningRejected(2, 24d, 5d, 0.55d, 18d, 0.7d, 0.08d, 0.15d);
            AssertTuningRejected(13, 24d, 5d, 0.55d, 18d, 0.7d, 0.08d, 0.15d);
            AssertTuningRejected(7, 0d, 5d, 0.55d, 18d, 0.7d, 0.08d, 0.15d);
            AssertTuningRejected(7, 61d, 5d, 0.55d, 18d, 0.7d, 0.08d, 0.15d);

            object minimum = CreateTuning(3, 1d, 5d, 0.55d, 18d, 0.7d, 0.08d, 0.15d);
            object maximum = CreateTuning(12, 60d, 5d, 0.55d, 18d, 0.7d, 0.08d, 0.15d);
            Assert.That(GetProperty<int>(minimum, "PelletCount"), Is.EqualTo(3));
            Assert.That(GetProperty<double>(minimum, "SpreadDegrees"), Is.EqualTo(1d));
            Assert.That(GetProperty<int>(maximum, "PelletCount"), Is.EqualTo(12));
            Assert.That(GetProperty<double>(maximum, "SpreadDegrees"), Is.EqualTo(60d));

            TestContext.WriteLine(
                "spread-bounds pellet-count=[3,12] spread-degrees=[1,60] rejected=2,13,0,61");
        }

        [Test]
        public void CloseAim_ProducesFiniteNormalizedSpread()
        {
            WeaponFireExecutionPlan plan = BuildPlan(
                false,
                "close-aim",
                2L,
                0.000000001d,
                0d);

            Assert.That(plan.OperationCount, Is.EqualTo(7));
            for (int index = 0; index < plan.OperationCount; index++)
            {
                object pellet = plan.GetOperation(index).Operation;
                double directionX = GetProperty<double>(pellet, "DirectionX");
                double directionY = GetProperty<double>(pellet, "DirectionY");
                double length = Math.Sqrt(
                    (directionX * directionX) + (directionY * directionY));

                Assert.That(double.IsNaN(directionX), Is.False);
                Assert.That(double.IsInfinity(directionX), Is.False);
                Assert.That(double.IsNaN(directionY), Is.False);
                Assert.That(double.IsInfinity(directionY), Is.False);
                Assert.That(length, Is.EqualTo(1d).Within(0.000000001d));
            }

            object first = plan.GetOperation(0).Operation;
            object center = plan.GetOperation(3).Operation;
            object last = plan.GetOperation(6).Operation;
            Assert.That(GetProperty<double>(first, "DirectionY"), Is.LessThan(0d));
            Assert.That(GetProperty<double>(center, "DirectionX"), Is.EqualTo(1d).Within(0.000000001d));
            Assert.That(GetProperty<double>(center, "DirectionY"), Is.EqualTo(0d).Within(0.000000001d));
            Assert.That(GetProperty<double>(last, "DirectionY"), Is.GreaterThan(0d));

            TestContext.WriteLine(
                "close-aim input-length=1e-9 output-directions=7 finite=true normalized=true");
        }

        [Test]
        public void IndependentPowerFallback_UsesUnlimitedNormalTopology()
        {
            object descriptor = InvokeStatic(
                RuntimeTypes.Definition,
                "CreateDefaultDescriptor");
            object normalFire = GetProperty<object>(descriptor, "NormalFire");
            object empoweredFire = GetProperty<object>(descriptor, "EmpoweredFire");
            WeaponRuntimeProfile normalProfile =
                GetProperty<WeaponRuntimeProfile>(normalFire, "RuntimeProfile");
            WeaponRuntimeProfile empoweredProfile =
                GetProperty<WeaponRuntimeProfile>(empoweredFire, "RuntimeProfile");

            WeaponPowerBankState emptyBank =
                WeaponPowerBankState.FromProfile(empoweredProfile, 0d);
            WeaponPowerFireDecision fallback = WeaponPowerBankPolicy.ResolveFire(
                emptyBank,
                true,
                true);
            WeaponPowerFireDecision empowered = WeaponPowerBankPolicy.ResolveFire(
                WeaponPowerBankState.FullFromProfile(empoweredProfile),
                true,
                true);

            Assert.That(
                fallback.Kind,
                Is.EqualTo(
                    WeaponPowerFireDecisionKind.NormalFallbackPowerUnavailable));
            Assert.That(fallback.FiresNormally, Is.True);
            Assert.That(fallback.SpentUnits, Is.Zero);
            Assert.That(empowered.FiresEmpowered, Is.True);
            Assert.That(GetProperty<bool>(normalFire, "ConsumesConsumableAmmunition"), Is.False);
            Assert.That(GetProperty<bool>(empoweredFire, "ConsumesConsumableAmmunition"), Is.False);
            Assert.That(normalProfile.HasIndependentPowerBank, Is.True);
            Assert.That(empoweredProfile.HasIndependentPowerBank, Is.True);
            Assert.That(
                GetProperty<object>(normalFire, "Topology").ToString(),
                Is.EqualTo(GetProperty<object>(empoweredFire, "Topology").ToString()));
            Assert.That(
                CopyCoefficientKinds(normalFire),
                Is.EqualTo(CopyCoefficientKinds(empoweredFire)));

            WeaponFireExecutionPlan fallbackPlan = BuildPlan(
                fallback.FiresEmpowered,
                "fallback",
                3L,
                1d,
                0d);
            WeaponFireExecutionPlan empoweredPlan = BuildPlan(
                empowered.FiresEmpowered,
                "empowered",
                3L,
                1d,
                0d);
            object normalTuning = GetStaticProperty<object>(
                RuntimeTypes.Definition,
                "NormalTuning");
            object empoweredTuning = GetStaticProperty<object>(
                RuntimeTypes.Definition,
                "EmpoweredTuning");
            double normalDamage = GetProperty<double>(
                normalTuning,
                "Damage");
            double empoweredDamage = GetProperty<double>(
                empoweredTuning,
                "Damage");

            Assert.That(
                GetProperty<double>(
                    fallbackPlan.GetOperation(0).Operation,
                    "Damage"),
                Is.EqualTo(normalDamage));
            Assert.That(
                GetProperty<double>(
                    empoweredPlan.GetOperation(0).Operation,
                    "Damage"),
                Is.EqualTo(empoweredDamage));
            Assert.That(fallbackPlan.OperationCount, Is.EqualTo(empoweredPlan.OperationCount));

            TestContext.WriteLine(
                "power-fallback empty-bank=normal spent=0 normal-ammo=unlimited independent-bank=true topology=unchanged");
        }

        [Test]
        public void RepeatedPelletCollision_ProducesOneUniqueHitPerPellet()
        {
            Collider2D target = CreateTarget("WP-004 Shared Pellet Target");
            StableId targetId = StableId.Parse("enemy.wp-004-shared-pellet-target");
            CombatHit2DAdapter hitAdapter = new CombatHit2DAdapter(SourceId);
            Assert.That(
                hitAdapter.RegisterTarget(target, targetId),
                Is.EqualTo(CombatHit2DTargetRegistrationStatus.Registered));

            object handler = CreateHandler(
                hitAdapter,
                false,
                out Component ignoredTemplate);
            WeaponMount2DAdapter mount = CreateMount(handler);
            WeaponMount2DExecutionResult result = mount.ExecutePlan(
                BuildPlan(false, "duplicate-hit", 4L, 1d, 0d));
            Assert.That(result.Succeeded, Is.True);

            Component[] projectiles = ((Array)Invoke(handler, "CopyActiveProjectiles"))
                .Cast<Component>()
                .ToArray();
            Assert.That(projectiles, Has.Length.EqualTo(7));
            Assert.That(
                projectiles.Select(
                        projectile => GetProperty<StableId>(
                            projectile,
                            "HitEventId"))
                    .Distinct()
                    .Count(),
                Is.EqualTo(7));

            for (int index = 0; index < projectiles.Length; index++)
            {
                Track(projectiles[index].gameObject);
                InvokePrivate(projectiles[index], "OnTriggerEnter2D", target);
                InvokePrivate(projectiles[index], "OnTriggerEnter2D", target);
            }

            Assert.That(hitAdapter.ProcessedEventCount, Is.EqualTo(7));
            Assert.That(GetProperty<int>(handler, "ActiveProjectileCount"), Is.Zero);
            Assert.That(GetProperty<int>(handler, "ReservedPelletCount"), Is.Zero);
            TestContext.WriteLine(
                "duplicate-damage-guard pellets=7 callbacks-per-pellet=2 unique-hit-events=7 processed-events=7");
        }

        [UnityTest]
        public IEnumerator Restart_FiftyCyclesLeaveNoPelletsOrReservations()
        {
            CombatHit2DAdapter hitAdapter = new CombatHit2DAdapter(SourceId);
            object handler = CreateHandler(
                hitAdapter,
                false,
                out Component template);
            WeaponMount2DAdapter mount = CreateMount(handler);

            for (int cycle = 0; cycle < 50; cycle++)
            {
                WeaponMount2DExecutionResult result = mount.ExecutePlan(
                    BuildPlan(
                        false,
                        "restart-" + cycle.ToString("D2"),
                        cycle + 10L,
                        1d,
                        0d));
                Assert.That(result.Succeeded, Is.True, "cycle " + cycle);
                Assert.That(GetProperty<int>(handler, "ActiveProjectileCount"), Is.EqualTo(7));
                Assert.That(GetProperty<int>(handler, "ReservedPelletCount"), Is.Zero);

                Invoke(handler, "ResetSession");
                Assert.That(GetProperty<int>(handler, "ActiveProjectileCount"), Is.Zero);
                Assert.That(GetProperty<int>(handler, "ReservedPelletCount"), Is.Zero);
                yield return null;
            }

            Component[] liveProjectiles = Resources.FindObjectsOfTypeAll(RuntimeTypes.Projectile)
                .OfType<Component>()
                .Where(
                    component => component != template
                        && component.gameObject.scene.IsValid())
                .ToArray();
            Assert.That(liveProjectiles, Is.Empty);
            Assert.That(hitAdapter.ProcessedEventCount, Is.Zero);
            TestContext.WriteLine(
                "restart cycles=50 active-pellets=0 reservations=0 live-clones=0 processed-hits=0");
        }

        [Test]
        public void DensityBudget_RejectsAuthoredAndRuntimeOverflow()
        {
            AssertTuningRejected(12, 60d, 5d, 0.1d, 18d, 0.7d, 0.08d, 0.1d);
            AssertTuningRejected(3, 24d, 5d, 0.25d, 18d, 5d, 0.08d, 0.1d);

            CombatHit2DAdapter hitAdapter = new CombatHit2DAdapter(SourceId);
            object handler = CreateHandler(
                hitAdapter,
                false,
                out Component ignoredTemplate);
            WeaponMount2DAdapter mount = CreateMount(handler);

            for (int cycle = 0; cycle < 6; cycle++)
            {
                WeaponMount2DExecutionResult accepted = mount.ExecutePlan(
                    BuildPlan(
                        false,
                        "density-" + cycle.ToString("D2"),
                        cycle + 100L,
                        1d,
                        0d));
                Assert.That(accepted.Succeeded, Is.True, "cycle " + cycle);
            }

            Assert.That(GetProperty<int>(handler, "ActiveProjectileCount"), Is.EqualTo(42));
            WeaponMount2DExecutionResult rejected = mount.ExecutePlan(
                BuildPlan(false, "density-overflow", 200L, 1d, 0d));
            Assert.That(
                rejected.Status,
                Is.EqualTo(WeaponMount2DExecutionStatus.HandlerRejected));
            Assert.That(rejected.ExecutedOperationCount, Is.Zero);
            Assert.That(rejected.FailedOperationIndex, Is.Zero);
            Assert.That(GetProperty<int>(handler, "ActiveProjectileCount"), Is.EqualTo(42));
            Assert.That(GetProperty<int>(handler, "ReservedPelletCount"), Is.Zero);

            Invoke(handler, "ResetSession");
            Assert.That(GetProperty<int>(handler, "ActiveProjectileCount"), Is.Zero);
            TestContext.WriteLine(
                "density authored-pps-cap=48 authored-concurrent-cap=48 runtime-active=42 next-spread=7 rejected-before-spawn");
        }

        [Test]
        public void PackageArtifacts_AreBoundedAndContainRequiredProofFixtures()
        {
            string source = ReadProjectFile(TuningPath)
                + "\n"
                + ReadProjectFile(ModulePath)
                + "\n"
                + ReadProjectFile(HandlerPath);
            string fixture = ReadProjectFile(FixturePath);
            string readability = ReadProjectFile(ReadabilityNotePath);

            string[] forbiddenTokens =
            {
                "UnityEngine.Random",
                "System.Random",
                "DamageMessage",
                "AddForce",
                "FindObject",
                "GameObject.Find",
                "ServiceLocator",
                "Singleton",
                "slug alternate",
                "Knockback",
            };
            foreach (string token in forbiddenTokens)
            {
                Assert.That(source, Does.Not.Contain(token), "Forbidden token: " + token);
            }

            Assert.That(source, Does.Contain("MaximumPelletCount"));
            Assert.That(source, Does.Contain("MaximumConcurrentPelletCount"));
            Assert.That(source, Does.Contain("BoundedProjectile2D"));
            Assert.That(fixture, Does.Contain("\"pellet_count\": 7"));
            Assert.That(fixture, Does.Contain("-12.0"));
            Assert.That(fixture, Does.Contain("12.0"));
            Assert.That(readability, Does.Contain("near enemies"));
            Assert.That(readability, Does.Contain("human Unity verification"));

            TestContext.WriteLine(
                "package-surface random=false damage-authority=false knockback=false fixture=present readability-note=present");
        }

        private static WeaponFireExecutionPlan BuildPlan(
            bool empowered,
            string eventSuffix,
            long simulationStep,
            double directionX,
            double directionY)
        {
            object descriptor = InvokeStatic(
                RuntimeTypes.Definition,
                "CreateDefaultDescriptor");
            object fireProfile = GetProperty<object>(
                descriptor,
                empowered ? "EmpoweredFire" : "NormalFire");
            WeaponRuntimeProfile runtimeProfile =
                GetProperty<WeaponRuntimeProfile>(
                    fireProfile,
                    "RuntimeProfile");
            IWeaponBehaviorModule module = (IWeaponBehaviorModule)InvokeStatic(
                RuntimeTypes.Definition,
                "CreateBehaviorModule");
            WeaponBehaviorPipeline pipeline = new WeaponBehaviorPipeline(
                new[] { module });
            WeaponBehaviorInput input = new WeaponBehaviorInput(
                StableId.Parse("combat-event.wp-004-" + eventSuffix),
                WeaponId,
                MountId,
                simulationStep,
                runtimeProfile,
                empowered,
                0d,
                0d,
                directionX,
                directionY,
                1d);
            return pipeline.BuildExecutionPlan(input);
        }

        private object CreateHandler(
            CombatHit2DAdapter hitAdapter,
            bool enablePresentation,
            out Component template)
        {
            template = CreateProjectileTemplate();
            Collider2D ownerCollider = CreateTarget("WP-004 Explicit Owner");
            ConstructorInfo constructor = RuntimeTypes.Handler.GetConstructors()
                .Single(candidate => candidate.GetParameters().Length == 6);
            object handler = constructor.Invoke(
                new object[]
                {
                    template,
                    hitAdapter,
                    new Collider2D[] { ownerCollider },
                    null,
                    enablePresentation,
                    0.12f,
                });
            createdAdapters.Add((IDisposable)handler);
            return handler;
        }

        private Component CreateProjectileTemplate()
        {
            GameObject root = CreateObject("WP-004 Projectile Template");
            Rigidbody2D body = root.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            CircleCollider2D collider = root.AddComponent<CircleCollider2D>();
            collider.isTrigger = true;

            GameObject presentationObject = new GameObject("TemporaryHitPresentation");
            presentationObject.transform.SetParent(root.transform, false);
            Component presentation = presentationObject.AddComponent(RuntimeTypes.Presentation);
            Component projectile = root.AddComponent(RuntimeTypes.Projectile);
            SetField(projectile, "body", body);
            SetField(projectile, "projectileCollider", collider);
            SetField(projectile, "temporaryHitPresentation", presentation);
            root.SetActive(false);
            return projectile;
        }

        private WeaponMount2DAdapter CreateMount(object handler)
        {
            GameObject mountObject = CreateObject("WP-004 Weapon Mount 2D Adapter");
            WeaponMount2DAdapter mount =
                mountObject.AddComponent<WeaponMount2DAdapter>();
            mount.Configure(
                SourceId,
                WeaponId,
                MountId,
                new[]
                {
                    (IWeaponFireExecutionOperation2DHandler)handler,
                });
            return mount;
        }

        private static object CreateTuning(
            int pelletCount,
            double spreadDegrees,
            double damage,
            double cadenceSeconds,
            double speed,
            double lifetimeSeconds,
            double radius,
            double recoverySeconds)
        {
            return Activator.CreateInstance(
                RuntimeTypes.Tuning,
                pelletCount,
                spreadDegrees,
                damage,
                cadenceSeconds,
                speed,
                lifetimeSeconds,
                radius,
                recoverySeconds);
        }

        private static void AssertTuningRejected(
            int pelletCount,
            double spreadDegrees,
            double damage,
            double cadenceSeconds,
            double speed,
            double lifetimeSeconds,
            double radius,
            double recoverySeconds)
        {
            TargetInvocationException exception =
                Assert.Throws<TargetInvocationException>(
                    () => CreateTuning(
                        pelletCount,
                        spreadDegrees,
                        damage,
                        cadenceSeconds,
                        speed,
                        lifetimeSeconds,
                        radius,
                        recoverySeconds));
            Assert.That(
                exception.InnerException,
                Is.TypeOf<ArgumentOutOfRangeException>());
        }

        private static List<int> CopyCoefficientKinds(object fireProfile)
        {
            return GetObjectList(fireProfile, "NumericCoefficients")
                .Select(
                    coefficient => Convert.ToInt32(
                        GetProperty<object>(coefficient, "Kind")))
                .OrderBy(value => value)
                .ToList();
        }

        private Collider2D CreateTarget(string name)
        {
            return CreateObject(name).AddComponent<BoxCollider2D>();
        }

        private GameObject CreateObject(string name)
        {
            GameObject gameObject = new GameObject(name);
            createdObjects.Add(gameObject);
            return gameObject;
        }

        private void Track(GameObject gameObject)
        {
            if (gameObject != null && !createdObjects.Contains(gameObject))
            {
                createdObjects.Add(gameObject);
            }
        }

        private static T GetProperty<T>(object instance, string propertyName)
        {
            PropertyInfo property = instance.GetType().GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(property, Is.Not.Null, propertyName);
            return (T)property.GetValue(instance, null);
        }

        private static T GetStaticProperty<T>(Type type, string propertyName)
        {
            PropertyInfo property = type.GetProperty(
                propertyName,
                BindingFlags.Static | BindingFlags.Public);
            Assert.That(property, Is.Not.Null, propertyName);
            return (T)property.GetValue(null, null);
        }

        private static List<object> GetObjectList(
            object instance,
            string propertyName)
        {
            IEnumerable enumerable = GetProperty<IEnumerable>(
                instance,
                propertyName);
            return enumerable.Cast<object>().ToList();
        }

        private static object Invoke(
            object instance,
            string methodName,
            params object[] arguments)
        {
            MethodInfo method = instance.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(method, Is.Not.Null, methodName);
            return method.Invoke(instance, arguments);
        }

        private static object InvokePrivate(
            object instance,
            string methodName,
            params object[] arguments)
        {
            MethodInfo method = instance.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, methodName);
            return method.Invoke(instance, arguments);
        }

        private static object InvokeStatic(
            Type type,
            string methodName,
            params object[] arguments)
        {
            MethodInfo method = type.GetMethod(
                methodName,
                BindingFlags.Static | BindingFlags.Public);
            Assert.That(method, Is.Not.Null, methodName);
            return method.Invoke(null, arguments);
        }

        private static void SetField(
            object instance,
            string fieldName,
            object value)
        {
            FieldInfo field = instance.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            field.SetValue(instance, value);
        }

        private static string ReadProjectFile(string assetPath)
        {
            string projectRoot = Directory.GetParent(
                UnityEngine.Application.dataPath).FullName;
            return File.ReadAllText(
                Path.Combine(
                    projectRoot,
                    assetPath.Replace(
                        '/',
                        Path.DirectorySeparatorChar)));
        }

        private static class RuntimeTypes
        {
            public static readonly Type Definition = Find(
                "ShooterMover.ContentPackages.Weapons.Shotgun.ShotgunPackageDefinition");
            public static readonly Type Tuning = Find(
                "ShooterMover.ContentPackages.Weapons.Shotgun.ShotgunTuning");
            public static readonly Type Handler = Find(
                "ShooterMover.ContentPackages.Weapons.Shotgun.ShotgunPellet2DHandler");
            public static readonly Type Projectile = Find(
                "ShooterMover.ContentPackages.Weapons.Shared.Runtime.BoundedProjectile2D");
            public static readonly Type Presentation = Find(
                "ShooterMover.ContentPackages.Weapons.Shared.Presentation.TemporaryHitPresentation");

            private static Type Find(string fullName)
            {
                Type type = AppDomain.CurrentDomain.GetAssemblies()
                    .Select(assembly => assembly.GetType(fullName, false))
                    .FirstOrDefault(candidate => candidate != null);
                if (type == null)
                {
                    throw new InvalidOperationException(
                        "Runtime type not found: " + fullName);
                }

                return type;
            }
        }
    }
}
#endif
