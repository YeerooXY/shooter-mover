using System;
using System.Collections.Generic;
using System.Globalization;
using ShooterMover.Domain.Common;
using ShooterMover.RunPickups;
using UnityEngine;

namespace ShooterMover.UnityAdapters.Rewards.RunPickups
{
    /// <summary>
    /// Typed Unity-side adapter for production-owned committed source positions.
    /// Callers register the exact terminal source/placement position; pickup realization
    /// never searches scenes, substitutes the player position, or invents coordinates.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RunPickupSourcePositionRegistry2D : MonoBehaviour,
        IRunPickupSourcePositionPortV1
    {
        private sealed class Registration
        {
            public Registration(
                StableId runStableId,
                long lifecycleGeneration,
                StableId sourceEntityStableId,
                StableId sourcePlacementStableId,
                RunPickupWorldSpawnContextV1 context,
                string fingerprint)
            {
                RunStableId = runStableId;
                LifecycleGeneration = lifecycleGeneration;
                SourceEntityStableId = sourceEntityStableId;
                SourcePlacementStableId = sourcePlacementStableId;
                Context = context;
                Fingerprint = fingerprint;
            }

            public StableId RunStableId { get; }
            public long LifecycleGeneration { get; }
            public StableId SourceEntityStableId { get; }
            public StableId SourcePlacementStableId { get; }
            public RunPickupWorldSpawnContextV1 Context { get; }
            public string Fingerprint { get; }
        }

        private readonly object gate = new object();
        private readonly Dictionary<string, Registration> registrations =
            new Dictionary<string, Registration>(StringComparer.Ordinal);

        public int RegistrationCount
        {
            get
            {
                lock (gate)
                {
                    return registrations.Count;
                }
            }
        }

        public bool Register(
            StableId runStableId,
            long runLifecycleGeneration,
            StableId sourceEntityStableId,
            StableId sourcePlacementStableId,
            StableId roomStableId,
            Vector2 committedPosition,
            string sourcePositionFingerprint,
            out string diagnostic)
        {
            diagnostic = string.Empty;
            if (runStableId == null
                || sourceEntityStableId == null
                || roomStableId == null
                || runLifecycleGeneration < 0L
                || float.IsNaN(committedPosition.x)
                || float.IsInfinity(committedPosition.x)
                || float.IsNaN(committedPosition.y)
                || float.IsInfinity(committedPosition.y)
                || string.IsNullOrWhiteSpace(sourcePositionFingerprint))
            {
                diagnostic = "run-pickup-source-position-registration-invalid";
                return false;
            }

            var context = new RunPickupWorldSpawnContextV1(
                roomStableId,
                committedPosition.x,
                committedPosition.y,
                sourcePositionFingerprint);
            string key = BuildKey(
                runStableId,
                runLifecycleGeneration,
                sourceEntityStableId,
                sourcePlacementStableId);
            string fingerprint = BuildRegistrationFingerprint(
                key,
                context.Fingerprint);

            lock (gate)
            {
                Registration existing;
                if (registrations.TryGetValue(key, out existing))
                {
                    if (string.Equals(
                        existing.Fingerprint,
                        fingerprint,
                        StringComparison.Ordinal))
                    {
                        diagnostic = "run-pickup-source-position-exact-replay";
                        return true;
                    }
                    diagnostic = "run-pickup-source-position-registration-conflict";
                    return false;
                }

                registrations.Add(
                    key,
                    new Registration(
                        runStableId,
                        runLifecycleGeneration,
                        sourceEntityStableId,
                        sourcePlacementStableId,
                        context,
                        fingerprint));
                return true;
            }
        }

        public bool TryResolve(
            StableId runStableId,
            long runLifecycleGeneration,
            StableId sourceEntityStableId,
            StableId sourcePlacementStableId,
            out RunPickupWorldSpawnContextV1 worldSpawnContext,
            out string diagnostic)
        {
            worldSpawnContext = null;
            diagnostic = string.Empty;
            if (runStableId == null
                || sourceEntityStableId == null
                || runLifecycleGeneration < 0L)
            {
                diagnostic = "run-pickup-source-position-query-invalid";
                return false;
            }

            string key = BuildKey(
                runStableId,
                runLifecycleGeneration,
                sourceEntityStableId,
                sourcePlacementStableId);
            lock (gate)
            {
                Registration registration;
                if (!registrations.TryGetValue(key, out registration))
                {
                    diagnostic = "run-pickup-source-position-not-registered";
                    return false;
                }
                worldSpawnContext = registration.Context;
                return worldSpawnContext != null;
            }
        }

        private static string BuildKey(
            StableId runStableId,
            long lifecycleGeneration,
            StableId sourceEntityStableId,
            StableId sourcePlacementStableId)
        {
            return runStableId
                + "|"
                + lifecycleGeneration.ToString(CultureInfo.InvariantCulture)
                + "|"
                + sourceEntityStableId
                + "|"
                + (sourcePlacementStableId == null
                    ? "none"
                    : sourcePlacementStableId.ToString());
        }

        private static string BuildRegistrationFingerprint(
            string key,
            string contextFingerprint)
        {
            unchecked
            {
                uint hash = 2166136261u;
                string text = key + "|" + contextFingerprint;
                for (int index = 0; index < text.Length; index++)
                {
                    hash ^= text[index];
                    hash *= 16777619u;
                }
                return "run-pickup-source-position-v1:"
                    + hash.ToString("x8", CultureInfo.InvariantCulture);
            }
        }
    }
}
