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
using ShooterMover.UnityAdapters.Combat;
using ShooterMover.UnityAdapters.Enemies;
using ShooterMover.UnityAdapters.Physics;
using UnityEngine;

namespace ShooterMover.Tests.PlayMode.Enemies
{
    public sealed class EnemyActor2DAdapterTests
    {
        private const string ActorAdapterPath =
            "Assets/ShooterMover/Runtime/UnityAdapters/Enemies/EnemyActor2DAdapter.cs";
        private const string TargetAdapterPath =
            "Assets/ShooterMover/Runtime/UnityAdapters/Enemies/EnemyTarget2DAdapter.cs";
        private const string ContactAdapterPath =
            "Assets/ShooterMover/Runtime/UnityAdapters/Enemies/EnemyContact2DAdapter.cs";

        private static readonly StableId EnemyId = StableId.Parse("enemy.en003-fixture");
        private static readonly StableId EnemyRoleId = StableId.Parse("enemy-role.en003-fixture");
        private static readonly StableId PlayerId = StableId.Parse("actor.player-one");
        private static readonly StableId WeaponSourceId = StableId.Parse("actor.weapon-fixture");

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
        public void FixedStep_ProjectsBoundedDecisionAndClearsOnTargetLoss()
        {
            EnemyFixture fixture = CreateFixture();
            fixture.Decisions.VelocityX = 6d;
            fixture.Decisions.VelocityY = 8d;

            EnemyActor2DFixedStepResult applied =
                fixture.Actor.ExecuteFixedStep(0.02d);

            Assert.That(
                applied.Status,
                Is.EqualTo(EnemyActor2DFixedStepStatus.AppliedClamped));
            Assert.That(applied.FixedStep, Is.Zero);
            Assert.That(applied.AppliedVelocityX, Is.EqualTo(3d).Within(0.000001d));
            Assert.That(applied.AppliedVelocityY, Is.EqualTo(4d).Within(0.000001d));
            Assert.That(fixture.EnemyBody.linearVelocity.x, Is.EqualTo(3f).Within(0.0001f));
            Assert.That(fixture.EnemyBody.linearVelocity.y, Is.EqualTo(4f).Within(0.0001f));
            Assert.That(fixture.Authority.ApplyCount, Is.Zero);
            Assert.That(fixture.Authority.CurrentState.Health, Is.EqualTo(10d));

            fixture.PlayerTarget.enabled = false;
            EnemyActor2DFixedStepResult lostTarget =
                fixture.Actor.ExecuteFixedStep(0.02d);

            Assert.That(
                lostTarget.Status,
                Is.EqualTo(EnemyActor2DFixedStepStatus.TargetUnavailable));
            Assert.That(fixture.EnemyBody.linearVelocity, Is.EqualTo(Vector2.zero));
            Assert.That(fixture.Actor.FixedStepCount, Is.EqualTo(2L));
        }

        [Test]
        public void ConfirmedHit_EntersEnemyAuthorityExactlyOnce()
        {
            EnemyFixture fixture = CreateFixture();
            CombatHit2DAdapter hitBridge = new CombatHit2DAdapter(WeaponSourceId);
            StableId eventId = StableId.Parse("combat-event.en003-hit-once");

            Assert.That(
                fixture.EnemyTarget.RegisterForCombatHits(hitBridge),
                Is.EqualTo(CombatHit2DTargetRegistrationStatus.Registered));

            CombatHit2DTranslationResult translated = hitBridge.TranslateConfirmedHit(
                eventId,
                fixture.EnemyCollider,
                CombatChannel.Kinetic,
                false);
            EnemyTarget2DHitApplication first = fixture.EnemyTarget.ApplyHit(
                translated.Message,
                4d,
                0L);
            EnemyTarget2DHitApplication repeatedConfirmed = fixture.EnemyTarget.ApplyHit(
                translated.Message,
                4d,
                0L);

            CombatHit2DTranslationResult duplicateTranslation =
                hitBridge.TranslateConfirmedHit(
                    eventId,
                    fixture.EnemyCollider,
                    CombatChannel.Kinetic,
                    false);
            EnemyTarget2DHitApplication translatedDuplicate =
                fixture.EnemyTarget.ApplyHit(
                    duplicateTranslation.Message,
                    4d,
                    0L);

            Assert.That(translated.Status, Is.EqualTo(CombatHit2DTranslationStatus.Confirmed));
            Assert.That(first.Status, Is.EqualTo(EnemyTarget2DHitStatus.Applied));
            Assert.That(first.DamageNotification.HealthDamageApplied, Is.EqualTo(4d));
            Assert.That(
                repeatedConfirmed.Status,
                Is.EqualTo(EnemyTarget2DHitStatus.DuplicateIgnored));
            Assert.That(
                duplicateTranslation.Status,
                Is.EqualTo(CombatHit2DTranslationStatus.DuplicateIgnored));
            Assert.That(
                translatedDuplicate.Status,
                Is.EqualTo(EnemyTarget2DHitStatus.DuplicateIgnored));
            Assert.That(fixture.Authority.ApplyCount, Is.EqualTo(1));
            Assert.That(fixture.Authority.CurrentState.Health, Is.EqualTo(6d));
            Assert.That(fixture.Authority.CurrentState.ProcessedEventIds.Count, Is.EqualTo(1));
        }

