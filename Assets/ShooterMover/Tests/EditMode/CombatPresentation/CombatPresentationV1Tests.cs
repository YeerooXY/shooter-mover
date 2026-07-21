using System;
using System.IO;
using NUnit.Framework;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Enemies;
using ShooterMover.EnemyRuntimeComposition;
using ShooterMover.UnityAdapters.CombatPresentation;
using ShooterMover.UnityAdapters.Enemies;
using UnityEngine;

namespace ShooterMover.Tests.EditMode.CombatPresentation
{
    public sealed class CombatPresentationV1Tests
    {
        [TestCase(100d, 100d, 1d)]
        [TestCase(50d, 100d, 0.5d)]
        [TestCase(0d, 100d, 0d)]
        [TestCase(33.375d, 100d, 0.33375d)]
        public void HealthProjection_PreservesFractionalAuthorityValues(
            double current,
            double maximum,
            double expectedFill)
        {
            CombatHealthBarSnapshotV1 snapshot = Snapshot(
                "projection",
                1L,
                current,
                maximum,
                current <= 0d
                    ? CombatHealthPresentationStateV1.Terminal
                    : CombatHealthPresentationStateV1.Alive);

            Assert.That(snapshot.CurrentHealth, Is.EqualTo(current));
            Assert.That(snapshot.MaximumHealth, Is.EqualTo(maximum));
            Assert.That(snapshot.NormalizedFill, Is.EqualTo(expectedFill).Within(0.0000001d));
        }

