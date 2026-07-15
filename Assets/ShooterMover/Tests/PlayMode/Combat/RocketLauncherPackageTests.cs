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
    public sealed class RocketLauncherPackageTests
    {
        private const string PackagePath =
            "Assets/ShooterMover/ContentPackages/Weapons/RocketLauncher/Runtime/RocketLauncherPackage.cs";
        private const string AdapterPath =
            "Assets/ShooterMover/ContentPackages/Weapons/RocketLauncher/Runtime/RocketLauncherExecutionPlanAdapter.cs";
        private const string WarningPath =
            "Assets/ShooterMover/ContentPackages/Weapons/RocketLauncher/Presentation/RocketImpactWarning2D.cs";
        private const string PrefabPath =
            "Assets/ShooterMover/ContentPackages/Weapons/RocketLauncher/Prefabs/RocketLauncherProjectile2D.prefab";
        private const string ContractPath =
            "Assets/ShooterMover/ContentPackages/Weapons/RocketLauncher/ROCKET_LAUNCHER_PACKAGE_V1.md";

        private static readonly StableId SourceId =
            StableId.Parse("actor.wp005-source");
        private static readonly StableId MountId =
            StableId.Parse("weapon-mount.wp005-fixture");

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
        public void ImpactOrAuthoredExpiry_ProducesExactlyOneDetonation()
        {
            Collider2D impactTarget = CreateBoxTarget(
                "WP-005 Single Detonation Target",
                Vector2.zero,
                new Vector2(0.2f, 0.2f));
            StableId targetId = StableId.Parse("enemy.wp005-single");
            CombatHit2DAdapter impactHitAdapter = new CombatHit2DAdapter(SourceId);
            Assert.That(
                impactHitAdapter.RegisterTarget(impactTarget, targetId),
                Is.EqualTo(CombatHit2DTargetRegistrationStatus.Registered));

            object impactAdapter = CreateExecutionAdapter(
                impactHitAdapter,
                new[] { CreateTargetBinding(impactTarget, targetId) },
                null,
                false,
                out Component impactTemplate);
            ExecuteNormal(impactAdapter, "single-impact");
            Component impactRocket = GetProperty<Component>(
                impactAdapter,
                "LastSpawnedRocket");
            Track(impactRocket.gameObject);
            Component sharedProjectile = GetProperty<Component>(
                impactRocket,
                "Projectile");
            Collider2D obstacle = CreateBoxTarget(
                "WP-005 Impact Obstacle",
                Vector2.zero,
                new Vector2(0.1f, 0.1f));

            InvokePrivate(sharedProjectile, "OnTriggerEnter2D", obstacle);
            InvokePrivate(sharedProjectile, "OnTriggerEnter2D", obstacle);
            Invoke(impactRocket, "RequestAuthoredExpiry");
            InvokePrivate(impactRocket, "LateUpdate");

            Assert.That(GetProperty<int>(impactAdapter, "DetonationCount"), Is.EqualTo(1));
            Assert.That(GetProperty<int>(impactAdapter, "ActiveRocketCount"), Is.Zero);
            Assert.That(GetProperty<int>(impactRocket, "DetonationRequestCount"), Is.EqualTo(1));
            object impactResult = GetProperty<object>(
                impactAdapter,
                "LastDetonationResult");
            Assert.That(GetProperty<object>(impactResult, "Reason").ToString(), Is.EqualTo("Impact"));
            Assert.That(GetResultTargetIds(impactResult), Is.EqualTo(new[] { targetId }));
            Assert.That(impactHitAdapter.ProcessedEventCount, Is.EqualTo(1));

            CombatHit2DAdapter expiryHitAdapter = new CombatHit2DAdapter(SourceId);
            object expiryAdapter = CreateExecutionAdapter(
                expiryHitAdapter,
                Array.Empty<object>(),
                null,
                false,
                out Component expiryTemplate);
            ExecuteNormal(expiryAdapter, "single-expiry");
            Component expiryRocket = GetProperty<Component>(
                expiryAdapter,
                "LastSpawnedRocket");
            Track(expiryRocket.gameObject);

            Invoke(expiryRocket, "RequestAuthoredExpiry");
            InvokePrivate(expiryRocket, "LateUpdate");
            InvokePrivate(expiryRocket, "LateUpdate");

            Assert.That(GetProperty<int>(expiryAdapter, "DetonationCount"), Is.EqualTo(1));
            Assert.That(GetProperty<int>(expiryRocket, "DetonationRequestCount"), Is.EqualTo(1));
            object expiryResult = GetProperty<object>(
                expiryAdapter,
                "LastDetonationResult");
            Assert.That(
                GetProperty<object>(expiryResult, "Reason").ToString(),
                Is.EqualTo("AuthoredExpiry"));
            Assert.That(GetResultTargetIds(expiryResult), Is.Empty);
            Assert.That(expiryHitAdapter.ProcessedEventCount, Is.Zero);
            Assert.That(impactTemplate.gameObject.activeSelf, Is.False);
            Assert.That(expiryTemplate.gameObject.activeSelf, Is.False);

            TestContext.WriteLine(
                "single-detonation impact_requests=1 impact_hits=1 expiry_requests=1 expiry_hits=0");
        }

        [Test]
        public void AreaBoundary_IncludesExactBoundaryExcludesOutsideAndOwner()
        {
            const float radius = 2f;
            Collider2D inside = CreateBoxTarget(
                "WP-005 AOE Inside",
                new Vector2(0.5f, 0f),
                new Vector2(0.2f, 0.2f));
            Collider2D boundary = CreateBoxTarget(
                "WP-005 AOE Boundary",
                new Vector2(2.1f, 0f),
                new Vector2(0.2f, 0.2f));
            Collider2D outside = CreateBoxTarget(
                "WP-005 AOE Outside",
                new Vector2(2.25f, 0f),
                new Vector2(0.2f, 0.2f));
            Collider2D owner = CreateBoxTarget(
                "WP-005 AOE Owner",
                new Vector2(0.25f, 0f),
                new Vector2(0.2f, 0.2f));

            StableId insideId = StableId.Parse("enemy.wp005-inside");
            StableId boundaryId = StableId.Parse("enemy.wp005-boundary");
            StableId outsideId = StableId.Parse("enemy.wp005-outside");
            StableId ownerId = StableId.Parse("actor.wp005-owner");
            CombatHit2DAdapter hitAdapter = new CombatHit2DAdapter(SourceId);
            Register(hitAdapter, inside, insideId);
            Register(hitAdapter, boundary, boundaryId);
            Register(hitAdapter, outside, outsideId);
            Register(hitAdapter, owner, ownerId);

            object adapter = CreateExecutionAdapter(
                hitAdapter,
                new[]
                {
                    CreateTargetBinding(outside, outsideId),
                    CreateTargetBinding(owner, ownerId),
                    CreateTargetBinding(boundary, boundaryId),
                    CreateTargetBinding(inside, insideId),
                },
                new[] { owner },
                false,
                out Component template);
            ExecuteNormal(adapter, "aoe-boundary");
            Component rocket = GetProperty<Component>(adapter, "LastSpawnedRocket");
            Track(rocket.gameObject);
            TriggerImpact(rocket);

            object result = GetProperty<object>(adapter, "LastDetonationResult");
            StableId[] targetIds = GetResultTargetIds(result);
            Assert.That(
                targetIds,
                Is.EqualTo(new[] { boundaryId, insideId }),
                "Targets are sorted by StableId after the inclusive radius test.");
            Assert.That(GetProperty<double>(result, "Radius"), Is.EqualTo(radius));
            Assert.That(hitAdapter.ProcessedEventCount, Is.EqualTo(2));
            Assert.That(template.gameObject.activeSelf, Is.False);

            TestContext.WriteLine(
                "aoe-boundary radius=2 inside=included exact-boundary=included outside=excluded owner=excluded hits=2");
        }

        [Test]
        public void AreaTargets_AreAlwaysReportedInStableIdentityOrder()
        {
            Collider2D zulu = CreateBoxTarget(
                "WP-005 Stable Zulu",
                new Vector2(0.4f, 0f),
                new Vector2(0.1f, 0.1f));
            Collider2D alpha = CreateBoxTarget(
                "WP-005 Stable Alpha",
                new Vector2(0.3f, 0f),
                new Vector2(0.1f, 0.1f));
            Collider2D mike = CreateBoxTarget(
                "WP-005 Stable Mike",
                new Vector2(0.2f, 0f),
                new Vector2(0.1f, 0.1f));
            StableId zuluId = StableId.Parse("enemy.wp005-zulu");
            StableId alphaId = StableId.Parse("enemy.wp005-alpha");
            StableId mikeId = StableId.Parse("enemy.wp005-mike");

            CombatHit2DAdapter hitAdapter = new CombatHit2DAdapter(SourceId);
            Register(hitAdapter, zulu, zuluId);
            Register(hitAdapter, alpha, alphaId);
            Register(hitAdapter, mike, mikeId);
            object adapter = CreateExecutionAdapter(
                hitAdapter,
                new[]
                {
                    CreateTargetBinding(zulu, zuluId),
                    CreateTargetBinding(alpha, alphaId),
                    CreateTargetBinding(mike, mikeId),
                },
                null,
                false,
                out Component ignoredTemplate);
            ExecuteNormal(adapter, "stable-order");
            Component rocket = GetProperty<Component>(adapter, "LastSpawnedRocket");
            Track(rocket.gameObject);
            TriggerImpact(rocket);

            object result = GetProperty<object>(adapter, "LastDetonationResult");
            Assert.That(
                GetResultTargetIds(result),
                Is.EqualTo(new[] { alphaId, mikeId, zuluId }));
            TestContext.WriteLine(
                "stable-target-order input=zulu,alpha,mike output=alpha,mike,zulu");
        }

        [Test]
        public void ImpactExpiryRace_ImpactWinsAndCannotDetonateAgain()
        {
            object policyReason = InvokeStatic(
                RuntimeTypes.RacePolicy,
                "Resolve",
                true,
                true);
            Assert.That(policyReason.ToString(), Is.EqualTo("Impact"));

            CombatHit2DAdapter hitAdapter = new CombatHit2DAdapter(SourceId);
            object adapter = CreateExecutionAdapter(
                hitAdapter,
                Array.Empty<object>(),
                null,
                false,
                out Component ignoredTemplate);
            ExecuteNormal(adapter, "impact-expiry-race");
            Component rocket = GetProperty<Component>(adapter, "LastSpawnedRocket");
            Track(rocket.gameObject);

            Invoke(rocket, "RequestAuthoredExpiry");
            TriggerImpact(rocket);
            InvokePrivate(rocket, "LateUpdate");

            object result = GetProperty<object>(adapter, "LastDetonationResult");
            Assert.That(GetProperty<object>(result, "Reason").ToString(), Is.EqualTo("Impact"));
            Assert.That(GetProperty<int>(adapter, "DetonationCount"), Is.EqualTo(1));
            Assert.That(GetProperty<int>(rocket, "DetonationRequestCount"), Is.EqualTo(1));
            TestContext.WriteLine(
                "impact-expiry-race impact=true expiry=true winner=Impact detonations=1");
        }

        [Test]
        public void EmptyPowerBank_FallsBackToUnlimitedNormalNumericTopology()
        {
            WeaponRuntimeProfile normalProfile = GetStaticProperty<WeaponRuntimeProfile>(
                RuntimeTypes.Package,
                "NormalRuntimeProfile");
            ShooterMover.Domain.Combat.WeaponPowerBankState emptyBank =
                ShooterMover.Domain.Combat.WeaponPowerBankState.FromProfile(normalProfile, 0d);
            WeaponPowerFireDecision decision = WeaponPowerBankPolicy.ResolveFire(
                emptyBank,
                true,
                true);

            Assert.That(
                decision.Kind,
                Is.EqualTo(
                    WeaponPowerFireDecisionKind.NormalFallbackPowerUnavailable));
            Assert.That(decision.FiresNormally, Is.True);
            Assert.That(decision.FiresEmpowered, Is.False);
            Assert.That(decision.SpentUnits, Is.Zero);

            IWeaponBehaviorModule module =
                (IWeaponBehaviorModule)InvokeStatic(
                    RuntimeTypes.Package,
                    "CreateBehaviorModule");
            WeaponFireExecutionPlan plan = BuildPlan(
                module,
                normalProfile,
                decision.FiresEmpowered,
                "power-fallback");
            object operation = plan.GetOperation(0).Operation;
            object normalTuning = GetStaticProperty<object>(
                RuntimeTypes.Package,
                "NormalTuning");
            Assert.That(
                GetProperty<double>(operation, "Damage"),
                Is.EqualTo(GetProperty<double>(normalTuning, "Damage")));
            Assert.That(
                GetProperty<double>(operation, "ProjectileSpeed"),
                Is.EqualTo(GetProperty<double>(normalTuning, "ProjectileSpeed")));
            Assert.That(
                GetProperty<double>(operation, "AreaRadius"),
                Is.EqualTo(GetProperty<double>(normalTuning, "AreaRadius")));

            object descriptor = GetStaticProperty<object>(
                RuntimeTypes.Package,
                "Descriptor");
            object normalFire = GetProperty<object>(descriptor, "NormalFire");
            object topology = GetProperty<object>(normalFire, "Topology");
            Assert.That(
                GetProperty<bool>(normalFire, "ConsumesConsumableAmmunition"),
                Is.False);
            Assert.That(GetProperty<int>(topology, "DetonationCount"), Is.EqualTo(1));
            Assert.That(GetProperty<bool>(topology, "HasFragmentation"), Is.False);

            TestContext.WriteLine(
                "power-fallback requested=empowered available=0 mode=normal spent=0 consumable-ammo=false topology-detonations=1");
        }

        [UnityTest]
        public IEnumerator RapidRestart_FiftyCyclesLeaveNoRocketOrCallbackState()
        {
            CombatHit2DAdapter hitAdapter = new CombatHit2DAdapter(SourceId);
            object adapter = CreateExecutionAdapter(
                hitAdapter,
                Array.Empty<object>(),
                null,
                false,
                out Component template);

            for (int cycle = 0; cycle < 50; cycle++)
            {
                ExecuteNormal(adapter, "restart-" + cycle.ToString("D2"));
                Component rocket = GetProperty<Component>(
                    adapter,
                    "LastSpawnedRocket");
                Track(rocket.gameObject);
                Assert.That(
                    GetProperty<int>(adapter, "ActiveRocketCount"),
                    Is.EqualTo(1),
                    "cycle " + cycle);

                Invoke(adapter, "ResetSession");
                Assert.That(GetProperty<int>(adapter, "ActiveRocketCount"), Is.Zero);
                Assert.That(
                    GetProperty<Component>(adapter, "LastSpawnedRocket"),
                    Is.Null);
                Assert.That(
                    GetProperty<object>(adapter, "LastDetonationResult"),
                    Is.Null);
                yield return null;
            }

            Component[] liveRockets = Resources.FindObjectsOfTypeAll(RuntimeTypes.Driver)
                .OfType<Component>()
                .Where(component =>
                    component != template && component.gameObject.scene.IsValid())
                .ToArray();
            Assert.That(liveRockets, Is.Empty);
            Assert.That(hitAdapter.ProcessedEventCount, Is.Zero);
            TestContext.WriteLine(
                "restart cycles=50 active=0 live-clones=0 detonations=0 processed-hits=0");
        }

        [Test]
        public void PackageSurface_IsBounded2DAndWarningCannotObstructScreen()
        {
            string packageSource = ReadProjectFile(PackagePath);
            string adapterSource = ReadProjectFile(AdapterPath);
            string warningSource = ReadProjectFile(WarningPath);
            string runtimeSource = packageSource + "\n" + adapterSource + "\n" + warningSource;
            string prefab = ReadProjectFile(PrefabPath);
            string contract = ReadProjectFile(ContractPath);

            string[] forbiddenRuntimeTokens =
            {
                "Physics.Raycast",
                "Physics.SphereCast",
                "Rigidbody ",
                "Collider collider",
                "Camera.main",
                "GameObject.Find",
                "FindObject",
                "Resources.Load",
                "ServiceLocator",
                "Singleton",
                "NavMesh",
                "Homing",
                "ClusterRocket",
                "PersistentFire",
                "Instantiate(fragment",
            };
            foreach (string token in forbiddenRuntimeTokens)
            {
                Assert.That(runtimeSource, Does.Not.Contain(token), token);
            }

            Assert.That(adapterSource, Does.Contain("MaximumAreaTargetCount = 64"));
            Assert.That(adapterSource, Does.Contain("MaximumAreaRadius = 10f"));
            Assert.That(adapterSource, Does.Contain("CopyAndValidateTargets"));
            Assert.That(prefab, Does.Contain("Rigidbody2D:"));
            Assert.That(prefab, Does.Contain("CircleCollider2D:"));
            Assert.That(prefab, Does.Contain("m_IsTrigger: 1"));
            Assert.That(prefab, Does.Contain("m_GravityScale: 0"));
            Assert.That(prefab, Does.Not.Contain("SpriteRenderer:"));
            Assert.That(prefab, Does.Not.Contain("Canvas:"));
            Assert.That(prefab, Does.Not.Contain("Camera:"));
            Assert.That(warningSource, Does.Not.Contain("Renderer"));
            Assert.That(warningSource, Does.Not.Contain("Canvas"));
            Assert.That(warningSource, Does.Not.Contain("Screen."));
            Assert.That(
                contract,
                Does.Contain("Manual readability note"));
            Assert.That(
                contract,
                Does.Contain("does not obstruct the screen"));

            TestContext.WriteLine(
                "readability warning=bounded-data-only lifetime<=0.5 renderer=false canvas=false screen-obstruction=false manual-playable=pending");
        }

        private object CreateExecutionAdapter(
            CombatHit2DAdapter hitAdapter,
            object[] targetBindings,
            Collider2D[] ownerColliders,
            bool enableWarning,
            out Component template)
        {
            template = CreateRocketTemplate();
            Array bindings = Array.CreateInstance(
                RuntimeTypes.TargetBinding,
                targetBindings.Length);
            for (int index = 0; index < targetBindings.Length; index++)
            {
                bindings.SetValue(targetBindings[index], index);
            }

            ConstructorInfo constructor = RuntimeTypes.Adapter.GetConstructors()
                .Single(candidate => candidate.GetParameters().Length == 8);
            object adapter = constructor.Invoke(
                new object[]
                {
                    GetStaticField<StableId>(
                        RuntimeTypes.Package,
                        "OperationKindId"),
                    template,
                    hitAdapter,
                    bindings,
                    ownerColliders,
                    null,
                    enableWarning,
                    0.12f,
                });
            createdAdapters.Add((IDisposable)adapter);
            return adapter;
        }

        private Component CreateRocketTemplate()
        {
            GameObject root = CreateObject("WP-005 Rocket Template");
            Rigidbody2D body = root.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            CircleCollider2D collider = root.AddComponent<CircleCollider2D>();
            collider.isTrigger = true;

            Component sharedProjectile = root.AddComponent(RuntimeTypes.SharedProjectile);
            SetField(sharedProjectile, "body", body);
            SetField(sharedProjectile, "projectileCollider", collider);
            SetField(sharedProjectile, "temporaryHitPresentation", null);

            GameObject warningObject = new GameObject("RocketImpactWarning");
            warningObject.transform.SetParent(root.transform, false);
            createdObjects.Add(warningObject);
            Component warning = warningObject.AddComponent(RuntimeTypes.Warning);
            Component driver = root.AddComponent(RuntimeTypes.Driver);
            SetField(driver, "projectile", sharedProjectile);
            SetField(driver, "impactWarning", warning);
            root.SetActive(false);
            return driver;
        }

        private void ExecuteNormal(object adapter, string suffix)
        {
            IWeaponBehaviorModule module =
                (IWeaponBehaviorModule)InvokeStatic(
                    RuntimeTypes.Package,
                    "CreateBehaviorModule");
            WeaponRuntimeProfile profile = GetStaticProperty<WeaponRuntimeProfile>(
                RuntimeTypes.Package,
                "NormalRuntimeProfile");
            WeaponFireExecutionPlan plan = BuildPlan(
                module,
                profile,
                false,
                suffix);
            WeaponMount2DExecutionResult result =
                CreateMount(adapter).ExecutePlan(plan);
            Assert.That(
                result.Status,
                Is.EqualTo(WeaponMount2DExecutionStatus.Executed),
                suffix);
            Assert.That(result.ExecutedOperationCount, Is.EqualTo(1));
        }

        private WeaponMount2DAdapter CreateMount(object adapter)
        {
            GameObject mountObject = CreateObject(
                "WP-005 Weapon Mount 2D Adapter");
            WeaponMount2DAdapter mount =
                mountObject.AddComponent<WeaponMount2DAdapter>();
            mount.Configure(
                SourceId,
                GetStaticField<StableId>(RuntimeTypes.Package, "WeaponId"),
                MountId,
                new[]
                {
                    (IWeaponFireExecutionOperation2DHandler)adapter,
                });
            return mount;
        }

        private static WeaponFireExecutionPlan BuildPlan(
            IWeaponBehaviorModule module,
            WeaponRuntimeProfile profile,
            bool isEmpowered,
            string suffix)
        {
            WeaponBehaviorPipeline pipeline =
                new WeaponBehaviorPipeline(new[] { module });
            WeaponBehaviorInput input = new WeaponBehaviorInput(
                StableId.Parse("combat-event.wp005-" + suffix),
                GetStaticField<StableId>(RuntimeTypes.Package, "WeaponId"),
                MountId,
                1L,
                profile,
                isEmpowered,
                0d,
                0d,
                1d,
                0d,
                1d);
            return pipeline.BuildExecutionPlan(input);
        }

        private object CreateTargetBinding(
            Collider2D collider,
            StableId targetId)
        {
            return Activator.CreateInstance(
                RuntimeTypes.TargetBinding,
                collider,
                targetId);
        }

        private void TriggerImpact(Component rocket)
        {
            Component sharedProjectile = GetProperty<Component>(
                rocket,
                "Projectile");
            Collider2D obstacle = CreateBoxTarget(
                "WP-005 Trigger Obstacle",
                Vector2.zero,
                new Vector2(0.05f, 0.05f));
            InvokePrivate(sharedProjectile, "OnTriggerEnter2D", obstacle);
        }

        private static StableId[] GetResultTargetIds(object result)
        {
            return ((IEnumerable)GetProperty<object>(result, "Targets"))
                .Cast<object>()
                .Select(target => GetProperty<StableId>(target, "TargetId"))
                .ToArray();
        }

        private static void Register(
            CombatHit2DAdapter hitAdapter,
            Collider2D collider,
            StableId targetId)
        {
            Assert.That(
                hitAdapter.RegisterTarget(collider, targetId),
                Is.EqualTo(CombatHit2DTargetRegistrationStatus.Registered));
        }

        private Collider2D CreateBoxTarget(
            string name,
            Vector2 position,
            Vector2 size)
        {
            GameObject target = CreateObject(name);
            target.transform.position = position;
            BoxCollider2D collider = target.AddComponent<BoxCollider2D>();
            collider.size = size;
            return collider;
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

        private static T GetStaticProperty<T>(
            Type type,
            string propertyName)
        {
            PropertyInfo property = type.GetProperty(
                propertyName,
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(property, Is.Not.Null, propertyName);
            return (T)property.GetValue(null, null);
        }

        private static T GetStaticField<T>(
            Type type,
            string fieldName)
        {
            FieldInfo field = type.GetField(
                fieldName,
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(field, Is.Not.Null, fieldName);
            return (T)field.GetValue(null);
        }

        private static T GetProperty<T>(
            object instance,
            string propertyName)
        {
            PropertyInfo property = instance.GetType().GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(property, Is.Not.Null, propertyName);
            return (T)property.GetValue(instance, null);
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
            return InvokeMethod(method, instance, arguments);
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
            return InvokeMethod(method, instance, arguments);
        }

        private static object InvokeStatic(
            Type type,
            string methodName,
            params object[] arguments)
        {
            MethodInfo[] matches = type.GetMethods(
                    BindingFlags.Public | BindingFlags.Static)
                .Where(method =>
                    string.Equals(
                        method.Name,
                        methodName,
                        StringComparison.Ordinal)
                    && method.GetParameters().Length == arguments.Length)
                .ToArray();
            Assert.That(
                matches.Length,
                Is.EqualTo(1),
                type.FullName + "." + methodName);
            return InvokeMethod(matches[0], null, arguments);
        }

        private static object InvokeMethod(
            MethodInfo method,
            object instance,
            object[] arguments)
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
            string projectRoot =
                Directory.GetParent(UnityEngine.Application.dataPath).FullName;
            return File.ReadAllText(
                Path.Combine(
                    projectRoot,
                    assetPath.Replace('/', Path.DirectorySeparatorChar)));
        }

        private static class RuntimeTypes
        {
            public static readonly Type Package = Find(
                "ShooterMover.ContentPackages.Weapons.RocketLauncher.Runtime.RocketLauncherPackage");
            public static readonly Type Adapter = Find(
                "ShooterMover.ContentPackages.Weapons.RocketLauncher.Runtime.RocketLauncherExecutionPlanAdapter");
            public static readonly Type Driver = Find(
                "ShooterMover.ContentPackages.Weapons.RocketLauncher.Runtime.RocketProjectileDetonationDriver2D");
            public static readonly Type TargetBinding = Find(
                "ShooterMover.ContentPackages.Weapons.RocketLauncher.Runtime.RocketAreaTarget2D");
            public static readonly Type RacePolicy = Find(
                "ShooterMover.ContentPackages.Weapons.RocketLauncher.Runtime.RocketDetonationRacePolicy");
            public static readonly Type Warning = Find(
                "ShooterMover.ContentPackages.Weapons.RocketLauncher.Presentation.RocketImpactWarning2D");
            public static readonly Type SharedProjectile = Find(
                "ShooterMover.ContentPackages.Weapons.Shared.Runtime.BoundedProjectile2D");

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
