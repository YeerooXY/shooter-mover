#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using ShooterMover.Contracts.Combat;
using ShooterMover.Domain.Combat;
using ShooterMover.Domain.Common;
using ShooterMover.UnityAdapters.Combat;
using UnityEngine;
using UnityEngine.TestTools;

namespace ShooterMover.Tests.PlayMode.Combat
{
    public sealed class BoundedProjectile2DTests
    {
        private const string ProjectilePath =
            "Assets/ShooterMover/ContentPackages/Weapons/Shared/Runtime/BoundedProjectile2D.cs";
        private const string PlanAdapterPath =
            "Assets/ShooterMover/ContentPackages/Weapons/Shared/Runtime/ProjectileExecutionPlanAdapter.cs";
        private const string PresentationPath =
            "Assets/ShooterMover/ContentPackages/Weapons/Shared/Presentation/TemporaryHitPresentation.cs";
        private const string PrefabPath =
            "Assets/ShooterMover/ContentPackages/Weapons/Shared/Prefabs/BoundedProjectile2D.prefab";

        private static readonly StableId SourceId = StableId.Parse("actor.wp002-source");
        private static readonly StableId WeaponId = StableId.Parse("weapon.wp002-fixture");
        private static readonly StableId MountId = StableId.Parse("weapon-mount.wp002-fixture");
        private static readonly StableId ModuleId = StableId.Parse("behavior.wp002-fixture");
        private static readonly StableId OperationKindId =
            StableId.Parse("operation-kind.bounded-projectile-2d");

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
        public void ValidatedPlan_SpawnsConfiguredFinite2DProjectile()
        {
            CombatHit2DAdapter hitAdapter = new CombatHit2DAdapter(SourceId);
            object adapter = CreateExecutionAdapter(hitAdapter, true, out Component template);
            WeaponMount2DExecutionResult result = Execute(
                adapter,
                CreateOperation("operation.wp002-spawn", 20d, 2d, 0.2d),
                "spawn");

            Assert.That(result.Status, Is.EqualTo(WeaponMount2DExecutionStatus.Executed));
            Assert.That(GetProperty<int>(adapter, "ActiveProjectileCount"), Is.EqualTo(1));

            Component projectile = GetProperty<Component>(adapter, "LastSpawnedProjectile");
            Track(projectile.gameObject);
            Assert.That(GetProperty<bool>(projectile, "IsInitialized"), Is.True);
            Assert.That(GetProperty<bool>(projectile, "IsComplete"), Is.False);
            Assert.That(GetProperty<float>(projectile, "RemainingLifetimeSeconds"), Is.EqualTo(2f));
            Assert.That(projectile.GetComponent<Rigidbody2D>().linearVelocity, Is.EqualTo(new Vector2(20f, 0f)));
            Assert.That(projectile.GetComponent<CircleCollider2D>().radius, Is.EqualTo(0.2f));
            Assert.That(projectile.GetComponent<Collider2D>(), Is.Not.Null);
            Assert.That(projectile.GetComponent<Rigidbody>(), Is.Null);
            Assert.That(projectile.GetComponent<Collider>(), Is.Null);
            Assert.That(template.gameObject.activeSelf, Is.False);

            TestContext.WriteLine(
                "spawn active=1 lifetime=2 speed=20 radius=0.2 components=Rigidbody2D,CircleCollider2D");
        }

        [UnityTest]
        public IEnumerator FiniteLifetime_ExpiresAndReleasesAdapterOwnership()
        {
            CombatHit2DAdapter hitAdapter = new CombatHit2DAdapter(SourceId);
            object adapter = CreateExecutionAdapter(hitAdapter, true, out Component ignoredTemplate);
            WeaponMount2DExecutionResult result = Execute(
                adapter,
                CreateOperation("operation.wp002-expiry", 1d, 0.02d, 0.1d),
                "expiry");
            Assert.That(result.Succeeded, Is.True);

            for (int frame = 0;
                frame < 10 && GetProperty<int>(adapter, "ActiveProjectileCount") != 0;
                frame++)
            {
                yield return null;
            }

            Assert.That(GetProperty<int>(adapter, "ActiveProjectileCount"), Is.Zero);
            Assert.That(GetProperty<Component>(adapter, "LastSpawnedProjectile"), Is.Null);
            Assert.That(hitAdapter.ProcessedEventCount, Is.Zero);
            TestContext.WriteLine("finite-expiry active=0 processed-hits=0");
        }