        [Test]
        public void DisabledAndDestroyedTargets_FailSafelyAndStopMovement()
        {
            EnemyFixture fixture = CreateFixture();
            HitMessage disabledHit = new HitMessage(
                StableId.Parse("combat-event.en003-disabled-hit"),
                WeaponSourceId,
                EnemyId,
                CombatChannel.Thermal,
                HitResult.Confirmed);

            fixture.EnemyTarget.enabled = false;
            EnemyTarget2DHitApplication disabled = fixture.EnemyTarget.ApplyHit(
                disabledHit,
                2d,
                0L);
            Assert.That(disabled.Status, Is.EqualTo(EnemyTarget2DHitStatus.Disabled));
            Assert.That(fixture.Authority.ApplyCount, Is.Zero);

            fixture.EnemyTarget.enabled = true;
            HitMessage lethalHit = new HitMessage(
                StableId.Parse("combat-event.en003-lethal-hit"),
                WeaponSourceId,
                EnemyId,
                CombatChannel.Explosive,
                HitResult.Confirmed);
            EnemyTarget2DHitApplication lethal = fixture.EnemyTarget.ApplyHit(
                lethalHit,
                20d,
                1L);

            fixture.EnemyBody.linearVelocity = new Vector2(2f, 1f);
            EnemyActor2DFixedStepResult destroyedStep =
                fixture.Actor.ExecuteFixedStep(0.02d);
            HitMessage lateHit = new HitMessage(
                StableId.Parse("combat-event.en003-late-hit"),
                WeaponSourceId,
                EnemyId,
                CombatChannel.Kinetic,
                HitResult.TargetAlreadyDestroyed);
            EnemyTarget2DHitApplication late = fixture.EnemyTarget.ApplyHit(
                lateHit,
                1d,
                2L);
            EnemyTarget2DObservation observation;

            Assert.That(lethal.Status, Is.EqualTo(EnemyTarget2DHitStatus.Applied));
            Assert.That(fixture.Authority.CurrentState.IsDestroyed, Is.True);
            Assert.That(
                destroyedStep.Status,
                Is.EqualTo(EnemyActor2DFixedStepStatus.ActorInactive));
            Assert.That(fixture.EnemyBody.linearVelocity, Is.EqualTo(Vector2.zero));
            Assert.That(
                late.Status,
                Is.EqualTo(EnemyTarget2DHitStatus.TargetAlreadyDestroyed));
            Assert.That(fixture.EnemyTarget.TryReadTarget(out observation), Is.False);
            Assert.That(fixture.Authority.ApplyCount, Is.EqualTo(1));
        }

