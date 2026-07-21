using System;
using System.Collections.Generic;
using System.Globalization;
using ShooterMover.ContentPackages.Weapons.Shared.Runtime;
using ShooterMover.Contracts.Combat;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Enemies.Catalog;
using ShooterMover.EnemyRuntimeComposition;
using ShooterMover.UnityAdapters.Combat;
using UnityEngine;

namespace ShooterMover.UnityAdapters.Enemies
{
    /// <summary>
    /// Concrete Unity realization for schema-v2 enemy projectile and melee emissions. It reuses
    /// BoundedProjectile2D and CombatHit2DAdapter, and delegates all damage decisions to the
    /// shared Combat Hit Policy router.
    /// </summary>
    public sealed class EnemyAttackPatternUnityEmissionRealizerV1 :
        IEnemyAttackPatternEmissionRealizerV1,
        IEnemyAttackPatternMeleeContactReporterV1,
        IDisposable
    {
        private sealed class SourceState
        {
            public SourceState(
                EnemyAttackPatternUnitySourceBindingV1 binding,
                CombatHit2DAdapter hitAdapter,
                EnemyAttackPatternMeleeContact2D meleeContact)
            {
                Binding = binding;
                HitAdapter = hitAdapter;
                MeleeContact = meleeContact;
            }

            public EnemyAttackPatternUnitySourceBindingV1 Binding { get; }
            public CombatHit2DAdapter HitAdapter { get; }
            public EnemyAttackPatternMeleeContact2D MeleeContact { get; }
        }

        private sealed class ProjectileState
        {
            public ProjectileState(
                EnemyAttackEffectEmissionV1 emission,
                SourceState source,
                BoundedProjectile2D projectile,
                IDictionary<StableId, long> observedTargetLifecycles)
            {
                Emission = emission;
                Source = source;
                Projectile = projectile;
                ObservedTargetLifecycles =
                    new Dictionary<StableId, long>(observedTargetLifecycles);
            }

            public EnemyAttackEffectEmissionV1 Emission { get; }
            public SourceState Source { get; }
            public BoundedProjectile2D Projectile { get; }
            public Dictionary<StableId, long> ObservedTargetLifecycles { get; }
        }

        private sealed class MeleeWindowState
        {
            public MeleeWindowState(
                EnemyAttackEffectEmissionV1 emission,
                SourceState source,
                IDictionary<StableId, long> observedTargetLifecycles)
            {
                Emission = emission;
                Source = source;
                ObservedTargetLifecycles =
                    new Dictionary<StableId, long>(observedTargetLifecycles);
                AcceptedTargets = new HashSet<StableId>();
            }

            public EnemyAttackEffectEmissionV1 Emission { get; }
            public SourceState Source { get; }
            public Dictionary<StableId, long> ObservedTargetLifecycles { get; }
            public HashSet<StableId> AcceptedTargets { get; }
        }

        private readonly IEnemyAttackPatternRunTimeV1 runTime;
        private readonly EnemyAttackPatternUnitySourceRegistryV1 sourceRegistry;
        private readonly EnemyAttackPatternHitRouterV1 hitRouter;
        private readonly IEnemyAttackPatternDamageChannelMapV1 channelMap;
        private readonly Dictionary<StableId, SourceState> sourceStates =
            new Dictionary<StableId, SourceState>();
        private readonly Dictionary<int, ProjectileState> projectiles =
            new Dictionary<int, ProjectileState>();
        private readonly Dictionary<StableId, MeleeWindowState> meleeWindows =
            new Dictionary<StableId, MeleeWindowState>();
        private bool disposed;

        public EnemyAttackPatternUnityEmissionRealizerV1(
            IEnemyAttackPatternRunTimeV1 runTime,
            EnemyAttackPatternUnitySourceRegistryV1 sourceRegistry,
            EnemyAttackPatternHitRouterV1 hitRouter,
            IEnemyAttackPatternDamageChannelMapV1 channelMap = null)
        {
            this.runTime = runTime ?? throw new ArgumentNullException(nameof(runTime));
            this.sourceRegistry = sourceRegistry
                ?? throw new ArgumentNullException(nameof(sourceRegistry));
            this.hitRouter = hitRouter
                ?? throw new ArgumentNullException(nameof(hitRouter));
            this.channelMap = channelMap
                ?? new BuiltInEnemyAttackPatternDamageChannelMapV1();
        }

        public int ActiveProjectileCount
        {
            get { return projectiles.Count; }
        }

        public int ActiveMeleeWindowCount
        {
            get { return meleeWindows.Count; }
        }

        public void AttachSource(StableId sourceEntityStableId)
        {
            ThrowIfDisposed();
            EnemyAttackPatternUnitySourceBindingV1 binding;
            if (!sourceRegistry.TryGet(sourceEntityStableId, out binding))
            {
                throw new InvalidOperationException(
                    "Cannot attach an unregistered enemy pattern source: "
                    + sourceEntityStableId);
            }

            SourceState existing;
            if (sourceStates.TryGetValue(sourceEntityStableId, out existing))
            {
                if (!ReferenceEquals(existing.Binding, binding))
                {
                    throw new InvalidOperationException(
                        "Enemy pattern source binding changed after attachment.");
                }
                return;
            }

            var hitAdapter = new CombatHit2DAdapter(sourceEntityStableId);
            for (int targetIndex = 0;
                targetIndex < binding.Targets.Count;
                targetIndex++)
            {
                EnemyAttackPatternTargetBindingV1 target =
                    binding.Targets[targetIndex];
                for (int colliderIndex = 0;
                    colliderIndex < target.Colliders.Count;
                    colliderIndex++)
                {
                    CombatHit2DTargetRegistrationStatus status =
                        hitAdapter.RegisterTarget(
                            target.Colliders[colliderIndex],
                            target.TargetEntityStableId);
                    if (status != CombatHit2DTargetRegistrationStatus.Registered
                        && status
                            != CombatHit2DTargetRegistrationStatus.AlreadyRegistered)
                    {
                        throw new InvalidOperationException(
                            "Explicit attack-pattern target registration failed: "
                            + status);
                    }
                }
            }

            EnemyAttackPatternMeleeContact2D relay =
                binding.SourceRoot.GetComponent<
                    EnemyAttackPatternMeleeContact2D>();
            if (relay == null)
            {
                relay = binding.SourceRoot.AddComponent<
                    EnemyAttackPatternMeleeContact2D>();
            }
            relay.Configure(sourceEntityStableId, this);
            sourceStates.Add(
                sourceEntityStableId,
                new SourceState(binding, hitAdapter, relay));
        }

        public bool CanRealize(
            EnemyAttackEffectEmissionV1 emission,
            out string rejectionCode)
        {
            rejectionCode = string.Empty;
            if (disposed || emission == null || emission.Execution == null)
            {
                rejectionCode = "enemy-pattern-emission-invalid";
                return false;
            }

            SourceState source;
            if (!sourceStates.TryGetValue(
                    emission.SourceEntityStableId,
                    out source)
                || !sourceRegistry.IsCurrent(
                    emission.SourceEntityStableId,
                    emission.SourceLifecycleGeneration))
            {
                rejectionCode = "enemy-pattern-source-not-current";
                return false;
            }

            CombatChannel channel;
            if (!channelMap.TryMap(
                    emission.Execution.Descriptor.DamageChannelId,
                    out channel))
            {
                rejectionCode = "enemy-pattern-damage-channel-unsupported";
                return false;
            }

            Vector2 origin;
            Vector2 direction;
            if (!TryCommittedVectors(emission, out origin, out direction))
            {
                rejectionCode = "enemy-pattern-committed-aim-invalid";
                return false;
            }

            if (emission.Kind == EnemyAttackEffectEmissionKindV1.Projectile)
            {
                return CanRealizeProjectile(
                    emission,
                    source,
                    out rejectionCode);
            }
            if (emission.Kind == EnemyAttackEffectEmissionKindV1.MeleeStrike)
            {
                return CanRealizeMelee(
                    emission,
                    source,
                    out rejectionCode);
            }

            rejectionCode = "enemy-pattern-emission-kind-unsupported";
            return false;
        }

        public void Realize(EnemyAttackEffectEmissionV1 emission)
        {
            ThrowIfDisposed();
            string rejection;
            if (!CanRealize(emission, out rejection))
            {
                throw new InvalidOperationException(
                    "A prevalidated enemy emission became unrealizable: "
                    + rejection);
            }

            SourceState source = sourceStates[emission.SourceEntityStableId];
            if (emission.Kind == EnemyAttackEffectEmissionKindV1.Projectile)
            {
                RealizeProjectile(emission, source);
                return;
            }
            RealizeMelee(emission, source);
        }

        public void CancelActiveWindow(EnemyAttackEffectEmissionV1 emission)
        {
            if (disposed || emission == null)
            {
                return;
            }
            CloseMeleeWindow(emission.EmissionStableId, true);
        }

        public void Tick()
        {
            ThrowIfDisposed();
            double now = runTime.CurrentTimeSeconds;
            var close = new List<StableId>();
            foreach (KeyValuePair<StableId, MeleeWindowState> pair in meleeWindows)
            {
                MeleeWindowState window = pair.Value;
                if (window.Emission.ActiveUntilSeconds <= now
                    || !sourceRegistry.IsCurrent(
                        window.Emission.SourceEntityStableId,
                        window.Emission.SourceLifecycleGeneration))
                {
                    close.Add(pair.Key);
                    continue;
                }
                if (window.Source.Binding.PounceMotion != null)
                {
                    window.Source.Binding.PounceMotion.Tick(
                        window.Emission,
                        now);
                }
            }
            for (int index = 0; index < close.Count; index++)
            {
                CloseMeleeWindow(close[index], false);
            }
        }

        public void ReportMeleeContact(
            StableId sourceEntityStableId,
            Collider2D candidate)
        {
            if (disposed
                || sourceEntityStableId == null
                || candidate == null)
            {
                return;
            }

            double now = runTime.CurrentTimeSeconds;
            var ordered = new List<MeleeWindowState>();
            foreach (MeleeWindowState window in meleeWindows.Values)
            {
                if (window.Emission.SourceEntityStableId == sourceEntityStableId
                    && window.Emission.ScheduledAtSeconds <= now
                    && window.Emission.ActiveUntilSeconds > now)
                {
                    ordered.Add(window);
                }
            }
            ordered.Sort((left, right) =>
            {
                int time = left.Emission.ScheduledAtSeconds.CompareTo(
                    right.Emission.ScheduledAtSeconds);
                return time != 0
                    ? time
                    : left.Emission.EmissionStableId.CompareTo(
                        right.Emission.EmissionStableId);
            });

            for (int index = 0; index < ordered.Count; index++)
            {
                MeleeWindowState window = ordered[index];
                EnemyAttackPatternTargetBindingV1 target =
                    window.Source.Binding.FindTarget(candidate);
                if (target == null
                    || !target.IsActive
                    || window.AcceptedTargets.Contains(
                        target.TargetEntityStableId))
                {
                    continue;
                }

                StableId committedTarget =
                    window.Emission.CommittedIntent.TargetEntityId;
                if (committedTarget != null
                    && committedTarget != target.TargetEntityStableId)
                {
                    continue;
                }

                long observedGeneration;
                if (!window.ObservedTargetLifecycles.TryGetValue(
                        target.TargetEntityStableId,
                        out observedGeneration))
                {
                    continue;
                }
                StableId hitEvent = StableId.Create(
                    "enemy-melee-hit",
                    "runtime-"
                        + Hash64(
                            window.Emission.EmissionStableId
                            + "|"
                            + target.TargetEntityStableId));
                EnemyAttackPatternHitRouteResultV1 result =
                    hitRouter.RouteActorContact(
                        window.Emission,
                        hitEvent,
                        target.TargetEntityStableId,
                        observedGeneration,
                        0d);
                if (result != null && result.IsAccepted)
                {
                    window.AcceptedTargets.Add(
                        target.TargetEntityStableId);
                }
            }
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }
            disposed = true;

            var activeProjectiles = new List<BoundedProjectile2D>();
            foreach (ProjectileState state in projectiles.Values)
            {
                if (state.Projectile != null)
                {
                    activeProjectiles.Add(state.Projectile);
                }
            }
            for (int index = 0; index < activeProjectiles.Count; index++)
            {
                activeProjectiles[index].Cancel();
            }
            projectiles.Clear();

            var windows = new List<StableId>(meleeWindows.Keys);
            for (int index = 0; index < windows.Count; index++)
            {
                CloseMeleeWindow(windows[index], true);
            }

            foreach (SourceState state in sourceStates.Values)
            {
                if (state.MeleeContact != null)
                {
                    state.MeleeContact.Unconfigure(this);
                }
                state.HitAdapter.ClearTargets();
                state.HitAdapter.ResetProcessedEvents();
            }
            sourceStates.Clear();
        }

