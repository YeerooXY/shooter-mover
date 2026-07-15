using System;
using System.Globalization;
using UnityEngine;

namespace ShooterMover.Presentation.VisibleSliceBlasterTurret
{
    public enum VisibleSliceBlasterTurretPhase
    {
        Idle = 1,
        Warning = 2,
        Firing = 3,
        Recovery = 4,
        Destroyed = 5,
        Deactivated = 6,
    }

    /// <summary>
    /// Getter-only source boundary supplied by the VS-007 composition owner.
    /// Implementations project already-accepted EN-002/EN-003/EN-007 facts and expose
    /// no damage, cadence, target, projectile, encounter, or persistence mutation.
    /// </summary>
    public interface IVisibleSliceBlasterTurretPresentationSource
    {
        bool TryReadSnapshot(out VisibleSliceBlasterTurretSnapshot snapshot);
    }

    /// <summary>
    /// Immutable presentation input. Warning timing/count values are observations of
    /// EN-007-owned cadence, never timers advanced by this package.
    /// </summary>
    public sealed class VisibleSliceBlasterTurretSnapshot
    {
        public VisibleSliceBlasterTurretSnapshot(
            long restartGeneration,
            long fixedStep,
            VisibleSliceBlasterTurretPhase phase,
            int currentHealth,
            int maximumHealth,
            double phaseElapsedSeconds,
            double phaseDurationSeconds,
            int warningCountRemaining,
            double warningDirectionX,
            double warningDirectionY,
            bool damageObserved,
            long damageSequence,
            bool reducedEffects,
            bool grayscaleRequested)
        {
            if (restartGeneration < 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(restartGeneration));
            }

            if (fixedStep < 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(fixedStep));
            }

            if (!Enum.IsDefined(typeof(VisibleSliceBlasterTurretPhase), phase))
            {
                throw new ArgumentOutOfRangeException(nameof(phase));
            }

