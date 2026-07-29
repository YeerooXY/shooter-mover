using System;
using System.Collections.Generic;
using System.Globalization;
using ShooterMover.Combat.HitPolicy;
using ShooterMover.Contracts.Combat;
using ShooterMover.Domain.Common;
using ShooterMover.EnemyRuntimeComposition;
using ShooterMover.GameplayEntities;
using ShooterMover.UnityAdapters.Players;

namespace ShooterMover.UnityAdapters.Enemies
{
    public interface IEnemyAttackPatternCombatContext
    {
        bool TryReadSource(
            EnemyAttackEffectEmission emission,
            out CombatActorSnapshot source);
        bool TryReadTarget(
            StableId targetEntityStableId,
            out CombatActorSnapshot target);
        DamageReceiverResult ApplyPlayerDamage(PlayerDamageRequest request);
    }

    public interface IEnemyAttackPatternDamageChannelMap
    {
        bool TryMap(StableId damageChannelStableId, out CombatChannel channel);
    }

    public sealed class BuiltInEnemyAttackPatternDamageChannelMap :
        IEnemyAttackPatternDamageChannelMap
    {
        private static readonly StableId Kinetic = StableId.Parse("damage.kinetic");
        private static readonly StableId Impact = StableId.Parse("damage.impact");
        private static readonly StableId Thermal = StableId.Parse("damage.thermal");
        private static readonly StableId Electrical = StableId.Parse("damage.electrical");
        private static readonly StableId Explosive = StableId.Parse("damage.explosive");

        public bool TryMap(StableId damageChannelStableId, out CombatChannel channel)
        {
            channel = CombatChannel.System;
            if (damageChannelStableId == Kinetic)
            {
                channel = CombatChannel.Kinetic;
                return true;
            }
            if (damageChannelStableId == Impact)
            {
                channel = CombatChannel.Contact;
                return true;
            }
            if (damageChannelStableId == Thermal)
            {
                channel = CombatChannel.Thermal;
                return true;
            }
            if (damageChannelStableId == Electrical)
            {
                channel = CombatChannel.Electrical;
                return true;
            }
            if (damageChannelStableId == Explosive)
            {
                channel = CombatChannel.Explosive;
                return true;
            }
            return false;
        }
    }

    public enum EnemyAttackPatternHitRouteStatus
    {
        Applied = 1,
        AppliedExactReplay = 2,
        RejectedByPolicy = 3,
        RejectedByDamageAuthority = 4,
        ConflictingDuplicate = 5,
        InvalidInput = 6,
        RetryableFailure = 7,
    }

    public sealed class EnemyAttackPatternHitRouteResult
    {
        public EnemyAttackPatternHitRouteResult(
            EnemyAttackPatternHitRouteStatus status,
            EnemyAttackEffectEmission emission,
            StableId hitEventStableId,
            StableId targetEntityStableId,
            CombatHitPolicyResult policyResult,
            DamageReceiverResult damageResult,
            string rejectionCode,
            bool isReplay)
        {
            if (!Enum.IsDefined(typeof(EnemyAttackPatternHitRouteStatus), status))
                throw new ArgumentOutOfRangeException(nameof(status));
            Status = status;
            Emission = emission;
            HitEventStableId = hitEventStableId;
            TargetEntityStableId = targetEntityStableId;
            PolicyResult = policyResult;
            DamageResult = damageResult;
            RejectionCode = rejectionCode ?? string.Empty;
            IsReplay = isReplay;
        }

        public EnemyAttackPatternHitRouteStatus Status { get; }
        public EnemyAttackEffectEmission Emission { get; }
        public StableId HitEventStableId { get; }
        public StableId TargetEntityStableId { get; }
        public CombatHitPolicyResult PolicyResult { get; }
        public DamageReceiverResult DamageResult { get; }
        public string RejectionCode { get; }
        public bool IsReplay { get; }
        public bool IsAccepted
        {
            get
            {
                return Status == EnemyAttackPatternHitRouteStatus.Applied
                    || Status == EnemyAttackPatternHitRouteStatus.AppliedExactReplay;
            }
        }
        public bool IsRetryable
        {
            get { return Status == EnemyAttackPatternHitRouteStatus.RetryableFailure; }
        }
    }

    public sealed class EnemyAttackPatternHitRouter
    {
        private sealed class ReplayRecord
        {
            public ReplayRecord(string fingerprint, EnemyAttackPatternHitRouteResult result)
            {
                Fingerprint = fingerprint;
                Result = result;
            }
            public string Fingerprint { get; }
            public EnemyAttackPatternHitRouteResult Result { get; }
        }

        private readonly ICombatHitRules policy;
        private readonly IEnemyAttackPatternCombatContext context;
        private readonly IEnemyAttackPatternDamageChannelMap channelMap;
        private readonly Dictionary<StableId, CombatHitHistorySnapshot> historyByEffect =
            new Dictionary<StableId, CombatHitHistorySnapshot>();
        private readonly Dictionary<StableId, ReplayRecord> replayByHitEvent =
            new Dictionary<StableId, ReplayRecord>();