        [Test]
        public void RepeatedCollisionCallback_TranslatesOneConfirmedHitOnly()
        {
            Collider2D target = CreateTarget("WP-002 Confirmed Target");
            StableId targetId = StableId.Parse("enemy.wp002-confirmed-target");
            CombatHit2DAdapter hitAdapter = new CombatHit2DAdapter(SourceId);
            Assert.That(
                hitAdapter.RegisterTarget(target, targetId),
                Is.EqualTo(CombatHit2DTargetRegistrationStatus.Registered));

            object adapter = CreateExecutionAdapter(hitAdapter, true, out Component ignoredTemplate);
            WeaponMount2DExecutionResult result = Execute(
                adapter,
                CreateOperation("operation.wp002-hit", 10d, 2d, 0.1d),
                "hit");
            Assert.That(result.Succeeded, Is.True);

            Component projectile = GetProperty<Component>(adapter, "LastSpawnedProjectile");
            Track(projectile.gameObject);
            Component presentation = projectile.transform.GetChild(0).GetComponent(RuntimeTypes.Presentation);
            Track(presentation.gameObject);

            InvokePrivate(projectile, "OnTriggerEnter2D", target);
            InvokePrivate(projectile, "OnTriggerEnter2D", target);

            CombatHit2DTranslationResult translation =
                GetProperty<CombatHit2DTranslationResult>(projectile, "LastHitTranslation");
            Assert.That(translation.Status, Is.EqualTo(CombatHit2DTranslationStatus.Confirmed));
            Assert.That(translation.Message.SourceId, Is.EqualTo(SourceId));
            Assert.That(translation.Message.TargetId, Is.EqualTo(targetId));
            Assert.That(translation.Message.Channel, Is.EqualTo(CombatChannel.Kinetic));
            Assert.That(translation.Message.Result, Is.EqualTo(HitResult.Confirmed));
            Assert.That(hitAdapter.ProcessedEventCount, Is.EqualTo(1));
            Assert.That(GetProperty<int>(adapter, "ActiveProjectileCount"), Is.Zero);
            Assert.That(GetProperty<bool>(presentation, "IsPlaying"), Is.True);
            Assert.That(presentation.transform.parent, Is.Null);

            TestContext.WriteLine(
                "one-hit status=Confirmed callbacks=2 confirmed-messages=1 presentation=playing");
        }

        [Test]
        public void DisabledPresentation_ReportsHitWithoutDetachingTemporaryHook()
        {
            Collider2D target = CreateTarget("WP-002 Presentation Disabled Target");
            CombatHit2DAdapter hitAdapter = new CombatHit2DAdapter(SourceId);
            hitAdapter.RegisterTarget(target, StableId.Parse("enemy.wp002-presentation-disabled"));

            object adapter = CreateExecutionAdapter(hitAdapter, false, out Component ignoredTemplate);
            WeaponMount2DExecutionResult result = Execute(
                adapter,
                CreateOperation("operation.wp002-disabled-presentation", 10d, 2d, 0.1d),
                "disabled-presentation");
            Assert.That(result.Succeeded, Is.True);

            Component projectile = GetProperty<Component>(adapter, "LastSpawnedProjectile");
            Track(projectile.gameObject);
            Component presentation = projectile.transform.GetChild(0).GetComponent(RuntimeTypes.Presentation);
            InvokePrivate(projectile, "OnTriggerEnter2D", target);

            Assert.That(hitAdapter.ProcessedEventCount, Is.EqualTo(1));
            Assert.That(GetProperty<bool>(presentation, "IsPlaying"), Is.False);
            Assert.That(presentation.transform.parent, Is.EqualTo(projectile.transform));
            Assert.That(GetProperty<int>(adapter, "ActiveProjectileCount"), Is.Zero);
            TestContext.WriteLine("disabled-presentation confirmed-hit=1 detached-hook=false");
        }