        [Test]
        public void Presenter_RejectsWrongEntityAndStaleLifecycle_ThenRestoresOnRestart()
        {
            GameObject root = new GameObject("health-presenter-test");
            try
            {
                StableId actor = Id("actor", "presenter");
                var source = new MutableSource(Snapshot(
                    "presenter",
                    1L,
                    100d,
                    100d,
                    CombatHealthPresentationStateV1.Alive));
                CombatHealthBarPresenter2D presenter =
                    root.AddComponent<CombatHealthBarPresenter2D>();
                presenter.Configure(actor, source, Vector3.up);

                source.Current = Snapshot(
                    "presenter",
                    1L,
                    25.5d,
                    100d,
                    CombatHealthPresentationStateV1.Alive);
                Assert.That(
                    presenter.Refresh(),
                    Is.EqualTo(CombatHealthBarRefreshStatusV1.Applied));
                Assert.That(presenter.CurrentSnapshot.NormalizedFill, Is.EqualTo(0.255d));

                int updates = presenter.PresentationUpdateCount;
                Assert.That(
                    presenter.Refresh(),
                    Is.EqualTo(CombatHealthBarRefreshStatusV1.Unchanged));
                Assert.That(presenter.PresentationUpdateCount, Is.EqualTo(updates));

                source.Current = Snapshot(
                    "other",
                    1L,
                    10d,
                    100d,
                    CombatHealthPresentationStateV1.Alive);
                Assert.That(
                    presenter.Refresh(),
                    Is.EqualTo(CombatHealthBarRefreshStatusV1.RejectedEntityMismatch));

                source.Current = Snapshot(
                    "presenter",
                    1L,
                    0d,
                    100d,
                    CombatHealthPresentationStateV1.Terminal);
                Assert.That(
                    presenter.Refresh(),
                    Is.EqualTo(CombatHealthBarRefreshStatusV1.HiddenTerminal));
                Assert.That(presenter.IsVisible, Is.False);

                source.Current = Snapshot(
                    "presenter",
                    2L,
                    100d,
                    100d,
                    CombatHealthPresentationStateV1.Alive);
                Assert.That(
                    presenter.Refresh(),
                    Is.EqualTo(CombatHealthBarRefreshStatusV1.Applied));
                Assert.That(presenter.IsVisible, Is.True);

                source.Current = Snapshot(
                    "presenter",
                    1L,
                    50d,
                    100d,
                    CombatHealthPresentationStateV1.Alive);
                Assert.That(
                    presenter.Refresh(),
                    Is.EqualTo(CombatHealthBarRefreshStatusV1.RejectedStaleLifecycle));
                Assert.That(presenter.CurrentSnapshot.LifecycleGeneration, Is.EqualTo(2L));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void GenericRegistration_AutomaticallySupportsThreeIndependentEnemies()
        {
            GameObject poolRoot = new GameObject("generic-vfx-pool");
            GameObject[] enemies =
            {
                new GameObject("registered-enemy-one"),
                new GameObject("registered-enemy-two"),
                new GameObject("registered-enemy-three"),
            };
            try
            {
                var factory = new RecordingFactory();
                CombatDeathVfxPool2D pool = poolRoot.AddComponent<CombatDeathVfxPool2D>();
                pool.Configure(factory, 4);
                var authorities = new FakeEnemyAuthority2D[enemies.Length];
                var registrations = new CombatEnemyPresentationRegistration2D[enemies.Length];
                var decorated = new CombatPresentationEnemyActorAuthority2D[enemies.Length];

                for (int index = 0; index < enemies.Length; index++)
                {
                    enemies[index].AddComponent<BoxCollider2D>();
                    authorities[index] = enemies[index].AddComponent<FakeEnemyAuthority2D>();
                    authorities[index].Configure("generic-" + index, 100d + (index * 50d));
                    registrations[index] = CombatEnemyPresentationRegistration2D.Attach(
                        enemies[index],
                        authorities[index],
                        pool,
                        Vector3.up);
                    decorated[index] = new CombatPresentationEnemyActorAuthority2D(
                        authorities[index],
                        registrations[index]);
                }

                Assert.That(registrations[0].GetType(), Is.EqualTo(registrations[1].GetType()));
                Assert.That(registrations[1].GetType(), Is.EqualTo(registrations[2].GetType()));
                for (int index = 0; index < registrations.Length; index++)
                {
                    Assert.That(registrations[index].HealthBar.IsVisible, Is.True);
                    Assert.That(registrations[index].HealthBar.HasPhysicsOwnership, Is.False);
                    Assert.That(registrations[index].UsesCanonicalRuntimeProjection, Is.False);
                }

                EnemyActorStepResult damaged = decorated[0].Apply(
                    EnemyActorCommand.Damage(
                        1L,
                        Id("combat-event", "third-proof-damage"),
                        Id("actor", "player"),
                        EnemyContactPolicy.KineticChannelValue,
                        25d));
                Assert.That(damaged.State.Health, Is.EqualTo(75d));
                Assert.That(
                    registrations[0].Refresh(),
                    Is.EqualTo(CombatHealthBarRefreshStatusV1.Applied));
                Assert.That(registrations[0].HealthBar.CurrentSnapshot.NormalizedFill, Is.EqualTo(0.75d));
                Assert.That(registrations[1].HealthBar.CurrentSnapshot.NormalizedFill, Is.EqualTo(1d));
                Assert.That(registrations[2].HealthBar.CurrentSnapshot.NormalizedFill, Is.EqualTo(1d));

                decorated[0].Apply(
                    EnemyActorCommand.Damage(
                        2L,
                        Id("combat-event", "third-proof-death"),
                        Id("actor", "player"),
                        EnemyContactPolicy.KineticChannelValue,
                        100d));
                Assert.That(pool.TotalSpawnCount, Is.EqualTo(1));
                Assert.That(registrations[0].HealthBar.IsVisible, Is.False);
                Assert.That(registrations[1].HealthBar.IsVisible, Is.True);
                Assert.That(registrations[2].HealthBar.IsVisible, Is.True);

                decorated[0].Apply(
                    EnemyActorCommand.Damage(
                        2L,
                        Id("combat-event", "third-proof-death"),
                        Id("actor", "player"),
                        EnemyContactPolicy.KineticChannelValue,
                        100d));
                Assert.That(pool.TotalSpawnCount, Is.EqualTo(1));

                Assert.That(decorated[0].Reset(), Is.True);
                Assert.That(registrations[0].HealthBar.IsVisible, Is.True);
                Assert.That(
                    registrations[0].HealthBar.CurrentSnapshot.LifecycleGeneration,
                    Is.EqualTo(2L));
            }
            finally
            {
                for (int index = 0; index < enemies.Length; index++)
                {
                    UnityEngine.Object.DestroyImmediate(enemies[index]);
                }
                UnityEngine.Object.DestroyImmediate(poolRoot);
            }
        }

        [Test]
        public void CanonicalDeathFact_ProjectsExactIdentityLifecycleAndReplayLedger()
        {
            GameObject poolRoot = new GameObject("canonical-vfx-pool");
            GameObject enemyRoot = new GameObject("canonical-enemy");
            try
            {
                StableId actor = Id("enemy-entity", "canonical");
                var source = new MutableSource(new CombatHealthBarSnapshotV1(
                    actor,
                    1L,
                    100d,
                    100d,
                    CombatHealthPresentationStateV1.Alive));
                CombatHealthBarPresenter2D healthBar =
                    enemyRoot.AddComponent<CombatHealthBarPresenter2D>();
                healthBar.Configure(actor, source, Vector3.up);

                CombatDeathVfxPool2D pool = poolRoot.AddComponent<CombatDeathVfxPool2D>();
                pool.Configure(new RecordingFactory(), 3);
                EnemyDeathVfxPresenter2D presenter =
                    enemyRoot.AddComponent<EnemyDeathVfxPresenter2D>();
                presenter.Configure(actor, 1L, healthBar, pool);

                EnemyDeathFactV1 death = CreateDeathFact(actor, 1L, "canonical-death-one");
                EnemyTerminalPresentationFactV1 projected =
                    EnemyTerminalPresentationFactProjectorV1.FromCanonical(
                        death,
                        new Vector3(3f, 4f, 0f),
                        1f);
                Assert.That(projected.TerminalEventStableId, Is.EqualTo(death.DeathEventStableId));
                Assert.That(projected.EntityInstanceStableId, Is.EqualTo(actor));
                Assert.That(projected.LifecycleGeneration, Is.EqualTo(1L));
                Assert.That(
                    presenter.TryPresent(projected),
                    Is.EqualTo(EnemyDeathVfxPresentationStatusV1.Spawned));
                Assert.That(
                    presenter.TryPresent(projected),
                    Is.EqualTo(EnemyDeathVfxPresentationStatusV1.ExactReplay));
                Assert.That(pool.TotalSpawnCount, Is.EqualTo(1));

                EnemyDeathFactV1 wrong = CreateDeathFact(
                    Id("enemy-entity", "wrong"),
                    1L,
                    "canonical-wrong");
                Assert.That(
                    presenter.TryPresent(
                        EnemyTerminalPresentationFactProjectorV1.FromCanonical(
                            wrong,
                            Vector3.zero,
                            1f)),
                    Is.EqualTo(EnemyDeathVfxPresentationStatusV1.RejectedWrongEntity));

                Assert.That(presenter.AdvanceLifecycle(2L), Is.True);
                Assert.That(
                    presenter.TryPresent(projected),
                    Is.EqualTo(EnemyDeathVfxPresentationStatusV1.RejectedStaleLifecycle));
                EnemyDeathFactV1 second = CreateDeathFact(actor, 2L, "canonical-death-two");
                Assert.That(
                    presenter.TryPresent(
                        EnemyTerminalPresentationFactProjectorV1.FromCanonical(
                            second,
                            Vector3.one,
                            4f)),
                    Is.EqualTo(EnemyDeathVfxPresentationStatusV1.Spawned));
                Assert.That(pool.TotalSpawnCount, Is.EqualTo(2));
                Assert.That(pool.LastSpawnScale, Is.EqualTo(2.25f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(enemyRoot);
                UnityEngine.Object.DestroyImmediate(poolRoot);
            }
        }

        [Test]
        public void Pool_UsesInjectedFactoryAndAddsNoGameplayPhysics()
        {
            GameObject root = new GameObject("factory-pool-test");
            try
            {
                var factory = new RecordingFactory();
                CombatDeathVfxPool2D pool = root.AddComponent<CombatDeathVfxPool2D>();
                pool.Configure(factory, 2);
                ICombatDeathVfxInstance2D first = pool.Spawn(Vector3.zero, 1f);
                ICombatDeathVfxInstance2D second = pool.Spawn(Vector3.one, 2f);

                Assert.That(factory.CreateCount, Is.EqualTo(2));
                Assert.That(pool.SourcePresentationId, Is.EqualTo("test.recording-factory"));
                Assert.That(first.Root.GetComponent<Collider2D>(), Is.Null);
                Assert.That(first.Root.GetComponent<Rigidbody2D>(), Is.Null);
                Assert.That(second.Root.GetComponent<Collider2D>(), Is.Null);
                Assert.That(second.Root.GetComponent<Rigidbody2D>(), Is.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ProductionSource_RegistersPresentationGenericallyAndReferencesRetainedAsset()
        {
            string assets = Application.dataPath;
            string presentationSource = File.ReadAllText(Path.Combine(
                assets,
                "ShooterMover",
                "Production",
                "Stage1",
                "Stage1PlayableLoopCompositionV1.CombatPresentation.cs"));
            string compositionSource = File.ReadAllText(Path.Combine(
                assets,
                "ShooterMover",
                "Production",
                "Stage1",
                "Stage1PlayableLoopCompositionV1.cs"));
            string resourceAsset = File.ReadAllText(Path.Combine(
                assets,
                "ShooterMover",
                "Production",
                "Stage1",
                "Resources",
                "CombatPresentation",
                "Stage1DefaultEnemyDeathVfx.asset"));

            Assert.That(presentationSource, Does.Not.Contain("MobileBlasterDroid"));
            Assert.That(presentationSource, Does.Not.Contain("TurretPackage"));
            Assert.That(presentationSource, Does.Contain("SpriteAnimationCombatDeathVfxFactory2D"));
            Assert.That(presentationSource, Does.Contain("Resources.Load"));
            Assert.That(
                compositionSource,
                Does.Contain("RegisterEnemyCombatPresentation(root, authority)"));
            Assert.That(
                resourceAsset,
                Does.Contain("guid: 57635762d1ab47529786c2db175e0f49"));
        }

        [Test]
        public void ExplosionScale_UsesPresentationBoundsAndClamps()
        {
            var configuration = new EnemyDeathVfxScaleConfigurationV1(
                2f,
                0.75f,
                2.25f);

            Assert.That(configuration.Resolve(0.1f), Is.EqualTo(0.75f));
            Assert.That(configuration.Resolve(2f), Is.EqualTo(1f));
            Assert.That(configuration.Resolve(100f), Is.EqualTo(2.25f));
        }

        private static EnemyDeathFactV1 CreateDeathFact(
            StableId actor,
            long generation,
            string eventValue)
        {
            var identity = new EnemyRuntimeIdentityV1(
                actor,
                Id("run-participant", "enemy-" + eventValue),
                Id("run", "test"),
                Id("room-runtime-instance", "test"),
                Id("room", "test"),
                Id("placement", eventValue));
            return new EnemyDeathFactV1(
                Id("enemy-death-event", eventValue),
                Id("combat-event", "trigger-" + eventValue),
                identity,
                Id("enemy-definition", "generic"),
                1,
                generation,
                Id("actor", "player"),
                Id("run-participant", "player"),
                Id("experience-profile", "generic"),
                Id("drop-profile", "generic"),
                EnemyActorDeathCause.IncomingDamage);
        }

        private static CombatHealthBarSnapshotV1 Snapshot(
            string actorValue,
            long generation,
            double current,
            double maximum,
            CombatHealthPresentationStateV1 state)
        {
            return new CombatHealthBarSnapshotV1(
                Id("actor", actorValue),
                generation,
                current,
                maximum,
                state);
        }

        private static StableId Id(string namespaceName, string value)
        {
            return StableId.Create(namespaceName, value);
        }

        private sealed class MutableSource : ICombatHealthBarSnapshotSourceV1
        {
            public MutableSource(CombatHealthBarSnapshotV1 current)
            {
                Current = current;
            }

            public CombatHealthBarSnapshotV1 Current { get; set; }

            public bool TryRead(out CombatHealthBarSnapshotV1 snapshot)
            {
                snapshot = Current;
                return snapshot != null;
            }
        }

        public sealed class FakeEnemyAuthority2D :
            MonoBehaviour,
            IEnemyActor2DAuthority,
            ICombatPresentationLifecycleSourceV1
        {
            private StableId actorId;
            private double maximumHealth;
            private EnemyActorState state;

            public long Generation { get; private set; }

            public void Configure(string value, double health)
            {
                actorId = Id("actor", value);
                maximumHealth = health;
                Generation = 1L;
                state = CreateState();
            }

            public bool TryReadState(out EnemyActorState current)
            {
                current = state;
                return current != null;
            }

            public EnemyActorStepResult Apply(EnemyActorCommand command)
            {
                EnemyActorStepResult result = EnemyActorStepper.Step(
                    state,
                    new[] { command });
                state = result.State;
                return result;
            }

            public bool Reset()
            {
                Generation++;
                state = CreateState();
                return true;
            }

            private EnemyActorState CreateState()
            {
                return EnemyActorState.Create(
                    actorId,
                    Id("enemy-role", "generic"),
                    maximumHealth,
                    1,
                    EnemyContactPolicy.Create(
                        EnemyContactMode.None,
                        0d,
                        0.5d,
                        0.05d,
                        8));
            }
        }

        private sealed class RecordingFactory : ICombatDeathVfxFactory2D
        {
            public string SourcePresentationId
            {
                get { return "test.recording-factory"; }
            }

            public int CreateCount { get; private set; }

            public ICombatDeathVfxInstance2D Create(Transform parent, int ordinal)
            {
                CreateCount++;
                GameObject root = new GameObject("recording-vfx-" + ordinal);
                root.transform.SetParent(parent, false);
                RecordingInstance instance = root.AddComponent<RecordingInstance>();
                instance.Recycle();
                return instance;
            }

            public void Dispose()
            {
            }
        }

        public sealed class RecordingInstance : MonoBehaviour, ICombatDeathVfxInstance2D
        {
            public bool IsActive { get; private set; }
            public GameObject Root { get { return gameObject; } }

            public void Activate(Vector3 worldPosition, float scale, long spawnSequence)
            {
                transform.position = worldPosition;
                transform.localScale = Vector3.one * scale;
                gameObject.SetActive(true);
                IsActive = true;
            }

            public void Recycle()
            {
                IsActive = false;
                gameObject.SetActive(false);
            }
        }
    }
}
