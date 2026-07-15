using System;
using System.Collections.Generic;
using System.Globalization;
using ShooterMover.Domain.Combat;
using UnityEngine;

namespace ShooterMover.ContentPackages.Weapons.Stage1Presentation
{
    /// <summary>Read-only projection from CB-010's immutable four-mount snapshot.</summary>
    public sealed class Stage1WeaponPresentationProjector
    {
        private const double Epsilon = 0.000000001d;

        public Stage1WeaponPresentationFrame Project(
            FourMountStatusSnapshot snapshot,
            Stage1WeaponPresentationFrame previous,
            Stage1WeaponPresentationOptions options)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            options = options ?? Stage1WeaponPresentationOptions.Default;
            Stage1WeaponSlotPresentation[] slots = new Stage1WeaponSlotPresentation[4];
            for (int i = 0; i < slots.Length; i++)
                slots[i] = ProjectSlot(snapshot.GetByStableIndex(i), previous == null ? null : previous.GetByStableIndex(i), options);
            return new Stage1WeaponPresentationFrame(options.ReducedEffects, slots);
        }

        public Stage1WeaponPresentationFrame Project(FourMountStatusSnapshot snapshot)
        { return Project(snapshot, null, Stage1WeaponPresentationOptions.Default); }

        private static Stage1WeaponSlotPresentation ProjectSlot(
            FourMountSlotStatusSnapshot source,
            Stage1WeaponSlotPresentation previous,
            Stage1WeaponPresentationOptions options)
        {
            if (!source.IsEquipped) return Empty(source.StableSlotNumber);

            Stage1WeaponIdentityCue cue;
            bool known = Stage1WeaponPresentationCatalog.TryGet(source.WeaponId, out cue);
            string warning = known ? string.Empty : "PACKAGE REF MISSING";
            string audio = known ? cue.AudioId : null;
            string effect = known ? cue.EffectId : null;
            if (known && !options.Available(audio)) { warning = Add(warning, "AUDIO REF MISSING"); audio = null; }
            if (known && !options.Available(effect)) { warning = Add(warning, "EFFECT REF MISSING"); effect = null; }
            bool emits = Emits(source.FireMode);
            if (!emits) { audio = null; effect = null; }
            if (options.ReducedEffects) effect = null;

            string weaponId = source.WeaponId.ToString();
            double delta = PowerDelta(source, previous, weaponId);
            string fault = source.IsFaulted
                ? "FAULT " + source.FaultKind.Value + ": " + source.FaultDetail
                : string.Empty;
            int priority = Priority(source);
            int pulses = known && emits && !options.ReducedEffects
                ? Math.Min(Stage1WeaponCueArbiter.MaximumPulseCount, cue.Pulses + ((source.IsFallback || source.IsFaulted || source.FireMode == FourMountFireMode.Empowered) ? 1 : 0))
                : 0;

            return new Stage1WeaponSlotPresentation(
                source.StableSlotNumber, weaponId, true, known,
                known ? cue.Label : "UNKNOWN", known ? cue.Glyph : "?",
                known ? cue.Pattern : "MISSING REF", known ? cue.Accent : Color.white,
                State(source), StateDetail(source), Mode(source.FireMode), Power(source),
                PowerChange(source, delta), fault, warning,
                source.FireMode == FourMountFireMode.Empowered, source.IsFallback,
                source.IsFaulted, source.HasPowerBank, source.PowerAvailableUnits,
                source.PowerCapacityUnits, delta, audio, effect, priority, pulses);
        }

        private static Stage1WeaponSlotPresentation Empty(int slot)
        {
            return new Stage1WeaponSlotPresentation(
                slot, null, false, false, "EMPTY", "--", "EMPTY", Color.gray,
                "EMPTY", "NO WEAPON EQUIPPED", "NO FIRE MODE", "POWER N/A",
                string.Empty, string.Empty, string.Empty, false, false, false,
                false, 0d, 0d, 0d, null, null, 0, 0);
        }