        [UnityTest]
        public IEnumerator RapidRestart_FiftyCyclesLeaveNoProjectileOrCallbackState()
        {
            CombatHit2DAdapter hitAdapter = new CombatHit2DAdapter(SourceId);
            object adapter = CreateExecutionAdapter(hitAdapter, false, out Component template);

            for (int cycle = 0; cycle < 50; cycle++)
            {
                WeaponMount2DExecutionResult result = Execute(
                    adapter,
                    CreateOperation(
                        "operation.wp002-restart-" + cycle.ToString("D2"),
                        5d,
                        5d,
                        0.1d),
                    "restart-" + cycle.ToString("D2"));
                Assert.That(result.Succeeded, Is.True, "cycle " + cycle);
                Assert.That(GetProperty<int>(adapter, "ActiveProjectileCount"), Is.EqualTo(1));

                Invoke(adapter, "ResetSession");
                Assert.That(GetProperty<int>(adapter, "ActiveProjectileCount"), Is.Zero);
                Assert.That(GetProperty<Component>(adapter, "LastSpawnedProjectile"), Is.Null);
                yield return null;
            }

            Component[] liveProjectiles = Resources.FindObjectsOfTypeAll(RuntimeTypes.Projectile)
                .OfType<Component>()
                .Where(component => component != template && component.gameObject.scene.IsValid())
                .ToArray();
            Assert.That(liveProjectiles, Is.Empty);
            Assert.That(hitAdapter.ProcessedEventCount, Is.Zero);
            TestContext.WriteLine(
                "rapid-restart cycles=50 active=0 live-clones=0 processed-hits=0");
        }

        [Test]
        public void InvalidPlanOrProjectilePayload_FailsBeforeSpawn()
        {
            CombatHit2DAdapter hitAdapter = new CombatHit2DAdapter(SourceId);
            object adapter = CreateExecutionAdapter(hitAdapter, false, out Component ignoredTemplate);
            WeaponMount2DAdapter mount = CreateMount(adapter);

            WeaponMount2DExecutionResult nullPlan = mount.ExecutePlan(null);
            Assert.That(nullPlan.Status, Is.EqualTo(WeaponMount2DExecutionStatus.InvalidPlan));

            IWeaponFireExecutionOperation invalidLifetime = CreateOperation(
                "operation.wp002-invalid-lifetime",
                10d,
                double.NaN,
                0.1d);
            WeaponMount2DExecutionResult rejected = mount.ExecutePlan(
                BuildPlan(invalidLifetime, "invalid-lifetime"));

            Assert.That(rejected.Status, Is.EqualTo(WeaponMount2DExecutionStatus.HandlerRejected));
            Assert.That(rejected.ExecutedOperationCount, Is.Zero);
            Assert.That(GetProperty<int>(adapter, "ActiveProjectileCount"), Is.Zero);
            Assert.That(GetProperty<Component>(adapter, "LastSpawnedProjectile"), Is.Null);
            TestContext.WriteLine("invalid-plan=null:InvalidPlan nan-lifetime:HandlerRejected spawned=0");
        }

