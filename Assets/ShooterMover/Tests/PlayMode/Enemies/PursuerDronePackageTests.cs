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
using ShooterMover.UnityAdapters.Enemies;
using UnityEngine;

namespace ShooterMover.Tests.PlayMode.Enemies
{
    public sealed class PursuerDronePackageTests
    {
        private const string PackageRoot =
            "Assets/ShooterMover/ContentPackages/Enemies/PursuerDrone/";
        private const string DefinitionSourcePath =
            PackageRoot + "PursuerDroneDefinition.cs";
        private const string PackageSourcePath =
            PackageRoot + "PursuerDronePackage.cs";
        private const string PresentationSourcePath =
            PackageRoot + "PursuerDronePresentation2D.cs";
        private const string DefinitionAssetPath =
            PackageRoot + "PursuerDroneDefinition.asset";
        private const string PrefabPath =
            PackageRoot + "PursuerDrone.prefab";
        private const string PackageManifestPath =
            PackageRoot + "PACKAGE.md";

        private static readonly StableId EnemyId =
            StableId.Parse("actor.pursuer-drone-test");
        private static readonly StableId PlayerId =
            StableId.Parse("actor.player-one");
        private static readonly StableId WeaponSourceId =
            StableId.Parse("actor.test-weapon");

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
        }

        [Test]
        public void Pursuit_UsesSharedDecisionPortAndApproachesDirectly()
        {
            PursuerFixture fixture = CreateFixture(new Vector2(3f, 4f));

            EnemyActor2DFixedStepResult result =
                fixture.Actor.ExecuteFixedStep(0.02d);

            Assert.That(result.Status, Is.EqualTo(EnemyActor2DFixedStepStatus.Applied));
            Assert.That(result.Decision.ActorId, Is.EqualTo(EnemyId));
            Assert.That(result.Decision.TargetId, Is.EqualTo(PlayerId));
            Assert.That(result.AppliedVelocityX, Is.EqualTo(2.4d).Within(0.000001d));
            Assert.That(result.AppliedVelocityY, Is.EqualTo(3.2d).Within(0.000001d));
            Assert.That(fixture.EnemyBody.linearVelocity.x, Is.EqualTo(2.4f).Within(0.0001f));
            Assert.That(fixture.EnemyBody.linearVelocity.y, Is.EqualTo(3.2f).Within(0.0001f));
            Assert.That(fixture.Authority, Is.InstanceOf<IEnemyActor2DAuthority>());
            Assert.That(fixture.DecisionSource, Is.InstanceOf<IEnemyActor2DDecisionSource>());
        }

        [Test]
        public void ContactDamage_UsesBoundedSharedCadenceAndNeverWritesPlayerVelocity()
        {
            PursuerFixture fixture = CreateFixture(new Vector2(0.4f, 0f));
            fixture.PlayerBody.linearVelocity = new Vector2(7f, -2f);
            Vector2 beforeVelocity = fixture.PlayerBody.linearVelocity;

            fixture.Contact.BeginFixedStep(10L);
            EnemyContact2DApplication first = fixture.Contact.TryProcessContact(
                fixture.PlayerCollider,
                ContactClassification.BodyImpact,
                1d);
            fixture.Contact.BeginFixedStep(11L);
            EnemyContact2DApplication grace = fixture.Contact.TryProcessContact(
                fixture.PlayerCollider,
                ContactClassification.SustainedBodyContact,
                1.2d);
            fixture.Contact.BeginFixedStep(12L);
            EnemyContact2DApplication nextCadence = fixture.Contact.TryProcessContact(
                fixture.PlayerCollider,
                ContactClassification.SustainedBodyContact,
                1.5d);

            Assert.That(first.Status, Is.EqualTo(EnemyContact2DStatus.Accepted));
            Assert.That(first.RequestsMoverDamage, Is.True);
            Assert.That(first.MoverDamageAmount, Is.EqualTo(2d));
            Assert.That(first.ContactMessage.Channel, Is.EqualTo(CombatChannel.Contact));
            Assert.That(grace.Status, Is.EqualTo(EnemyContact2DStatus.GraceIgnored));
            Assert.That(grace.RequestsMoverDamage, Is.False);
            Assert.That(grace.MoverDamageAmount, Is.Zero);
            Assert.That(nextCadence.Status, Is.EqualTo(EnemyContact2DStatus.Accepted));
            Assert.That(nextCadence.RequestsMoverDamage, Is.True);
            Assert.That(nextCadence.MoverDamageAmount, Is.EqualTo(2d));
            Assert.That(fixture.PlayerBody.linearVelocity, Is.EqualTo(beforeVelocity));
            Assert.That(GetAuthorityProperty<int>(fixture, "ApplyCount"), Is.EqualTo(3));
        }

