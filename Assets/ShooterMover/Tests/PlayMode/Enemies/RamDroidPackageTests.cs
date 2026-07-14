#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using ShooterMover.Contracts.Combat;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Enemies;
using ShooterMover.UnityAdapters.Enemies;
using UnityEditor;
using UnityEngine;

namespace ShooterMover.Tests.PlayMode.Enemies
{
    public sealed class RamDroidPackageTests
    {
        private const string PackageFolder =
            "Assets/ShooterMover/ContentPackages/Enemies/RamDroid/";
        private const string DefinitionAssetPath = PackageFolder + "RamDroidDefinition.asset";
        private const string PrefabPath = PackageFolder + "RamDroid.prefab";
        private const string DefinitionSourcePath = PackageFolder + "RamDroidDefinition.cs";
        private const string RuntimeSourcePath = PackageFolder + "RamDroidRuntime2D.cs";
        private const string PresentationSourcePath =
            PackageFolder + "RamDroidTemporaryPresentation.cs";
        private const string PackageDocumentPath = PackageFolder + "RAM_DROID_PACKAGE.md";

        private static readonly StableId ActorId =
            StableId.Parse("actor.ram-droid-playmode");
        private static readonly StableId PlayerId =
            StableId.Parse("actor.player-one");
        private static readonly StableId WeaponId =
            StableId.Parse("actor.test-weapon");

        private readonly List<GameObject> createdObjects = new List<GameObject>();

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
        public void DefinitionPrefabAndWarning_AreOwnedReadableAndRelationallyTuned()
        {
            ScriptableObject definition = LoadDefinition();
            InvokeInstance(definition, "ValidateOrThrow");

            float speed = GetProperty<float>(definition, "MovementSpeed");
            float health = GetProperty<float>(definition, "MaximumHealth");
            float radius = GetProperty<float>(definition, "ColliderRadius");
            float pursuerSpeed = GetPublicConstant<float>(
                RuntimeTypes.Definition,
                "PursuerComparisonSpeed");
            float pursuerHealth = GetPublicConstant<float>(
                RuntimeTypes.Definition,
                "PursuerComparisonMaximumHealth");
            float pursuerRadius = GetPublicConstant<float>(
                RuntimeTypes.Definition,
                "PursuerComparisonColliderRadius");

            Assert.That(speed, Is.GreaterThan(pursuerSpeed));
            Assert.That(health, Is.LessThan(pursuerHealth));
            Assert.That(radius, Is.LessThan(pursuerRadius));
            Assert.That(
                GetProperty<bool>(definition, "IsFasterSmallerAndLowerHealthThanPursuerReference"),
                Is.True);

            object descriptor = InvokeInstance(definition, "CreatePackageDescriptor");
            Assert.That(
                GetProperty<StableId>(descriptor, "DefinitionId"),
                Is.EqualTo(StableId.Parse("enemy.ram-droid")));
            Assert.That(
                GetProperty<CombatWeightClass>(descriptor, "WeightClass"),
                Is.EqualTo(CombatWeightClass.Light));
            Assert.That(
                GetProperty<CombatChannel>(descriptor, "DamageChannel"),
                Is.EqualTo(CombatChannel.Contact));
            Assert.That(
                GetProperty<object>(descriptor, "Capabilities").ToString(),
                Does.Contain("DirectPursuit"));
            Assert.That(
                GetProperty<object>(descriptor, "Capabilities").ToString(),
                Does.Contain("DisposableImpactAttack"));

            GameObject prefab = LoadPrefab();
            CircleCollider2D collider = prefab.GetComponent<CircleCollider2D>();
            Component runtime = prefab.GetComponent(RuntimeTypes.Runtime);
            Component presentation = prefab.GetComponent(RuntimeTypes.Presentation);
            Transform warning = prefab.transform.Find("WARNING_RAM_TEXT_AND_PULSE");

            Assert.That(runtime, Is.Not.Null);
            Assert.That(presentation, Is.Not.Null);
            Assert.That(collider, Is.Not.Null);
            Assert.That(collider.radius, Is.EqualTo(radius).Within(0.0001f));
            Assert.That(warning, Is.Not.Null);
            Assert.That(warning.GetComponent<TextMesh>().text, Is.EqualTo("RAM!"));
            Assert.That(GetProperty<bool>(presentation, "UsesTextCue"), Is.True);
            Assert.That(GetProperty<bool>(presentation, "UsesShapePulse"), Is.True);
            Assert.That(GetProperty<bool>(presentation, "UsesColorOnly"), Is.False);
            Assert.That(File.Exists(ProjectPath(PackageDocumentPath)), Is.True);
        }

