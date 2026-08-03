using NUnit.Framework;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Enemies;
using ShooterMover.EnemyRuntimeComposition;
using ShooterMover.UnityAdapters.CombatPresentation;
using UnityEngine;

namespace ShooterMover.Tests.EditMode.CombatPresentation
{
    public sealed class CombatPresentationTests
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
            CombatHealthBarSnapshot snapshot = Snapshot(
                "projection",
                1L,
                current,
                maximum,
                current <= 0d
                    ? CombatHealthPresentationState.Terminal
                    : CombatHealthPresentationState.Alive);

            Assert.That(snapshot.CurrentHealth, Is.EqualTo(current));
            Assert.That(snapshot.MaximumHealth, Is.EqualTo(maximum));
            Assert.That(
                snapshot.NormalizedFill,
                Is.EqualTo(expectedFill).Within(0.0000001d));
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
                    CombatHealthPresentationState.Alive));
                HealthBar presenter = root.AddComponent<HealthBar>();
                presenter.Configure(actor, source, Vector3.up);

                source.Current = Snapshot(
                    "presenter",
                    1L,
                    25.5d,
                    100d,
                    CombatHealthPresentationState.Alive);
                Assert.That(
                    presenter.Refresh(),
                    Is.EqualTo(CombatHealthBarRefreshStatus.Applied));
                Assert.That(
                    presenter.CurrentSnapshot.NormalizedFill,
                    Is.EqualTo(0.255d));

                int updates = presenter.PresentationUpdateCount;
                Assert.That(
                    presenter.Refresh(),
                    Is.EqualTo(CombatHealthBarRefreshStatus.Unchanged));
                Assert.That(
                    presenter.PresentationUpdateCount,
                    Is.EqualTo(updates));

                source.Current = Snapshot(
                    "other",
                    1L,
                    10d,
                    100d,
                    CombatHealthPresentationState.Alive);
                Assert.That(
                    presenter.Refresh(),
                    Is.EqualTo(
                        CombatHealthBarRefreshStatus.RejectedEntityMismatch));

                source.Current = Snapshot(
                    "presenter",
                    1L,
                    0d,
                    100d,
                    CombatHealthPresentationState.Terminal);
                Assert.That(
                    presenter.Refresh(),
                    Is.EqualTo(CombatHealthBarRefreshStatus.HiddenTerminal));
                Assert.That(presenter.IsVisible, Is.False);

                source.Current = Snapshot(
                    "presenter",
                    2L,
                    100d,
                    100d,
                    CombatHealthPresentationState.Alive);
                Assert.That(
                    presenter.Refresh(),
                    Is.EqualTo(CombatHealthBarRefreshStatus.Applied));
                Assert.That(presenter.IsVisible, Is.True);

                source.Current = Snapshot(
                    "presenter",
                    1L,
                    50d,
                    100d,
                    CombatHealthPresentationState.Alive);
                Assert.That(
                    presenter.Refresh(),
                    Is.EqualTo(
                        CombatHealthBarRefreshStatus.RejectedStaleLifecycle));
                Assert.That(
                    presenter.CurrentSnapshot.LifecycleGeneration,
                    Is.EqualTo(2L));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
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
                var source = new MutableSource(new CombatHealthBarSnapshot(
                    actor,
                    1L,
                    100d,
                    100d,
                    CombatHealthPresentationState.Alive));
                HealthBar healthBar = enemyRoot.AddComponent<HealthBar>();
                healthBar.Configure(actor, source, Vector3.up);

                DeathEffects pool = poolRoot.AddComponent<DeathEffects>();
                pool.Configure(new RecordingFactory(), 3);
                EnemyDeathEffects presenter =
                    enemyRoot.AddComponent<EnemyDeathEffects>();
                presenter.Configure(actor, 1L, healthBar, pool);

                EnemyDeathFact death = CreateDeathFact(
                    actor,
                    1L,
                    "canonical-death-one");
                EnemyTerminalPresentationFact projected =
                    EnemyTerminalPresentationFactProjector.FromCanonical(
                        death,
                        new Vector3(3f, 4f, 0f),
                        1f);
                Assert.That(
                    projected.TerminalEventStableId,
                    Is.EqualTo(death.DeathEventStableId));
                Assert.That(
                    projected.EntityInstanceStableId,
                    Is.EqualTo(actor));
                Assert.That(projected.LifecycleGeneration, Is.EqualTo(1L));
                Assert.That(
                    presenter.TryPresent(projected),
                    Is.EqualTo(EnemyDeathVfxPresentationStatus.Spawned));
                Assert.That(
                    presenter.TryPresent(projected),
                    Is.EqualTo(EnemyDeathVfxPresentationStatus.ExactReplay));
                Assert.That(pool.TotalSpawnCount, Is.EqualTo(1));

                EnemyDeathFact wrong = CreateDeathFact(
                    Id("enemy-entity", "wrong"),
                    1L,
                    "canonical-wrong");
                Assert.That(
                    presenter.TryPresent(
                        EnemyTerminalPresentationFactProjector.FromCanonical(
                            wrong,
                            Vector3.zero,
                            1f)),
                    Is.EqualTo(
                        EnemyDeathVfxPresentationStatus.RejectedWrongEntity));

                Assert.That(presenter.AdvanceLifecycle(2L), Is.True);
                Assert.That(
                    presenter.TryPresent(projected),
                    Is.EqualTo(
                        EnemyDeathVfxPresentationStatus.RejectedStaleLifecycle));
                EnemyDeathFact second = CreateDeathFact(
                    actor,
                    2L,
                    "canonical-death-two");
                Assert.That(
                    presenter.TryPresent(
                        EnemyTerminalPresentationFactProjector.FromCanonical(
                            second,
                            Vector3.one,
                            4f)),
                    Is.EqualTo(EnemyDeathVfxPresentationStatus.Spawned));
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
                DeathEffects pool = root.AddComponent<DeathEffects>();
                pool.Configure(factory, 2);
                IDeathEffects first = pool.Spawn(Vector3.zero, 1f);
                IDeathEffects second = pool.Spawn(Vector3.one, 2f);

                Assert.That(factory.CreateCount, Is.EqualTo(2));
                Assert.That(
                    pool.SourcePresentationId,
                    Is.EqualTo("test.recording-factory"));
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
        public void ExplosionScale_UsesPresentationBoundsAndClamps()
        {
            var configuration = new EnemyDeathVfxScaleConfiguration(
                2f,
                0.75f,
                2.25f);

            Assert.That(configuration.Resolve(0.1f), Is.EqualTo(0.75f));
            Assert.That(configuration.Resolve(2f), Is.EqualTo(1f));
            Assert.That(configuration.Resolve(100f), Is.EqualTo(2.25f));
        }

        private static EnemyDeathFact CreateDeathFact(
            StableId actor,
            long generation,
            string eventValue)
        {
            var identity = new EnemyLiveIdentity(
                actor,
                Id("run-participant", "enemy-" + eventValue),
                Id("run", "test"),
                Id("room-runtime-instance", "test"),
                Id("room", "test"),
                Id("placement", eventValue));
            return new EnemyDeathFact(
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

        private static CombatHealthBarSnapshot Snapshot(
            string actorValue,
            long generation,
            double current,
            double maximum,
            CombatHealthPresentationState state)
        {
            return new CombatHealthBarSnapshot(
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

        private sealed class MutableSource : ICombatHealthBarSnapshotSource
        {
            public MutableSource(CombatHealthBarSnapshot current)
            {
                Current = current;
            }

            public CombatHealthBarSnapshot Current { get; set; }

            public bool TryRead(out CombatHealthBarSnapshot snapshot)
            {
                snapshot = Current;
                return snapshot != null;
            }
        }

        private sealed class RecordingFactory : IDeathEffectsFactory
        {
            public string SourcePresentationId
            {
                get { return "test.recording-factory"; }
            }

            public int CreateCount { get; private set; }

            public IDeathEffects Create(Transform parent, int ordinal)
            {
                CreateCount++;
                GameObject root = new GameObject("recording-vfx-" + ordinal);
                root.transform.SetParent(parent, false);
                RecordingInstance instance =
                    root.AddComponent<RecordingInstance>();
                instance.Recycle();
                return instance;
            }

            public void Dispose()
            {
            }
        }

        public sealed class RecordingInstance : MonoBehaviour, IDeathEffects
        {
            public bool IsActive { get; private set; }
            public GameObject Root { get { return gameObject; } }

            public void Activate(
                Vector3 worldPosition,
                float scale,
                long spawnSequence)
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