        [Test]
        public void TargetLoss_ClearsPursuitVelocitySafely()
        {
            PursuerFixture fixture = CreateFixture(new Vector2(5f, 0f));
            Assert.That(fixture.Actor.ExecuteFixedStep(0.02d).Applied, Is.True);
            Assert.That(fixture.EnemyBody.linearVelocity, Is.Not.EqualTo(Vector2.zero));

            fixture.PlayerTarget.enabled = false;
            EnemyActor2DFixedStepResult lostTarget =
                fixture.Actor.ExecuteFixedStep(0.02d);

            Assert.That(
                lostTarget.Status,
                Is.EqualTo(EnemyActor2DFixedStepStatus.TargetUnavailable));
            Assert.That(fixture.EnemyBody.linearVelocity, Is.EqualTo(Vector2.zero));
            Assert.That(fixture.Contact.IsActive, Is.True);
        }

        [Test]
        public void Death_StopsPursuitAndRejectsLateContactDamage()
        {
            PursuerFixture fixture = CreateFixture(new Vector2(5f, 0f));
            Assert.That(fixture.Actor.ExecuteFixedStep(0.02d).Applied, Is.True);

            HitMessage lethalMessage = new HitMessage(
                StableId.Create("event", "en004-lethal-hit"),
                WeaponSourceId,
                EnemyId,
                CombatChannel.Kinetic,
                HitResult.Confirmed);
            EnemyTarget2DHitApplication lethal = fixture.EnemyTarget.ApplyHit(
                lethalMessage,
                100d,
                0L);
            EnemyActor2DFixedStepResult stopped =
                fixture.Actor.ExecuteFixedStep(0.02d);
            fixture.Contact.BeginFixedStep(20L);
            EnemyContact2DApplication lateContact =
                fixture.Contact.TryProcessContact(
                    fixture.PlayerCollider,
                    ContactClassification.BodyImpact,
                    2d);

            Assert.That(lethal.Status, Is.EqualTo(EnemyTarget2DHitStatus.Applied));
            Assert.That(
                GetAuthorityState(fixture).LifecyclePhase,
                Is.EqualTo(EnemyActorLifecyclePhase.Destroyed));
            Assert.That(
                stopped.Status,
                Is.EqualTo(EnemyActor2DFixedStepStatus.ActorInactive));
            Assert.That(fixture.EnemyBody.linearVelocity, Is.EqualTo(Vector2.zero));
            Assert.That(
                lateContact.Status,
                Is.EqualTo(EnemyContact2DStatus.TargetAlreadyDestroyed));
            Assert.That(lateContact.RequestsMoverDamage, Is.False);
        }

        [Test]
        public void Disable_ClearsVelocityAndDeactivatesSharedAdapters()
        {
            PursuerFixture fixture = CreateFixture(new Vector2(5f, 0f));
            Assert.That(fixture.Actor.ExecuteFixedStep(0.02d).Applied, Is.True);
            Assert.That(fixture.EnemyBody.linearVelocity, Is.Not.EqualTo(Vector2.zero));

            fixture.Package.enabled = false;
            EnemyActor2DFixedStepResult disabled =
                fixture.Actor.ExecuteFixedStep(0.02d);

            Assert.That(
                disabled.Status,
                Is.EqualTo(EnemyActor2DFixedStepStatus.AdapterInactive));
            Assert.That(fixture.EnemyBody.linearVelocity, Is.EqualTo(Vector2.zero));
            Assert.That(fixture.Actor.IsActive, Is.False);
            Assert.That(fixture.Contact.IsActive, Is.False);
            Assert.That(GetProperty<bool>(fixture.Package, "IsActive"), Is.False);
        }