        [Test]
        public void FirstValidPlayerImpact_RequestsOneBoundedHitAndDestroysExactlyOnce()
        {
            RamFixture fixture = CreateFixture();
            fixture.PlayerBody.linearVelocity = new Vector2(6f, -3f);
            Vector2 playerVelocity = fixture.PlayerBody.linearVelocity;
            fixture.Contact.BeginFixedStep(1L);

            EnemyContact2DApplication first = ProcessImpact(
                fixture,
                fixture.PlayerCollider,
                ContactClassification.BodyImpact,
                1d);
            EnemyContact2DApplication sameStepDuplicate = ProcessImpact(
                fixture,
                fixture.PlayerSecondaryCollider,
                ContactClassification.BodyImpact,
                1d);
            fixture.Contact.BeginFixedStep(2L);
            EnemyContact2DApplication late = ProcessImpact(
                fixture,
                fixture.PlayerCollider,
                ContactClassification.BodyImpact,
                1.1d);

            EnemyActorState state = CurrentState(fixture);
            float authoredImpactDamage = GetProperty<float>(fixture.Definition, "ImpactDamage");

            Assert.That(first.Status, Is.EqualTo(EnemyContact2DStatus.Accepted));
            Assert.That(first.RequestsMoverDamage, Is.True);
            Assert.That(first.MoverDamageAmount, Is.EqualTo(authoredImpactDamage));
            Assert.That(first.MoverDamageAmount, Is.GreaterThan(0d).And.LessThanOrEqualTo(100d));
            Assert.That(state.IsDestroyed, Is.True);
            Assert.That(state.Health, Is.Zero);
            Assert.That(state.DeathCause, Is.EqualTo(EnemyActorDeathCause.DisposableImpact));
            Assert.That(
                first.DomainResult.Notifications.OfType<EnemyDestroyedNotification>().Count(),
                Is.EqualTo(1));
            Assert.That(
                first.DomainResult.Notifications
                    .OfType<EnemyEncounterResolutionNotification>()
                    .Count(),
                Is.EqualTo(1));

            Assert.That(
                sameStepDuplicate.Status,
                Is.EqualTo(EnemyContact2DStatus.DuplicateIgnored));
            Assert.That(sameStepDuplicate.RequestsMoverDamage, Is.False);
            Assert.That(late.Status, Is.EqualTo(EnemyContact2DStatus.TargetAlreadyDestroyed));
            Assert.That(late.RequestsMoverDamage, Is.False);
            Assert.That(fixture.PlayerBody.linearVelocity, Is.EqualTo(playerVelocity));
        }