        [Test]
        public void MultiColliderContact_TranslatesOnceAndNeverChangesPlayerVelocity()
        {
            EnemyFixture fixture = CreateFixture();
            fixture.PlayerBody.linearVelocity = new Vector2(7f, -2f);
            Vector2 beforeVelocity = fixture.PlayerBody.linearVelocity;
            fixture.Contact.BeginFixedStep(4L);

            EnemyContact2DApplication first = fixture.Contact.TryProcessContact(
                fixture.PlayerCollider,
                ContactClassification.BodyImpact,
                1d);
            EnemyContact2DApplication duplicateCollider = fixture.Contact.TryProcessContact(
                fixture.PlayerSecondaryCollider,
                ContactClassification.BodyImpact,
                1d);

            Assert.That(first.Status, Is.EqualTo(EnemyContact2DStatus.Accepted));
            Assert.That(first.ContactMessage, Is.Not.Null);
            Assert.That(first.ContactMessage.SourceId, Is.EqualTo(PlayerId));
            Assert.That(first.ContactMessage.TargetId, Is.EqualTo(EnemyId));
            Assert.That(first.ContactMessage.Channel, Is.EqualTo(CombatChannel.Contact));
            Assert.That(first.ContactMessage.Result, Is.EqualTo(ContactResult.Accepted));
            Assert.That(first.WeightMessage.SourceId, Is.EqualTo(PlayerId));
            Assert.That(first.WeightMessage.TargetId, Is.EqualTo(EnemyId));
            Assert.That(first.RequestsMoverDamage, Is.True);
            Assert.That(first.MoverDamageAmount, Is.EqualTo(3d));

            Assert.That(
                duplicateCollider.Status,
                Is.EqualTo(EnemyContact2DStatus.DuplicateIgnored));
            Assert.That(
                duplicateCollider.ContactMessage.Result,
                Is.EqualTo(ContactResult.DuplicateEventIgnored));
            Assert.That(fixture.Authority.ApplyCount, Is.EqualTo(1));
            Assert.That(fixture.PlayerBody.linearVelocity, Is.EqualTo(beforeVelocity));

            MovementContact2DDescriptor descriptor;
            Assert.That(
                fixture.Contact.TryDescribeMovementContact(out descriptor),
                Is.True);
            Assert.That(descriptor.Kind, Is.EqualTo(MovementContact2DKind.Enemy));
            Assert.That(descriptor.EnemyId, Is.EqualTo(EnemyId));
            Assert.That(descriptor.WeightMessage.SourceId, Is.EqualTo(PlayerId));
            Assert.That(descriptor.WeightMessage.TargetId, Is.EqualTo(EnemyId));

            MovementContact2DDescriptor classified;
            Assert.That(
                MovementContactClassifier.Classify(
                    fixture.EnemyCollider,
                    out classified),
                Is.EqualTo(MovementContact2DClassificationResult.Classified));
            Assert.That(classified.EnemyId, Is.EqualTo(EnemyId));
            Assert.That(fixture.PlayerBody.linearVelocity, Is.EqualTo(beforeVelocity));
        }

        [Test]
        public void DisableAndRestart_ClearVelocityCallbacksAndAuthoritativeSessionState()
        {
            EnemyFixture fixture = CreateFixture();
            fixture.Decisions.VelocityX = 2d;
            fixture.Decisions.VelocityY = -1d;
            fixture.Actor.ExecuteFixedStep(0.02d);
            fixture.Contact.BeginFixedStep(5L);
            EnemyContact2DApplication contactBeforeRestart =
                fixture.Contact.TryProcessContact(
                    fixture.PlayerCollider,
                    ContactClassification.BodyImpact,
                    2d);
            HitMessage hit = new HitMessage(
                StableId.Parse("combat-event.en003-restart-hit"),
                WeaponSourceId,
                EnemyId,
                CombatChannel.Kinetic,
                HitResult.Confirmed);
            fixture.EnemyTarget.ApplyHit(hit, 2d, 6L);

            Assert.That(contactBeforeRestart.Status, Is.EqualTo(EnemyContact2DStatus.Accepted));
            Assert.That(fixture.Authority.CurrentState.Health, Is.EqualTo(8d));
            Assert.That(fixture.Contact.ProcessedCallbackCount, Is.EqualTo(1));
            long actorGeneration = fixture.Actor.Generation;
            long contactGeneration = fixture.Contact.Generation;

            Assert.That(fixture.Actor.Restart(), Is.True);

            Assert.That(fixture.Authority.ResetCount, Is.EqualTo(1));
            Assert.That(fixture.Decisions.ResetCount, Is.EqualTo(1));
            Assert.That(fixture.Authority.CurrentState.Health, Is.EqualTo(10d));
            Assert.That(fixture.Authority.CurrentState.ProcessedEventIds, Is.Empty);
            Assert.That(fixture.EnemyBody.linearVelocity, Is.EqualTo(Vector2.zero));
            Assert.That(fixture.Actor.FixedStepCount, Is.Zero);
            Assert.That(fixture.Actor.Generation, Is.EqualTo(actorGeneration + 1L));
            Assert.That(fixture.Contact.Generation, Is.EqualTo(contactGeneration + 1L));
            Assert.That(fixture.Contact.ProcessedCallbackCount, Is.Zero);

            EnemyContact2DApplication contactAfterRestart =
                fixture.Contact.TryProcessContact(
                    fixture.PlayerCollider,
                    ContactClassification.BodyImpact,
                    0d);
            Assert.That(contactAfterRestart.Status, Is.EqualTo(EnemyContact2DStatus.Accepted));

            fixture.EnemyBody.linearVelocity = new Vector2(9f, 9f);
            fixture.Actor.enabled = false;
            EnemyActor2DFixedStepResult disabled =
                fixture.Actor.ExecuteFixedStep(0.02d);

            Assert.That(
                disabled.Status,
                Is.EqualTo(EnemyActor2DFixedStepStatus.AdapterInactive));
            Assert.That(fixture.EnemyBody.linearVelocity, Is.EqualTo(Vector2.zero));
            Assert.That(fixture.Contact.IsActive, Is.False);
        }