        [Test]
        public void Restart_RestoresAuthorityCadenceAndPursuitWithoutStaleState()
        {
            PursuerFixture fixture = CreateFixture(new Vector2(5f, 0f));
            Assert.That(fixture.Actor.ExecuteFixedStep(0.02d).Applied, Is.True);
            fixture.Contact.BeginFixedStep(3L);
            Assert.That(
                fixture.Contact.TryProcessContact(
                    fixture.PlayerCollider,
                    ContactClassification.BodyImpact,
                    1d).Status,
                Is.EqualTo(EnemyContact2DStatus.Accepted));

            HitMessage hitMessage = new HitMessage(
                StableId.Create("event", "en004-restart-hit"),
                WeaponSourceId,
                EnemyId,
                CombatChannel.Kinetic,
                HitResult.Confirmed);
            Assert.That(
                fixture.EnemyTarget.ApplyHit(hitMessage, 3d, 4L).Status,
                Is.EqualTo(EnemyTarget2DHitStatus.Applied));
            Assert.That(GetAuthorityState(fixture).Health, Is.EqualTo(9d));
            long actorGeneration = fixture.Actor.Generation;
            long contactGeneration = fixture.Contact.Generation;

            Assert.That(
                (bool)InvokeInstance(fixture.Package, "RestartSession"),
                Is.True);

            EnemyActorState restarted = GetAuthorityState(fixture);
            Assert.That(restarted.Health, Is.EqualTo(12d));
            Assert.That(restarted.IsActive, Is.True);
            Assert.That(restarted.ProcessedEventIds, Is.Empty);
            Assert.That(GetAuthorityProperty<int>(fixture, "ResetCount"), Is.EqualTo(1));
            Assert.That(GetProperty<long>(fixture.DecisionSource, "Sequence"), Is.Zero);
            Assert.That(fixture.Actor.FixedStepCount, Is.Zero);
            Assert.That(fixture.Actor.Generation, Is.EqualTo(actorGeneration + 1L));
            Assert.That(fixture.Contact.Generation, Is.EqualTo(contactGeneration + 1L));
            Assert.That(fixture.Contact.ProcessedCallbackCount, Is.Zero);
            Assert.That(fixture.EnemyBody.linearVelocity, Is.EqualTo(Vector2.zero));

            EnemyContact2DApplication contactAfterRestart =
                fixture.Contact.TryProcessContact(
                    fixture.PlayerCollider,
                    ContactClassification.BodyImpact,
                    0d);
            EnemyActor2DFixedStepResult pursuitAfterRestart =
                fixture.Actor.ExecuteFixedStep(0.02d);

            Assert.That(
                contactAfterRestart.Status,
                Is.EqualTo(EnemyContact2DStatus.Accepted));
            Assert.That(pursuitAfterRestart.Applied, Is.True);
            Assert.That(fixture.EnemyBody.linearVelocity, Is.Not.EqualTo(Vector2.zero));
        }

