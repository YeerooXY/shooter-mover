using System;
using System.Collections.Generic;

namespace ShooterMover.ContentPackages.Weapons.Stage1Presentation
{
    public sealed class Stage1WeaponPresentationOptions
    {
        private readonly HashSet<string> unavailable;

        private Stage1WeaponPresentationOptions(bool reducedEffects, IEnumerable<string> missingCueIds)
        {
            ReducedEffects = reducedEffects;
            unavailable = new HashSet<string>(StringComparer.Ordinal);
            if (missingCueIds == null) return;
            foreach (string id in missingCueIds)
                if (!string.IsNullOrWhiteSpace(id)) unavailable.Add(id);
        }

        public bool ReducedEffects { get; }

        public bool Available(string cueId)
        {
            return !string.IsNullOrWhiteSpace(cueId) && !unavailable.Contains(cueId);
        }

        public static Stage1WeaponPresentationOptions Create(
            bool reducedEffects,
            IEnumerable<string> missingCueIds)
        {
            return new Stage1WeaponPresentationOptions(reducedEffects, missingCueIds);
        }

        public static Stage1WeaponPresentationOptions Default
        {
            get { return Create(false, null); }
        }
    }
}