        [Test]
        public void ContactGrace_RejectsRepeatedPlayerContactAndExpiresExactly()
        {
            ScriptableObject definition = LoadDefinition();
            EnemyActorState state = (EnemyActorState)InvokeInstance(
                definition,
                "CreateInitialState",
                ActorId);
            EnemyContactResolution accepted;
            EnemyContactPolicy afterFirst = state.ContactPolicy.Register(
                PlayerId,
                2d,
                (int)CombatWeightClass.Standard,
                (int)CombatWeightClass.Light,
                out accepted);
            EnemyContactResolution withinGrace;
            EnemyContactPolicy afterGraceRejection = afterFirst.Register(
                PlayerId,
                2.1d,
                (int)CombatWeightClass.Standard,
                (int)CombatWeightClass.Light,
                out withinGrace);
            EnemyContactResolution atExpiry;
            afterGraceRejection.Register(
                PlayerId,
                2d + GetProperty<float>(definition, "ContactGraceSeconds"),
                (int)CombatWeightClass.Standard,
                (int)CombatWeightClass.Light,
                out atExpiry);

            Assert.That(accepted.Decision, Is.EqualTo(EnemyContactDecision.Accepted));
            Assert.That(accepted.RequestsMoverDamage, Is.True);
            Assert.That(withinGrace.Decision, Is.EqualTo(EnemyContactDecision.GraceActive));
            Assert.That(withinGrace.RequestsMoverDamage, Is.False);
            Assert.That(atExpiry.Decision, Is.EqualTo(EnemyContactDecision.Accepted));
            Assert.That(atExpiry.RequestsMoverDamage, Is.True);
        }

        [Test]
        public void WallAndNonPlayerCollisions_CannotBecomePlayerImpactDamage()
        {
            RamFixture fixture = CreateFixture();
            GameObject wall = CreateObject("EN-005 Wall");
            BoxCollider2D wallCollider = wall.AddComponent<BoxCollider2D>();
            GameObject prop = CreateObject("EN-005 Non-player Prop");
            CircleCollider2D propCollider = prop.AddComponent<CircleCollider2D>();
            fixture.Contact.BeginFixedStep(3L);

            EnemyContact2DApplication wallResult = ProcessImpact(
                fixture,
                wallCollider,
                ContactClassification.BodyImpact,
                1d);
            EnemyContact2DApplication propResult = ProcessImpact(
                fixture,
                propCollider,
                ContactClassification.BodyImpact,
                1d);

            Assert.That(wallResult.Status, Is.EqualTo(EnemyContact2DStatus.UnknownMover));
            Assert.That(propResult.Status, Is.EqualTo(EnemyContact2DStatus.UnknownMover));
            Assert.That(wallResult.RequestsMoverDamage, Is.False);
            Assert.That(propResult.RequestsMoverDamage, Is.False);
            Assert.That(CurrentState(fixture).IsActive, Is.True);
            Assert.That(CurrentState(fixture).ProcessedEventIds, Is.Empty);
        }

        [Test]
        public void ProjectileDeathBeforeImpact_PreventsDamageAndStopsMovement()
        {
            RamFixture fixture = CreateFixture();
            HitMessage lethal = new HitMessage(
                StableId.Parse("combat-event.ram-droid-lethal"),
                WeaponId,
                ActorId,
                CombatChannel.Kinetic,
                HitResult.Confirmed);
            double lethalDamage = GetProperty<float>(fixture.Definition, "MaximumHealth") + 1d;

            EnemyTarget2DHitApplication applied = fixture.EnemyTarget.ApplyHit(
                lethal,
                lethalDamage,
                0L);
            fixture.Contact.BeginFixedStep(4L);
            EnemyContact2DApplication afterDeath = ProcessImpact(
                fixture,
                fixture.PlayerCollider,
                ContactClassification.BodyImpact,
                2d);
            fixture.EnemyBody.linearVelocity = new Vector2(8f, 1f);
            EnemyActor2DFixedStepResult stopped = ExecuteFixedStep(fixture, 0.02d);

            Assert.That(applied.Status, Is.EqualTo(EnemyTarget2DHitStatus.Applied));
            Assert.That(CurrentState(fixture).DeathCause, Is.EqualTo(EnemyActorDeathCause.IncomingDamage));
            Assert.That(afterDeath.Status, Is.EqualTo(EnemyContact2DStatus.TargetAlreadyDestroyed));
            Assert.That(afterDeath.RequestsMoverDamage, Is.False);
            Assert.That(stopped.Status, Is.EqualTo(EnemyActor2DFixedStepStatus.ActorInactive));
            Assert.That(fixture.EnemyBody.linearVelocity, Is.EqualTo(Vector2.zero));
        }