        [Test]
        public void PackageBoundary_ConsumesEN003AndKeepsAssetsAndReadabilityLocal()
        {
            PursuerFixture fixture = CreateFixture(new Vector2(3f, 1f));
            object descriptor = InvokeInstance(fixture.Definition, "CreatePackageDescriptor");
            ulong capabilities = Convert.ToUInt64(
                GetProperty<object>(descriptor, "Capabilities"));

            Assert.That(
                GetProperty<StableId>(descriptor, "DefinitionId"),
                Is.EqualTo(StableId.Parse("enemy.pursuer-drone")));
            Assert.That(
                GetProperty<CombatChannel>(descriptor, "DamageChannel"),
                Is.EqualTo(CombatChannel.Contact));
            Assert.That(
                GetProperty<CombatWeightClass>(descriptor, "WeightClass"),
                Is.EqualTo(CombatWeightClass.Standard));
            Assert.That(capabilities, Is.EqualTo(3UL));
            Assert.That(
                typeof(IEnemyActor2DAuthority).IsAssignableFrom(RuntimeTypes.Authority),
                Is.True);
            Assert.That(
                typeof(IEnemyActor2DDecisionSource).IsAssignableFrom(RuntimeTypes.DecisionSource),
                Is.True);
            Assert.That(
                fixture.Package.GetComponent<EnemyActor2DAdapter>(),
                Is.SameAs(fixture.Actor));
            Assert.That(
                fixture.Package.GetComponent<EnemyTarget2DAdapter>(),
                Is.SameAs(fixture.EnemyTarget));
            Assert.That(
                fixture.Package.GetComponent<EnemyContact2DAdapter>(),
                Is.SameAs(fixture.Contact));
            Assert.That(
                fixture.Package.GetComponents<EnemyActor2DAdapter>(),
                Has.Length.EqualTo(1));
            Assert.That(
                fixture.Package.GetComponents<EnemyTarget2DAdapter>(),
                Has.Length.EqualTo(1));
            Assert.That(
                fixture.Package.GetComponents<EnemyContact2DAdapter>(),
                Has.Length.EqualTo(1));

            string definitionSource = ReadProjectFile(DefinitionSourcePath);
            string packageSource = ReadProjectFile(PackageSourcePath);
            string presentationSource = ReadProjectFile(PresentationSourcePath);
            string allSource = definitionSource + "\n" + packageSource + "\n" + presentationSource;
            string[] forbiddenTokens =
            {
                "NavMesh",
                "UnityEngine.Physics.",
                "Physics.Raycast",
                "RaycastHit",
                "Rigidbody ",
                "Collider ",
                "Collision ",
                "GameObject.Find",
                "FindObject",
                "FindWithTag",
                "Camera.main",
                "AddForce(",
                "MovePosition(",
                "linearVelocity",
                "playerBody",
                "class EnemyActorState",
            };

            foreach (string token in forbiddenTokens)
            {
                Assert.That(allSource, Does.Not.Contain(token), "Forbidden token: " + token);
            }

            Assert.That(packageSource, Does.Contain("EnemyActorStepper.Step"));
            Assert.That(packageSource, Does.Contain("IEnemyActor2DAuthority"));
            Assert.That(packageSource, Does.Contain("IEnemyActor2DDecisionSource"));
            Assert.That(packageSource, Does.Contain("EnemyActor2DAdapter"));
            Assert.That(packageSource, Does.Contain("EnemyTarget2DAdapter"));
            Assert.That(packageSource, Does.Contain("EnemyContact2DAdapter"));
            Assert.That(presentationSource, Does.Contain("Mathf.PingPong"));
            Assert.That(presentationSource, Does.Contain("Warning Left"));
            Assert.That(presentationSource, Does.Contain("Warning Right"));
            Assert.That(fixture.Package.GetComponentsInChildren<SpriteRenderer>(), Has.Length.EqualTo(3));
            Assert.That(ProjectFileExists(DefinitionAssetPath), Is.True);
            Assert.That(ProjectFileExists(PrefabPath), Is.True);
            Assert.That(ProjectFileExists(PackageManifestPath), Is.True);

            TestContext.WriteLine(
                "EN-004 composition: package authority -> EnemyActorStepper; "
                + "package decision -> IEnemyActor2DDecisionSource; "
                + "EnemyActor2DAdapter -> enemy Rigidbody2D; "
                + "EnemyContact2DAdapter -> ordinary bounded damage request.");
            TestContext.WriteLine(
                "No encounter scene integration is claimed by this package fixture.");
        }

