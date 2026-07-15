using System;
using System.Collections.Generic;
using System.Globalization;
using ShooterMover.Domain.Combat;
using UnityEngine;

namespace ShooterMover.ContentPackages.Weapons.Stage1Presentation
{
    public sealed class Stage1WeaponSlotPresentation
    {
        internal Stage1WeaponSlotPresentation(
            int slot, string weaponId, bool equipped, bool identityKnown,
            string label, string glyph, string pattern, Color accent,
            string state, string stateDetail, string mode, string power,
            string powerChange, string fault, string referenceWarning,
            bool empowered, bool fallback, bool faulted, bool hasPower,
            double powerAvailable, double powerCapacity, double powerDelta,
            string audioId, string effectId, int priority, int pulses)
        {
            StableSlotNumber = slot;
            WeaponId = weaponId;
            IsEquipped = equipped;
            IdentityKnown = identityKnown;
            Label = label;
            Glyph = glyph;
            Pattern = pattern;
            Accent = accent;
            State = state;
            StateDetail = stateDetail;
            Mode = mode;
            Power = power;
            PowerChange = powerChange;
            Fault = fault;
            ReferenceWarning = referenceWarning;
            IsEmpowered = empowered;
            IsFallback = fallback;
            IsFaulted = faulted;
            HasPower = hasPower;
            PowerAvailable = powerAvailable;
            PowerCapacity = powerCapacity;
            PowerDelta = powerDelta;
            AudioId = audioId;
            EffectId = effectId;
            Priority = priority;
            Pulses = pulses;
            CriticalText = BuildCriticalText();
        }

        public int StableSlotNumber { get; }
        public string WeaponId { get; }
        public bool IsEquipped { get; }
        public bool IdentityKnown { get; }
        public string Label { get; }
        public string Glyph { get; }
        public string Pattern { get; }
        public Color Accent { get; }
        public string State { get; }
        public string StateDetail { get; }
        public string Mode { get; }
        public string Power { get; }
        public string PowerChange { get; }
        public string Fault { get; }
        public string ReferenceWarning { get; }
        public string CriticalText { get; }
        public bool IsEmpowered { get; }
        public bool IsFallback { get; }
        public bool IsFaulted { get; }
        public bool HasPower { get; }
        public double PowerAvailable { get; }
        public double PowerCapacity { get; }
        public double PowerLevel { get { return HasPower ? PowerAvailable / PowerCapacity : 0d; } }
        public double PowerDelta { get; }
        public string AudioId { get; }
        public string EffectId { get; }
        public int Priority { get; }
        public int Pulses { get; }

        private string BuildCriticalText()
        {
            List<string> parts = new List<string>
            {
                "S" + StableSlotNumber,
                "[" + Glyph + "]",
                Pattern,
                State,
                Mode,
                Power,
            };
            Add(parts, PowerChange);
            Add(parts, Fault);
            Add(parts, ReferenceWarning);
            return string.Join(" | ", parts.ToArray());
        }

        private static void Add(ICollection<string> parts, string value)
        {
            if (!string.IsNullOrWhiteSpace(value)) parts.Add(value);
        }

        public override string ToString()
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "S{0}[weapon={1};state={2};mode={3};power={4};delta={5:R};audio={6};effect={7};priority={8};pulses={9}]",
                StableSlotNumber,
                WeaponId ?? "none",
                State,
                Mode,
                Power,
                PowerDelta,
                AudioId ?? "none",
                EffectId ?? "none",
                Priority,
                Pulses);
        }
    }

    public sealed class Stage1WeaponPresentationFrame
    {
        private readonly Stage1WeaponSlotPresentation[] slots;

        internal Stage1WeaponPresentationFrame(
            bool reducedEffects,
            Stage1WeaponSlotPresentation[] source)
        {
            if (source == null || source.Length != FourMountStatusSnapshot.SlotCount)
                throw new ArgumentException("Exactly four slots are required.", nameof(source));

            slots = (Stage1WeaponSlotPresentation[])source.Clone();
            for (int index = 0; index < slots.Length; index++)
            {
                if (slots[index] == null || slots[index].StableSlotNumber != index + 1)
                    throw new ArgumentException("Slots must use stable order.", nameof(source));
            }

            ReducedEffects = reducedEffects;
        }

        public bool ReducedEffects { get; }
        public int Count { get { return slots.Length; } }

        public Stage1WeaponSlotPresentation GetByStableIndex(int index)
        {
            if (index < 0 || index >= slots.Length)
                throw new ArgumentOutOfRangeException(nameof(index));
            return slots[index];
        }

        public Stage1WeaponCuePlan BuildCuePlan()
        {
            return Stage1WeaponCueArbiter.Select(this);
        }

        public string ToTraceString()
        {
            string[] rows = new string[slots.Length + 1];
            rows[0] = "reduced_effects=" + (ReducedEffects ? "true" : "false");
            for (int index = 0; index < slots.Length; index++)
                rows[index + 1] = slots[index].ToString();
            return string.Join("\n", rows);
        }
    }
}