            if (maximumHealth <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maximumHealth));
            }

            if (currentHealth < 0 || currentHealth > maximumHealth)
            {
                throw new ArgumentOutOfRangeException(nameof(currentHealth));
            }

            RequireFiniteNonNegative(phaseElapsedSeconds, nameof(phaseElapsedSeconds));
            RequireFiniteNonNegative(phaseDurationSeconds, nameof(phaseDurationSeconds));
            if (warningCountRemaining < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(warningCountRemaining));
            }

            RequireFinite(warningDirectionX, nameof(warningDirectionX));
            RequireFinite(warningDirectionY, nameof(warningDirectionY));
            if (damageSequence < -1L)
            {
                throw new ArgumentOutOfRangeException(nameof(damageSequence));
            }

            RestartGeneration = restartGeneration;
            FixedStep = fixedStep;
            Phase = phase;
            CurrentHealth = currentHealth;
            MaximumHealth = maximumHealth;
            PhaseElapsedSeconds = phaseElapsedSeconds;
            PhaseDurationSeconds = phaseDurationSeconds;
            WarningCountRemaining = warningCountRemaining;
            WarningDirectionX = warningDirectionX;
            WarningDirectionY = warningDirectionY;
            DamageObserved = damageObserved;
            DamageSequence = damageSequence;
            ReducedEffects = reducedEffects;
            GrayscaleRequested = grayscaleRequested;
        }

        public long RestartGeneration { get; }

        public long FixedStep { get; }

        public VisibleSliceBlasterTurretPhase Phase { get; }

        public int CurrentHealth { get; }

        public int MaximumHealth { get; }

        public double PhaseElapsedSeconds { get; }

        public double PhaseDurationSeconds { get; }

        public int WarningCountRemaining { get; }

        public double WarningDirectionX { get; }

        public double WarningDirectionY { get; }

        public bool DamageObserved { get; }

        public long DamageSequence { get; }

        public bool ReducedEffects { get; }

        public bool GrayscaleRequested { get; }

        public double NormalizedHealth
        {
            get { return (double)CurrentHealth / MaximumHealth; }
        }

        private static void RequireFinite(double value, string parameterName)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }
        }

        private static void RequireFiniteNonNegative(double value, string parameterName)
        {
            RequireFinite(value, parameterName);
            if (value < 0d)
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }
        }
    }

    public sealed class VisibleSliceBlasterTurretFrame
    {
        internal VisibleSliceBlasterTurretFrame(
            long restartGeneration,
            long fixedStep,
            VisibleSliceBlasterTurretPhase phase,
            int currentHealth,
            int maximumHealth,
            double normalizedHealth,
            Vector2 warningDirection,
            bool warningVisible,
            bool firingVisible,
            bool recoveryVisible,
            bool damageVisible,
            bool destroyedVisible,
            bool deactivatedVisible,
            bool reducedEffects,
            bool grayscale,
            bool optionalPulse,
            bool optionalMotion,
            string stateText,
            string healthText,
            string warningGlyph,
            string warningShapeText,
            string warningCountText,
            string warningTimingText,
            string damageText)
        {
            RestartGeneration = restartGeneration;
            FixedStep = fixedStep;
            Phase = phase;
            CurrentHealth = currentHealth;
            MaximumHealth = maximumHealth;
            NormalizedHealth = normalizedHealth;
            WarningDirection = warningDirection;
            WarningVisible = warningVisible;
            FiringVisible = firingVisible;
            RecoveryVisible = recoveryVisible;
            DamageVisible = damageVisible;
            DestroyedVisible = destroyedVisible;
            DeactivatedVisible = deactivatedVisible;
            ReducedEffects = reducedEffects;
            Grayscale = grayscale;
            OptionalPulse = optionalPulse;
            OptionalMotion = optionalMotion;
            StateText = stateText;
            HealthText = healthText;
            WarningGlyph = warningGlyph;
            WarningShapeText = warningShapeText;
            WarningCountText = warningCountText;
            WarningTimingText = warningTimingText;
            DamageText = damageText;
        }

        public long RestartGeneration { get; }

        public long FixedStep { get; }

        public VisibleSliceBlasterTurretPhase Phase { get; }

        public int CurrentHealth { get; }

        public int MaximumHealth { get; }

        public double NormalizedHealth { get; }

        public Vector2 WarningDirection { get; }

        public bool WarningVisible { get; }

        public bool FiringVisible { get; }

        public bool RecoveryVisible { get; }

        public bool DamageVisible { get; }

        public bool DestroyedVisible { get; }

        public bool DeactivatedVisible { get; }

        public bool ReducedEffects { get; }

        public bool Grayscale { get; }

        public bool OptionalPulse { get; }

        public bool OptionalMotion { get; }

        public string StateText { get; }

        public string HealthText { get; }

        public string WarningGlyph { get; }

        public string WarningShapeText { get; }

        public string WarningCountText { get; }

        public string WarningTimingText { get; }

        public string DamageText { get; }

        public string ToTraceString()
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "generation={0}|step={1}|phase={2}|health={3}/{4}|warning={5}|count={6}|timing={7}|firing={8}|recovery={9}|damage={10}|destroyed={11}|deactivated={12}|reduced={13}|grayscale={14}",
                RestartGeneration,
                FixedStep,
                Phase,
                CurrentHealth,
                MaximumHealth,
                WarningVisible ? "on" : "off",
                WarningCountText,
                WarningTimingText,
                FiringVisible ? "on" : "off",
                RecoveryVisible ? "on" : "off",
                DamageVisible ? "on" : "off",
                DestroyedVisible ? "on" : "off",
                DeactivatedVisible ? "on" : "off",
                ReducedEffects ? "on" : "off",
                Grayscale ? "on" : "off");
        }
    }

    public static class VisibleSliceBlasterTurretProjector
    {
        public static VisibleSliceBlasterTurretFrame Project(
            VisibleSliceBlasterTurretSnapshot snapshot,
            bool damageTransientVisible,
            bool reducedEffectsOverride,
            bool grayscaleOverride)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            bool destroyed = snapshot.CurrentHealth <= 0
                || snapshot.Phase == VisibleSliceBlasterTurretPhase.Destroyed;
            bool deactivated = snapshot.Phase == VisibleSliceBlasterTurretPhase.Deactivated;
            bool terminal = destroyed || deactivated;
            VisibleSliceBlasterTurretPhase effectivePhase = destroyed
                ? VisibleSliceBlasterTurretPhase.Destroyed
                : snapshot.Phase;

            bool warning = !terminal
                && effectivePhase == VisibleSliceBlasterTurretPhase.Warning;
            bool firing = !terminal
                && effectivePhase == VisibleSliceBlasterTurretPhase.Firing;
            bool recovery = !terminal
                && effectivePhase == VisibleSliceBlasterTurretPhase.Recovery;
            bool damage = !terminal && damageTransientVisible;
            bool reducedEffects = reducedEffectsOverride || snapshot.ReducedEffects;
            bool grayscale = grayscaleOverride || snapshot.GrayscaleRequested;

            Vector2 direction = new Vector2(
                (float)snapshot.WarningDirectionX,
                (float)snapshot.WarningDirectionY);
            if (direction.sqrMagnitude <= 0.000001f)
            {
                direction = Vector2.up;
            }
            else
            {
                direction.Normalize();
            }

            int percentage = Mathf.Clamp(
                Mathf.RoundToInt((float)(snapshot.NormalizedHealth * 100d)),
                0,
                100);
            string healthText = string.Format(
                CultureInfo.InvariantCulture,
                "HP {0}/{1} ({2}%)",
                snapshot.CurrentHealth,
                snapshot.MaximumHealth,
                percentage);
            string stateText = ResolveStateText(effectivePhase, damage, destroyed, deactivated);
            string warningCountText = warning
                ? snapshot.WarningCountRemaining.ToString("00", CultureInfo.InvariantCulture)
                : "--";
            string warningTimingText = warning
                ? ResolveRemainingSeconds(snapshot).ToString("0.00", CultureInfo.InvariantCulture)
                    + "s"
                : "--";

            return new VisibleSliceBlasterTurretFrame(
                snapshot.RestartGeneration,
                snapshot.FixedStep,
                effectivePhase,
                snapshot.CurrentHealth,
                snapshot.MaximumHealth,
                snapshot.NormalizedHealth,
                direction,
                warning,
                firing,
                recovery,
                damage,
                destroyed,
                deactivated,
                reducedEffects,
                grayscale,
                warning && !reducedEffects,
                (warning || damage) && !reducedEffects,
                stateText,
                healthText,
                warning ? "!" : "",
                warning ? "TRIANGLE + RAIL" : "",
                warningCountText,
                warningTimingText,
                damage ? "HIT" : "");
        }

        private static double ResolveRemainingSeconds(
            VisibleSliceBlasterTurretSnapshot snapshot)
        {
            if (snapshot.PhaseDurationSeconds <= 0d)
            {
                return 0d;
            }

            return Math.Max(
                0d,
                snapshot.PhaseDurationSeconds - snapshot.PhaseElapsedSeconds);
        }

        private static string ResolveStateText(
            VisibleSliceBlasterTurretPhase phase,
            bool damage,
            bool destroyed,
            bool deactivated)
        {
            if (destroyed)
            {
                return "X DESTROYED";
            }

            if (deactivated)
            {
                return "X DEACTIVATED";
            }

            switch (phase)
            {
                case VisibleSliceBlasterTurretPhase.Warning:
                    return damage ? "WARNING + HIT" : "WARNING";
                case VisibleSliceBlasterTurretPhase.Firing:
                    return damage ? "FIRING + HIT" : "FIRING";
                case VisibleSliceBlasterTurretPhase.Recovery:
                    return damage ? "RECOVERY + HIT" : "RECOVERY";
                default:
                    return damage ? "ACTIVE + HIT" : "ACTIVE / IDLE";
            }
        }
    }
}