        [Test]
        public void TargetLossAndDisable_StopPursuitAndNeverWritePlayerVelocity()
        {
            RamFixture fixture = CreateFixture();
            fixture.PlayerBody.linearVelocity = new Vector2(-4f, 2f);
            Vector2 playerVelocity = fixture.PlayerBody.linearVelocity;

            EnemyActor2DFixedStepResult moving = ExecuteFixedStep(fixture, 0.02d);
            Assert.That(moving.Applied, Is.True);
            Assert.That(
                fixture.EnemyBody.linearVelocity.magnitude,
                Is.EqualTo(GetProperty<float>(fixture.Definition, "MovementSpeed")).Within(0.001f));

            fixture.PlayerTarget.enabled = false;
            EnemyActor2DFixedStepResult lost = ExecuteFixedStep(fixture, 0.02d);
            Assert.That(lost.Status, Is.EqualTo(EnemyActor2DFixedStepStatus.TargetUnavailable));
            Assert.That(fixture.EnemyBody.linearVelocity, Is.EqualTo(Vector2.zero));

            ((Behaviour)fixture.Runtime).enabled = false;
            fixture.Contact.BeginFixedStep(5L);
            EnemyContact2DApplication disabled = fixture.Contact.TryProcessContact(
                fixture.PlayerCollider,
                ContactClassification.BodyImpact,
                3d);
            Assert.That(disabled.Status, Is.EqualTo(EnemyContact2DStatus.AdapterInactive));
            Assert.That(disabled.RequestsMoverDamage, Is.False);
            Assert.That(fixture.PlayerBody.linearVelocity, Is.EqualTo(playerVelocity));
        }

        [Test]
        public void Restart_ClearsSpentStateAndAllowsOneFreshImpactOnly()
        {
            RamFixture fixture = CreateFixture();
            fixture.PlayerBody.linearVelocity = new Vector2(3f, 5f);
            Vector2 playerVelocity = fixture.PlayerBody.linearVelocity;
            fixture.Contact.BeginFixedStep(6L);
            EnemyContact2DApplication first = ProcessImpact(
                fixture,
                fixture.PlayerCollider,
                ContactClassification.BodyImpact,
                4d);
            long actorGeneration = fixture.Actor.Generation;
            long contactGeneration = fixture.Contact.Generation;

            bool restarted = (bool)InvokeInstance(fixture.Runtime, "RestartSession");
            EnemyActorState resetState = CurrentState(fixture);
            EnemyContact2DApplication afterRestart = ProcessImpact(
                fixture,
                fixture.PlayerCollider,
                ContactClassification.BodyImpact,
                0d);
            EnemyContact2DApplication duplicate = ProcessImpact(
                fixture,
                fixture.PlayerSecondaryCollider,
                ContactClassification.BodyImpact,
                0d);

            Assert.That(first.Status, Is.EqualTo(EnemyContact2DStatus.Accepted));
            Assert.That(restarted, Is.True);
            Assert.That(resetState.IsActive, Is.True);
            Assert.That(
                resetState.Health,
                Is.EqualTo(GetProperty<float>(fixture.Definition, "MaximumHealth")));
            Assert.That(resetState.ProcessedEventIds, Is.Empty);
            Assert.That(fixture.Actor.FixedStepCount, Is.Zero);
            Assert.That(fixture.Actor.Generation, Is.EqualTo(actorGeneration + 1L));
            Assert.That(fixture.Contact.Generation, Is.EqualTo(contactGeneration + 1L));
            Assert.That(afterRestart.Status, Is.EqualTo(EnemyContact2DStatus.Accepted));
            Assert.That(afterRestart.RequestsMoverDamage, Is.True);
            Assert.That(duplicate.Status, Is.EqualTo(EnemyContact2DStatus.DuplicateIgnored));
            Assert.That(duplicate.RequestsMoverDamage, Is.False);
            Assert.That(fixture.PlayerBody.linearVelocity, Is.EqualTo(playerVelocity));
        }