        private PursuerFixture CreateFixture(Vector2 playerPosition)
        {
            GameObject player = CreateObject("EN-004 Player Target");
            player.transform.position = playerPosition;
            Rigidbody2D playerBody = player.AddComponent<Rigidbody2D>();
            playerBody.gravityScale = 0f;
            BoxCollider2D playerCollider = player.AddComponent<BoxCollider2D>();
            EnemyTarget2DAdapter playerTarget =
                player.AddComponent<EnemyTarget2DAdapter>();
            playerTarget.Configure(
                PlayerId,
                player.transform,
                playerCollider);

            GameObject enemy = CreateObject("EN-004 Pursuer Drone");
            enemy.transform.position = Vector2.zero;
            Behaviour package = (Behaviour)enemy.AddComponent(RuntimeTypes.Package);
            ScriptableObject definition = (ScriptableObject)InvokeStatic(
                RuntimeTypes.Definition,
                "CreateRuntime",
                12d,
                4d,
                0.2d,
                2d,
                0.5d,
                0.02d,
                4,
                0.6d);
            createdObjects.Add(definition);
            InvokeInstance(
                package,
                "Configure",
                definition,
                playerTarget,
                playerCollider,
                EnemyId,
                PlayerId,
                CombatWeightClass.Standard);

            IEnemyActor2DAuthority authority =
                (IEnemyActor2DAuthority)GetProperty<object>(package, "Authority");
            IEnemyActor2DDecisionSource decisionSource =
                (IEnemyActor2DDecisionSource)GetProperty<object>(package, "DecisionSource");
            EnemyActor2DAdapter actor =
                GetProperty<EnemyActor2DAdapter>(package, "ActorAdapter");
            EnemyTarget2DAdapter enemyTarget =
                GetProperty<EnemyTarget2DAdapter>(package, "TargetAdapter");
            EnemyContact2DAdapter contact =
                GetProperty<EnemyContact2DAdapter>(package, "ContactAdapter");
            Rigidbody2D enemyBody =
                GetProperty<Rigidbody2D>(package, "EnemyBody");
            Collider2D enemyCollider =
                GetProperty<Collider2D>(package, "EnemyCollider");

            Assert.That(actor.IsConfigured, Is.True);
            Assert.That(actor.IsActive, Is.True);
            Assert.That(contact.IsConfigured, Is.True);
            Assert.That(contact.IsActive, Is.True);
            Assert.That(enemyTarget.IsConfigured, Is.True);

            return new PursuerFixture(
                package,
                definition,
                authority,
                decisionSource,
                actor,
                enemyTarget,
                contact,
                enemyBody,
                enemyCollider,
                playerTarget,
                playerBody,
                playerCollider);
        }

        private GameObject CreateObject(string name)
        {
            GameObject gameObject = new GameObject(name);
            createdObjects.Add(gameObject);
            return gameObject;
        }

        private static EnemyActorState GetAuthorityState(PursuerFixture fixture)
        {
            EnemyActorState state;
            Assert.That(fixture.Authority.TryReadState(out state), Is.True);
            Assert.That(state, Is.Not.Null);
            return state;
        }

        private static T GetAuthorityProperty<T>(
            PursuerFixture fixture,
            string propertyName)
        {
            return GetProperty<T>(fixture.Authority, propertyName);
        }

        private static T GetProperty<T>(object instance, string propertyName)
        {
            PropertyInfo property = instance.GetType().GetProperty(
                propertyName,
                BindingFlags.Public | BindingFlags.Instance);
            Assert.That(property, Is.Not.Null, instance.GetType().FullName + "." + propertyName);
            return (T)property.GetValue(instance, null);
        }

        private static object InvokeStatic(
            Type type,
            string methodName,
            params object[] arguments)
        {
            MethodInfo method = RequireMethod(
                type,
                methodName,
                BindingFlags.Public | BindingFlags.Static,
                arguments.Length);
            return Invoke(method, null, arguments);
        }

