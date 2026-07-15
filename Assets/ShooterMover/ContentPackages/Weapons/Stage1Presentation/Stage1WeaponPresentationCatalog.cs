using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ShooterMover.Domain.Common;
using UnityEngine;

namespace ShooterMover.ContentPackages.Weapons.Stage1Presentation
{
    public sealed class Stage1WeaponIdentityCue
    {
        internal Stage1WeaponIdentityCue(
            string weaponId, string label, string glyph, string pattern,
            string audioId, string effectId, Color accent, int pulses,
            double frequencyHz, double durationSeconds)
        {
            WeaponId = StableId.Parse(weaponId).ToString();
            Label = Require(label, nameof(label));
            Glyph = Require(glyph, nameof(glyph));
            Pattern = Require(pattern, nameof(pattern));
            AudioId = Require(audioId, nameof(audioId));
            EffectId = Require(effectId, nameof(effectId));
            if (pulses < 1 || pulses > Stage1WeaponCueArbiter.MaximumPulseCount)
                throw new ArgumentOutOfRangeException(nameof(pulses));
            if (!Positive(frequencyHz) || !Positive(durationSeconds) || durationSeconds > 0.25d)
                throw new ArgumentOutOfRangeException(nameof(durationSeconds));
            Accent = accent;
            Pulses = pulses;
            FrequencyHz = frequencyHz;
            DurationSeconds = durationSeconds;
        }

        public string WeaponId { get; }
        public string Label { get; }
        public string Glyph { get; }
        public string Pattern { get; }
        public string AudioId { get; }
        public string EffectId { get; }
        public Color Accent { get; }
        public int Pulses { get; }
        public double FrequencyHz { get; }
        public double DurationSeconds { get; }

        private static string Require(string value, string name)
        {
            if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Value is required.", name);
            return value;
        }

        private static bool Positive(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value) && value > 0d;
        }
    }

    public static class Stage1WeaponPresentationCatalog
    {
        private static readonly Stage1WeaponIdentityCue[] Values =
        {
            Cue("weapon.blaster-machine-gun", "BLASTER", "|||", "RAPID", "blaster-tick", "blaster-streak", new Color(0.28f, 0.78f, 1f), 3, 720d, 0.055d),
            Cue("weapon.shotgun", "SHOTGUN", "###", "SPREAD", "shotgun-thump", "shotgun-fan", new Color(1f, 0.72f, 0.24f), 1, 170d, 0.14d),
            Cue("weapon.rocket-launcher", "ROCKET", "O>", "BLAST", "rocket-boom", "rocket-ring", new Color(1f, 0.34f, 0.22f), 1, 95d, 0.22d),
            Cue("weapon.arc-gun", "ARC", "Z", "CHAIN", "arc-snap", "arc-fork", new Color(0.66f, 0.48f, 1f), 2, 1080d, 0.09d),
            Cue("weapon.ricochet-gun", "RICOCHET", "<>", "BOUNCE", "ricochet-ping", "ricochet-chevron", new Color(0.42f, 1f, 0.58f), 2, 880d, 0.08d),
        };

        private static readonly ReadOnlyCollection<Stage1WeaponIdentityCue> ReadOnlyValues =
            new ReadOnlyCollection<Stage1WeaponIdentityCue>(Values);
        private static readonly Dictionary<string, Stage1WeaponIdentityCue> ByWeapon = Build(false);
        private static readonly Dictionary<string, Stage1WeaponIdentityCue> ByAudio = Build(true);

        public static IReadOnlyList<Stage1WeaponIdentityCue> Entries { get { return ReadOnlyValues; } }

        public static bool TryGet(StableId weaponId, out Stage1WeaponIdentityCue cue)
        {
            cue = null;
            return weaponId != null && ByWeapon.TryGetValue(weaponId.ToString(), out cue);
        }

        public static bool TryGetByAudioId(string audioId, out Stage1WeaponIdentityCue cue)
        {
            cue = null;
            return audioId != null && ByAudio.TryGetValue(audioId, out cue);
        }

        private static Stage1WeaponIdentityCue Cue(
            string id, string label, string glyph, string pattern,
            string audio, string effect, Color color, int pulses, double hz, double seconds)
        {
            return new Stage1WeaponIdentityCue(
                id, label, glyph, pattern, "wp10.audio." + audio,
                "wp10.effect." + effect, color, pulses, hz, seconds);
        }

        private static Dictionary<string, Stage1WeaponIdentityCue> Build(bool audio)
        {
            Dictionary<string, Stage1WeaponIdentityCue> result =
                new Dictionary<string, Stage1WeaponIdentityCue>(StringComparer.Ordinal);
            foreach (Stage1WeaponIdentityCue cue in Values)
                result.Add(audio ? cue.AudioId : cue.WeaponId, cue);
            return result;
        }
    }
}
