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
    public sealed class RicochetGunPackageTests
    {
        private const string PackageRoot =
            "Assets/ShooterMover/ContentPackages/Weapons/RicochetGun/";
        private const string PrefabPath =
            "Assets/ShooterMover/ContentPackages/Weapons/RicochetGun/Prefabs/RicochetProjectile2D.prefab";

        private static readonly StableId SourceId =
            StableId.Parse("actor.wp007-source");
        private static readonly StableId WeaponId =
            StableId.Parse("weapon.ricochet-gun");
        private static readonly StableId MountId =
            StableId.Parse("weapon-mount.wp007-fixture");
        private static readonly StableId SyntheticModuleId =
            StableId.Parse("module.wp007-synthetic");
        private static readonly StableId OperationKindId =
            StableId.Parse("operation-kind.ricochet-projectile-2d");

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
        public void ValidatedPlan_SpawnsLongLivedFinite2DProjectile()
        {
            CombatHit2DAdapter hitAdapter = new CombatHit2DAdapter(SourceId);
            object adapter = CreateExecutionAdapter(
                hitAdapter,
                false,
                out Component template);
            WeaponMount2DExecutionResult result = ExecutePackagePlan(
                adapter,
                false,
                "spawn");

            Assert.That(
                result.Status,
                Is.EqualTo(WeaponMount2DExecutionStatus.Executed));
            Assert.That(GetProperty<int>(adapter, "ActiveProjectileCount"), Is.EqualTo(1));

            Component projectile = GetProperty<Component>(
                adapter,
                "LastSpawnedProjectile");
            Track(projectile.gameObject);
            Rigidbody2D body = projectile.GetComponent<Rigidbody2D>();
            CircleCollider2D collider = projectile.GetComponent<CircleCollider2D>();

            Assert.That(GetProperty<bool>(projectile, "IsInitialized"), Is.True);
            Assert.That(GetProperty<bool>(projectile, "IsComplete"), Is.False);
            Assert.That(GetProperty<int>(projectile, "WallBounceCount"), Is.Zero);
            Assert.That(
                GetProperty<float>(projectile, "RemainingLifetimeSeconds"),
                Is.EqualTo(8f).Within(0.0001f));
            Assert.That(body.linearVelocity, Is.EqualTo(new Vector2(15f, 0f)));
            Assert.That(collider.radius, Is.EqualTo(0.12f).Within(0.0001f));
            Assert.That(collider.isTrigger, Is.False);
            Assert.That(projectile.GetComponent<Rigidbody>(), Is.Null);
            Assert.That(projectile.GetComponent<Collider>(), Is.Null);
            Assert.That(template.gameObject.activeSelf, Is.False);

            TestContext.WriteLine(
                "ricochet-spawn active=1 lifetime=8 speed=15 radius=0.12 max-bounces=2 finite=true");
        }

        [Test]
        public void ValidWallContacts_ReflectTwiceThenThirdCollisionExpires()
        {
            CombatHit2DAdapter hitAdapter = new CombatHit2DAdapter(SourceId);
            object adapter = CreateExecutionAdapter(
                hitAdapter,
                false,
                out Component ignoredTemplate);
            WeaponMount2DExecutionResult result = ExecutePackagePlan(
                adapter,
                false,
                "bounce-trace");
            Assert.That(result.Succeeded, Is.True);

            Component projectile = GetProperty<Component>(
                adapter,
                "LastSpawnedProjectile");
            Track(projectile.gameObject);
            Rigidbody2D body = projectile.GetComponent<Rigidbody2D>();
            body.linearVelocity = new Vector2(10f, -10f);
            SetField(
                projectile,
                "travelDirection",
                new Vector2(1f, -1f).normalized);

            Collider2D wall = CreateValidWall("WP-007 Valid Wall");
            InvokePrivate(
                projectile,
                "ProcessCollision",
                wall,
                new[] { new Vector2(0f, 1f) },
                Vector2.zero);
            Assert.That(GetProperty<int>(projectile, "WallBounceCount"), Is.EqualTo(1));
            Assert.That(body.linearVelocity.x, Is.GreaterThan(0f));
            Assert.That(body.linearVelocity.y, Is.GreaterThan(0f));

            InvokePrivate(
                projectile,
                "ProcessCollision",
                wall,
                new[] { new Vector2(-1f, 0f) },
                Vector2.zero);
            Assert.That(GetProperty<int>(projectile, "WallBounceCount"), Is.EqualTo(2));
            Assert.That(body.linearVelocity.x, Is.LessThan(0f));
            Assert.That(body.linearVelocity.y, Is.GreaterThan(0f));

            InvokePrivate(
                projectile,
                "ProcessCollision",
                wall,
                new[] { new Vector2(0f, -1f) },
                Vector2.zero);
            Assert.That(GetProperty<int>(projectile, "WallBounceCount"), Is.EqualTo(2));
            Assert.That(GetProperty<bool>(projectile, "IsComplete"), Is.True);
            Assert.That(
                EnumName(projectile, "CompletionReason"),
                Is.EqualTo("ThirdWallCollision"));
            Assert.That(GetProperty<int>(adapter, "ActiveProjectileCount"), Is.Zero);
            Assert.That(hitAdapter.ProcessedEventCount, Is.Zero);

            TestContext.WriteLine(
                "bounce-trace first=(+,+) count=1 second=(-,+) count=2 third=expired reflected=false confirmed-hits=0");
        }

        [Test]
        public void Unmarked2DCollider_DoesNotReflect()
        {
            CombatHit2DAdapter hitAdapter = new CombatHit2DAdapter(SourceId);
            object adapter = CreateExecutionAdapter(
                hitAdapter,
                false,
                out Component ignoredTemplate);
            Assert.That(
                ExecutePackagePlan(adapter, false, "invalid-wall").Succeeded,
                Is.True);

            Component projectile = GetProperty<Component>(
                adapter,
                "LastSpawnedProjectile");
            Track(projectile.gameObject);
            Collider2D unmarked = CreateObject("WP-007 Unmarked Collider")
                .AddComponent<BoxCollider2D>();

            InvokePrivate(
                projectile,
                "ProcessCollision",
                unmarked,
                new[] { new Vector2(-1f, 0f) },
                Vector2.zero);

            Assert.That(GetProperty<int>(projectile, "WallBounceCount"), Is.Zero);
            Assert.That(GetProperty<bool>(projectile, "IsComplete"), Is.True);
            Assert.That(
                EnumName(projectile, "CompletionReason"),
                Is.EqualTo("CollisionWithoutConfirmedTarget"));
            Assert.That(hitAdapter.ProcessedEventCount, Is.Zero);
            TestContext.WriteLine(
                "wall-validation marked=false reflected=false terminated=true");
        }

        [Test]
        public void ConfirmedTargetContact_TerminatesWithoutTargetRicochet()
        {
            CombatHit2DAdapter hitAdapter = new CombatHit2DAdapter(SourceId);
            Collider2D target = CreateObject("WP-007 Confirmed Target")
                .AddComponent<BoxCollider2D>();
            StableId targetId = StableId.Parse("enemy.wp007-confirmed-target");
            Assert.That(
                hitAdapter.RegisterTarget(target, targetId),
                Is.EqualTo(CombatHit2DTargetRegistrationStatus.Registered));

            object adapter = CreateExecutionAdapter(
                hitAdapter,
                false,
                out Component ignoredTemplate);
            Assert.That(
                ExecutePackagePlan(adapter, false, "target-hit").Succeeded,
                Is.True);

            Component projectile = GetProperty<Component>(
                adapter,
                "LastSpawnedProjectile");
            Track(projectile.gameObject);
            InvokePrivate(
                projectile,
                "ProcessCollision",
                target,
                new Vector2[0],
                Vector2.zero);

            Assert.That(GetProperty<int>(projectile, "WallBounceCount"), Is.Zero);
            Assert.That(GetProperty<bool>(projectile, "IsComplete"), Is.True);
            Assert.That(
                EnumName(projectile, "CompletionReason"),
                Is.EqualTo("ConfirmedHit"));
            Assert.That(hitAdapter.ProcessedEventCount, Is.EqualTo(1));
            CombatHit2DTranslationResult translation =
                GetProperty<CombatHit2DTranslationResult>(
                    projectile,
                    "LastHitTranslation");
            Assert.That(
                translation.Status,
                Is.EqualTo(CombatHit2DTranslationStatus.Confirmed));
            Assert.That(translation.Message.TargetId, Is.EqualTo(targetId));
            TestContext.WriteLine(
                "target-contact confirmed=true reflected=false bounce-count=0 terminated=true");
        }

        [UnityTest]
        public IEnumerator LifetimeExpiry_ReleasesProjectileWithoutPersistence()
        {
            CombatHit2DAdapter hitAdapter = new CombatHit2DAdapter(SourceId);
            object adapter = CreateExecutionAdapter(
                hitAdapter,
                false,
                out Component ignoredTemplate);
            IWeaponFireExecutionOperation operation = CreateOperation(
                "operation.wp007-short-lifetime",
                1d,
                0.02d,
                0.1d);
            WeaponMount2DExecutionResult result = ExecuteOperation(
                adapter,
                operation,
                "lifetime");
            Assert.That(result.Succeeded, Is.True);

            yield return new WaitForSeconds(0.15f);

            Assert.That(GetProperty<int>(adapter, "ActiveProjectileCount"), Is.Zero);
            Assert.That(
                GetProperty<Component>(adapter, "LastSpawnedProjectile"),
                Is.Null);
            Assert.That(hitAdapter.ProcessedEventCount, Is.Zero);
            TestContext.WriteLine(
                "lifetime-expiry lifetime=0.02 active=0 persistent=false");
        }

        [Test]
        public void PowerFallback_UsesNormalNumericTuningAndFixedBounceCap()
        {
            WeaponRuntimeProfile normalProfile =
                (WeaponRuntimeProfile)InvokeStatic(
                    RuntimeTypes.Package,
                    "CreateRuntimeProfile",
                    false);
            WeaponPowerBankState emptyBank =
                WeaponPowerBankState.FromProfile(normalProfile, 0d);
            WeaponPowerFireDecision decision =
                WeaponPowerBankPolicy.ResolveFire(emptyBank, true, true);

            Assert.That(
                decision.Kind,
                Is.EqualTo(
                    WeaponPowerFireDecisionKind.NormalFallbackPowerUnavailable));
            Assert.That(decision.FiresNormally, Is.True);
            Assert.That(decision.FiresEmpowered, Is.False);

            WeaponFireExecutionPlan plan = BuildPackagePlan(
                decision.FiresEmpowered,
                "power-fallback");
            object operation = plan.GetOperation(0).Operation;
            Assert.That(
                GetProperty<double>(operation, "ProjectileSpeed"),
                Is.EqualTo(15d));
            Assert.That(
                GetProperty<double>(operation, "ProjectileLifetimeSeconds"),
                Is.EqualTo(8d));
            Assert.That(
                GetStaticProperty<int>(
                    RuntimeTypes.Package,
                    "MaximumWallBounces"),
                Is.EqualTo(2));

            TestContext.WriteLine(
                "power-fallback requested=empowered available=0 decision=normal speed=15 lifetime=8 max-bounces=2");
        }

        [UnityTest]
        public IEnumerator Restart_FiftyCyclesLeaveNoProjectileOrCallbackState()
        {
            CombatHit2DAdapter hitAdapter = new CombatHit2DAdapter(SourceId);
            object adapter = CreateExecutionAdapter(
                hitAdapter,
                false,
                out Component template);
            WeaponMount2DAdapter mount = CreateMount(adapter);

            for (int cycle = 0; cycle < 50; cycle++)
            {
                WeaponMount2DExecutionResult result = mount.ExecutePlan(
                    BuildPackagePlan(
                        cycle % 2 == 1,
                        "restart-" + cycle.ToString("D2")));
                Assert.That(result.Succeeded, Is.True, "cycle " + cycle);
                Assert.That(
                    GetProperty<int>(adapter, "ActiveProjectileCount"),
                    Is.EqualTo(1));

                Invoke(adapter, "ResetSession");
                Assert.That(
                    GetProperty<int>(adapter, "ActiveProjectileCount"),
                    Is.Zero);
                Assert.That(
                    GetProperty<Component>(adapter, "LastSpawnedProjectile"),
                    Is.Null);
                yield return null;
            }

            Component[] liveProjectiles = Resources
                .FindObjectsOfTypeAll(RuntimeTypes.Projectile)
                .OfType<Component>()
                .Where(component =>
                    component != template
                    && component.gameObject.scene.IsValid())
                .ToArray();
            Assert.That(liveProjectiles, Is.Empty);
            Assert.That(hitAdapter.ProcessedEventCount, Is.Zero);
            TestContext.WriteLine(
                "restart cycles=50 active=0 live-clones=0 processed-hits=0 stale-callbacks=0");
        }

        [Test]
        public void RuntimeAndPrefabSurface_Are2DOnlyFiniteAndPackageOwned()
        {
            string runtimeSource = string.Join(
                "\n",
                Directory.GetFiles(ProjectPath(PackageRoot + "Runtime"), "*.cs")
                    .OrderBy(path => path, StringComparer.Ordinal)
                    .Select(File.ReadAllText));
            string prefab = File.ReadAllText(ProjectPath(PrefabPath));

            string[] forbiddenTokens =
            {
                "MovementActor",
                "Rigidbody3D",
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
                "TargetRicochet",
                "MaximumWallBounces = 3",
                "InfiniteLifetime",
            };
            foreach (string token in forbiddenTokens)
            {
                Assert.That(
                    runtimeSource,
                    Does.Not.Contain(token),
                    "Forbidden token: " + token);
            }

            string[] forbiddenThreeDimensionalPatterns =
            {
                @"\bRigidbody\b",
                @"\bCollider\b",
                @"\bCollision\b",
                @"\bPhysics\.",
            };
            foreach (string pattern in forbiddenThreeDimensionalPatterns)
            {
                Assert.That(
                    Regex.IsMatch(runtimeSource, pattern),
                    Is.False,
                    "Forbidden 3D type pattern: " + pattern);
            }

            Assert.That(runtimeSource, Does.Contain("RicochetWall2D"));
            Assert.That(runtimeSource, Does.Contain("MaximumWallBounces = 2"));
            Assert.That(runtimeSource, Does.Contain("MaximumLifetimeSeconds = 30d"));
            Assert.That(runtimeSource, Does.Contain("Physics2D.IgnoreCollision"));
            Assert.That(prefab, Does.Contain("Rigidbody2D:"));
            Assert.That(prefab, Does.Contain("CircleCollider2D:"));
            Assert.That(prefab, Does.Contain("m_IsTrigger: 0"));
            Assert.That(prefab, Does.Contain("m_GravityScale: 0"));
            Assert.That(prefab, Does.Contain("RicochetProjectile2D"));
            Assert.That(prefab, Does.Not.Contain("Rigidbody:"));
            Assert.That(prefab, Does.Not.Contain("BoxCollider:"));
            Assert.That(prefab, Does.Not.Contain("MeshCollider:"));
            Assert.That(prefab, Does.Not.Contain("Camera:"));

            TestContext.WriteLine(
                "surface package-owned=true physics=2d max-bounces=2 max-lifetime=30 prefab-trigger=false final-art=false");
        }

        private object CreateExecutionAdapter(
            CombatHit2DAdapter hitAdapter,
            bool enablePresentation,
            out Component template)
        {
            template = CreateProjectileTemplate();
            Collider2D ownerCollider = CreateObject("WP-007 Explicit Owner")
                .AddComponent<BoxCollider2D>();
            ConstructorInfo constructor = RuntimeTypes.ExecutionAdapter.GetConstructors()
                .Single(candidate => candidate.GetParameters().Length == 6);
            object adapter = constructor.Invoke(
                new object[]
                {
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
            GameObject root = CreateObject("WP-007 Ricochet Projectile Template");
            Rigidbody2D body = root.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.constraints = RigidbodyConstraints2D.FreezeRotation;
            CircleCollider2D collider = root.AddComponent<CircleCollider2D>();
            collider.isTrigger = false;

            GameObject presentationObject = new GameObject("TemporaryHitPresentation");
            createdObjects.Add(presentationObject);
            presentationObject.transform.SetParent(root.transform, false);
            Component presentation = presentationObject.AddComponent(
                RuntimeTypes.Presentation);
            Component projectile = root.AddComponent(RuntimeTypes.Projectile);
            SetField(projectile, "body", body);
            SetField(projectile, "projectileCollider", collider);
            SetField(projectile, "temporaryHitPresentation", presentation);
            root.SetActive(false);
            return projectile;
        }

        private Collider2D CreateValidWall(string name)
        {
            GameObject wallObject = CreateObject(name);
            BoxCollider2D collider = wallObject.AddComponent<BoxCollider2D>();
            Component marker = wallObject.AddComponent(RuntimeTypes.Wall);
            Assert.That((bool)Invoke(marker, "TryConfigure", collider), Is.True);
            return collider;
        }

        private WeaponMount2DExecutionResult ExecutePackagePlan(
            object adapter,
            bool empowered,
            string eventSuffix)
        {
            return CreateMount(adapter).ExecutePlan(
                BuildPackagePlan(empowered, eventSuffix));
        }

        private WeaponMount2DExecutionResult ExecuteOperation(
            object adapter,
            IWeaponFireExecutionOperation operation,
            string eventSuffix)
        {
            return CreateMount(adapter).ExecutePlan(
                BuildSyntheticPlan(operation, eventSuffix));
        }

        private WeaponMount2DAdapter CreateMount(object adapter)
        {
            GameObject mountObject = CreateObject("WP-007 Weapon Mount 2D Adapter");
            WeaponMount2DAdapter mount =
                mountObject.AddComponent<WeaponMount2DAdapter>();
            mount.Configure(
                SourceId,
                WeaponId,
                MountId,
                new[] { (IWeaponFireExecutionOperation2DHandler)adapter });
            return mount;
        }

        private static WeaponFireExecutionPlan BuildPackagePlan(
            bool empowered,
            string eventSuffix)
        {
            IWeaponBehaviorModule module =
                (IWeaponBehaviorModule)InvokeStatic(
                    RuntimeTypes.Package,
                    "CreateBehaviorModule");
            WeaponRuntimeProfile profile =
                (WeaponRuntimeProfile)InvokeStatic(
                    RuntimeTypes.Package,
                    "CreateRuntimeProfile",
                    empowered);
            WeaponBehaviorPipeline pipeline =
                new WeaponBehaviorPipeline(new[] { module });
            return pipeline.BuildExecutionPlan(
                CreateInput(profile, empowered, eventSuffix));
        }

        private static WeaponFireExecutionPlan BuildSyntheticPlan(
            IWeaponFireExecutionOperation operation,
            string eventSuffix)
        {
            WeaponRuntimeProfile profile = BuildSyntheticProfile();
            SyntheticModule module = new SyntheticModule(
                SyntheticModuleId,
                operation);
            WeaponBehaviorPipeline pipeline =
                new WeaponBehaviorPipeline(
                    new IWeaponBehaviorModule[] { module });
            return pipeline.BuildExecutionPlan(
                CreateInput(profile, false, eventSuffix));
        }

        private static WeaponBehaviorInput CreateInput(
            WeaponRuntimeProfile profile,
            bool empowered,
            string eventSuffix)
        {
            return new WeaponBehaviorInput(
                StableId.Parse("combat-event.wp007-" + eventSuffix),
                WeaponId,
                MountId,
                1L,
                profile,
                empowered,
                0d,
                0d,
                1d,
                0d,
                1d);
        }

        private static WeaponRuntimeProfile BuildSyntheticProfile()
        {
            StableId[] moduleIds = { SyntheticModuleId };
            return WeaponRuntimeProfile.Create(
                WeaponRuntimeProfile.CurrentProfileVersion,
                StableId.Parse("weapon-profile.wp007-synthetic"),
                0.5d,
                1,
                0d,
                0.1d,
                WeaponCycleMode.None,
                0d,
                0d,
                0d,
                0d,
                true,
                10d,
                2d,
                0d,
                moduleIds,
                moduleIds,
                1);
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

        private static string ProjectPath(string assetPath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            return Path.Combine(
                projectRoot,
                assetPath.Replace('/', Path.DirectorySeparatorChar));
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

        private static string EnumName(object instance, string propertyName)
        {
            return GetProperty<object>(instance, propertyName).ToString();
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
            MethodInfo method = type.GetMethods(
                    BindingFlags.Static | BindingFlags.Public)
                .Single(candidate =>
                    candidate.Name == methodName
                    && candidate.GetParameters().Length == arguments.Length);
            return method.Invoke(null, arguments);
        }

        private static void SetField(object instance, string fieldName, object value)
        {
            FieldInfo field = instance.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            field.SetValue(instance, value);
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

            public WeaponBehaviorModulePlan BuildExecutionPlan(
                WeaponBehaviorInput input)
            {
                return new WeaponBehaviorModulePlan(ModuleId, operation);
            }
        }

        private static class RuntimeTypes
        {
            public static readonly Type Package = Find(
                "ShooterMover.ContentPackages.Weapons.RicochetGun.Runtime.RicochetGunPackage");
            public static readonly Type Operation = Find(
                "ShooterMover.ContentPackages.Weapons.RicochetGun.Runtime.RicochetProjectileExecutionOperation");
            public static readonly Type Projectile = Find(
                "ShooterMover.ContentPackages.Weapons.RicochetGun.Runtime.RicochetProjectile2D");
            public static readonly Type ExecutionAdapter = Find(
                "ShooterMover.ContentPackages.Weapons.RicochetGun.Runtime.RicochetProjectileExecutionAdapter");
            public static readonly Type Wall = Find(
                "ShooterMover.ContentPackages.Weapons.RicochetGun.Runtime.RicochetWall2D");
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