        private static object InvokeInstance(
            object instance,
            string methodName,
            params object[] arguments)
        {
            MethodInfo method = RequireMethod(
                instance.GetType(),
                methodName,
                BindingFlags.Public | BindingFlags.Instance,
                arguments.Length);
            return Invoke(method, instance, arguments);
        }

        private static MethodInfo RequireMethod(
            Type type,
            string methodName,
            BindingFlags flags,
            int argumentCount)
        {
            MethodInfo[] matches = type.GetMethods(flags)
                .Where(method => string.Equals(
                    method.Name,
                    methodName,
                    StringComparison.Ordinal))
                .Where(method => method.GetParameters().Length == argumentCount)
                .ToArray();
            Assert.That(
                matches,
                Has.Length.EqualTo(1),
                type.FullName + "." + methodName + " with " + argumentCount + " arguments");
            return matches[0];
        }

        private static object Invoke(
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

        private static string ReadProjectFile(string assetPath)
        {
            return File.ReadAllText(ToProjectPath(assetPath));
        }

        private static bool ProjectFileExists(string assetPath)
        {
            return File.Exists(ToProjectPath(assetPath));
        }

        private static string ToProjectPath(string assetPath)
        {
            string projectRoot = Directory.GetParent(
                UnityEngine.Application.dataPath).FullName;
            return Path.Combine(
                projectRoot,
                assetPath.Replace('/', Path.DirectorySeparatorChar));
        }

        private sealed class PursuerFixture
        {
            public PursuerFixture(
                Behaviour package,
                ScriptableObject definition,
                IEnemyActor2DAuthority authority,
                IEnemyActor2DDecisionSource decisionSource,
                EnemyActor2DAdapter actor,
                EnemyTarget2DAdapter enemyTarget,
                EnemyContact2DAdapter contact,
                Rigidbody2D enemyBody,
                Collider2D enemyCollider,
                EnemyTarget2DAdapter playerTarget,
                Rigidbody2D playerBody,
                Collider2D playerCollider)
            {
                Package = package;
                Definition = definition;
                Authority = authority;
                DecisionSource = decisionSource;
                Actor = actor;
                EnemyTarget = enemyTarget;
                Contact = contact;
                EnemyBody = enemyBody;
                EnemyCollider = enemyCollider;
                PlayerTarget = playerTarget;
                PlayerBody = playerBody;
                PlayerCollider = playerCollider;
            }

            public Behaviour Package { get; }

            public ScriptableObject Definition { get; }

            public IEnemyActor2DAuthority Authority { get; }

            public IEnemyActor2DDecisionSource DecisionSource { get; }

            public EnemyActor2DAdapter Actor { get; }

            public EnemyTarget2DAdapter EnemyTarget { get; }

            public EnemyContact2DAdapter Contact { get; }

            public Rigidbody2D EnemyBody { get; }

            public Collider2D EnemyCollider { get; }

            public EnemyTarget2DAdapter PlayerTarget { get; }

            public Rigidbody2D PlayerBody { get; }

            public Collider2D PlayerCollider { get; }
        }

        private static class RuntimeTypes
        {
            public static readonly Type Definition = RequireRuntimeType(
                "ShooterMover.ContentPackages.Enemies.PursuerDrone.PursuerDroneDefinition");
            public static readonly Type Package = RequireRuntimeType(
                "ShooterMover.ContentPackages.Enemies.PursuerDrone.PursuerDronePackage");
            public static readonly Type Authority = RequireRuntimeType(
                "ShooterMover.ContentPackages.Enemies.PursuerDrone.PursuerDroneAuthority");
            public static readonly Type DecisionSource = RequireRuntimeType(
                "ShooterMover.ContentPackages.Enemies.PursuerDrone.PursuerDroneDecisionSource");

            private static Type RequireRuntimeType(string fullName)
            {
                Type[] matches = AppDomain.CurrentDomain.GetAssemblies()
                    .Select(assembly => assembly.GetType(fullName, false))
                    .Where(type => type != null)
                    .ToArray();
                Assert.That(matches, Has.Length.EqualTo(1), fullName);
                return matches[0];
            }
        }
    }
}
#endif
