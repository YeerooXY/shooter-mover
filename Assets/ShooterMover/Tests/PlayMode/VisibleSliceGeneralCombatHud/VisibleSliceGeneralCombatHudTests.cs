using System.Collections;
using System.IO;
using NUnit.Framework;
using ShooterMover.Contracts.Combat;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Enemies;
using ShooterMover.Domain.Movement;
using ShooterMover.UI.VisibleSliceGeneralCombatHud;
using UnityEngine;
using UnityEngine.TestTools;
using GeneralCombatHudComponent =
    ShooterMover.UI.VisibleSliceGeneralCombatHud.VisibleSliceGeneralCombatHud;

namespace ShooterMover.Tests.PlayMode.VisibleSliceGeneralCombatHud
{
    public sealed class VisibleSliceGeneralCombatHudTests
    {
        [Test]
        public void Projector_ProjectsAllInjectedCriticalStateWithoutColorDependence()
        {
            GeneralCombatHudSnapshot snapshot = CreateSnapshot(
                restartGeneration: 3L,
                reducedEffects: false,
                focusedEnemy: CreateEnemy(),
                playerHealth: 20d);

            GeneralCombatHudFrame frame = new GeneralCombatHudProjector().Project(
                snapshot,
                confirmedHitVisible: true);

            Assert.That(frame.PlayerHealthText, Is.EqualTo("HEALTH 20/100 (20%)"));
            Assert.That(frame.PlayerCritical, Is.True);
            Assert.That(frame.PlayerStateText, Is.EqualTo("CRITICAL"));
            Assert.That(frame.ThrusterText, Does.Contain("CHARGES 3/3"));
            Assert.That(frame.RoomText, Is.EqualTo("ROOM: TEST CHAMBER"));
            Assert.That(frame.ObjectiveText, Is.EqualTo("OBJECTIVE: DESTROY THE TURRET"));
            Assert.That(frame.RestartHint, Is.EqualTo("RESTART: R / MENU"));
            Assert.That(frame.HasFocusedEnemy, Is.True);
            Assert.That(frame.FocusedEnemyHealthText, Is.EqualTo("HEALTH 100/100 (100%)"));
            Assert.That(frame.FocusedEnemyStateText, Is.EqualTo("ACTIVE"));
            Assert.That(frame.ReticleVisible, Is.True);
            Assert.That(frame.ConfirmedHitVisible, Is.True);
            Assert.That(frame.ConfirmedHitText, Is.EqualTo("HIT CONFIRMED +"));
            Assert.That(frame.ToTraceString(), Does.Contain("player=HEALTH 20/100 (20%)|CRITICAL"));
        }

        [TestCase(1920, 1080)]
        [TestCase(1280, 720)]
        public void Layout_ReservesWp010StripAndKeepsEveryHudPanelInsideSafeArea(
            int width,
            int height)
        {
            GeneralCombatHudLayout layout = GeneralCombatHudLayoutCalculator.Compute(
                width,
                height,
                0.5d,
                0.5d);

            Assert.That(
                layout.WeaponStripReservation.height,
                Is.EqualTo(GeneralCombatHudLayoutCalculator.Wp010ReservedHeight));
            Assert.That(
                layout.WeaponStripReservation.yMax,
                Is.EqualTo(height));
            Assert.That(layout.AnyHudPanelOverlapsWeaponStrip(), Is.False);

            AssertInside(layout.Screen, layout.PlayerPanel);
            AssertInside(layout.Screen, layout.RoomPanel);
            AssertInside(layout.Screen, layout.FocusedEnemyPanel);
            AssertInside(layout.Screen, layout.ReducedEffectsPanel);
            AssertInside(layout.Screen, layout.RestartPanel);
            AssertInside(layout.Screen, layout.ReticleBounds);
            AssertInside(layout.Screen, layout.HitConfirmationPanel);
        }