        private bool CanRealizeProjectile(
            EnemyAttackEffectEmissionV1 emission,
            SourceState source,
            out string rejectionCode)
        {
            rejectionCode = string.Empty;
            EnemyAttackScheduledProjectileV1 projectile = emission.Projectile;
            EnemyProjectilePayloadV1 payload =
                projectile == null ? null : projectile.Payload;
            BoundedProjectile2D prefab;
            if (payload == null
                || payload.ProjectileProfileId == null
                || !source.Binding.ProjectilePrefabs.TryResolve(
                    payload.ProjectileProfileId,
                    out prefab)
                || prefab == null)
            {
                rejectionCode = "enemy-pattern-projectile-profile-unresolved";
                return false;
            }
            if (payload.PierceCount > 0)
            {
                rejectionCode = "enemy-pattern-bounded-projectile-pierce-unsupported";
                return false;
            }

            float speed = (float)payload.Speed;
            float lifetime = payload.Speed <= 0d
                ? 0f
                : (float)(payload.MaximumTravelDistance / payload.Speed);
            float radius = (float)payload.CollisionRadius;
            if (!BoundedProjectile2D.IsValidSpeed(speed)
                || !BoundedProjectile2D.IsValidLifetime(lifetime)
                || !BoundedProjectile2D.IsValidRadius(radius))
            {
                rejectionCode = "enemy-pattern-projectile-bounds-invalid";
                return false;
            }

            EnemyAreaPayloadV1 area = payload.AreaPayload;
            if (area != null
                && (area.DurationSeconds != 0d
                    || area.MaximumTargets <= 0
                    || double.IsNaN(area.Radius)
                    || double.IsInfinity(area.Radius)
                    || area.Radius <= 0d))
            {
                rejectionCode = "enemy-pattern-area-payload-unsupported";
                return false;
            }
            return true;
        }