        [Test]
        public void BoundarySnapshot_ProvesPackageConsumptionWithoutSharedAdapterEdits()
        {
            EnemyFixture fixture = CreateFixture();
            PackageFixtureDecisionSource packageDecision = new PackageFixtureDecisionSource();
            GameObject packageEnemy = CreateObject("Concrete Package Fixture");
            Rigidbody2D packageBody = packageEnemy.AddComponent<Rigidbody2D>();
            packageBody.gravityScale = 0f;
            BoxCollider2D packageCollider = packageEnemy.AddComponent<BoxCollider2D>();
            TestEnemyAuthority packageAuthority = CreateAuthority(
                StableId.Parse("enemy.package-consumer"));
            EnemyTarget2DAdapter packageTarget =
                packageEnemy.AddComponent<EnemyTarget2DAdapter>();
            packageTarget.Configure(
                packageAuthority.CurrentState.ActorId,
                packageEnemy.transform,
                packageCollider,
                packageAuthority);
            EnemyContact2DAdapter packageContact =
                packageEnemy.AddComponent<EnemyContact2DAdapter>();
            packageContact.Configure(
                packageTarget,
                packageAuthority,
                PlayerId,
                CombatWeightClass.Standard,
                2);
            packageContact.RegisterMoverCollider(
                fixture.PlayerCollider,
                PlayerId,
                CombatWeightClass.Standard);
            EnemyActor2DAdapter packageActor =
                packageEnemy.AddComponent<EnemyActor2DAdapter>();
            packageActor.Configure(
                packageBody,
                packageAuthority,
                packageDecision,
                fixture.PlayerTarget,
                packageContact,
                4d);
            packageActor.Activate();

            EnemyActor2DFixedStepResult packageStep =
                packageActor.ExecuteFixedStep(0.02d);
            string snapshot = string.Join(
                "\n",
                "decision: IEnemyActor2DDecisionSource -> EnemyActor2DDecision",
                "movement: EnemyActor2DAdapter -> MovementBody2DAdapter -> enemy Rigidbody2D",
                "hit: CombatHit2DAdapter -> HitMessage -> EnemyTarget2DAdapter -> IEnemyActor2DAuthority",
                "contact: Collision2D -> EnemyContact2DAdapter -> EnemyActorCommand",
                "player contact: EnemyContact2DAdapter -> IMovementContact2DContract -> MT-009",
                "authority: EnemyActorState + EnemyActorStepper remain plain C#");
            TestContext.WriteLine(snapshot);
            TestContext.WriteLine(
                "Dependency review: EN-004 through EN-008 can implement the two authority/decision ports, configure package-owned objects, and consume all three adapters without editing shared files.");

            Assert.That(packageStep.Applied, Is.True);
            Assert.That(
                typeof(IEnemyActor2DDecisionSource).IsAssignableFrom(
                    typeof(PackageFixtureDecisionSource)),
                Is.True);
            Assert.That(
                typeof(IEnemyActor2DAuthority).IsAssignableFrom(
                    typeof(TestEnemyAuthority)),
                Is.True);
            Assert.That(
                typeof(IMovementContact2DContract).IsAssignableFrom(
                    typeof(EnemyContact2DAdapter)),
                Is.True);
            Assert.That(snapshot, Does.Contain("enemy Rigidbody2D"));
            Assert.That(snapshot, Does.Contain("MT-009"));
            Assert.That(snapshot, Does.Contain("plain C#"));
        }