        [Test]
        public void RuntimeSurface_Is2DOnlyAndHasNoPlayerVelocityOrSpawnAuthority()
        {
            Type[] inspectedTypes =
            {
                RuntimeTypes.Definition,
                RuntimeTypes.Runtime,
                RuntimeTypes.Presentation,
            };
            Type[] forbiddenTypes =
            {
                typeof(Rigidbody),
                typeof(Collider),
                typeof(Collision),
                typeof(RaycastHit),
                typeof(PhysicsScene),
                typeof(Vector3),
                typeof(Quaternion),
            };

            foreach (Type inspected in inspectedTypes)
            {
                foreach (Type exposed in GetDeclaredPublicMemberTypes(inspected))
                {
                    Assert.That(
                        forbiddenTypes.Contains(Unwrap(exposed)),
                        Is.False,
                        inspected.FullName + " exposes forbidden 3D type " + exposed.FullName);
                }
            }

            string sources = ReadProjectFile(DefinitionSourcePath)
                + "\n"
                + ReadProjectFile(RuntimeSourcePath)
                + "\n"
                + ReadProjectFile(PresentationSourcePath);
            string[] forbiddenTokens =
            {
                "UnityEngine.Physics.",
                "Physics.Raycast",
                "RaycastHit",
                "NavMesh",
                "FindObject",
                "GameObject.Find",
                "FindWithTag",
                "Camera.main",
                "AddForce(",
                "MovePosition(",
                "playerBody",
                "linearVelocity",
                "Instantiate(",
                "Destroy(",
            };

            foreach (string token in forbiddenTokens)
            {
                Assert.That(sources, Does.Not.Contain(token), "Forbidden token: " + token);
            }

            string[] forbiddenThreeDimensionalTypePatterns =
            {
                @"\bPhysicsScene\b\s+[A-Za-z_]",
                @"\bCollider\b\s+[A-Za-z_]",
                @"\bRigidbody\b\s+[A-Za-z_]",
                @"\bCollision\b\s+[A-Za-z_]",
                @"\bVector3\b\s+[A-Za-z_]",
            };
            foreach (string pattern in forbiddenThreeDimensionalTypePatterns)
            {
                Assert.That(
                    Regex.IsMatch(sources, pattern),
                    Is.False,
                    "Forbidden 3D type pattern: " + pattern);
            }

            string prefab = ReadProjectFile(PrefabPath);
            Assert.That(prefab, Does.Contain("Rigidbody2D"));
            Assert.That(prefab, Does.Contain("CircleCollider2D"));
            Assert.That(prefab, Does.Contain("WARNING_RAM_TEXT_AND_PULSE"));
            Assert.That(prefab, Does.Not.Contain("--- !u!54 "));
            Assert.That(sources, Does.Contain("IEnemyActor2DAuthority"));
            Assert.That(sources, Does.Contain("IEnemyActor2DDecisionSource"));
            Assert.That(sources, Does.Contain("EnemyActorStepper.Step"));
            Assert.That(sources, Does.Contain("EnemyContact2DAdapter"));
        }

        private RamFixture CreateFixture()
        {
            GameObject player = CreateObject("EN-005 Player");
            player.transform.position = new Vector2(2f, 0f);
            Rigidbody2D playerBody = player.AddComponent<Rigidbody2D>();
            playerBody.gravityScale = 0f;
            BoxCollider2D playerCollider = player.AddComponent<BoxCollider2D>();
            BoxCollider2D playerSecondaryCollider = player.AddComponent<BoxCollider2D>();
            playerSecondaryCollider.offset = new Vector2(0.2f, 0f);
            EnemyTarget2DAdapter playerTarget = player.AddComponent<EnemyTarget2DAdapter>();
            playerTarget.Configure(PlayerId, player.transform, playerCollider);

            GameObject prefab = LoadPrefab();
            GameObject instance = UnityEngine.Object.Instantiate(prefab);
            instance.name = "EN-005 Ram Droid Fixture";
            createdObjects.Add(instance);
            Component runtime = instance.GetComponent(RuntimeTypes.Runtime);
            Assert.That(runtime, Is.Not.Null);
            InvokeInstance(
                runtime,
                "ConfigureSession",
                ActorId,
                playerTarget,
                new Collider2D[] { playerCollider, playerSecondaryCollider },
                PlayerId,
                CombatWeightClass.Standard);

            return new RamFixture(
                runtime,
                GetProperty<ScriptableObject>(runtime, "Definition"),
                instance.GetComponent<EnemyActor2DAdapter>(),
                instance.GetComponent<EnemyTarget2DAdapter>(),
                instance.GetComponent<EnemyContact2DAdapter>(),
                instance.GetComponent<Rigidbody2D>(),
                instance.GetComponent<CircleCollider2D>(),
                playerTarget,
                playerBody,
                playerCollider,
                playerSecondaryCollider);
        }

