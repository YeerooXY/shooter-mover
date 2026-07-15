using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace ShooterMover.ContentPackages.Weapons.Stage1Presentation
{
    public sealed class Stage1WeaponCueRequest
    {
        internal Stage1WeaponCueRequest(int stableSlotNumber, string cueId, int priority)
        {
            StableSlotNumber = stableSlotNumber;
            CueId = cueId;
            Priority = priority;
        }

        public int StableSlotNumber { get; }
        public string CueId { get; }
        public int Priority { get; }

        public override string ToString()
        {
            return "S" + StableSlotNumber + ":" + CueId + ":P" + Priority;
        }
    }

    public sealed class Stage1WeaponCuePlan
    {
        private readonly ReadOnlyCollection<Stage1WeaponCueRequest> audio;
        private readonly ReadOnlyCollection<Stage1WeaponCueRequest> effects;

        internal Stage1WeaponCuePlan(
            IList<Stage1WeaponCueRequest> audioRequests,
            IList<Stage1WeaponCueRequest> effectRequests)
        {
            audio = new ReadOnlyCollection<Stage1WeaponCueRequest>(
                new List<Stage1WeaponCueRequest>(audioRequests));
            effects = new ReadOnlyCollection<Stage1WeaponCueRequest>(
                new List<Stage1WeaponCueRequest>(effectRequests));

            foreach (Stage1WeaponCueRequest request in audio)
                MaximumSelectedPriority = Math.Max(MaximumSelectedPriority, request.Priority);
            foreach (Stage1WeaponCueRequest request in effects)
                MaximumSelectedPriority = Math.Max(MaximumSelectedPriority, request.Priority);
        }

        public int AudioCount { get { return audio.Count; } }
        public int EffectCount { get { return effects.Count; } }
        public int MaximumSelectedPriority { get; private set; }

        public Stage1WeaponCueRequest GetAudioRequest(int index)
        {
            return audio[index];
        }

        public string ToTraceString()
        {
            List<string> rows = new List<string>
            {
                "weapon_audio=" + AudioCount + "/" + Stage1WeaponCueArbiter.MaximumAudioVoices,
                "weapon_effects=" + EffectCount + "/" + Stage1WeaponCueArbiter.MaximumEffects,
                "max_weapon_priority=" + MaximumSelectedPriority,
                "reserved_enemy_warning_priority=" + Stage1WeaponCueArbiter.ReservedEnemyWarningPriority,
            };
            foreach (Stage1WeaponCueRequest request in audio)
                rows.Add("audio=" + request);
            foreach (Stage1WeaponCueRequest request in effects)
                rows.Add("effect=" + request);
            return string.Join("\n", rows.ToArray());
        }
    }

    public static class Stage1WeaponCueArbiter
    {
        public const int ReservedEnemyWarningPriority = 100;
        public const int MaximumAudioVoices = 2;
        public const int MaximumEffects = 4;
        public const int MaximumPulseCount = 4;

        public static Stage1WeaponCuePlan Select(Stage1WeaponPresentationFrame frame)
        {
            if (frame == null) throw new ArgumentNullException(nameof(frame));

            List<Stage1WeaponCueRequest> audio = new List<Stage1WeaponCueRequest>();
            List<Stage1WeaponCueRequest> effects = new List<Stage1WeaponCueRequest>();
            for (int index = 0; index < frame.Count; index++)
            {
                Stage1WeaponSlotPresentation slot = frame.GetByStableIndex(index);
                if (slot.AudioId != null)
                    audio.Add(new Stage1WeaponCueRequest(slot.StableSlotNumber, slot.AudioId, slot.Priority));
                if (slot.EffectId != null)
                    effects.Add(new Stage1WeaponCueRequest(slot.StableSlotNumber, slot.EffectId, slot.Priority));
            }

            audio.Sort(Compare);
            effects.Sort(Compare);
            Trim(audio, MaximumAudioVoices);
            Trim(effects, frame.ReducedEffects ? 0 : MaximumEffects);
            return new Stage1WeaponCuePlan(audio, effects);
        }

        private static int Compare(Stage1WeaponCueRequest left, Stage1WeaponCueRequest right)
        {
            int byPriority = right.Priority.CompareTo(left.Priority);
            return byPriority != 0
                ? byPriority
                : left.StableSlotNumber.CompareTo(right.StableSlotNumber);
        }

        private static void Trim<T>(List<T> values, int maximum)
        {
            if (values.Count > maximum)
                values.RemoveRange(maximum, values.Count - maximum);
        }
    }
}