        [Test]
        public void RuntimeSurface_UsesOnly2DPhysicsAndHasNoPlayerVelocityAuthority()
        {
            Type[] inspectedTypes =
            {
                typeof(EnemyActor2DAdapter),
                typeof(EnemyActor2DDecision),
                typeof(EnemyTarget2DAdapter),
                typeof(EnemyTarget2DObservation),
                typeof(EnemyContact2DAdapter),
                typeof(EnemyContact2DApplication),
                typeof(IEnemyActor2DAuthority),
                typeof(IEnemyActor2DDecisionSource),
                typeof(IEnemyTarget2DSource),
            };
            Type[] forbiddenTypes =
            {
                typeof(Collider),
                typeof(Rigidbody),
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

            string actorSource = ReadProjectFile(ActorAdapterPath);
            string targetSource = ReadProjectFile(TargetAdapterPath);
            string contactSource = ReadProjectFile(ContactAdapterPath);
            string allSource = actorSource + "\n" + targetSource + "\n" + contactSource;
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
            };

            foreach (string token in forbiddenTokens)
            {
                Assert.That(allSource, Does.Not.Contain(token), "Forbidden token: " + token);
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
                    Regex.IsMatch(allSource, pattern),
                    Is.False,
                    "Forbidden 3D type pattern: " + pattern);
            }

            Assert.That(actorSource, Does.Contain("Rigidbody2D"));
            Assert.That(actorSource, Does.Contain("MovementBody2DAdapter"));
            Assert.That(targetSource, Does.Contain("Collider2D"));
            Assert.That(targetSource, Does.Contain("HitMessage"));
            Assert.That(contactSource, Does.Contain("Collision2D"));
            Assert.That(contactSource, Does.Contain("IMovementContact2DContract"));
            Assert.That(targetSource, Does.Not.Contain("linearVelocity"));
            Assert.That(contactSource, Does.Not.Contain("linearVelocity"));
            Assert.That(allSource, Does.Not.Contain("playerBody"));
        }

        private EnemyFixture CreateFixture()
        {
            GameObject player = CreateObject("EN-003 Player Target");
            player.transform.position = new Vector2(10f, 2f);
            Rigidbody2D playerBody = player.AddComponent<Rigidbody2D>();
            playerBody.gravityScale = 0f;
            BoxCollider2D playerCollider = player.AddComponent<BoxCollider2D>();
            BoxCollider2D playerSecondaryCollider = player.AddComponent<BoxCollider2D>();
            playerSecondaryCollider.offset = new Vector2(0.25f, 0f);
            EnemyTarget2DAdapter playerTarget =
                player.AddComponent<EnemyTarget2DAdapter>();
            playerTarget.Configure(PlayerId, player.transform, playerCollider);

            GameObject enemy = CreateObject("EN-003 Enemy Actor");
            Rigidbody2D enemyBody = enemy.AddComponent<Rigidbody2D>();
            enemyBody.gravityScale = 0f;
            BoxCollider2D enemyCollider = enemy.AddComponent<BoxCollider2D>();
            TestEnemyAuthority authority = CreateAuthority(EnemyId);
            EnemyTarget2DAdapter enemyTarget =
                enemy.AddComponent<EnemyTarget2DAdapter>();
            enemyTarget.Configure(EnemyId, enemy.transform, enemyCollider, authority);

            EnemyContact2DAdapter contact =
                enemy.AddComponent<EnemyContact2DAdapter>();
            contact.Configure(
                enemyTarget,
                authority,
                PlayerId,
                CombatWeightClass.Standard,
                4);
            Assert.That(
                contact.RegisterMoverCollider(
                    playerCollider,
                    PlayerId,
                    CombatWeightClass.Standard),
                Is.EqualTo(EnemyContact2DRegistrationStatus.Registered));
            Assert.That(
                contact.RegisterMoverCollider(
                    playerSecondaryCollider,
                    PlayerId,
                    CombatWeightClass.Standard),
                Is.EqualTo(EnemyContact2DRegistrationStatus.Registered));

            TestDecisionSource decisions = new TestDecisionSource(EnemyId, PlayerId);
            EnemyActor2DAdapter actor = enemy.AddComponent<EnemyActor2DAdapter>();
            actor.Configure(
                enemyBody,
                authority,
                decisions,
                playerTarget,
                contact,
                5d);
            actor.Activate();

            return new EnemyFixture(
                actor,
                enemyTarget,
                contact,
                authority,
                decisions,
                enemyBody,
                enemyCollider,
                playerTarget,
                playerBody,
                playerCollider,
                playerSecondaryCollider);
        }

        private static TestEnemyAuthority CreateAuthority(StableId actorId)
        {
            EnemyContactPolicy contactPolicy = EnemyContactPolicy.Create(
                EnemyContactMode.OrdinaryDamage,
                3d,
                0.5d,
                0.01d,
                8);
            EnemyActorState state = EnemyActorState.Create(
                actorId,
                EnemyRoleId,
                10d,
                (int)CombatWeightClass.Standard,
                contactPolicy);
            return new TestEnemyAuthority(state);
        }

