#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShooterMover.Domain.Common;
using ShooterMover.UnityAdapters.Authoring;
using ShooterMover.UnityAdapters.Combat;
using ShooterMover.UnityAdapters.Enemies;
using UnityEngine;

namespace ShooterMover.Tests.PlayMode.Enemies
{
    /// <summary>
    /// Package scripts live in predefined Assembly-CSharp, so this fixture uses
    /// reflection instead of directly referencing package types from the test asmdef.
    /// </summary>
    public sealed class BlasterTurretPackageTests
    {
        private static readonly Type DefinitionType = Find(
            "ShooterMover.ContentPackages.Enemies.BlasterTurret.BlasterTurretDefinition");
        private static readonly Type PackageType = Find(
            "ShooterMover.ContentPackages.Enemies.BlasterTurret.BlasterTurretPackage");
        private static readonly Type CadenceType = Find(
            "ShooterMover.ContentPackages.Enemies.BlasterTurret.BlasterTurretCadence");
        private static readonly Type FacingRulesType = Find(
            "ShooterMover.ContentPackages.Enemies.BlasterTurret.BlasterTurretFacingRules");
        private static readonly Type AuthoringType = Find(
            "ShooterMover.ContentPackages.Enemies.BlasterTurret.BlasterTurretAuthoring2D");
        private static readonly Type SceneContextType = Find(
            "ShooterMover.ContentPackages.Enemies.BlasterTurret.BlasterTurretSceneContext2D");

        private readonly List<UnityEngine.Object> runtimeObjects =
            new List<UnityEngine.Object>();

        private sealed class ScopeHarness
        {
            public ScopeHarness(
                GameObject root,
                GameplaySceneScope2D scope,
                Component context)
            {
                Root = root;
                Scope = scope;
                Context = context;
            }

            public GameObject Root { get; }

            public GameplaySceneScope2D Scope { get; }

            public Component Context { get; }
        }

        [TearDown]
        public void TearDown()
        {
            for (int index = runtimeObjects.Count - 1; index >= 0; index--)
            {
                UnityEngine.Object value = runtimeObjects[index];
                if (value != null)
                {
                    UnityEngine.Object.DestroyImmediate(value);
                }
            }

            runtimeObjects.Clear();
        }

        [Test]
        public void PackageTypes_ArePresentInPredefinedAssembly()
        {
            Assert.That(DefinitionType, Is.Not.Null);
            Assert.That(PackageType, Is.Not.Null);
            Assert.That(CadenceType, Is.Not.Null);
            Assert.That(FacingRulesType, Is.Not.Null);
            Assert.That(AuthoringType, Is.Not.Null);
            Assert.That(SceneContextType, Is.Not.Null);
            Assert.That(PackageType.GetMethod("RestartSession"), Is.Not.Null);
            TestContext.WriteLine("package-types definition=true package=true cadence=true restart=true");
        }

        [Test]
        public void Descriptor_IsStationaryImmovableBlasterInput()
        {
            object definition = CreateDefinition();
            object descriptor = Invoke(definition, "CreatePackageDescriptor");
            Assert.That(Read(descriptor, "DefinitionId").ToString(), Is.EqualTo("enemy.blaster-turret"));
            Assert.That(Read(descriptor, "Classification").ToString(), Is.EqualTo("Ordinary"));
            Assert.That(Read(descriptor, "WeightClass").ToString(), Is.EqualTo("Immovable"));
            Assert.That(Read(descriptor, "AttackReference").ToString(), Does.Contain("weapon.blaster-machine-gun"));
            TestContext.WriteLine("descriptor stationary=true immovable=true attack=weapon.blaster-machine-gun");
        }

        [Test]
        public void Cadence_ProducesWarningShotAndRecoveryDeterministically()
        {
            object cadence = Activator.CreateInstance(
                CadenceType,
                new object[] { 0.2d, 1.5d });
            object warning = Invoke(cadence, "Step", 0.2d, true);
            object shot = Invoke(cadence, "Step", 0.2d, true);
            object recovery = Invoke(cadence, "Step", 0.15d, true);
            Assert.That((bool)Read(warning, "WarningVisible"), Is.True);
            Assert.That((bool)Read(warning, "ShouldFire"), Is.False);
            Assert.That((bool)Read(shot, "ShouldFire"), Is.True);
            Assert.That((bool)Read(recovery, "ShouldFire"), Is.False);
            TestContext.WriteLine("cadence warning=true shot=true recovery=true");
        }

