using System;
using NUnit.Framework;
using ShooterMover.Domain.Common;
using ShooterMover.UnityAdapters.CombatPresentation;
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

                Assert.That(presenter.IsVisible, Is.True);
                Assert.That(presenter.CurrentSnapshot.NormalizedFill, Is.EqualTo(1d));

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
        public void GenericPresenter_BindsIndependentEnemyInstancesWithoutPhysics()
        {
            GameObject droid = new GameObject("generic-enemy-a");
            GameObject turret = new GameObject("generic-enemy-b");
            try
            {
                var droidSource = new MutableSource(Snapshot(
                    "enemy-a",
                    1L,
                    80d,
                    100d,
                    CombatHealthPresentationStateV1.Alive));
                var turretSource = new MutableSource(Snapshot(
                    "enemy-b",
                    1L,
                    240d,
                    240d,
                    CombatHealthPresentationStateV1.Alive));
                CombatHealthBarPresenter2D first =
                    droid.AddComponent<CombatHealthBarPresenter2D>();
                CombatHealthBarPresenter2D second =
                    turret.AddComponent<CombatHealthBarPresenter2D>();

                first.Configure(Id("actor", "enemy-a"), droidSource, Vector3.up);
                second.Configure(Id("actor", "enemy-b"), turretSource, Vector3.up);
                droidSource.Current = Snapshot(
                    "enemy-a",
                    1L,
                    40d,
                    100d,
                    CombatHealthPresentationStateV1.Alive);
                first.Refresh();

                Assert.That(first.GetType(), Is.EqualTo(second.GetType()));
                Assert.That(first.CurrentSnapshot.NormalizedFill, Is.EqualTo(0.4d));
                Assert.That(second.CurrentSnapshot.NormalizedFill, Is.EqualTo(1d));
                Assert.That(first.HasPhysicsOwnership, Is.False);
                Assert.That(second.HasPhysicsOwnership, Is.False);
                Assert.That(
                    droid.GetComponentsInChildren<Collider2D>(true),
                    Is.Empty);
                Assert.That(
                    turret.GetComponentsInChildren<Collider2D>(true),
                    Is.Empty);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(droid);
                UnityEngine.Object.DestroyImmediate(turret);
            }
        }

        [Test]
        public void DeathVfx_LedgersExactFact_RejectsWrongOrStale_AndAllowsNewLifecycle()
        {
            GameObject poolRoot = new GameObject("explosion-pool");
            GameObject enemyRoot = new GameObject("enemy-vfx");
            try
            {
                StableId actor = Id("actor", "enemy-vfx");
                var source = new MutableSource(Snapshot(
                    "enemy-vfx",
                    1L,
                    100d,
                    100d,
                    CombatHealthPresentationStateV1.Alive));
                CombatHealthBarPresenter2D healthBar =
                    enemyRoot.AddComponent<CombatHealthBarPresenter2D>();
                healthBar.Configure(actor, source, Vector3.up);
                DefaultCombatExplosionPool2D pool =
                    poolRoot.AddComponent<DefaultCombatExplosionPool2D>();
                pool.ConfigureForTests(2, 10f);
                EnemyDeathVfxPresenter2D presenter =
                    enemyRoot.AddComponent<EnemyDeathVfxPresenter2D>();
                presenter.Configure(
                    actor,
                    1L,
                    healthBar,
                    pool,
                    new EnemyDeathVfxScaleConfigurationV1(1f, 0.75f, 2.25f));

                EnemyTerminalPresentationFactV1 fact = new EnemyTerminalPresentationFactV1(
                    Id("event", "death-1"),
                    actor,
                    1L,
                    new Vector3(3f, 4f, 0f),
                    1f);
                Assert.That(
                    presenter.TryPresent(fact),
                    Is.EqualTo(EnemyDeathVfxPresentationStatusV1.Spawned));
                Assert.That(pool.TotalSpawnCount, Is.EqualTo(1));
                Assert.That(healthBar.IsVisible, Is.False);
                Assert.That(
                    presenter.TryPresent(fact),
                    Is.EqualTo(EnemyDeathVfxPresentationStatusV1.ExactReplay));
                Assert.That(pool.TotalSpawnCount, Is.EqualTo(1));

                Assert.That(
                    presenter.TryPresent(new EnemyTerminalPresentationFactV1(
                        Id("event", "wrong"),
                        Id("actor", "other"),
                        1L,
                        Vector3.zero,
                        1f)),
                    Is.EqualTo(EnemyDeathVfxPresentationStatusV1.RejectedWrongEntity));
                Assert.That(
                    presenter.TryPresent(new EnemyTerminalPresentationFactV1(
                        Id("event", "stale"),
                        actor,
                        0L,
                        Vector3.zero,
                        1f)),
                    Is.EqualTo(EnemyDeathVfxPresentationStatusV1.RejectedStaleLifecycle));

                Assert.That(presenter.AdvanceLifecycle(2L), Is.True);
                Assert.That(
                    presenter.TryPresent(new EnemyTerminalPresentationFactV1(
                        Id("event", "death-2"),
                        actor,
                        2L,
                        Vector3.one,
                        4f)),
                    Is.EqualTo(EnemyDeathVfxPresentationStatusV1.Spawned));
                Assert.That(pool.TotalSpawnCount, Is.EqualTo(2));
                Assert.That(pool.LastSpawnScale, Is.EqualTo(2.25f));

                DefaultCombatExplosionInstance2D[] explosions =
                    poolRoot.GetComponentsInChildren<DefaultCombatExplosionInstance2D>(true);
                Assert.That(explosions.Length, Is.EqualTo(2));
                for (int index = 0; index < explosions.Length; index++)
                {
                    Assert.That(explosions[index].GetComponent<Collider2D>(), Is.Null);
                    Assert.That(explosions[index].GetComponent<Rigidbody2D>(), Is.Null);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(enemyRoot);
                UnityEngine.Object.DestroyImmediate(poolRoot);
            }
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
    }
}
