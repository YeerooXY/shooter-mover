using System;
using System.Collections.Generic;
using UnityEngine;

namespace ShooterMover.ContentPackages.Props.Breakables
{
    [DisallowMultipleComponent]
    public sealed class Breakables : MonoBehaviour
    {
        private readonly List<Breakable> props =
            new List<Breakable>();
        private Func<long> restartGenerationSource;
        private long observedRestartGeneration;
        private bool configured;

        public int PropCount => props.Count;
        public long ObservedRestartGeneration => observedRestartGeneration;

        internal void Configure(
            IEnumerable<Breakable> configuredProps,
            Func<long> configuredRestartGenerationSource)
        {
            if (configured)
                throw new InvalidOperationException(
                    "Destructible prop set is already configured.");
            if (configuredProps == null)
                throw new ArgumentNullException(nameof(configuredProps));
            if (configuredRestartGenerationSource == null)
                throw new ArgumentNullException(
                    nameof(configuredRestartGenerationSource));

            foreach (Breakable prop in configuredProps)
            {
                if (prop != null && !props.Contains(prop)) props.Add(prop);
            }
            if (props.Count == 0)
            {
                throw new ArgumentException(
                    "At least one configured breakable is required.",
                    nameof(configuredProps));
            }

            restartGenerationSource = configuredRestartGenerationSource;
            observedRestartGeneration = restartGenerationSource();
            configured = true;
        }

        public void RestartAll()
        {
            if (!configured) return;
            for (int index = 0; index < props.Count; index++)
            {
                Breakable prop = props[index];
                if (prop != null) prop.Restart();
            }
        }

        private void LateUpdate()
        {
            if (!configured || restartGenerationSource == null) return;
            long currentGeneration = restartGenerationSource();
            if (currentGeneration == observedRestartGeneration) return;
            observedRestartGeneration = currentGeneration;
            RestartAll();
        }

        private void OnDestroy()
        {
            restartGenerationSource = null;
            props.Clear();
        }
    }
}