        public EnemyAttackPatternHitRouter(
            IEnemyAttackPatternCombatContext context,
            IEnemyAttackPatternDamageChannelMap channelMap = null,
            ICombatHitRules policy = null)
        {
            this.context = context ?? throw new ArgumentNullException(nameof(context));
            this.channelMap = channelMap ?? new BuiltInEnemyAttackPatternDamageChannelMap();
            this.policy = policy
                ?? new CombatHitRules(CombatHitPolicyRegistry.CreateDefault());
        }

        public EnemyAttackPatternHitRouteResult RouteActorContact(
            EnemyAttackEffectEmission emission,
            StableId hitEventStableId,
            StableId targetEntityStableId,
            long observedTargetLifecycleGeneration,
            double distanceSquared)
        {
            string fingerprint = Fingerprint(emission, hitEventStableId,
                targetEntityStableId, observedTargetLifecycleGeneration,
                distanceSquared);
            ReplayRecord replay;
            if (hitEventStableId != null
                && replayByHitEvent.TryGetValue(hitEventStableId, out replay))
            {
                if (!string.Equals(replay.Fingerprint, fingerprint,
                        StringComparison.Ordinal))
                {
                    return Result(
                        EnemyAttackPatternHitRouteStatus.ConflictingDuplicate,
                        emission, hitEventStableId, targetEntityStableId,
                        null, null, "enemy-pattern-hit-event-conflict", false);
                }

                EnemyAttackPatternHitRouteResult original = replay.Result;
                return Result(
                    original.IsAccepted
                        ? EnemyAttackPatternHitRouteStatus.AppliedExactReplay
                        : original.Status,
                    emission, hitEventStableId, targetEntityStableId,
                    original.PolicyResult, original.DamageResult,
                    original.RejectionCode, true);
            }

            string invalid = Validate(emission, hitEventStableId,
                targetEntityStableId, observedTargetLifecycleGeneration,
                distanceSquared);
            if (!string.IsNullOrEmpty(invalid))
            {
                return Remember(fingerprint, Result(
                    EnemyAttackPatternHitRouteStatus.InvalidInput,
                    emission, hitEventStableId, targetEntityStableId,
                    null, null, invalid, false));
            }

            CombatActorSnapshot source;
            CombatActorSnapshot target;
            CombatChannel channel;
            if (!context.TryReadSource(emission, out source)
                || source == null
                || !context.TryReadTarget(targetEntityStableId, out target)
                || target == null
                || !channelMap.TryMap(
                    emission.Execution.Descriptor.DamageChannelId, out channel))
            {
                return Result(
                    EnemyAttackPatternHitRouteStatus.RetryableFailure,
                    emission, hitEventStableId, targetEntityStableId,
                    null, null, "enemy-pattern-hit-context-unavailable", false);
            }

            CombatEffectSnapshot effect = BuildEffect(emission);
            CombatHitHistorySnapshot history;
            if (!historyByEffect.TryGetValue(effect.EffectId, out history))
                history = CombatHitHistorySnapshot.Empty(effect.EffectId);
            CombatHitPolicyResult policyResult = policy.Evaluate(
                new CombatHitPolicyInput(
                    source,
                    effect,
                    CombatHitContact.Actor(target,
                        observedTargetLifecycleGeneration, distanceSquared),
                    history));
            if (policyResult == null || !policyResult.DamageEligible)
            {
                return Remember(fingerprint, Result(
                    EnemyAttackPatternHitRouteStatus.RejectedByPolicy,
                    emission, hitEventStableId, targetEntityStableId,
                    policyResult, null,
                    policyResult == null
                        ? "enemy-pattern-hit-policy-null"
                        : "enemy-pattern-hit-policy-" + policyResult.RejectionCode,
                    false));
            }

            DamageReceiverCommand command;
            if (!CombatHitDamageCommandBridge.TryCreate(
                    policyResult,
                    hitEventStableId,
                    emission.ResolvedDamage,
                    channel,
                    out command)
                || command == null)
            {
                return Remember(fingerprint, Result(
                    EnemyAttackPatternHitRouteStatus.InvalidInput,
                    emission, hitEventStableId, targetEntityStableId,
                    policyResult, null,
                    "enemy-pattern-damage-command-unavailable", false));
            }

            DamageReceiverResult damageResult = context.ApplyPlayerDamage(
                new PlayerDamageRequest(
                    command.EventId,
                    command.SourceActorId,
                    command.SourceRunParticipantId,
                    command.TargetActorId,
                    command.Amount,
                    command.Channel,
                    command.LifecycleGeneration));
            if (damageResult == null)
            {
                return Result(
                    EnemyAttackPatternHitRouteStatus.RetryableFailure,
                    emission, hitEventStableId, targetEntityStableId,
                    policyResult, null,
                    "enemy-pattern-player-damage-unavailable", false);
            }

            bool acceptedDamage = damageResult.Status == DamageReceiverStatus.Applied
                || damageResult.Status == DamageReceiverStatus.Duplicate;
            if (!acceptedDamage)
            {
                return Remember(fingerprint, Result(
                    EnemyAttackPatternHitRouteStatus.RejectedByDamageAuthority,
                    emission, hitEventStableId, targetEntityStableId,
                    policyResult, damageResult,
                    "enemy-pattern-player-damage-" + damageResult.RejectionCode,
                    false));
            }

            historyByEffect[effect.EffectId] = policyResult.NextHistory;
            return Remember(fingerprint, Result(
                damageResult.Status == DamageReceiverStatus.Duplicate
                    ? EnemyAttackPatternHitRouteStatus.AppliedExactReplay
                    : EnemyAttackPatternHitRouteStatus.Applied,
                emission, hitEventStableId, targetEntityStableId,
                policyResult, damageResult, string.Empty,
                damageResult.Status == DamageReceiverStatus.Duplicate));
        }