        [Test]
        public void RuntimeAndPrefabSurface_Are2DOnlyBoundedAndNonAuthoritative()
        {
            string runtimeSource = ReadProjectFile(ProjectilePath)
                + "\n"
                + ReadProjectFile(PlanAdapterPath)
                + "\n"
                + ReadProjectFile(PresentationPath);
            string prefab = ReadProjectFile(PrefabPath);

            string[] forbiddenRuntimeTokens =
            {
                "DamageMessage",
                "VitalState",
                "Physics.Raycast",
                "Physics.SphereCast",
                "RaycastHit",
                "FindObject",
                "GameObject.Find",
                "FindWithTag",
                "Camera.main",
                "Resources.Load",
                "ServiceLocator",
                "Singleton",
                "weapon.blaster-machine-gun",
                "weapon.shotgun",
                "weapon.rocket-launcher",
                "weapon.arc-gun",
                "weapon.ricochet-gun",
            };
            foreach (string token in forbiddenRuntimeTokens)
            {
                Assert.That(runtimeSource, Does.Not.Contain(token), "Forbidden token: " + token);
            }

            string[] forbiddenThreeDimensionalPatterns =
            {
                @"\bRigidbody\b",
                @"\bCollider\b",
                @"\bCollision\b",
                @"\bPhysicsScene\b",
            };
            foreach (string pattern in forbiddenThreeDimensionalPatterns)
            {
                Assert.That(
                    Regex.IsMatch(runtimeSource, pattern),
                    Is.False,
                    "Forbidden 3D type pattern: " + pattern);
            }

            Assert.That(runtimeSource, Does.Contain("Rigidbody2D"));
            Assert.That(runtimeSource, Does.Contain("Collider2D"));
            Assert.That(runtimeSource, Does.Contain("PhysicsScene2D"));
            Assert.That(runtimeSource, Does.Contain("CombatHit2DAdapter"));
            Assert.That(runtimeSource, Does.Contain("MaximumLifetimeSeconds"));

            Assert.That(prefab, Does.Contain("Rigidbody2D:"));
            Assert.That(prefab, Does.Contain("CircleCollider2D:"));
            Assert.That(prefab, Does.Contain("m_IsTrigger: 1"));
            Assert.That(prefab, Does.Contain("m_GravityScale: 0"));
            Assert.That(prefab, Does.Contain("TemporaryHitPresentation"));
            Assert.That(prefab, Does.Not.Contain("Rigidbody:"));
            Assert.That(prefab, Does.Not.Contain("BoxCollider:"));
            Assert.That(prefab, Does.Not.Contain("MeshCollider:"));
            Assert.That(prefab, Does.Not.Contain("Camera:"));
            Assert.That(prefab, Does.Not.Contain("m_TagString: Player"));

            TestContext.WriteLine(
                "prefab components=Transform,Rigidbody2D,CircleCollider2D,BoundedProjectile2D; child=Transform,TemporaryHitPresentation; package-id=false scene-ref=false 3d-component=false");
        }

        private object CreateExecutionAdapter(
            CombatHit2DAdapter hitAdapter,
            bool enablePresentation,
            out Component template)
        {
            template = CreateProjectileTemplate();
            Collider2D ownerCollider = CreateTarget("WP-002 Explicit Owner");
            ConstructorInfo constructor = RuntimeTypes.PlanAdapter.GetConstructors()
                .Single(candidate => candidate.GetParameters().Length == 7);
            object adapter = constructor.Invoke(
                new object[]
                {
                    OperationKindId,
                    template,
                    hitAdapter,
                    new Collider2D[] { ownerCollider },
                    null,
                    enablePresentation,
                    0.12f,
                });
            createdAdapters.Add((IDisposable)adapter);
            return adapter;
        }