        private EnemyContact2DApplication ProcessImpact(
            RamFixture fixture,
            Collider2D collider,
            ContactClassification classification,
            double observedAtSeconds)
        {
            return (EnemyContact2DApplication)InvokeInstance(
                fixture.Runtime,
                "TryProcessImpact",
                collider,
                classification,
                observedAtSeconds);
        }

        private EnemyActor2DFixedStepResult ExecuteFixedStep(
            RamFixture fixture,
            double deltaTimeSeconds)
        {
            return (EnemyActor2DFixedStepResult)InvokeInstance(
                fixture.Runtime,
                "ExecuteFixedStep",
                deltaTimeSeconds);
        }

        private static EnemyActorState CurrentState(RamFixture fixture)
        {
            return GetProperty<EnemyActorState>(fixture.Runtime, "CurrentState");
        }

        private static ScriptableObject LoadDefinition()
        {
            ScriptableObject definition = AssetDatabase.LoadAssetAtPath<ScriptableObject>(
                DefinitionAssetPath);
            Assert.That(definition, Is.Not.Null, "Missing Ram Droid definition asset.");
            Assert.That(definition.GetType(), Is.EqualTo(RuntimeTypes.Definition));
            return definition;
        }

        private static GameObject LoadPrefab()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.That(prefab, Is.Not.Null, "Missing Ram Droid prefab.");
            return prefab;
        }

        private GameObject CreateObject(string name)
        {
            GameObject gameObject = new GameObject(name);
            createdObjects.Add(gameObject);
            return gameObject;
        }

        private static string ProjectPath(string assetPath)
        {
            string projectRoot = Directory.GetParent(UnityEngine.Application.dataPath).FullName;
            return Path.Combine(
                projectRoot,
                assetPath.Replace('/', Path.DirectorySeparatorChar));
        }

        private static string ReadProjectFile(string assetPath)
        {
            return File.ReadAllText(ProjectPath(assetPath));
        }

        private static T GetProperty<T>(object instance, string propertyName)
        {
            PropertyInfo property = instance.GetType().GetProperty(
                propertyName,
                BindingFlags.Public | BindingFlags.Instance);
            if (property == null)
            {
                throw new InvalidOperationException(
                    "Missing property " + instance.GetType().FullName + "." + propertyName + ".");
            }

            return (T)property.GetValue(instance, null);
        }

        private static T GetPublicConstant<T>(Type type, string fieldName)
        {
            FieldInfo field = type.GetField(
                fieldName,
                BindingFlags.Public | BindingFlags.Static);
            if (field == null || !field.IsLiteral)
            {
                throw new InvalidOperationException(
                    "Missing public constant " + type.FullName + "." + fieldName + ".");
            }

            return (T)field.GetRawConstantValue();
        }