        [Test]
        public void ZeroWarningCadence_FiresImmediatelyThenRecovers()
        {
            object cadence = Activator.CreateInstance(
                CadenceType,
                new object[] { 0d, 1d });
            object shot = Invoke(cadence, "Step", 0.02d, true);
            object recovery = Invoke(cadence, "Step", 0.5d, true);
            object nextShot = Invoke(cadence, "Step", 0.5d, true);

            Assert.That((bool)Read(shot, "WarningVisible"), Is.False);
            Assert.That((bool)Read(shot, "ShouldFire"), Is.True);
            Assert.That((bool)Read(recovery, "ShouldFire"), Is.False);
            Assert.That((bool)Read(nextShot, "WarningVisible"), Is.False);
            Assert.That((bool)Read(nextShot, "ShouldFire"), Is.True);
            TestContext.WriteLine("cadence warning=false immediate-shot=true recovery=true");
        }

        [Test]
        public void FacingCone_UsesAuthoredDirectionAndIncludesBoundary()
        {
            MethodInfo method = FacingRulesType.GetMethod(
                "IsWithinCone",
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(method, Is.Not.Null);

            bool ahead = (bool)method.Invoke(
                null,
                new object[] { Vector2.zero, Vector2.right, Vector2.right * 5f, 70d });
            Vector2 boundary = (Vector2)(
                Quaternion.Euler(0f, 0f, 35f) * Vector3.right);
            bool edge = (bool)method.Invoke(
                null,
                new object[] { Vector2.zero, Vector2.right, boundary * 5f, 70d });
            bool outside = (bool)method.Invoke(
                null,
                new object[]
                {
                    Vector2.zero,
                    Vector2.right,
                    (Vector2)(Quaternion.Euler(0f, 0f, 36f) * Vector3.right) * 5f,
                    70d,
                });
            bool behind = (bool)method.Invoke(
                null,
                new object[] { Vector2.zero, Vector2.right, Vector2.left * 5f, 70d });

            Assert.That(ahead, Is.True);
            Assert.That(edge, Is.True);
            Assert.That(outside, Is.False);
            Assert.That(behind, Is.False);

            MethodInfo snap = FacingRulesType.GetMethod(
                "SnapToCardinal",
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(snap, Is.Not.Null);
            Assert.That(
                (Vector2)snap.Invoke(null, new object[] { new Vector2(0.9f, 0.2f) }),
                Is.EqualTo(Vector2.right));
            Assert.That(
                (Vector2)snap.Invoke(null, new object[] { new Vector2(-0.2f, -0.9f) }),
                Is.EqualTo(Vector2.down));
            TestContext.WriteLine("facing-cone total=70 inside=true boundary=true outside=false behind=false");
        }

        [Test]
        public void RotateTowards_UsesBoundedAngularSpeed()
        {
            MethodInfo rotate = FacingRulesType.GetMethod(
                "RotateTowards",
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(rotate, Is.Not.Null);

            Vector2 halfway = (Vector2)rotate.Invoke(
                null,
                new object[] { Vector2.right, Vector2.up, 45d });
            Vector2 reached = (Vector2)rotate.Invoke(
                null,
                new object[] { Vector2.right, Vector2.up, 120d });

            Assert.That(Vector2.Angle(Vector2.right, halfway), Is.EqualTo(45f).Within(0.01f));
            Assert.That(Vector2.Angle(Vector2.up, reached), Is.LessThan(0.01f));
        }

        [Test]
        public void TwoAuthoredTurrets_RegisterAndRestartIndependently()
        {
            EnemyTarget2DAdapter playerTarget = CreatePlayerTarget();
            CombatHit2DAdapter playerHits = CreatePlayerHitAdapter("source.player-a");
            ScopeHarness scope = CreateScope("a", playerTarget, playerHits);
            Component first = CreateTurret(
                scope.Root.transform,
                "placed.blaster-turret-a",
                null);
            Component second = CreateTurret(
                scope.Root.transform,
                "placed.blaster-turret-b",
                null);

            Assert.That((bool)Invoke(first, "TryConfigureNow"), Is.True);
            Assert.That((bool)Invoke(second, "TryConfigureNow"), Is.True);
            Assert.That(scope.Scope.RegisteredParticipantCount, Is.EqualTo(2));
            Assert.That((int)Read(scope.Context, "RegisteredTurretCount"), Is.EqualTo(2));
            Assert.That(Read(first, "ActorId").ToString(), Is.EqualTo("placed.blaster-turret-a"));
            Assert.That(Read(second, "ActorId").ToString(), Is.EqualTo("placed.blaster-turret-b"));

            object firstPackage = Read(first, "Package");
            object secondPackage = Read(second, "Package");
            Assert.That((bool)Invoke(firstPackage, "RestartSession"), Is.True);
            Assert.That((long)Read(firstPackage, "Generation"), Is.EqualTo(1L));
            Assert.That((long)Read(secondPackage, "Generation"), Is.EqualTo(0L));
            Assert.That(scope.Scope.RegisteredParticipantCount, Is.EqualTo(2));
            Assert.That((int)Read(scope.Context, "RegisteredTurretCount"), Is.EqualTo(2));
        }

        [Test]
        public void DuplicateAuthoredId_FailsBeforeSecondPackageConfigures()
        {
            EnemyTarget2DAdapter playerTarget = CreatePlayerTarget();
            CombatHit2DAdapter playerHits = CreatePlayerHitAdapter("source.player-duplicate");
            ScopeHarness scope = CreateScope("duplicate", playerTarget, playerHits);
            Component first = CreateTurret(
                scope.Root.transform,
                "placed.blaster-turret-duplicate",
                null);
            Component second = CreateTurret(
                scope.Root.transform,
                "placed.blaster-turret-duplicate",
                null);

            Assert.That((bool)Invoke(first, "TryConfigureNow"), Is.True);
            Assert.That((bool)Invoke(second, "TryConfigureNow"), Is.False);
            Assert.That(scope.Scope.RegisteredParticipantCount, Is.EqualTo(1));
            Assert.That((int)Read(scope.Context, "RegisteredTurretCount"), Is.EqualTo(1));
            Assert.That(
                (bool)Read(Read(second, "Package"), "IsConfigured"),
                Is.False);

            object registration = Read(second, "LastSceneScopeRegistrationResult");
            Assert.That(registration, Is.Not.Null);
            Assert.That(
                Read(registration, "Status").ToString(),
                Is.EqualTo("RejectedDuplicateIdentity"));
            Assert.That(
                Read(second, "LastBindingDiagnostic").ToString(),
                Does.Contain("identity"));
        }

        [Test]
        public void ExplicitScope_PrecedesNearestParentScope()
        {
            EnemyTarget2DAdapter playerTarget = CreatePlayerTarget();
            CombatHit2DAdapter playerHits = CreatePlayerHitAdapter("source.player-explicit");
            ScopeHarness parentScope = CreateScope("parent", playerTarget, playerHits);
            ScopeHarness explicitScope = CreateScope("explicit", playerTarget, playerHits);
            Component explicitTurret = CreateTurret(
                parentScope.Root.transform,
                "placed.blaster-turret-explicit",
                explicitScope.Scope);
            Component parentTurret = CreateTurret(
                parentScope.Root.transform,
                "placed.blaster-turret-parent",
                null);

            Assert.That((bool)Invoke(explicitTurret, "TryConfigureNow"), Is.True);
            Assert.That((bool)Invoke(parentTurret, "TryConfigureNow"), Is.True);
            Assert.That(
                Read(explicitTurret, "BoundSceneScope"),
                Is.SameAs(explicitScope.Scope));
            Assert.That(
                Read(parentTurret, "BoundSceneScope"),
                Is.SameAs(parentScope.Scope));
            Assert.That(parentScope.Scope.RegisteredParticipantCount, Is.EqualTo(1));
            Assert.That(explicitScope.Scope.RegisteredParticipantCount, Is.EqualTo(1));
        }

        [Test]
        public void DistinctTurrets_RegisterInTwoIndependentScopes()
        {
            EnemyTarget2DAdapter playerTarget = CreatePlayerTarget();
            ScopeHarness firstScope = CreateScope(
                "isolated-a",
                playerTarget,
                CreatePlayerHitAdapter("source.player-isolated-a"));
            ScopeHarness secondScope = CreateScope(
                "isolated-b",
                playerTarget,
                CreatePlayerHitAdapter("source.player-isolated-b"));
            Component first = CreateTurret(
                firstScope.Root.transform,
                "placed.blaster-turret-isolated-a",
                null);
            Component second = CreateTurret(
                secondScope.Root.transform,
                "placed.blaster-turret-isolated-b",
                null);

            Assert.That((bool)Invoke(first, "TryConfigureNow"), Is.True);
            Assert.That((bool)Invoke(second, "TryConfigureNow"), Is.True);
            Assert.That(firstScope.Scope.RegisteredParticipantCount, Is.EqualTo(1));
            Assert.That(secondScope.Scope.RegisteredParticipantCount, Is.EqualTo(1));
            Assert.That((int)Read(firstScope.Context, "RegisteredTurretCount"), Is.EqualTo(1));
            Assert.That((int)Read(secondScope.Context, "RegisteredTurretCount"), Is.EqualTo(1));
        }

        [Test]
        public void AuthoredIdentity_SurvivesRenameTransformSiblingAndReparentRebind()
        {
            EnemyTarget2DAdapter playerTarget = CreatePlayerTarget();
            CombatHit2DAdapter playerHits = CreatePlayerHitAdapter("source.player-reparent");
            ScopeHarness firstScope = CreateScope("reparent-a", playerTarget, playerHits);
            ScopeHarness secondScope = CreateScope("reparent-b", playerTarget, playerHits);
            Component authoring = CreateTurret(
                firstScope.Root.transform,
                "placed.blaster-turret-stable",
                null);

            Assert.That((bool)Invoke(authoring, "TryConfigureNow"), Is.True);
            StableId initialId = (StableId)Read(authoring, "ActorId");
            GameObject turretObject = authoring.gameObject;
            turretObject.SetActive(false);
            Assert.That(firstScope.Scope.RegisteredParticipantCount, Is.Zero);
            Assert.That((int)Read(firstScope.Context, "RegisteredTurretCount"), Is.Zero);

            GameObject sibling = Track(new GameObject("EarlierSibling"));
            sibling.transform.SetParent(secondScope.Root.transform, false);
            turretObject.name = "RenamedAndMovedTurret";
            turretObject.transform.SetParent(secondScope.Root.transform, false);
            turretObject.transform.SetSiblingIndex(0);
            turretObject.transform.position = new Vector3(17.4f, -9.6f, 3f);
            turretObject.transform.rotation = Quaternion.Euler(0f, 0f, 91f);
            turretObject.SetActive(true);

            Assert.That((bool)Invoke(authoring, "TryConfigureNow"), Is.True);
            Assert.That(Read(authoring, "ActorId"), Is.EqualTo(initialId));
            Assert.That(
                Read(authoring, "BoundSceneScope"),
                Is.SameAs(secondScope.Scope));
            Assert.That(firstScope.Scope.RegisteredParticipantCount, Is.Zero);
            Assert.That(secondScope.Scope.RegisteredParticipantCount, Is.EqualTo(1));
            Assert.That((int)Read(secondScope.Context, "RegisteredTurretCount"), Is.EqualTo(1));
        }

        [Test]
        public void MissingScopeAndMalformedIdentity_FailClosedWithDiagnostics()
        {
            GameObject orphanRoot = Track(new GameObject("OrphanRoot"));
            Component missingScope = CreateTurret(
                orphanRoot.transform,
                "placed.blaster-turret-orphan",
                null);
            Assert.That((bool)Invoke(missingScope, "TryConfigureNow"), Is.False);
            Assert.That(
                Read(missingScope, "LastBindingDiagnostic").ToString(),
                Does.Contain("No compatible"));
            Assert.That(
                (bool)Read(Read(missingScope, "Package"), "IsConfigured"),
                Is.False);

            EnemyTarget2DAdapter playerTarget = CreatePlayerTarget();
            ScopeHarness scope = CreateScope(
                "malformed",
                playerTarget,
                CreatePlayerHitAdapter("source.player-malformed"));
            Component malformed = CreateTurret(
                scope.Root.transform,
                "NOT A CANONICAL ID",
                null);
            Assert.That((bool)Invoke(malformed, "TryConfigureNow"), Is.False);
            Assert.That(scope.Scope.RegisteredParticipantCount, Is.Zero);
            Assert.That(
                Read(malformed, "LastBindingDiagnostic").ToString(),
                Does.Contain("malformed"));
        }

        [Test]
        public void MultipleCompatibleNearestParentScopes_FailClosed()
        {
            EnemyTarget2DAdapter playerTarget = CreatePlayerTarget();
            CombatHit2DAdapter playerHits = CreatePlayerHitAdapter("source.player-conflict");
            GameObject root = Track(new GameObject("ConflictingTurretScopes"));
            GameplaySceneScope2D firstScope = root.AddComponent<GameplaySceneScope2D>();
            firstScope.ConfigureForTests(
                "scope.turret-conflict-a",
                "scope.gameplay",
                "projection.turret-conflict-a",
                "run.turret-tests",
                0L);
            GameplaySceneScope2D secondScope = root.AddComponent<GameplaySceneScope2D>();
            secondScope.ConfigureForTests(
                "scope.turret-conflict-b",
                "scope.gameplay",
                "projection.turret-conflict-b",
                "run.turret-tests",
                0L);
            Component context = (Component)root.AddComponent(SceneContextType);
            Invoke(context, "Configure", playerTarget, playerHits, 10d, 5d, null);
            Component turret = CreateTurret(
                root.transform,
                "placed.blaster-turret-conflicting-scope",
                null);

            Assert.That((bool)Invoke(turret, "TryConfigureNow"), Is.False);
            Assert.That(firstScope.RegisteredParticipantCount, Is.Zero);
            Assert.That(secondScope.RegisteredParticipantCount, Is.Zero);
            Assert.That(
                Read(turret, "LastBindingDiagnostic").ToString(),
                Does.Contain("multiple compatible"));
        }

        [Test]
        public void SourceSurface_StaysBoundedColorIndependentAndFreeOfGlobalDiscovery()
        {
            string source = ReadProjectFile(
                "Assets/ShooterMover/ContentPackages/Enemies/BlasterTurret/BlasterTurretPackage.cs");
            string presentation = ReadProjectFile(
                "Assets/ShooterMover/ContentPackages/Enemies/BlasterTurret/BlasterTurretPresentation2D.cs");
            string authoring = ReadProjectFile(
                "Assets/ShooterMover/ContentPackages/Enemies/BlasterTurret/BlasterTurretAuthoring2D.cs");
            string context = ReadProjectFile(
                "Assets/ShooterMover/ContentPackages/Enemies/BlasterTurret/BlasterTurretSceneContext2D.cs");
            string prefab = ReadProjectFile(
                "Assets/ShooterMover/ContentPackages/Enemies/BlasterTurret/BlasterTurret.prefab");
            Assert.That(source, Does.Contain("RestartSession"));
            Assert.That(source, Does.Contain("BlasterTurretProjectileModule"));
            Assert.That(source, Does.Contain("direction = currentFacing"));
            Assert.That(source, Does.Contain("BlasterTurretFacingRules.IsWithinCone"));
            Assert.That(source, Does.Contain("SynchronizeCollider"));
            Assert.That(presentation, Does.Contain("WarningTickCount"));
            Assert.That(presentation, Does.Not.Contain("ColorUtility"));
            Assert.That(source, Does.Not.Contain("Physics.Raycast"));
            Assert.That(source, Does.Not.Contain("GameObject.Find"));
            Assert.That(authoring, Does.Contain("snapToGrid"));
            Assert.That(authoring, Does.Contain("BlasterTurretCardinalFacing"));
            Assert.That(authoring, Does.Contain("authoredPlacedInstanceId"));
            Assert.That(authoring, Does.Contain("GameplaySceneScope2D"));
            Assert.That(authoring, Does.Contain("trackPlayer"));
            Assert.That(authoring, Does.Contain("keepColliderWhenDestroyed"));
            Assert.That(authoring, Does.Not.Contain("CreateActorId"));
            Assert.That(authoring, Does.Not.Contain("FindFirstObjectByType"));
            Assert.That(authoring, Does.Not.Contain("FindObjectsByType"));
            Assert.That(context, Does.Not.Contain("FindFirstObjectByType"));
            Assert.That(context, Does.Not.Contain("FindObjectsByType"));
            Assert.That(context, Does.Contain("RequireComponent(typeof(GameplaySceneScope2D))"));
            Assert.That(prefab, Does.Contain("authoredPlacedInstanceId"));
            Assert.That(prefab, Does.Contain("sceneScopeOverride"));
            Assert.That(prefab, Does.Contain("c1bfe54ad71d4f0090fbd0b7d4cf35a4"));
            TestContext.WriteLine(
                "surface bounded-2d=true restart=true color-independent=true global-search=false");
        }

        [Test]
        public void RuntimeContract_HasExpectedStationaryBounds()
        {
            Assert.That(ReadStatic<double>(DefinitionType, "HardMaximumRange"), Is.EqualTo(250d));
            Assert.That(ReadStatic<double>(DefinitionType, "HardMaximumWarningSeconds"), Is.EqualTo(10d));
            Assert.That(ReadStatic<double>(DefinitionType, "HardMaximumRecoverySeconds"), Is.EqualTo(60d));
            Assert.That(ReadStatic<double>(DefinitionType, "HardMaximumFacingConeDegrees"), Is.EqualTo(360d));
            Assert.That(ReadStatic<double>(DefinitionType, "HardMaximumProjectileSpeed"), Is.EqualTo(250d));
            TestContext.WriteLine("bounds range=250 warning<=10 recovery<=60 cone<=360 projectile-speed<=250");
        }

        private EnemyTarget2DAdapter CreatePlayerTarget()
        {
            GameObject player = Track(new GameObject("BlasterTurretTestPlayer"));
            CircleCollider2D collider = player.AddComponent<CircleCollider2D>();
            EnemyTarget2DAdapter target = player.AddComponent<EnemyTarget2DAdapter>();
            target.Configure(
                StableId.Parse("actor.blaster-turret-test-player"),
                player.transform,
                collider);
            return target;
        }

        private static CombatHit2DAdapter CreatePlayerHitAdapter(string sourceId)
        {
            return new CombatHit2DAdapter(StableId.Parse(sourceId));
        }

        private ScopeHarness CreateScope(
            string suffix,
            EnemyTarget2DAdapter playerTarget,
            CombatHit2DAdapter playerHits)
        {
            GameObject root = Track(new GameObject("TurretScope-" + suffix));
            GameplaySceneScope2D scope = root.AddComponent<GameplaySceneScope2D>();
            scope.ConfigureForTests(
                "scope.turret-" + suffix,
                "scope.gameplay",
                "projection.turret-" + suffix,
                "run.turret-tests",
                0L);
            Component context = (Component)root.AddComponent(SceneContextType);
            Invoke(
                context,
                "Configure",
                playerTarget,
                playerHits,
                10d,
                5d,
                null);
            return new ScopeHarness(root, scope, context);
        }

        private Component CreateTurret(
            Transform parent,
            string placedId,
            GameplaySceneScope2D explicitScope)
        {
            GameObject turret = Track(new GameObject("AuthoredTurret"));
            turret.transform.SetParent(parent, false);
            Component authoring = (Component)turret.AddComponent(AuthoringType);
            Invoke(
                authoring,
                "ConfigurePlacementForTests",
                placedId,
                explicitScope,
                "scope.gameplay");
            object definition = CreateDefinition();
            Track((UnityEngine.Object)definition);
            Invoke(authoring, "SetRuntimeOverrides", definition, null);
            return authoring;
        }

        private T Track<T>(T value)
            where T : UnityEngine.Object
        {
            runtimeObjects.Add(value);
            return value;
        }

        private static object CreateDefinition()
        {
            MethodInfo method = DefinitionType.GetMethods(
                    BindingFlags.Public | BindingFlags.Static)
                .Single(candidate =>
                    candidate.Name == "CreateRuntime"
                    && candidate.GetParameters().Length == 9);
            return method.Invoke(
                null,
                new object[]
                {
                    120d,
                    0.2d,
                    1.5d,
                    25d,
                    0.5d,
                    0.1d,
                    0.2d,
                    0.05d,
                    16,
                });
        }

        private static object Invoke(object instance, string name, params object[] args)
        {
            MethodInfo method = instance.GetType()
                .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Single(candidate =>
                    candidate.Name == name
                    && candidate.GetParameters().Length == args.Length);
            return method.Invoke(instance, args);
        }

        private static object Read(object instance, string name)
        {
            PropertyInfo property = instance.GetType().GetProperty(name);
            Assert.That(property, Is.Not.Null, name);
            return property.GetValue(instance, null);
        }

        private static T ReadStatic<T>(Type type, string name)
        {
            FieldInfo field = type.GetField(name, BindingFlags.Public | BindingFlags.Static);
            Assert.That(field, Is.Not.Null, name);
            return (T)field.GetValue(null);
        }

        private static Type Find(string fullName)
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(fullName, false))
                .FirstOrDefault(type => type != null);
        }

        private static string ReadProjectFile(string path)
        {
            string root = Directory.GetParent(UnityEngine.Application.dataPath).FullName;
            return File.ReadAllText(
                Path.Combine(
                    root,
                    path.Replace('/', Path.DirectorySeparatorChar)));
        }
    }
}
#endif
