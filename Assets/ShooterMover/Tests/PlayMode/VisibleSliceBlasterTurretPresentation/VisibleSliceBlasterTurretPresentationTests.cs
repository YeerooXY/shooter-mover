using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShooterMover.Presentation.VisibleSliceBlasterTurret;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ShooterMover.Tests.PlayMode.VisibleSliceBlasterTurretPresentation
{
    public sealed class VisibleSliceBlasterTurretPresentationTests
    {
        [Test]
        public void Projector_ProjectsIdleWarningFiringRecoveryDamageAndHealth()
        {
            VisibleSliceBlasterTurretFrame idle = Project(
                CreateSnapshot(
                    phase: VisibleSliceBlasterTurretPhase.Idle,
                    currentHealth: 100));
            Assert.That(idle.StateText, Is.EqualTo("ACTIVE / IDLE"));
            Assert.That(idle.HealthText, Is.EqualTo("HP 100/100 (100%)"));
            Assert.That(idle.NormalizedHealth, Is.EqualTo(1d));
            Assert.That(idle.WarningVisible, Is.False);
            Assert.That(idle.FiringVisible, Is.False);
            Assert.That(idle.RecoveryVisible, Is.False);

            VisibleSliceBlasterTurretFrame warning = Project(
                CreateSnapshot(
                    fixedStep: 1L,
                    phase: VisibleSliceBlasterTurretPhase.Warning,
                    phaseElapsedSeconds: 0.25d,
                    phaseDurationSeconds: 1d,
                    warningCountRemaining: 4));
            Assert.That(warning.StateText, Is.EqualTo("WARNING"));
            Assert.That(warning.WarningVisible, Is.True);
            Assert.That(warning.WarningGlyph, Is.EqualTo("!"));
            Assert.That(warning.WarningShapeText, Is.EqualTo("TRIANGLE + RAIL"));
            Assert.That(warning.WarningCountText, Is.EqualTo("04"));
            Assert.That(warning.WarningTimingText, Is.EqualTo("0.75s"));
            Assert.That(warning.OptionalPulse, Is.True);
            Assert.That(warning.OptionalMotion, Is.True);

            VisibleSliceBlasterTurretFrame firing = Project(
                CreateSnapshot(
                    fixedStep: 2L,
                    phase: VisibleSliceBlasterTurretPhase.Firing));
            Assert.That(firing.StateText, Is.EqualTo("FIRING"));
            Assert.That(firing.FiringVisible, Is.True);
            Assert.That(firing.WarningVisible, Is.False);

            VisibleSliceBlasterTurretFrame recovery = Project(
                CreateSnapshot(
                    fixedStep: 3L,
                    phase: VisibleSliceBlasterTurretPhase.Recovery,
                    currentHealth: 75));
            Assert.That(recovery.StateText, Is.EqualTo("RECOVERY"));
            Assert.That(recovery.HealthText, Is.EqualTo("HP 75/100 (75%)"));
            Assert.That(recovery.NormalizedHealth, Is.EqualTo(0.75d));
            Assert.That(recovery.RecoveryVisible, Is.True);

            VisibleSliceBlasterTurretFrame damaged = VisibleSliceBlasterTurretProjector.Project(
                CreateSnapshot(
                    fixedStep: 4L,
                    phase: VisibleSliceBlasterTurretPhase.Idle,
                    currentHealth: 60,
                    damageObserved: true,
                    damageSequence: 7L),
                damageTransientVisible: true,
                reducedEffectsOverride: false,
                grayscaleOverride: false);
            Assert.That(damaged.StateText, Is.EqualTo("ACTIVE + HIT"));
            Assert.That(damaged.DamageVisible, Is.True);
            Assert.That(damaged.DamageText, Is.EqualTo("HIT"));
        }

        [Test]
        public void WarningAndDamageSameFrame_PreservesWarningIdentityAndTiming()
        {
            VisibleSliceBlasterTurretSnapshot snapshot = CreateSnapshot(
                fixedStep: 12L,
                phase: VisibleSliceBlasterTurretPhase.Warning,
                currentHealth: 68,
                phaseElapsedSeconds: 0.40d,
                phaseDurationSeconds: 1d,
                warningCountRemaining: 3,
                damageObserved: true,
                damageSequence: 2L);

            VisibleSliceBlasterTurretFrame frame =
                VisibleSliceBlasterTurretProjector.Project(
                    snapshot,
                    damageTransientVisible: true,
                    reducedEffectsOverride: false,
                    grayscaleOverride: false);

            Assert.That(frame.WarningVisible, Is.True);
            Assert.That(frame.DamageVisible, Is.True);
            Assert.That(frame.StateText, Is.EqualTo("WARNING + HIT"));
            Assert.That(frame.WarningGlyph, Is.EqualTo("!"));
            Assert.That(frame.WarningShapeText, Is.EqualTo("TRIANGLE + RAIL"));
            Assert.That(frame.WarningCountText, Is.EqualTo("03"));
            Assert.That(frame.WarningTimingText, Is.EqualTo("0.60s"));
        }

        [Test]
        public void ZeroHealthBeforeScheduledShot_ProjectsDestroyedAndNeverInventsFire()
        {
            VisibleSliceBlasterTurretSnapshot snapshot = CreateSnapshot(
                fixedStep: 20L,
                phase: VisibleSliceBlasterTurretPhase.Firing,
                currentHealth: 0,
                damageObserved: true,
                damageSequence: 9L);

            VisibleSliceBlasterTurretFrame frame =
                VisibleSliceBlasterTurretProjector.Project(
                    snapshot,
                    damageTransientVisible: true,
                    reducedEffectsOverride: false,
                    grayscaleOverride: false);

            Assert.That(frame.Phase, Is.EqualTo(VisibleSliceBlasterTurretPhase.Destroyed));
            Assert.That(frame.StateText, Is.EqualTo("X DESTROYED"));
            Assert.That(frame.DestroyedVisible, Is.True);
            Assert.That(frame.DeactivatedVisible, Is.False);
            Assert.That(frame.FiringVisible, Is.False);
            Assert.That(frame.WarningVisible, Is.False);
            Assert.That(frame.RecoveryVisible, Is.False);
            Assert.That(frame.DamageVisible, Is.False);
            Assert.That(frame.HealthText, Is.EqualTo("HP 0/100 (0%)"));
        }

        [Test]
        public void DeactivatedState_IsDistinctFromDestroyedAndCarriesNoAttackCue()
        {
            VisibleSliceBlasterTurretFrame frame = Project(
                CreateSnapshot(
                    phase: VisibleSliceBlasterTurretPhase.Deactivated,
                    currentHealth: 40));

            Assert.That(frame.Phase, Is.EqualTo(VisibleSliceBlasterTurretPhase.Deactivated));
            Assert.That(frame.StateText, Is.EqualTo("X DEACTIVATED"));
            Assert.That(frame.DestroyedVisible, Is.False);
            Assert.That(frame.DeactivatedVisible, Is.True);
            Assert.That(frame.WarningVisible, Is.False);
            Assert.That(frame.FiringVisible, Is.False);
            Assert.That(frame.RecoveryVisible, Is.False);
            Assert.That(frame.HealthText, Is.EqualTo("HP 40/100 (40%)"));
        }

        [UnityTest]
        public IEnumerator RestartGenerationDuringWarning_ClearsStaleDamageAndWarningImmediately()
        {
            GameObject host = new GameObject("VS-003 Restart Presentation Test");
            try
            {
                VisibleSliceBlasterTurretPresenter presenter =
                    host.AddComponent<VisibleSliceBlasterTurretPresenter>();
                presenter.SetAutoRefreshSource(false);
                presenter.Present(
                    CreateSnapshot(
                        restartGeneration: 0L,
                        fixedStep: 8L,
                        phase: VisibleSliceBlasterTurretPhase.Warning,
                        currentHealth: 70,
                        phaseElapsedSeconds: 0.2d,
                        phaseDurationSeconds: 1d,
                        warningCountRemaining: 4,
                        damageObserved: true,
                        damageSequence: 5L),
                    10d);

                Assert.That(presenter.CurrentFrame.WarningVisible, Is.True);
                Assert.That(presenter.CurrentFrame.DamageVisible, Is.True);

                presenter.Present(
                    CreateSnapshot(
                        restartGeneration: 1L,
                        fixedStep: 0L,
                        phase: VisibleSliceBlasterTurretPhase.Idle,
                        currentHealth: 100),
                    10.01d);

                Assert.That(presenter.CurrentFrame.RestartGeneration, Is.EqualTo(1L));
                Assert.That(presenter.CurrentFrame.FixedStep, Is.EqualTo(0L));
                Assert.That(presenter.CurrentFrame.WarningVisible, Is.False);
                Assert.That(presenter.CurrentFrame.DamageVisible, Is.False);
                Assert.That(presenter.CurrentFrame.StateText, Is.EqualTo("ACTIVE / IDLE"));
                yield return null;
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [UnityTest]
        public IEnumerator ReadOnlySourceAndPresenterRemoval_DoNotMutateAnyAuthoritySentinel()
        {
            FakeReadOnlySource source = new FakeReadOnlySource(
                CreateSnapshot(
                    restartGeneration: 3L,
                    fixedStep: 31L,
                    phase: VisibleSliceBlasterTurretPhase.Warning,
                    currentHealth: 67,
                    phaseElapsedSeconds: 0.5d,
                    phaseDurationSeconds: 1d,
                    warningCountRemaining: 2));
            AuthoritySentinels before = source.CaptureSentinels();
            GameObject host = new GameObject("VS-003 Parity Test");
            try
            {
                VisibleSliceBlasterTurretPresenter presenter =
                    host.AddComponent<VisibleSliceBlasterTurretPresenter>();
                presenter.SetAutoRefreshSource(false);
                presenter.BindSource(source);

                Assert.That(presenter.RefreshFromSource(1d), Is.True);
                Assert.That(presenter.RefreshFromSource(1.1d), Is.True);
                Assert.That(source.ReadCount, Is.EqualTo(2));
                Assert.That(source.CaptureSentinels(), Is.EqualTo(before));
                Assert.That(source.Snapshot.CurrentHealth, Is.EqualTo(67));
                Assert.That(source.Snapshot.FixedStep, Is.EqualTo(31L));
            }
            finally
            {
                Object.DestroyImmediate(host);
            }

            Assert.That(source.CaptureSentinels(), Is.EqualTo(before));
            Assert.That(source.Snapshot.CurrentHealth, Is.EqualTo(67));
            Assert.That(source.Snapshot.FixedStep, Is.EqualTo(31L));
            yield return null;
        }

        [Test]
        public void ReducedEffectsAndGrayscale_PreserveWarningShapeTextCountAndTiming()
        {
            VisibleSliceBlasterTurretSnapshot snapshot = CreateSnapshot(
                phase: VisibleSliceBlasterTurretPhase.Warning,
                phaseElapsedSeconds: 0.25d,
                phaseDurationSeconds: 1d,
                warningCountRemaining: 4);

            VisibleSliceBlasterTurretFrame baseline =
                VisibleSliceBlasterTurretProjector.Project(
                    snapshot,
                    false,
                    false,
                    false);
            VisibleSliceBlasterTurretFrame reduced =
                VisibleSliceBlasterTurretProjector.Project(
                    snapshot,
                    false,
                    true,
                    false);
            VisibleSliceBlasterTurretFrame grayscale =
                VisibleSliceBlasterTurretProjector.Project(
                    snapshot,
                    false,
                    false,
                    true);

            AssertWarningIdentityEqual(baseline, reduced);
            AssertWarningIdentityEqual(baseline, grayscale);
            Assert.That(reduced.ReducedEffects, Is.True);
            Assert.That(reduced.OptionalPulse, Is.False);
            Assert.That(reduced.OptionalMotion, Is.False);
            Assert.That(grayscale.Grayscale, Is.True);
            Assert.That(grayscale.WarningVisible, Is.True);
        }

        [Test]
        public void StateTransitionTrace_IsDeterministic()
        {
            List<string> trace = new List<string>
            {
                Project(CreateSnapshot(
                    fixedStep: 0L,
                    phase: VisibleSliceBlasterTurretPhase.Idle)).ToTraceString(),
                Project(CreateSnapshot(
                    fixedStep: 1L,
                    phase: VisibleSliceBlasterTurretPhase.Warning,
                    phaseElapsedSeconds: 0.25d,
                    phaseDurationSeconds: 1d,
                    warningCountRemaining: 4)).ToTraceString(),
                VisibleSliceBlasterTurretProjector.Project(
                    CreateSnapshot(
                        fixedStep: 1L,
                        phase: VisibleSliceBlasterTurretPhase.Warning,
                        currentHealth: 75,
                        phaseElapsedSeconds: 0.25d,
                        phaseDurationSeconds: 1d,
                        warningCountRemaining: 4,
                        damageObserved: true,
                        damageSequence: 1L),
                    true,
                    false,
                    false).ToTraceString(),
                Project(CreateSnapshot(
                    fixedStep: 2L,
                    phase: VisibleSliceBlasterTurretPhase.Firing,
                    currentHealth: 75)).ToTraceString(),
                Project(CreateSnapshot(
                    fixedStep: 3L,
                    phase: VisibleSliceBlasterTurretPhase.Recovery,
                    currentHealth: 75)).ToTraceString(),
                Project(CreateSnapshot(
                    fixedStep: 4L,
                    phase: VisibleSliceBlasterTurretPhase.Warning,
                    currentHealth: 25,
                    phaseElapsedSeconds: 0.75d,
                    phaseDurationSeconds: 1d,
                    warningCountRemaining: 1)).ToTraceString(),
                Project(CreateSnapshot(
                    fixedStep: 5L,
                    phase: VisibleSliceBlasterTurretPhase.Firing,
                    currentHealth: 0)).ToTraceString(),
                Project(CreateSnapshot(
                    restartGeneration: 1L,
                    fixedStep: 0L,
                    phase: VisibleSliceBlasterTurretPhase.Idle,
                    currentHealth: 100)).ToTraceString(),
            };

            string joined = string.Join("\n", trace);
            string expected =
                "generation=0|step=0|phase=Idle|health=100/100|warning=off|count=--|timing=--|firing=off|recovery=off|damage=off|destroyed=off|deactivated=off|reduced=off|grayscale=off\n"
                + "generation=0|step=1|phase=Warning|health=100/100|warning=on|count=04|timing=0.75s|firing=off|recovery=off|damage=off|destroyed=off|deactivated=off|reduced=off|grayscale=off\n"
                + "generation=0|step=1|phase=Warning|health=75/100|warning=on|count=04|timing=0.75s|firing=off|recovery=off|damage=on|destroyed=off|deactivated=off|reduced=off|grayscale=off\n"
                + "generation=0|step=2|phase=Firing|health=75/100|warning=off|count=--|timing=--|firing=on|recovery=off|damage=off|destroyed=off|deactivated=off|reduced=off|grayscale=off\n"
                + "generation=0|step=3|phase=Recovery|health=75/100|warning=off|count=--|timing=--|firing=off|recovery=on|damage=off|destroyed=off|deactivated=off|reduced=off|grayscale=off\n"
                + "generation=0|step=4|phase=Warning|health=25/100|warning=on|count=01|timing=0.25s|firing=off|recovery=off|damage=off|destroyed=off|deactivated=off|reduced=off|grayscale=off\n"
                + "generation=0|step=5|phase=Destroyed|health=0/100|warning=off|count=--|timing=--|firing=off|recovery=off|damage=off|destroyed=on|deactivated=off|reduced=off|grayscale=off\n"
                + "generation=1|step=0|phase=Idle|health=100/100|warning=off|count=--|timing=--|firing=off|recovery=off|damage=off|destroyed=off|deactivated=off|reduced=off|grayscale=off";

            Assert.That(joined, Is.EqualTo(expected));
            TestContext.WriteLine("VS-003 deterministic state-transition trace:");
            TestContext.WriteLine(joined);
        }

        [Test]
        public void PublicSourceBoundary_IsGetterOnlyAndHasNoMutationSurface()
        {
            Type sourceType = typeof(IVisibleSliceBlasterTurretPresentationSource);
            MethodInfo[] methods = sourceType.GetMethods();
            PropertyInfo[] properties = sourceType.GetProperties();

            Assert.That(methods.Length, Is.EqualTo(1));
            Assert.That(methods[0].Name, Is.EqualTo("TryReadSnapshot"));
            Assert.That(methods[0].ReturnType, Is.EqualTo(typeof(bool)));
            Assert.That(properties.Length, Is.EqualTo(0));

            string methodNames = string.Join(",", methods.Select(method => method.Name));
            Assert.That(methodNames, Does.Not.Contain("Apply"));
            Assert.That(methodNames, Does.Not.Contain("Damage"));
            Assert.That(methodNames, Does.Not.Contain("Step"));
            Assert.That(methodNames, Does.Not.Contain("Target"));
            Assert.That(methodNames, Does.Not.Contain("Execute"));
            Assert.That(methodNames, Does.Not.Contain("Restart"));
        }

        [Test]
        public void OwnedRuntimeSourceAndPrefab_ContainNoGameplayAuthority()
        {
            string runtimeRoot = Path.Combine(
                UnityEngine.Application.dataPath,
                "ShooterMover",
                "Runtime",
                "Presentation",
                "VisibleSliceBlasterTurret");
            string[] sources = Directory.GetFiles(
                runtimeRoot,
                "*.cs",
                SearchOption.AllDirectories);
            Assert.That(sources.Length, Is.EqualTo(2));

            string combined = string.Join(
                "\n",
                sources.OrderBy(path => path, StringComparer.Ordinal)
                    .Select(File.ReadAllText));
            string[] forbidden =
            {
                "ExecuteFixedStep(",
                "EnemyActorStepper.Step",
                "Cadence.Step",
                "ExecutePlan(",
                "ProjectileExecutionPlanAdapter",
                "WeaponMount2DAdapter",
                "Authority.Apply",
                "RestartSession(",
                "TargetAdapter",
                "EncounterLifecycle",
                "MissionRunState",
                "PlayerPrefs",
                "UnityEngine.SceneManagement",
                "Resources.Load",
            };
            foreach (string token in forbidden)
            {
                Assert.That(combined, Does.Not.Contain(token), token);
            }

            string prefabPath = Path.Combine(
                runtimeRoot,
                "VisibleSliceBlasterTurretPresentation.prefab");
            string prefab = File.ReadAllText(prefabPath);
            Assert.That(
                prefab,
                Does.Contain("guid: b65aa471a0fda0948b4ea90b01e87ed1"));
            Assert.That(prefab, Does.Not.Contain("Rigidbody2D"));
            Assert.That(prefab, Does.Not.Contain("Collider2D"));
            Assert.That(prefab, Does.Not.Contain("BlasterTurretPackage"));
            Assert.That(prefab, Does.Not.Contain("WeaponMount2DAdapter"));
            Assert.That(prefab, Does.Not.Contain("Projectile"));
            Assert.That(
                VisibleSliceBlasterTurretPresenter.AcceptedPrototypeSpritePath,
                Is.EqualTo(
                    "Assets/ShooterMover/Art/Prototype/Stage1VisibleSlice/enemy_standing_turret_weak.png"));
        }

        [Test]
        public void SnapshotValidation_FailsClosedForInvalidProjectionFacts()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => CreateSnapshot(currentHealth: -1));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => CreateSnapshot(currentHealth: 101));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new VisibleSliceBlasterTurretSnapshot(
                    0L,
                    0L,
                    VisibleSliceBlasterTurretPhase.Warning,
                    100,
                    100,
                    double.NaN,
                    1d,
                    4,
                    0d,
                    1d,
                    false,
                    -1L,
                    false,
                    false));
        }

        private static VisibleSliceBlasterTurretFrame Project(
            VisibleSliceBlasterTurretSnapshot snapshot)
        {
            return VisibleSliceBlasterTurretProjector.Project(
                snapshot,
                false,
                false,
                false);
        }

        private static VisibleSliceBlasterTurretSnapshot CreateSnapshot(
            long restartGeneration = 0L,
            long fixedStep = 0L,
            VisibleSliceBlasterTurretPhase phase = VisibleSliceBlasterTurretPhase.Idle,
            int currentHealth = 100,
            int maximumHealth = 100,
            double phaseElapsedSeconds = 0d,
            double phaseDurationSeconds = 0d,
            int warningCountRemaining = 0,
            double warningDirectionX = 1d,
            double warningDirectionY = 0d,
            bool damageObserved = false,
            long damageSequence = -1L,
            bool reducedEffects = false,
            bool grayscaleRequested = false)
        {
            return new VisibleSliceBlasterTurretSnapshot(
                restartGeneration,
                fixedStep,
                phase,
                currentHealth,
                maximumHealth,
                phaseElapsedSeconds,
                phaseDurationSeconds,
                warningCountRemaining,
                warningDirectionX,
                warningDirectionY,
                damageObserved,
                damageSequence,
                reducedEffects,
                grayscaleRequested);
        }

        private static void AssertWarningIdentityEqual(
            VisibleSliceBlasterTurretFrame expected,
            VisibleSliceBlasterTurretFrame actual)
        {
            Assert.That(actual.WarningVisible, Is.EqualTo(expected.WarningVisible));
            Assert.That(actual.WarningGlyph, Is.EqualTo(expected.WarningGlyph));
            Assert.That(actual.WarningShapeText, Is.EqualTo(expected.WarningShapeText));
            Assert.That(actual.WarningCountText, Is.EqualTo(expected.WarningCountText));
            Assert.That(actual.WarningTimingText, Is.EqualTo(expected.WarningTimingText));
            Assert.That(actual.StateText, Is.EqualTo(expected.StateText));
        }

        private sealed class FakeReadOnlySource :
            IVisibleSliceBlasterTurretPresentationSource
        {
            private readonly AuthoritySentinels sentinels =
                new AuthoritySentinels(
                    enemyHealth: 67,
                    lifecycle: "active",
                    selectedTarget: "player.primary",
                    cadenceStep: 31L,
                    projectileExecutions: 3,
                    encounterState: "running",
                    persistenceState: "untouched");

            public FakeReadOnlySource(VisibleSliceBlasterTurretSnapshot snapshot)
            {
                Snapshot = snapshot;
            }

            public VisibleSliceBlasterTurretSnapshot Snapshot { get; }

            public int ReadCount { get; private set; }

            public bool TryReadSnapshot(
                out VisibleSliceBlasterTurretSnapshot snapshot)
            {
                ReadCount++;
                snapshot = Snapshot;
                return true;
            }

            public AuthoritySentinels CaptureSentinels()
            {
                return sentinels;
            }
        }

        private sealed class AuthoritySentinels : IEquatable<AuthoritySentinels>
        {
            public AuthoritySentinels(
                int enemyHealth,
                string lifecycle,
                string selectedTarget,
                long cadenceStep,
                int projectileExecutions,
                string encounterState,
                string persistenceState)
            {
                EnemyHealth = enemyHealth;
                Lifecycle = lifecycle;
                SelectedTarget = selectedTarget;
                CadenceStep = cadenceStep;
                ProjectileExecutions = projectileExecutions;
                EncounterState = encounterState;
                PersistenceState = persistenceState;
            }

            public int EnemyHealth { get; }

            public string Lifecycle { get; }

            public string SelectedTarget { get; }

            public long CadenceStep { get; }

            public int ProjectileExecutions { get; }

            public string EncounterState { get; }

            public string PersistenceState { get; }

            public bool Equals(AuthoritySentinels other)
            {
                return other != null
                    && EnemyHealth == other.EnemyHealth
                    && string.Equals(Lifecycle, other.Lifecycle, StringComparison.Ordinal)
                    && string.Equals(SelectedTarget, other.SelectedTarget, StringComparison.Ordinal)
                    && CadenceStep == other.CadenceStep
                    && ProjectileExecutions == other.ProjectileExecutions
                    && string.Equals(EncounterState, other.EncounterState, StringComparison.Ordinal)
                    && string.Equals(PersistenceState, other.PersistenceState, StringComparison.Ordinal);
            }

            public override bool Equals(object obj)
            {
                return Equals(obj as AuthoritySentinels);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = 17;
                    hash = (hash * 31) + EnemyHealth;
                    hash = (hash * 31) + Lifecycle.GetHashCode();
                    hash = (hash * 31) + SelectedTarget.GetHashCode();
                    hash = (hash * 31) + CadenceStep.GetHashCode();
                    hash = (hash * 31) + ProjectileExecutions;
                    hash = (hash * 31) + EncounterState.GetHashCode();
                    hash = (hash * 31) + PersistenceState.GetHashCode();
                    return hash;
                }
            }
        }
    }
}