        private static object InvokeInstance(
            object instance,
            string methodName,
            params object[] arguments)
        {
            MethodInfo[] methods = instance.GetType().GetMethods(
                BindingFlags.Public | BindingFlags.Instance);
            MethodInfo[] matches = methods
                .Where(method => string.Equals(method.Name, methodName, StringComparison.Ordinal))
                .Where(method => method.GetParameters().Length == arguments.Length)
                .ToArray();
            if (matches.Length != 1)
            {
                throw new InvalidOperationException(
                    "Expected one "
                    + instance.GetType().FullName
                    + "."
                    + methodName
                    + " overload, found "
                    + matches.Length
                    + ".");
            }

            try
            {
                return matches[0].Invoke(instance, arguments);
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

        private static IEnumerable<Type> GetDeclaredPublicMemberTypes(Type type)
        {
            BindingFlags flags = BindingFlags.Public
                | BindingFlags.Instance
                | BindingFlags.Static
                | BindingFlags.DeclaredOnly;
            foreach (PropertyInfo property in type.GetProperties(flags))
            {
                yield return property.PropertyType;
            }

            foreach (FieldInfo field in type.GetFields(flags))
            {
                yield return field.FieldType;
            }

            foreach (ConstructorInfo constructor in type.GetConstructors(flags))
            {
                foreach (ParameterInfo parameter in constructor.GetParameters())
                {
                    yield return parameter.ParameterType;
                }
            }

            foreach (MethodInfo method in type.GetMethods(flags))
            {
                yield return method.ReturnType;
                foreach (ParameterInfo parameter in method.GetParameters())
                {
                    yield return parameter.ParameterType;
                }
            }
        }

        private static Type Unwrap(Type type)
        {
            if (type.IsByRef || type.IsArray)
            {
                return Unwrap(type.GetElementType());
            }

            if (type.IsGenericType)
            {
                Type[] arguments = type.GetGenericArguments();
                if (arguments.Length == 1)
                {
                    return Unwrap(arguments[0]);
                }
            }

            return type;
        }

        private sealed class RamFixture
        {
            public RamFixture(
                Component runtime,
                ScriptableObject definition,
                EnemyActor2DAdapter actor,
                EnemyTarget2DAdapter enemyTarget,
                EnemyContact2DAdapter contact,
                Rigidbody2D enemyBody,
                CircleCollider2D enemyCollider,
                EnemyTarget2DAdapter playerTarget,
                Rigidbody2D playerBody,
                Collider2D playerCollider,
                Collider2D playerSecondaryCollider)
            {
                Runtime = runtime;
                Definition = definition;
                Actor = actor;
                EnemyTarget = enemyTarget;
                Contact = contact;
                EnemyBody = enemyBody;
                EnemyCollider = enemyCollider;
                PlayerTarget = playerTarget;
                PlayerBody = playerBody;
                PlayerCollider = playerCollider;
                PlayerSecondaryCollider = playerSecondaryCollider;
            }

            public Component Runtime { get; }

            public ScriptableObject Definition { get; }

            public EnemyActor2DAdapter Actor { get; }

            public EnemyTarget2DAdapter EnemyTarget { get; }

            public EnemyContact2DAdapter Contact { get; }

            public Rigidbody2D EnemyBody { get; }

            public CircleCollider2D EnemyCollider { get; }

            public EnemyTarget2DAdapter PlayerTarget { get; }

            public Rigidbody2D PlayerBody { get; }

            public Collider2D PlayerCollider { get; }

            public Collider2D PlayerSecondaryCollider { get; }
        }

        private static class RuntimeTypes
        {
            public static readonly Type Definition = Find(
                "ShooterMover.ContentPackages.Enemies.RamDroid.RamDroidDefinition");
            public static readonly Type Runtime = Find(
                "ShooterMover.ContentPackages.Enemies.RamDroid.RamDroidRuntime2D");
            public static readonly Type Presentation = Find(
                "ShooterMover.ContentPackages.Enemies.RamDroid.RamDroidTemporaryPresentation");

            private static Type Find(string fullName)
            {
                Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
                for (int index = 0; index < assemblies.Length; index++)
                {
                    Type type = assemblies[index].GetType(fullName, false);
                    if (type != null)
                    {
                        return type;
                    }
                }

                throw new InvalidOperationException(
                    "Production type was not loaded from the Unity project: " + fullName + ".");
            }
        }
    }
}
#endif