        private Component CreateProjectileTemplate()
        {
            GameObject root = CreateObject("WP-002 Projectile Template");
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

        private WeaponMount2DExecutionResult Execute(
            object adapter,
            IWeaponFireExecutionOperation operation,
            string eventSuffix)
        {
            return CreateMount(adapter).ExecutePlan(BuildPlan(operation, eventSuffix));
        }

        private WeaponMount2DAdapter CreateMount(object adapter)
        {
            GameObject mountObject = CreateObject("WP-002 Weapon Mount 2D Adapter");
            WeaponMount2DAdapter mount = mountObject.AddComponent<WeaponMount2DAdapter>();
            mount.Configure(
                SourceId,
                WeaponId,
                MountId,
                new[] { (IWeaponFireExecutionOperation2DHandler)adapter });
            return mount;
        }

        private static IWeaponFireExecutionOperation CreateOperation(
            string operationId,
            double speed,
            double lifetimeSeconds,
            double radius)
        {
            return (IWeaponFireExecutionOperation)Activator.CreateInstance(
                RuntimeTypes.Operation,
                OperationKindId,
                StableId.Parse(operationId),
                speed,
                lifetimeSeconds,
                radius,
                CombatChannel.Kinetic);
        }

        private static WeaponFireExecutionPlan BuildPlan(
            IWeaponFireExecutionOperation operation,
            string eventSuffix)
        {
            WeaponRuntimeProfile profile = BuildProfile(ModuleId);
            SyntheticModule module = new SyntheticModule(ModuleId, operation);
            WeaponBehaviorPipeline pipeline = new WeaponBehaviorPipeline(
                new IWeaponBehaviorModule[] { module });
            WeaponBehaviorInput input = new WeaponBehaviorInput(
                StableId.Parse("combat-event.wp002-" + eventSuffix),
                WeaponId,
                MountId,
                1L,
                profile,
                false,
                0d,
                0d,
                1d,
                0d,
                1d);
            return pipeline.BuildExecutionPlan(input);
        }

        private static WeaponRuntimeProfile BuildProfile(params StableId[] moduleIds)
        {
            StableId[] copied = (StableId[])moduleIds.Clone();
            return WeaponRuntimeProfile.Create(
                WeaponRuntimeProfile.CurrentProfileVersion,
                StableId.Parse("weapon-profile.wp002-fixture"),
                0.1d,
                1,
                0d,
                0d,
                WeaponCycleMode.None,
                0d,
                0d,
                0d,
                0d,
                false,
                0d,
                0d,
                0.25d,
                copied,
                copied,
                0);
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

        private static object Invoke(object instance, string methodName, params object[] arguments)
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

        private static void SetField(object instance, string fieldName, object value)
        {
            FieldInfo field = instance.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            field.SetValue(instance, value);
        }

        private static string ReadProjectFile(string assetPath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            return File.ReadAllText(
                Path.Combine(
                    projectRoot,
                    assetPath.Replace('/', Path.DirectorySeparatorChar)));
        }

        private sealed class SyntheticModule : IWeaponBehaviorModule
        {
            private readonly IWeaponFireExecutionOperation operation;

            public SyntheticModule(
                StableId moduleId,
                IWeaponFireExecutionOperation operation)
            {
                ModuleId = moduleId;
                this.operation = operation;
            }

            public StableId ModuleId { get; }

            public WeaponBehaviorModulePlan BuildExecutionPlan(WeaponBehaviorInput input)
            {
                return new WeaponBehaviorModulePlan(ModuleId, operation);
            }
        }

        private static class RuntimeTypes
        {
            public static readonly Type Projectile = Find(
                "ShooterMover.ContentPackages.Weapons.Shared.Runtime.BoundedProjectile2D");
            public static readonly Type Operation = Find(
                "ShooterMover.ContentPackages.Weapons.Shared.Runtime.BoundedProjectileExecutionOperation");
            public static readonly Type PlanAdapter = Find(
                "ShooterMover.ContentPackages.Weapons.Shared.Runtime.ProjectileExecutionPlanAdapter");
            public static readonly Type Presentation = Find(
                "ShooterMover.ContentPackages.Weapons.Shared.Presentation.TemporaryHitPresentation");

            private static Type Find(string fullName)
            {
                Type type = AppDomain.CurrentDomain.GetAssemblies()
                    .Select(assembly => assembly.GetType(fullName, false))
                    .FirstOrDefault(candidate => candidate != null);
                if (type == null)
                {
                    throw new InvalidOperationException("Runtime type not found: " + fullName);
                }

                return type;
            }
        }
    }
}
#endif