        [UnityTest]
        public IEnumerator Presenter_ShowsTransientOnlyForAcceptedConfirmedHitFacts()
        {
            GameObject host = new GameObject("VS-004 HUD Test");
            try
            {
                GeneralCombatHudComponent hud =
                    host.AddComponent<GeneralCombatHudComponent>();
                hud.SetHitConfirmationSeconds(0.20f);
                hud.Present(CreateSnapshot(), 10d);

                HitMessage blocked = CreateHit("blocked", HitResult.Blocked);
                Assert.That(hud.AcceptHitFact(blocked, 10d), Is.False);
                Assert.That(hud.CurrentFrame.ConfirmedHitVisible, Is.False);

                HitMessage missed = CreateHit("missed", HitResult.Missed);
                Assert.That(hud.AcceptHitFact(missed, 10d), Is.False);
                Assert.That(hud.CurrentFrame.ConfirmedHitVisible, Is.False);

                HitMessage duplicate = CreateHit(
                    "duplicate",
                    HitResult.DuplicateEventIgnored);
                Assert.That(hud.AcceptHitFact(duplicate, 10d), Is.False);
                Assert.That(hud.CurrentFrame.ConfirmedHitVisible, Is.False);

                HitMessage destroyed = CreateHit(
                    "already-destroyed",
                    HitResult.TargetAlreadyDestroyed);
                Assert.That(hud.AcceptHitFact(destroyed, 10d), Is.False);
                Assert.That(hud.CurrentFrame.ConfirmedHitVisible, Is.False);

                HitMessage confirmed = CreateHit("confirmed", HitResult.Confirmed);
                Assert.That(hud.AcceptHitFact(confirmed, 10d), Is.True);
                Assert.That(hud.CurrentFrame.ConfirmedHitVisible, Is.True);

                Assert.That(hud.AcceptHitFact(confirmed, 10.01d), Is.False);
                hud.Present(CreateSnapshot(), 10.21d);
                Assert.That(hud.CurrentFrame.ConfirmedHitVisible, Is.False);
                yield return null;
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [UnityTest]
        public IEnumerator Presenter_RestartGenerationClearsVisibleHitWithoutChangingSourceState()
        {
            GameObject host = new GameObject("VS-004 Restart Test");
            GeneralCombatHudSnapshot generationOne = CreateSnapshot(restartGeneration: 1L);
            GeneralCombatHudSnapshot generationTwo = CreateSnapshot(restartGeneration: 2L);
            try
            {
                GeneralCombatHudComponent hud =
                    host.AddComponent<GeneralCombatHudComponent>();
                hud.Present(generationOne, 2d);
                Assert.That(
                    hud.AcceptHitFact(
                        CreateHit("restart-visible", HitResult.Confirmed),
                        2d),
                    Is.True);
                Assert.That(hud.CurrentFrame.ConfirmedHitVisible, Is.True);

                hud.Present(generationTwo, 2.01d);

                Assert.That(hud.CurrentFrame.ConfirmedHitVisible, Is.False);
                Assert.That(hud.CurrentFrame.RestartGeneration, Is.EqualTo(2L));
                Assert.That(generationOne.RestartGeneration, Is.EqualTo(1L));
                Assert.That(generationTwo.RestartGeneration, Is.EqualTo(2L));
                yield return null;
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [UnityTest]
        public IEnumerator BoundSources_AreReadOnlyAndRepeatedFactsAreDeduplicated()
        {
            GeneralCombatHudSnapshot snapshot = CreateSnapshot(
                restartGeneration: 5L,
                focusedEnemy: CreateEnemy(),
                playerHealth: 75d);
            HitMessage hit = CreateHit("source-hit", HitResult.Confirmed);
            FakeStateSource stateSource = new FakeStateSource(snapshot);
            FakeHitSource hitSource = new FakeHitSource(hit);
            GameObject host = new GameObject("VS-004 Source Test");
            try
            {
                GeneralCombatHudComponent hud =
                    host.AddComponent<GeneralCombatHudComponent>();
                hud.BindSources(stateSource, hitSource);

                Assert.That(hud.RefreshFromSources(4d), Is.True);
                Assert.That(hud.CurrentFrame.ConfirmedHitVisible, Is.True);
                Assert.That(hud.RefreshFromSources(4.01d), Is.True);
                Assert.That(stateSource.ReadCount, Is.EqualTo(2));
                Assert.That(hitSource.ReadCount, Is.EqualTo(2));

                Assert.That(stateSource.Snapshot, Is.SameAs(snapshot));
                Assert.That(snapshot.PlayerVital.Health, Is.EqualTo(75d));
                Assert.That(snapshot.FocusedEnemy.Health, Is.EqualTo(100d));
                Assert.That(snapshot.ThrusterStatus.AvailableCharges, Is.EqualTo(3));
                Assert.That(hud.CurrentFrame.RestartGeneration, Is.EqualTo(5L));
                yield return null;
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [UnityTest]
        public IEnumerator Presenter_VisibilityTogglePreservesProjectedReadOnlyFrame()
        {
            GameObject host = new GameObject("VS-004 Visibility Test");
            try
            {
                GeneralCombatHudComponent hud =
                    host.AddComponent<GeneralCombatHudComponent>();
                GeneralCombatHudSnapshot snapshot = CreateSnapshot(playerHealth: 60d);
                hud.Present(snapshot, 1d);
                GeneralCombatHudFrame projected = hud.CurrentFrame;

                hud.SetVisible(false);
                Assert.That(hud.IsVisible, Is.False);
                Assert.That(hud.CurrentFrame, Is.SameAs(projected));
                Assert.That(snapshot.PlayerVital.Health, Is.EqualTo(60d));

                hud.SetVisible(true);
                Assert.That(hud.IsVisible, Is.True);
                Assert.That(hud.CurrentFrame, Is.SameAs(projected));
                yield return null;
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void ReducedEffectsAndMissingFocusKeepCriticalInformation()
        {
            GeneralCombatHudSnapshot snapshot = CreateSnapshot(
                reducedEffects: true,
                focusedEnemy: null,
                playerHealth: 0d);
            GeneralCombatHudFrame frame = new GeneralCombatHudProjector().Project(
                snapshot,
                confirmedHitVisible: false);

            Assert.That(frame.PlayerStateText, Is.EqualTo("DESTROYED"));
            Assert.That(frame.HasFocusedEnemy, Is.False);
            Assert.That(frame.FocusedEnemyTitle, Is.EqualTo("NO FOCUSED ENEMY"));
            Assert.That(frame.FocusedEnemyStateText, Is.EqualTo("NO TARGET"));
            Assert.That(frame.ReducedEffects, Is.True);
            Assert.That(
                frame.ReducedEffectsWarning,
                Is.EqualTo("REDUCED EFFECTS ENABLED"));
            Assert.That(frame.RestartHint, Is.Not.Empty);
            Assert.That(frame.ObjectiveText, Is.Not.Empty);
        }

        [Test]
        public void Snapshot_BoundsReticleAndLongObjectiveDeterministically()
        {
            string longObjective = new string('X', 200);
            GeneralCombatHudSnapshot snapshot = new GeneralCombatHudSnapshot(
                new VitalState(100d, 100d, 0d, 0d),
                CreateThruster(),
                null,
                null,
                "  TEST   ROOM  ",
                longObjective,
                "",
                "",
                true,
                -10d,
                5d,
                false,
                0L);

            Assert.That(snapshot.RoomName, Is.EqualTo("TEST ROOM"));
            Assert.That(snapshot.ObjectiveText.Length, Is.EqualTo(120));
            Assert.That(snapshot.ObjectiveText, Does.EndWith("..."));
            Assert.That(snapshot.ReticleNormalizedX, Is.EqualTo(0d));
            Assert.That(snapshot.ReticleNormalizedY, Is.EqualTo(1d));

            GeneralCombatHudFrame frame =
                new GeneralCombatHudProjector().Project(snapshot, false);
            Assert.That(frame.RestartHint, Is.EqualTo("RESTART: R / MENU"));
        }

        [Test]
        public void OwnedRuntimeSource_DoesNotReferenceOrDuplicateWp010()
        {
            string ownedRoot = Path.Combine(
                UnityEngine.Application.dataPath,
                "ShooterMover",
                "UI",
                "VisibleSliceGeneralCombatHud");
            string[] sources = Directory.GetFiles(
                ownedRoot,
                "*.cs",
                SearchOption.AllDirectories);

            Assert.That(sources.Length, Is.EqualTo(1));
            string source = File.ReadAllText(sources[0]);
            Assert.That(source, Does.Not.Contain("Stage1WeaponStatusStrip"));
            Assert.That(source, Does.Not.Contain("FourMountStatusSnapshot"));
            Assert.That(
                source,
                Does.Not.Contain("ContentPackages.Weapons.Stage1Presentation"));
            Assert.That(
                GeneralCombatHudLayoutCalculator.Wp010ReservedHeight,
                Is.EqualTo(196f));
        }

        private static GeneralCombatHudSnapshot CreateSnapshot(
            long restartGeneration = 0L,
            bool reducedEffects = false,
            EnemyActorState focusedEnemy = null,
            double playerHealth = 100d)
        {
            return new GeneralCombatHudSnapshot(
                new VitalState(playerHealth, 100d, 0d, 0d),
                CreateThruster(),
                focusedEnemy,
                "BLASTER TURRET",
                "TEST CHAMBER",
                "DESTROY THE TURRET",
                "R",
                "MENU",
                true,
                0.5d,
                0.5d,
                reducedEffects,
                restartGeneration);
        }

        private static ThrusterStatusSnapshot CreateThruster()
        {
            return new ThrusterStatusSnapshot(
                ThrusterStatusState.Ready,
                StableId.Parse("tuning.test-thruster"),
                0L,
                3,
                3,
                0,
                1.50d,
                ThrusterBurstPhase.Ready,
                0d,
                0d,
                0d,
                0d,
                0d,
                0d,
                0d,
                0d,
                0.25d,
                0.25d);
        }

        private static EnemyActorState CreateEnemy()
        {
            return EnemyActorState.Create(
                StableId.Parse("enemy.focused-turret"),
                StableId.Parse("role.blaster-turret"),
                100d,
                4,
                EnemyContactPolicy.Create(
                    EnemyContactMode.None,
                    0d,
                    0.50d,
                    0d,
                    4));
        }

        private static HitMessage CreateHit(string value, HitResult result)
        {
            return new HitMessage(
                StableId.Create("event", value),
                StableId.Parse("actor.player"),
                StableId.Parse("enemy.focused-turret"),
                CombatChannel.Kinetic,
                result);
        }

        private static void AssertInside(Rect outer, Rect inner)
        {
            Assert.That(inner.xMin, Is.GreaterThanOrEqualTo(outer.xMin));
            Assert.That(inner.yMin, Is.GreaterThanOrEqualTo(outer.yMin));
            Assert.That(inner.xMax, Is.LessThanOrEqualTo(outer.xMax));
            Assert.That(inner.yMax, Is.LessThanOrEqualTo(outer.yMax));
        }

        private sealed class FakeStateSource : IGeneralCombatHudStateSource
        {
            public FakeStateSource(GeneralCombatHudSnapshot snapshot)
            {
                Snapshot = snapshot;
            }

            public GeneralCombatHudSnapshot Snapshot { get; }

            public int ReadCount { get; private set; }

            public bool TryRead(out GeneralCombatHudSnapshot snapshot)
            {
                ReadCount++;
                snapshot = Snapshot;
                return true;
            }
        }

        private sealed class FakeHitSource : IGeneralCombatHudHitFactSource
        {
            private readonly HitMessage hit;

            public FakeHitSource(HitMessage hit)
            {
                this.hit = hit;
            }

            public int ReadCount { get; private set; }

            public bool TryReadLatest(out HitMessage hitFact)
            {
                ReadCount++;
                hitFact = hit;
                return true;
            }
        }
    }
}