        private static bool CanRealizeMelee(
            EnemyAttackEffectEmissionV1 emission,
            SourceState source,
            out string rejectionCode)
        {
            rejectionCode = string.Empty;
            EnemyAttackScheduledMeleeStrikeV1 strike = emission.MeleeStrike;
            EnemyMeleePatternV1 pattern =
                strike == null ? null : strike.Pattern;
            if (pattern == null
                || pattern.HitsPerTarget != 1
                || double.IsNaN(pattern.ContactRadius)
                || double.IsInfinity(pattern.ContactRadius)
                || pattern.ContactRadius <= 0d)
            {
                rejectionCode = "enemy-pattern-melee-definition-unsupported";
                return false;
            }
            StableId targetId = emission.CommittedIntent.TargetEntityId;
            if (targetId == null
                || source.Binding.FindTarget(targetId) == null)
            {
                rejectionCode = "enemy-pattern-melee-target-unavailable";
                return false;
            }
            if (pattern.LungeDistance > 0d
                && source.Binding.PounceMotion == null)
            {
                rejectionCode = "enemy-pattern-pounce-motion-unavailable";
                return false;
            }
            return true;
        }

        private void RealizeProjectile(
            EnemyAttackEffectEmissionV1 emission,
            SourceState source)
        {
            EnemyProjectilePayloadV1 payload = emission.Projectile.Payload;
            BoundedProjectile2D prefab;
            source.Binding.ProjectilePrefabs.TryResolve(
                payload.ProjectileProfileId,
                out prefab);
            Vector2 origin;
            Vector2 direction;
            TryCommittedVectors(emission, out origin, out direction);
            direction = Rotate(
                direction,
                emission.Projectile.SpreadOffsetDegrees);

            CombatChannel channel;
            channelMap.TryMap(
                emission.Execution.Descriptor.DamageChannelId,
                out channel);
            BoundedProjectile2D instance = UnityEngine.Object.Instantiate(
                prefab,
                origin,
                Quaternion.identity);
            var state = new ProjectileState(
                emission,
                source,
                instance,
                CaptureTargetLifecycles(source.Binding));
            int instanceId = instance.GetInstanceID();
            projectiles.Add(instanceId, state);
            instance.Completed += HandleProjectileCompleted;

            float lifetime = (float)(
                payload.MaximumTravelDistance / payload.Speed);
            bool initialized = instance.TryInitialize(
                emission.EmissionStableId,
                origin,
                direction,
                (float)payload.Speed,
                lifetime,
                (float)payload.CollisionRadius,
                channel,
                source.HitAdapter,
                source.Binding.OwnerColliders,
                false,
                0f);
            if (!initialized)
            {
                instance.Completed -= HandleProjectileCompleted;
                projectiles.Remove(instanceId);
                UnityEngine.Object.Destroy(instance.gameObject);
                throw new InvalidOperationException(
                    "A prevalidated bounded projectile failed initialization.");
            }
        }