        private static string State(FourMountSlotStatusSnapshot s)
        { return s.Phase.Value.ToString().ToUpperInvariant(); }

        private static string StateDetail(FourMountSlotStatusSnapshot s)
        {
            switch (s.Phase.Value)
            {
                case WeaponMountPhase.Ready: return "READY NOW";
                case WeaponMountPhase.Firing: return "CADENCE " + Seconds(s.CadenceRemainingSeconds);
                case WeaponMountPhase.Recovering: return "RECOVER " + Seconds(Math.Max(s.CadenceRemainingSeconds, s.RecoveryRemainingSeconds));
                case WeaponMountPhase.Depleted: return s.CycleMode == WeaponCycleMode.Heat ? "HEAT LOCK" : s.CycleMode == WeaponCycleMode.Charge ? "CHARGING" : "RESOURCE DEPLETED";
                case WeaponMountPhase.Faulted: return "FAILED CLOSED";
                default: throw new ArgumentOutOfRangeException();
            }
        }

        private static string Mode(FourMountFireMode mode)
        {
            switch (mode)
            {
                case FourMountFireMode.NoRecentAttempt: return "NORMAL READY";
                case FourMountFireMode.Normal: return "NORMAL SHOT";
                case FourMountFireMode.Empowered: return "EMPOWERED SHOT";
                case FourMountFireMode.NormalFallbackPowerUnavailable: return "NORMAL FALLBACK - NO POWER";
                case FourMountFireMode.NotReady: return "NO SHOT - NOT READY";
                case FourMountFireMode.Faulted: return "NO SHOT - FAULT";
                default: throw new ArgumentOutOfRangeException(nameof(mode));
            }
        }

        private static string Power(FourMountSlotStatusSnapshot s)
        {
            if (!s.HasPowerBank) return "POWER N/A";
            string numeric = Number(s.PowerAvailableUnits) + "/" + Number(s.PowerCapacityUnits);
            if (s.PowerAvailableUnits <= Epsilon) return "POWER EMPTY " + numeric;
            return s.CanAffordEmpoweredFire ? "POWER " + numeric : "POWER LOW " + numeric;
        }

        private static string PowerChange(FourMountSlotStatusSnapshot s, double delta)
        {
            if (s.IsFallback) return "NO POWER -> NORMAL";
            if (delta < -Epsilon) return "SPENT " + Number(-delta) + " POWER";
            if (delta > Epsilon) return "GAINED " + Number(delta) + " POWER";
            return s.FireMode == FourMountFireMode.Empowered ? "EMPOWERED USE" : string.Empty;
        }

        private static double PowerDelta(FourMountSlotStatusSnapshot s, Stage1WeaponSlotPresentation previous, string id)
        {
            if (!s.HasPowerBank || previous == null || !previous.HasPower || previous.WeaponId != id) return 0d;
            return s.PowerAvailableUnits - previous.PowerAvailable;
        }

        private static bool Emits(FourMountFireMode mode)
        { return mode == FourMountFireMode.Normal || mode == FourMountFireMode.Empowered || mode == FourMountFireMode.NormalFallbackPowerUnavailable || mode == FourMountFireMode.Faulted; }

        private static int Priority(FourMountSlotStatusSnapshot s)
        {
            if (s.IsFaulted) return 40;
            if (s.FireMode == FourMountFireMode.Empowered) return 32;
            if (s.IsFallback) return 28;
            if (s.FireMode == FourMountFireMode.Normal) return 20;
            return 8;
        }

        private static string Add(string left, string right)
        { return string.IsNullOrEmpty(left) ? right : left + " | " + right; }
        private static string Seconds(double value)
        { return value.ToString("0.00", CultureInfo.InvariantCulture) + "s"; }
        private static string Number(double value)
        { return value.ToString("0.##", CultureInfo.InvariantCulture); }
    }
}
