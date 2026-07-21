using System;
using System.Collections.Generic;
using ShooterMover.Combat.HitPolicy;
using ShooterMover.Contracts.Combat;
using ShooterMover.Domain.Common;
using ShooterMover.EnemyRuntimeComposition;
using ShooterMover.GameplayEntities;
using ShooterMover.UnityAdapters.Players;

namespace ShooterMover.UnityAdapters.Enemies
{
    public interface IEnemyAttackPatternCombatContextV1
    {
        bool TryReadSource(
            EnemyAttackEffectEmissionV1 emission,
            out CombatActorSnapshotV1 source);

        bool TryReadTarget(
            StableId targetEntityStableId,
            out CombatActorSnapshotV1 target);

        DamageReceiverResult ApplyPlayerDamage(PlayerDamageRequest request);
    }

    public interface IEnemyAttackPatternDamageChannelMapV1
    {
        bool TryMap(StableId damageChannelStableId, out CombatChannel channel);
    }

    public sealed class BuiltInEnemyAttackPatternDamageChannelMapV1 :
        IEnemyAttackPatternDamageChannelMapV1
    {
        private static readonly StableId Kinetic =
            StableId.Parse("damage.kinetic");
        private static readonly StableId Impact =
            StableId.Parse("damage.impact");
        private static readonly StableId Thermal =
            StableId.Parse("damage.thermal");
        private static readonly StableId Electrical =
            StableId.Parse("damage.electrical");
        private static readonly StableId Explosive =
            StableId.Parse("damage.explosive");

        public bool TryMap(
            StableId damageChannelStableId,
            out CombatChannel channel)
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

    public enum EnemyAttackPatternHitRouteStatusV1
    {
        Applied = 1,
        ExactReplay = 2,
        RejectedByPolicy = 3,
        RejectedByDamageAuthority = 4,
        ConflictingDuplicate = 5,
        InvalidInput = 6,
    }

    public sealed class EnemyAttackPatternHitRouteResultV1
    {
        public EnemyAttackPatternHitRouteResultV1(
            EnemyAttackPatternHitRouteStatusV1 status,
            EnemyAttackEffectEmissionV1 emission,
            StableId hitEventStableId,
            StableId targetEntityStableId,
            CombatHitPolicyResultV1 policyResult,
            DamageReceiverResult damageResult,
            string rejectionCode)
        {
            if (!Enum.IsDefined(
                    typeof(EnemyAttackPatternHitRouteStatusV1),
                    status))
            {
                throw new ArgumentOutOfRangeException(nameof(status));
            }
            Status = status;
            Emission = emission;
            HitEventStableId = hitEventStableId;
            TargetEntityStableId = targetEntityStableId;
            PolicyResult = policyResult;
            DamageResult = damageResult;
            RejectionCode = rejectionCode ?? string.Empty;
        }

        public EnemyAttackPatternHitRouteStatusV1 Status { get; }
        public EnemyAttackEffectEmissionV1 Emission { get; }
        public StableId HitEventStableId { get; }
        public StableId TargetEntityStableId { get; }
        public CombatHitPolicyResultV1 PolicyResult { get; }
        public DamageReceiverResult DamageResult { get; }
        public string RejectionCode { get; }
        public bool IsAccepted
        {
            get
            {
                return Status == EnemyAttackPatternHitRouteStatusV1.Applied
                    || Status == EnemyAttackPatternHitRouteStatusV1.ExactReplay;
            }
        }
    }

    /// <summary>
    /// Session-local policy and replay ledger for schema-v2 enemy emissions. This class never
    /// mutates health. Only an accepted Combat Hit Policy result is translated into the existing
    /// PlayerDamageRequest and forwarded to the injected player runtime authority.
    /// </summary>
    public sealed class EnemyAttackPatternHitRouterV1
    {
        private sealed class ReplayRecord
        {
            public ReplayRecord(
                string fingerprint,
                EnemyAttackPatternHitRouteResultV1 result)
            {
                Fingerprint = fingerprint;
                Result = result;
            }

            public string Fingerprint { get; }
            public EnemyAttackPatternHitRouteResultV1 Result { get; }
        }

        private readonly ICombatHitPolicyV1 policy;
        private readonly IEnemyAttackPatternCombatContextV1 context;
        private readonly IEnemyAttackPatternDamageChannelMapV1 channelMap;
        private readonly Dictionary<StableId, CombatHitHistorySnapshotV1> historyByEffect =
            new Dictionary<StableId, CombatHitHistorySnapshotV1>();
        private readonly Dictionary<StableId, ReplayRecord> replayByHitEvent =
            new Dictionary<StableId, ReplayRecord>();

        public EnemyAttackPatternHitRouterV1(
            IEnemyAttackPatternCombatContextV1 context,
            IEnemyAttackPatternDamageChannelMapV1 channelMap = null,
            ICombatHitPolicyV1 policy = null)
        {
            this.context = context ?? throw new ArgumentNullException(nameof(context));
            this.channelMap = channelMap
                ?? new BuiltInEnemyAttackPatternDamageChannelMapV1();
            this.policy = policy
                ?? new CombatHitPolicyV1(CombatHitPolicyRegistryV1.CreateDefault());
        }

        public EnemyAttackPatternHitRouteResultV1 RouteActorContact(
            EnemyAttackEffectEmissionV1 emission,
            StableId hitEventStableId,
            StableId targetEntityStableId,
            long observedTargetLifecycleGeneration,
            double distanceSquared)
        {
            string fingerprint = Fingerprint(
                emission,
                hitEventStableId,
                targetEntityStableId,
                observedTargetLifecycleGeneration,
                distanceSquared);
            if (hitEventStableId != null)
            {
                ReplayRecord replay;
                if (replayByHitEvent.TryGetValue(hitEventStableId, out replay))
                {
                    if (string.Equals(
                            replay.Fingerprint,
                            fingerprint,
                            StringComparison.Ordinal))
                    {
                        return new EnemyAttackPatternHitRouteResultV1(
                            EnemyAttackPatternHitRouteStatusV1.ExactReplay,
                            emission,
                            hitEventStableId,
                            targetEntityStableId,
                            replay.Result.PolicyResult,
                            replay.Result.DamageResult,
                            string.Empty);
                    }
                    return Result(
                        EnemyAttackPatternHitRouteStatusV1.ConflictingDuplicate,
                        emission,
                        hitEventStableId,
                        targetEntityStableId,
                        null,
                        null,
                        "enemy-pattern-hit-event-conflict");
                }
            }

            string invalid = Validate(
                emission,
                hitEventStableId,
                targetEntityStableId,
                observedTargetLifecycleGeneration,
                distanceSquared);
            if (!string.IsNullOrEmpty(invalid))
            {
                return Remember(
                    fingerprint,
                    Result(
                        EnemyAttackPatternHitRouteStatusV1.InvalidInput,
                        emission,
                        hitEventStableId,
                        targetEntityStableId,
                        null,
                        null,
                        invalid));
            }

            CombatActorSnapshotV1 source;
            CombatActorSnapshotV1 target;
            CombatChannel channel;
            if (!context.TryReadSource(emission, out source)
                || source == null
                || !context.TryReadTarget(targetEntityStableId, out target)
                || target == null
                || !channelMap.TryMap(
                    emission.Execution.Descriptor.DamageChannelId,
                    out channel))
            {
                return Remember(
                    fingerprint,
                    Result(
                        EnemyAttackPatternHitRouteStatusV1.InvalidInput,
                        emission,
                        hitEventStableId,
                        targetEntityStableId,
                        null,
                        null,
                        "enemy-pattern-hit-context-unavailable"));
            }

            CombatEffectSnapshotV1 effect = BuildEffect(emission);
            CombatHitHistorySnapshotV1 history;
            if (!historyByEffect.TryGetValue(effect.EffectId, out history))
            {
                history = CombatHitHistorySnapshotV1.Empty(effect.EffectId);
            }
            CombatHitPolicyResultV1 policyResult = policy.Evaluate(
                new CombatHitPolicyInputV1(
                    source,
                    effect,
                    CombatHitContactV1.Actor(
                        target,
                        observedTargetLifecycleGeneration,
                        distanceSquared),
                    history));
            if (policyResult == null || !policyResult.DamageEligible)
            {
                return Remember(
                    fingerprint,
                    Result(
                        EnemyAttackPatternHitRouteStatusV1.RejectedByPolicy,
                        emission,
                        hitEventStableId,
                        targetEntityStableId,
                        policyResult,
                        null,
                        policyResult == null
                            ? "enemy-pattern-hit-policy-null"
                            : "enemy-pattern-hit-policy-"
                                + policyResult.RejectionCode));
            }

            DamageReceiverCommand command;
            if (!CombatHitDamageCommandAdapterV1.TryCreate(
                    policyResult,
                    hitEventStableId,
                    emission.ResolvedDamage,
                    channel,
                    out command)
                || command == null)
            {
                return Remember(
                    fingerprint,
                    Result(
                        EnemyAttackPatternHitRouteStatusV1.InvalidInput,
                        emission,
                        hitEventStableId,
                        targetEntityStableId,
                        policyResult,
                        null,
                        "enemy-pattern-damage-command-unavailable"));
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
            bool acceptedDamage = damageResult != null
                && (damageResult.Status == DamageReceiverStatus.Applied
                    || damageResult.Status == DamageReceiverStatus.Duplicate);
            if (!acceptedDamage)
            {
                return Remember(
                    fingerprint,
                    Result(
                        EnemyAttackPatternHitRouteStatusV1.RejectedByDamageAuthority,
                        emission,
                        hitEventStableId,
                        targetEntityStableId,
                        policyResult,
                        damageResult,
                        damageResult == null
                            ? "enemy-pattern-player-damage-null"
                            : "enemy-pattern-player-damage-"
                                + damageResult.RejectionCode));
            }

            historyByEffect[effect.EffectId] = policyResult.NextHistory;
            return Remember(
                fingerprint,
                Result(
                    damageResult.Status == DamageReceiverStatus.Duplicate
                        ? EnemyAttackPatternHitRouteStatusV1.ExactReplay
                        : EnemyAttackPatternHitRouteStatusV1.Applied,
                    emission,
                    hitEventStableId,
                    targetEntityStableId,
                    policyResult,
                    damageResult,
                    string.Empty));
        }

        public void Clear()
        {
            historyByEffect.Clear();
            replayByHitEvent.Clear();
        }

        private EnemyAttackPatternHitRouteResultV1 Remember(
            string fingerprint,
            EnemyAttackPatternHitRouteResultV1 result)
        {
            if (result != null && result.HitEventStableId != null)
            {
                replayByHitEvent.Add(
                    result.HitEventStableId,
                    new ReplayRecord(fingerprint, result));
            }
            return result;
        }

        private static CombatEffectSnapshotV1 BuildEffect(
            EnemyAttackEffectEmissionV1 emission)
        {
            int pierce = emission.Projectile == null
                ? 0
                : Math.Max(0, emission.Projectile.Payload.Pierce);
            int maximumHitsPerTarget = emission.MeleeStrike == null
                ? 1
                : Math.Max(1, emission.MeleeStrike.Pattern.HitsPerTarget);
            CombatEffectGeometryKindV1 geometry =
                emission.Kind == EnemyAttackEffectEmissionKindV1.Projectile
                    ? CombatEffectGeometryKindV1.Projectile
                    : (emission.MeleeStrike.Pattern.LungeDistance > 0d
                        ? CombatEffectGeometryKindV1.ContactAttack
                        : CombatEffectGeometryKindV1.MeleeSwing);
            return new CombatEffectSnapshotV1(
                emission.EmissionStableId,
                CombatHitPolicyIdsV1.EnemyNormal,
                emission.SourceEntityStableId,
                emission.SourceLifecycleGeneration,
                geometry,
                CombatWorldBlockerBehaviorV1.Terminate,
                false,
                false,
                pierce,
                maximumHitsPerTarget);
        }

        private static string Validate(
            EnemyAttackEffectEmissionV1 emission,
            StableId hitEventStableId,
            StableId targetEntityStableId,
            long observedTargetLifecycleGeneration,
            double distanceSquared)
        {
            if (emission == null
                || emission.Execution == null
                || emission.Execution.Descriptor == null
                || hitEventStableId == null
                || targetEntityStableId == null
                || observedTargetLifecycleGeneration < 0L
                || double.IsNaN(distanceSquared)
                || double.IsInfinity(distanceSquared)
                || distanceSquared < 0d)
            {
                return "enemy-pattern-hit-input-invalid";
            }
            return string.Empty;
        }

        private static string Fingerprint(
            EnemyAttackEffectEmissionV1 emission,
            StableId hitEventStableId,
            StableId targetEntityStableId,
            long observedTargetLifecycleGeneration,
            double distanceSquared)
        {
            return (emission == null ? "-" : emission.Fingerprint)
                + "|"
                + (hitEventStableId == null ? "-" : hitEventStableId.ToString())
                + "|"
                + (targetEntityStableId == null
                    ? "-"
                    : targetEntityStableId.ToString())
                + "|"
                + observedTargetLifecycleGeneration
                + "|"
                + distanceSquared.ToString("R");
        }

        private static EnemyAttackPatternHitRouteResultV1 Result(
            EnemyAttackPatternHitRouteStatusV1 status,
            EnemyAttackEffectEmissionV1 emission,
            StableId hitEventStableId,
            StableId targetEntityStableId,
            CombatHitPolicyResultV1 policyResult,
            DamageReceiverResult damageResult,
            string rejectionCode)
        {
            return new EnemyAttackPatternHitRouteResultV1(
                status,
                emission,
                hitEventStableId,
                targetEntityStableId,
                policyResult,
                damageResult,
                rejectionCode);
        }
    }
}