        private void RealizeMelee(
            EnemyAttackEffectEmissionV1 emission,
            SourceState source)
        {
            if (meleeWindows.ContainsKey(emission.EmissionStableId))
            {
                return;
            }
            var window = new MeleeWindowState(
                emission,
                source,
                CaptureTargetLifecycles(source.Binding));
            meleeWindows.Add(emission.EmissionStableId, window);
            if (source.Binding.PounceMotion != null
                && emission.MeleeStrike.Pattern.LungeDistance > 0d)
            {
                Vector2 origin;
                Vector2 direction;
                TryCommittedVectors(emission, out origin, out direction);
                source.Binding.PounceMotion.Open(
                    emission,
                    origin,
                    direction,
                    (float)emission.MeleeStrike.Pattern.LungeDistance);
            }
        }

        private void HandleProjectileCompleted(BoundedProjectile2D projectile)
        {
            if (projectile == null)
            {
                return;
            }
            ProjectileState state;
            if (!projectiles.TryGetValue(projectile.GetInstanceID(), out state))
            {
                return;
            }
            projectile.Completed -= HandleProjectileCompleted;
            projectiles.Remove(projectile.GetInstanceID());
            if (disposed
                || projectile.CompletionReason
                    == BoundedProjectile2DCompletionReason.Cancelled)
            {
                return;
            }

            EnemyAreaPayloadV1 area = state.Emission.Projectile.Payload.AreaPayload;
            if (area != null)
            {
                RouteAreaCompletion(state, projectile, area);
                return;
            }

            CombatHit2DTranslationResult translation =
                projectile.LastHitTranslation;
            if (translation == null
                || translation.Status
                    != CombatHit2DTranslationStatus.Confirmed
                || translation.Message == null)
            {
                return;
            }
            StableId targetId = translation.Message.TargetId;
            long observedGeneration;
            if (!state.ObservedTargetLifecycles.TryGetValue(
                    targetId,
                    out observedGeneration))
            {
                return;
            }
            Vector2 origin = new Vector2(
                (float)state.Emission.CommittedIntent.CommittedOrigin.X,
                (float)state.Emission.CommittedIntent.CommittedOrigin.Y);
            double distanceSquared =
                (projectile.LastCollisionPoint - origin).sqrMagnitude;
            hitRouter.RouteActorContact(
                state.Emission,
                state.Emission.EmissionStableId,
                targetId,
                observedGeneration,
                distanceSquared);
        }