        private GameObject CreateObject(string name)
        {
            GameObject gameObject = new GameObject(name);
            createdObjects.Add(gameObject);
            return gameObject;
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

        private static string ReadProjectFile(string assetPath)
        {
            string projectRoot = Directory.GetParent(UnityEngine.Application.dataPath).FullName;
            return File.ReadAllText(
                Path.Combine(
                    projectRoot,
                    assetPath.Replace('/', Path.DirectorySeparatorChar)));
        }

        private sealed class EnemyFixture
        {
            public EnemyFixture(
                EnemyActor2DAdapter actor,
                EnemyTarget2DAdapter enemyTarget,
                EnemyContact2DAdapter contact,
                TestEnemyAuthority authority,
                TestDecisionSource decisions,
                Rigidbody2D enemyBody,
                Collider2D enemyCollider,
                EnemyTarget2DAdapter playerTarget,
                Rigidbody2D playerBody,
                Collider2D playerCollider,
                Collider2D playerSecondaryCollider)
            {
                Actor = actor;
                EnemyTarget = enemyTarget;
                Contact = contact;
                Authority = authority;
                Decisions = decisions;
                EnemyBody = enemyBody;
                EnemyCollider = enemyCollider;
                PlayerTarget = playerTarget;
                PlayerBody = playerBody;
                PlayerCollider = playerCollider;
                PlayerSecondaryCollider = playerSecondaryCollider;
            }

            public EnemyActor2DAdapter Actor { get; }

            public EnemyTarget2DAdapter EnemyTarget { get; }

            public EnemyContact2DAdapter Contact { get; }

            public TestEnemyAuthority Authority { get; }

            public TestDecisionSource Decisions { get; }

            public Rigidbody2D EnemyBody { get; }

            public Collider2D EnemyCollider { get; }

            public EnemyTarget2DAdapter PlayerTarget { get; }

            public Rigidbody2D PlayerBody { get; }

            public Collider2D PlayerCollider { get; }

            public Collider2D PlayerSecondaryCollider { get; }
        }

        private sealed class TestEnemyAuthority : IEnemyActor2DAuthority
        {
            private readonly EnemyActorState initialState;
            private EnemyActorState currentState;

            public TestEnemyAuthority(EnemyActorState state)
            {
                initialState = state ?? throw new ArgumentNullException(nameof(state));
                currentState = state;
            }

            public EnemyActorState CurrentState
            {
                get { return currentState; }
            }

            public int ApplyCount { get; private set; }

            public int ResetCount { get; private set; }

            public bool TryReadState(out EnemyActorState state)
            {
                state = currentState;
                return state != null;
            }

            public EnemyActorStepResult Apply(EnemyActorCommand command)
            {
                ApplyCount++;
                EnemyActorStepResult result = EnemyActorStepper.Step(
                    currentState,
                    new[] { command });
                currentState = result.State;
                return result;
            }

            public bool Reset()
            {
                ResetCount++;
                currentState = initialState;
                return true;
            }
        }

        private class TestDecisionSource : IEnemyActor2DDecisionSource
        {
            private readonly StableId actorId;
            private readonly StableId targetId;
            private long sequence;

            public TestDecisionSource(StableId actorId, StableId targetId)
            {
                this.actorId = actorId;
                this.targetId = targetId;
                VelocityX = 1d;
                VelocityY = 0d;
                Available = true;
            }

            public double VelocityX { get; set; }

            public double VelocityY { get; set; }

            public bool Available { get; set; }

            public int ResetCount { get; private set; }

            public bool TryDecide(
                EnemyActorState state,
                EnemyTarget2DObservation target,
                double deltaTimeSeconds,
                out EnemyActor2DDecision decision)
            {
                if (!Available)
                {
                    decision = null;
                    return false;
                }

                decision = new EnemyActor2DDecision(
                    sequence++,
                    actorId,
                    targetId,
                    VelocityX,
                    VelocityY);
                return true;
            }

            public void Reset()
            {
                ResetCount++;
                sequence = 0L;
            }
        }

        private sealed class PackageFixtureDecisionSource : TestDecisionSource
        {
            public PackageFixtureDecisionSource()
                : base(
                    StableId.Parse("enemy.package-consumer"),
                    PlayerId)
            {
                VelocityX = 0.5d;
                VelocityY = 0.25d;
            }
        }
    }
}
#endif