        public void Clear()
        {
            historyByEffect.Clear();
            replayByHitEvent.Clear();
        }

        private EnemyAttackPatternHitRouteResult Remember(
            string fingerprint,
            EnemyAttackPatternHitRouteResult result)
        {
            if (result != null && result.HitEventStableId != null)
            {
                replayByHitEvent.Add(result.HitEventStableId,
                    new ReplayRecord(fingerprint, result));
            }
            return result;
        }

        private static CombatEffectSnapshot BuildEffect(
            EnemyAttackEffectEmission emission)
        {
            int pierce = 0;
            int maximumHitsPerTarget = 1;
            CombatEffectGeometryKind geometry;
            if (emission.Kind == EnemyAttackEffectEmissionKind.Projectile)
            {
                if (emission.Projectile.Payload.AreaPayload != null)
                {
                    geometry = CombatEffectGeometryKind.Explosion;
                    pierce = Math.Max(0,
                        emission.Projectile.Payload.AreaPayload.MaximumTargets - 1);
                }
                else
                {
                    geometry = CombatEffectGeometryKind.Projectile;
                    pierce = Math.Max(0, emission.Projectile.Payload.PierceCount);
                }
            }
            else
            {
                maximumHitsPerTarget = Math.Max(1,
                    emission.MeleeStrike.Pattern.HitsPerTarget);
                geometry = emission.MeleeStrike.Pattern.LungeDistance > 0d
                    ? CombatEffectGeometryKind.ContactAttack
                    : CombatEffectGeometryKind.MeleeSwing;
            }
            return new CombatEffectSnapshot(
                emission.EmissionStableId,
                CombatHitPolicyIds.EnemyNormal,
                emission.SourceEntityStableId,
                emission.SourceLifecycleGeneration,
                geometry,
                CombatWorldBlockerBehavior.Terminate,
                false,
                false,
                pierce,
                maximumHitsPerTarget);
        }

        private static string Validate(
            EnemyAttackEffectEmission emission,
            StableId hitEventStableId,
            StableId targetEntityStableId,
            long observedTargetLifecycleGeneration,
            double distanceSquared)
        {
            return emission == null
                || emission.Execution == null
                || emission.Execution.Descriptor == null
                || hitEventStableId == null
                || targetEntityStableId == null
                || observedTargetLifecycleGeneration < 0L
                || double.IsNaN(distanceSquared)
                || double.IsInfinity(distanceSquared)
                || distanceSquared < 0d
                ? "enemy-pattern-hit-input-invalid"
                : string.Empty;
        }

        private static string Fingerprint(
            EnemyAttackEffectEmission emission,
            StableId hitEventStableId,
            StableId targetEntityStableId,
            long observedTargetLifecycleGeneration,
            double distanceSquared)
        {
            return (emission == null ? "-" : emission.Fingerprint)
                + "|"
                + (hitEventStableId == null ? "-" : hitEventStableId.ToString())
                + "|"
                + (targetEntityStableId == null ? "-" : targetEntityStableId.ToString())
                + "|"
                + observedTargetLifecycleGeneration.ToString(
                    CultureInfo.InvariantCulture)
                + "|"
                + distanceSquared.ToString("R", CultureInfo.InvariantCulture);
        }

        private static EnemyAttackPatternHitRouteResult Result(
            EnemyAttackPatternHitRouteStatus status,
            EnemyAttackEffectEmission emission,
            StableId hitEventStableId,
            StableId targetEntityStableId,
            CombatHitPolicyResult policyResult,
            DamageReceiverResult damageResult,
            string rejectionCode,
            bool isReplay)
        {
            return new EnemyAttackPatternHitRouteResult(
                status, emission, hitEventStableId, targetEntityStableId,
                policyResult, damageResult, rejectionCode, isReplay);
        }
    }
}