        private void RouteAreaCompletion(
            ProjectileState state,
            BoundedProjectile2D projectile,
            EnemyAreaPayloadV1 area)
        {
            Vector2 center = projectile.LastCollisionCollider == null
                ? (Vector2)projectile.transform.position
                : projectile.LastCollisionPoint;
            int acceptedTargets = 0;
            for (int index = 0;
                index < state.Source.Binding.Targets.Count
                    && acceptedTargets < area.MaximumTargets;
                index++)
            {
                EnemyAttackPatternTargetBindingV1 target =
                    state.Source.Binding.Targets[index];
                if (!target.IsActive
                    || !WithinArea(target, center, (float)area.Radius))
                {
                    continue;
                }
                long observedGeneration;
                if (!state.ObservedTargetLifecycles.TryGetValue(
                        target.TargetEntityStableId,
                        out observedGeneration))
                {
                    continue;
                }
                StableId eventId = StableId.Create(
                    "enemy-area-hit",
                    "runtime-"
                        + Hash64(
                            state.Emission.EmissionStableId
                            + "|"
                            + target.TargetEntityStableId));
                EnemyAttackPatternHitRouteResultV1 result =
                    hitRouter.RouteActorContact(
                        state.Emission,
                        eventId,
                        target.TargetEntityStableId,
                        observedGeneration,
                        AreaDistanceSquared(target, center));
                if (result != null && result.IsAccepted)
                {
                    acceptedTargets++;
                }
            }
        }

        private void CloseMeleeWindow(
            StableId emissionStableId,
            bool cancelled)
        {
            MeleeWindowState window;
            if (emissionStableId == null
                || !meleeWindows.TryGetValue(emissionStableId, out window))
            {
                return;
            }
            meleeWindows.Remove(emissionStableId);
            if (window.Source.Binding.PounceMotion != null
                && window.Emission.MeleeStrike.Pattern.LungeDistance > 0d)
            {
                window.Source.Binding.PounceMotion.Close(
                    window.Emission,
                    cancelled);
            }
        }

        private static Dictionary<StableId, long> CaptureTargetLifecycles(
            EnemyAttackPatternUnitySourceBindingV1 binding)
        {
            var result = new Dictionary<StableId, long>();
            for (int index = 0; index < binding.Targets.Count; index++)
            {
                EnemyAttackPatternTargetBindingV1 target = binding.Targets[index];
                result.Add(
                    target.TargetEntityStableId,
                    target.LifecycleGeneration);
            }
            return result;
        }

        private static bool TryCommittedVectors(
            EnemyAttackEffectEmissionV1 emission,
            out Vector2 origin,
            out Vector2 direction)
        {
            origin = Vector2.zero;
            direction = Vector2.zero;
            if (emission == null
                || emission.CommittedIntent == null)
            {
                return false;
            }
            origin = new Vector2(
                (float)emission.CommittedIntent.CommittedOrigin.X,
                (float)emission.CommittedIntent.CommittedOrigin.Y);
            direction = new Vector2(
                (float)emission.CommittedIntent.CommittedDirection.X,
                (float)emission.CommittedIntent.CommittedDirection.Y);
            return Finite(origin)
                && Finite(direction)
                && direction.sqrMagnitude > 0.000001f;
        }

        private static Vector2 Rotate(Vector2 direction, double degrees)
        {
            float radians = (float)degrees * Mathf.Deg2Rad;
            float cosine = Mathf.Cos(radians);
            float sine = Mathf.Sin(radians);
            Vector2 normalized = direction.normalized;
            return new Vector2(
                (normalized.x * cosine) - (normalized.y * sine),
                (normalized.x * sine) + (normalized.y * cosine));
        }

        private static bool WithinArea(
            EnemyAttackPatternTargetBindingV1 target,
            Vector2 center,
            float radius)
        {
            return AreaDistanceSquared(target, center)
                <= radius * radius;
        }

        private static double AreaDistanceSquared(
            EnemyAttackPatternTargetBindingV1 target,
            Vector2 center)
        {
            double best = double.PositiveInfinity;
            for (int index = 0; index < target.Colliders.Count; index++)
            {
                Collider2D collider = target.Colliders[index];
                if (collider == null
                    || !collider.enabled
                    || !collider.gameObject.activeInHierarchy)
                {
                    continue;
                }
                Vector2 closest = collider.ClosestPoint(center);
                double distance = (closest - center).sqrMagnitude;
                if (distance < best)
                {
                    best = distance;
                }
            }
            return best;
        }

        private static bool Finite(Vector2 value)
        {
            return !float.IsNaN(value.x)
                && !float.IsInfinity(value.x)
                && !float.IsNaN(value.y)
                && !float.IsInfinity(value.y);
        }

        private static string Hash64(string value)
        {
            const ulong offsetBasis = 14695981039346656037UL;
            const ulong prime = 1099511628211UL;
            ulong hash = offsetBasis;
            string source = value ?? string.Empty;
            for (int index = 0; index < source.Length; index++)
            {
                hash ^= source[index];
                hash *= prime;
            }
            return hash.ToString("x16", CultureInfo.InvariantCulture);
        }

        private void ThrowIfDisposed()
        {
            if (disposed)
            {
                throw new ObjectDisposedException(
                    nameof(EnemyAttackPatternUnityEmissionRealizerV1));
            }
        }
    }
}
