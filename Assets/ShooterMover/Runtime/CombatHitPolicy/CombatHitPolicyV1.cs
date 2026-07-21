using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ShooterMover.Domain.Common;
using ShooterMover.GameplayEntities;

namespace ShooterMover.Combat.HitPolicy
{
    public static class CombatHitPolicyIdsV1
    {
        public static readonly StableId PlayerNormal =
            StableId.Parse("combat-hit-policy.player-normal-v1");
        public static readonly StableId EnemyNormal =
            StableId.Parse("combat-hit-policy.enemy-normal-v1");
        public static readonly StableId ChaoticAllFactions =
            StableId.Parse("combat-hit-policy.chaotic-all-factions-v1");
    }

    public static class CombatHitCapabilityIdsV1
    {
        public static readonly StableId DamageReceiver =
            StableId.Parse("combat-capability.damage-receiver");
    }

    public enum CombatEffectGeometryKindV1
    {
        Projectile = 1,
        Explosion = 2,
        MeleeSwing = 3,
        ContactAttack = 4,
        PersistentField = 5,
        Chain = 6,
    }

    public enum CombatWorldBlockerBehaviorV1
    {
        Ignore = 1,
        Terminate = 2,
        Reflect = 3,
    }

    public enum CombatHitContactKindV1
    {
        Actor = 1,
        WorldBlocker = 2,
    }

    public enum CombatRelationRuleV1
    {
        EffectControlled = 1,
        AlwaysAllow = 2,
        AlwaysDeny = 3,
    }

    public enum CombatHitDispositionV1
    {
        Ignore = 1,
        Apply = 2,
        ApplyAndTerminate = 3,
        Terminate = 4,
        Reflect = 5,
    }

    public enum CombatHitRejectionCodeV1
    {
        None = 0,
        MissingInput = 1,
        InvalidEffect = 2,
        UnknownPolicy = 3,
        UnknownSourceActor = 4,
        SourceInactive = 5,
        SourceActorMismatch = 6,
        StaleSourceGeneration = 7,
        InvalidHistory = 8,
        InvalidContact = 9,
        UnknownTargetActor = 10,
        TargetActorMismatch = 11,
        TargetInactive = 12,
        StaleTargetGeneration = 13,
        MissingDamageReceiverCapability = 14,
        SelfHitDenied = 15,
        FriendlyFireDenied = 16,
        AlreadyHitLimitReached = 17,
        PierceExhausted = 18,
    }

    public sealed class CombatActorSnapshotV1
    {
        private readonly ReadOnlyCollection<StableId> capabilities;

        public CombatActorSnapshotV1(
            StableId observedActorId,
            GameplayEntityIdentity identity,
            long lifecycleGeneration,
            bool isKnown,
            bool isActive,
            IList<StableId> capabilityIds)
        {
            ObservedActorId = observedActorId;
            Identity = identity;
            LifecycleGeneration = lifecycleGeneration;
            IsKnown = isKnown;
            IsActive = isActive;

            var copy = new List<StableId>();
            if (capabilityIds != null)
            {
                for (int index = 0; index < capabilityIds.Count; index++)
                {
                    copy.Add(capabilityIds[index]);
                }
            }
            copy.Sort(CompareIds);
            capabilities = new ReadOnlyCollection<StableId>(copy);
        }

        public StableId ObservedActorId { get; }
        public GameplayEntityIdentity Identity { get; }
        public long LifecycleGeneration { get; }
        public bool IsKnown { get; }
        public bool IsActive { get; }
        public IReadOnlyList<StableId> CapabilityIds { get { return capabilities; } }

        public StableId ActorId
        {
            get
            {
                return Identity == null
                    ? ObservedActorId
                    : Identity.EntityInstanceId;
            }
        }

        public StableId FactionId
        {
            get { return Identity == null ? null : Identity.FactionId; }
        }

        public bool HasCapability(StableId capabilityId)
        {
            if (capabilityId == null)
            {
                return false;
            }
            for (int index = 0; index < capabilities.Count; index++)
            {
                if (capabilities[index] == capabilityId)
                {
                    return true;
                }
            }
            return false;
        }

        private static int CompareIds(StableId left, StableId right)
        {
            if (ReferenceEquals(left, right))
            {
                return 0;
            }
            if (left == null)
            {
                return 1;
            }
            return right == null ? -1 : left.CompareTo(right);
        }
    }

    public sealed class CombatEffectSnapshotV1
    {
        public CombatEffectSnapshotV1(
            StableId effectId,
            StableId policyId,
            StableId sourceActorId,
            long sourceLifecycleGeneration,
            CombatEffectGeometryKindV1 geometryKind,
            CombatWorldBlockerBehaviorV1 worldBlockerBehavior,
            bool allowsSelfHit,
            bool allowsFriendlyFire,
            int pierce,
            int maximumHitsPerTarget)
        {
            EffectId = effectId;
            PolicyId = policyId;
            SourceActorId = sourceActorId;
            SourceLifecycleGeneration = sourceLifecycleGeneration;
            GeometryKind = geometryKind;
            WorldBlockerBehavior = worldBlockerBehavior;
            AllowsSelfHit = allowsSelfHit;
            AllowsFriendlyFire = allowsFriendlyFire;
            Pierce = pierce;
            MaximumHitsPerTarget = maximumHitsPerTarget;
        }

        public StableId EffectId { get; }
        public StableId PolicyId { get; }
        public StableId SourceActorId { get; }
        public long SourceLifecycleGeneration { get; }
        public CombatEffectGeometryKindV1 GeometryKind { get; }
        public CombatWorldBlockerBehaviorV1 WorldBlockerBehavior { get; }
        public bool AllowsSelfHit { get; }
        public bool AllowsFriendlyFire { get; }
        public int Pierce { get; }
        public int MaximumHitsPerTarget { get; }
    }

    public sealed class CombatHitContactV1
    {
        private CombatHitContactV1(
            CombatHitContactKindV1 kind,
            CombatActorSnapshotV1 targetActor,
            long observedTargetGeneration,
            StableId worldBlockerId,
            double distanceSquared)
        {
            Kind = kind;
            TargetActor = targetActor;
            ObservedTargetGeneration = observedTargetGeneration;
            WorldBlockerId = worldBlockerId;
            DistanceSquared = distanceSquared;
        }

        public CombatHitContactKindV1 Kind { get; }
        public CombatActorSnapshotV1 TargetActor { get; }
        public long ObservedTargetGeneration { get; }
        public StableId WorldBlockerId { get; }
        public double DistanceSquared { get; }

        public StableId SortId
        {
            get
            {
                return Kind == CombatHitContactKindV1.Actor
                    ? (TargetActor == null ? null : TargetActor.ActorId)
                    : WorldBlockerId;
            }
        }

        public static CombatHitContactV1 Actor(
            CombatActorSnapshotV1 targetActor,
            long observedTargetGeneration,
            double distanceSquared)
        {
            return new CombatHitContactV1(
                CombatHitContactKindV1.Actor,
                targetActor,
                observedTargetGeneration,
                null,
                distanceSquared);
        }

        public static CombatHitContactV1 WorldBlocker(
            StableId worldBlockerId,
            double distanceSquared)
        {
            return new CombatHitContactV1(
                CombatHitContactKindV1.WorldBlocker,
                null,
                0L,
                worldBlockerId,
                distanceSquared);
        }
    }

    public sealed class CombatHitTargetCountV1
    {
        public CombatHitTargetCountV1(StableId targetActorId, int acceptedHitCount)
        {
            TargetActorId = targetActorId;
            AcceptedHitCount = acceptedHitCount;
        }

        public StableId TargetActorId { get; }
        public int AcceptedHitCount { get; }
    }

    public sealed class CombatHitHistorySnapshotV1
    {
        private readonly ReadOnlyCollection<CombatHitTargetCountV1> targetCounts;

        public CombatHitHistorySnapshotV1(
            StableId effectId,
            int acceptedActorHitCount,
            IList<CombatHitTargetCountV1> acceptedTargetCounts)
        {
            EffectId = effectId;
            AcceptedActorHitCount = acceptedActorHitCount;
            var copy = new List<CombatHitTargetCountV1>();
            if (acceptedTargetCounts != null)
            {
                for (int index = 0; index < acceptedTargetCounts.Count; index++)
                {
                    copy.Add(acceptedTargetCounts[index]);
                }
            }
            copy.Sort(CompareTargetCounts);
            targetCounts = new ReadOnlyCollection<CombatHitTargetCountV1>(copy);
        }

        public StableId EffectId { get; }
        public int AcceptedActorHitCount { get; }
        public IReadOnlyList<CombatHitTargetCountV1> TargetCounts
        {
            get { return targetCounts; }
        }

        public static CombatHitHistorySnapshotV1 Empty(StableId effectId)
        {
            return new CombatHitHistorySnapshotV1(
                effectId,
                0,
                new List<CombatHitTargetCountV1>());
        }

        internal bool IsValid()
        {
            if (EffectId == null || AcceptedActorHitCount < 0)
            {
                return false;
            }

            StableId previous = null;
            long sum = 0L;
            for (int index = 0; index < targetCounts.Count; index++)
            {
                CombatHitTargetCountV1 entry = targetCounts[index];
                if (entry == null
                    || entry.TargetActorId == null
                    || entry.AcceptedHitCount <= 0
                    || (previous != null && previous == entry.TargetActorId))
                {
                    return false;
                }
                sum += entry.AcceptedHitCount;
                if (sum > int.MaxValue)
                {
                    return false;
                }
                previous = entry.TargetActorId;
            }
            return sum == AcceptedActorHitCount;
        }

        internal bool TryGetHitCount(StableId targetActorId, out int hitCount)
        {
            hitCount = 0;
            if (targetActorId == null)
            {
                return false;
            }
            for (int index = 0; index < targetCounts.Count; index++)
            {
                if (targetCounts[index].TargetActorId == targetActorId)
                {
                    hitCount = targetCounts[index].AcceptedHitCount;
                    return true;
                }
            }
            return true;
        }

        internal CombatHitHistorySnapshotV1 WithAcceptedHit(StableId targetActorId)
        {
            var next = new List<CombatHitTargetCountV1>(targetCounts.Count + 1);
            bool replaced = false;
            for (int index = 0; index < targetCounts.Count; index++)
            {
                CombatHitTargetCountV1 entry = targetCounts[index];
                if (entry.TargetActorId == targetActorId)
                {
                    next.Add(new CombatHitTargetCountV1(
                        targetActorId,
                        entry.AcceptedHitCount + 1));
                    replaced = true;
                }
                else
                {
                    next.Add(entry);
                }
            }
            if (!replaced)
            {
                next.Add(new CombatHitTargetCountV1(targetActorId, 1));
            }
            return new CombatHitHistorySnapshotV1(
                EffectId,
                AcceptedActorHitCount + 1,
                next);
        }

        private static int CompareTargetCounts(
            CombatHitTargetCountV1 left,
            CombatHitTargetCountV1 right)
        {
            if (ReferenceEquals(left, right))
            {
                return 0;
            }
            if (left == null || left.TargetActorId == null)
            {
                return 1;
            }
            if (right == null || right.TargetActorId == null)
            {
                return -1;
            }
            return left.TargetActorId.CompareTo(right.TargetActorId);
        }
    }

    public sealed class CombatHitPolicyDefinitionV1
    {
        public CombatHitPolicyDefinitionV1(
            StableId policyId,
            CombatRelationRuleV1 selfHitRule,
            CombatRelationRuleV1 friendlyFireRule,
            bool requiresDamageReceiverCapability)
        {
            PolicyId = policyId ?? throw new ArgumentNullException(nameof(policyId));
            if (!Enum.IsDefined(typeof(CombatRelationRuleV1), selfHitRule))
            {
                throw new ArgumentOutOfRangeException(nameof(selfHitRule));
            }
            if (!Enum.IsDefined(typeof(CombatRelationRuleV1), friendlyFireRule))
            {
                throw new ArgumentOutOfRangeException(nameof(friendlyFireRule));
            }
            SelfHitRule = selfHitRule;
            FriendlyFireRule = friendlyFireRule;
            RequiresDamageReceiverCapability = requiresDamageReceiverCapability;
        }

        public StableId PolicyId { get; }
        public CombatRelationRuleV1 SelfHitRule { get; }
        public CombatRelationRuleV1 FriendlyFireRule { get; }
        public bool RequiresDamageReceiverCapability { get; }
    }

    public sealed class CombatHitPolicyRegistryV1
    {
        private readonly Dictionary<StableId, CombatHitPolicyDefinitionV1> byId;

        public CombatHitPolicyRegistryV1(
            IList<CombatHitPolicyDefinitionV1> definitions)
        {
            if (definitions == null)
            {
                throw new ArgumentNullException(nameof(definitions));
            }
            byId = new Dictionary<StableId, CombatHitPolicyDefinitionV1>();
            for (int index = 0; index < definitions.Count; index++)
            {
                CombatHitPolicyDefinitionV1 definition = definitions[index];
                if (definition == null || byId.ContainsKey(definition.PolicyId))
                {
                    throw new ArgumentException(
                        "Combat hit policy definitions must be non-null and unique.",
                        nameof(definitions));
                }
                byId.Add(definition.PolicyId, definition);
            }
        }

        public bool TryResolve(
            StableId policyId,
            out CombatHitPolicyDefinitionV1 definition)
        {
            if (policyId == null)
            {
                definition = null;
                return false;
            }
            return byId.TryGetValue(policyId, out definition);
        }

        public static CombatHitPolicyRegistryV1 CreateDefault()
        {
            return new CombatHitPolicyRegistryV1(
                new List<CombatHitPolicyDefinitionV1>
                {
                    new CombatHitPolicyDefinitionV1(
                        CombatHitPolicyIdsV1.PlayerNormal,
                        CombatRelationRuleV1.EffectControlled,
                        CombatRelationRuleV1.EffectControlled,
                        true),
                    new CombatHitPolicyDefinitionV1(
                        CombatHitPolicyIdsV1.EnemyNormal,
                        CombatRelationRuleV1.EffectControlled,
                        CombatRelationRuleV1.EffectControlled,
                        true),
                    new CombatHitPolicyDefinitionV1(
                        CombatHitPolicyIdsV1.ChaoticAllFactions,
                        CombatRelationRuleV1.AlwaysDeny,
                        CombatRelationRuleV1.AlwaysAllow,
                        true),
                });
        }
    }

    public sealed class CombatHitPolicyInputV1
    {
        public CombatHitPolicyInputV1(
            CombatActorSnapshotV1 sourceActor,
            CombatEffectSnapshotV1 effect,
            CombatHitContactV1 contact,
            CombatHitHistorySnapshotV1 history)
        {
            SourceActor = sourceActor;
            Effect = effect;
            Contact = contact;
            History = history;
        }

        public CombatActorSnapshotV1 SourceActor { get; }
        public CombatEffectSnapshotV1 Effect { get; }
        public CombatHitContactV1 Contact { get; }
        public CombatHitHistorySnapshotV1 History { get; }
    }

    public sealed class CombatHitPolicyResultV1
    {
        internal CombatHitPolicyResultV1(
            CombatHitPolicyInputV1 input,
            CombatHitDispositionV1 disposition,
            CombatHitRejectionCodeV1 rejectionCode,
            CombatHitHistorySnapshotV1 nextHistory)
        {
            Input = input;
            Disposition = disposition;
            RejectionCode = rejectionCode;
            NextHistory = nextHistory;
        }

        public CombatHitPolicyInputV1 Input { get; }
        public CombatHitDispositionV1 Disposition { get; }
        public CombatHitRejectionCodeV1 RejectionCode { get; }
        public CombatHitHistorySnapshotV1 NextHistory { get; }

        public bool DamageEligible
        {
            get
            {
                return Disposition == CombatHitDispositionV1.Apply
                    || Disposition == CombatHitDispositionV1.ApplyAndTerminate;
            }
        }

        public bool TerminatesEffect
        {
            get
            {
                return Disposition == CombatHitDispositionV1.ApplyAndTerminate
                    || Disposition == CombatHitDispositionV1.Terminate;
            }
        }

        public bool ReflectsEffect
        {
            get { return Disposition == CombatHitDispositionV1.Reflect; }
        }
    }

    public interface ICombatHitPolicyV1
    {
        CombatHitPolicyResultV1 Evaluate(CombatHitPolicyInputV1 input);
        IReadOnlyList<CombatHitContactV1> OrderContacts(
            IEnumerable<CombatHitContactV1> contacts);
    }

    public sealed class CombatHitPolicyV1 : ICombatHitPolicyV1
    {
        private readonly CombatHitPolicyRegistryV1 registry;

        public CombatHitPolicyV1(CombatHitPolicyRegistryV1 registry)
        {
            this.registry = registry ?? throw new ArgumentNullException(nameof(registry));
        }

        public CombatHitPolicyResultV1 Evaluate(CombatHitPolicyInputV1 input)
        {
            if (input == null)
            {
                return Reject(null, CombatHitRejectionCodeV1.MissingInput, null);
            }

            CombatEffectSnapshotV1 effect = input.Effect;
            if (!ValidEffect(effect))
            {
                return Reject(input, CombatHitRejectionCodeV1.InvalidEffect, input.History);
            }

            CombatHitPolicyDefinitionV1 definition;
            if (!registry.TryResolve(effect.PolicyId, out definition))
            {
                return Reject(input, CombatHitRejectionCodeV1.UnknownPolicy, input.History);
            }

            CombatHitRejectionCodeV1 sourceCode = ValidateSource(
                input.SourceActor,
                effect);
            if (sourceCode != CombatHitRejectionCodeV1.None)
            {
                return Reject(input, sourceCode, input.History);
            }

            CombatHitHistorySnapshotV1 history = input.History;
            if (history == null
                || !history.IsValid()
                || history.EffectId != effect.EffectId)
            {
                return Reject(input, CombatHitRejectionCodeV1.InvalidHistory, history);
            }

            CombatHitContactV1 contact = input.Contact;
            if (!ValidContact(contact))
            {
                return Reject(input, CombatHitRejectionCodeV1.InvalidContact, history);
            }

            if (contact.Kind == CombatHitContactKindV1.WorldBlocker)
            {
                return ResolveWorld(input, effect, history);
            }

            CombatActorSnapshotV1 target = contact.TargetActor;
            CombatHitRejectionCodeV1 targetCode = ValidateTarget(
                target,
                contact.ObservedTargetGeneration);
            if (targetCode != CombatHitRejectionCodeV1.None)
            {
                return Reject(input, targetCode, history);
            }

            if (definition.RequiresDamageReceiverCapability
                && !target.HasCapability(CombatHitCapabilityIdsV1.DamageReceiver))
            {
                return Reject(
                    input,
                    CombatHitRejectionCodeV1.MissingDamageReceiverCapability,
                    history);
            }

            bool self = input.SourceActor.ActorId == target.ActorId;
            if (self && !RelationAllowed(
                definition.SelfHitRule,
                effect.AllowsSelfHit))
            {
                return Reject(input, CombatHitRejectionCodeV1.SelfHitDenied, history);
            }

            bool friendly = !self
                && input.SourceActor.FactionId == target.FactionId;
            if (friendly && !RelationAllowed(
                definition.FriendlyFireRule,
                effect.AllowsFriendlyFire))
            {
                return Reject(
                    input,
                    CombatHitRejectionCodeV1.FriendlyFireDenied,
                    history);
            }

            int targetHitCount;
            if (!history.TryGetHitCount(target.ActorId, out targetHitCount))
            {
                return Reject(input, CombatHitRejectionCodeV1.InvalidHistory, history);
            }
            if (targetHitCount >= effect.MaximumHitsPerTarget)
            {
                return Reject(
                    input,
                    CombatHitRejectionCodeV1.AlreadyHitLimitReached,
                    history);
            }

            long maximumActorHits = (long)effect.Pierce + 1L;
            if (history.AcceptedActorHitCount >= maximumActorHits)
            {
                return Reject(input, CombatHitRejectionCodeV1.PierceExhausted, history);
            }

            CombatHitHistorySnapshotV1 next = history.WithAcceptedHit(target.ActorId);
            CombatHitDispositionV1 disposition =
                next.AcceptedActorHitCount >= maximumActorHits
                    ? CombatHitDispositionV1.ApplyAndTerminate
                    : CombatHitDispositionV1.Apply;
            return new CombatHitPolicyResultV1(
                input,
                disposition,
                CombatHitRejectionCodeV1.None,
                next);
        }

        public IReadOnlyList<CombatHitContactV1> OrderContacts(
            IEnumerable<CombatHitContactV1> contacts)
        {
            var ordered = contacts == null
                ? new List<CombatHitContactV1>()
                : new List<CombatHitContactV1>(contacts);
            ordered.Sort(CompareContacts);
            return new ReadOnlyCollection<CombatHitContactV1>(ordered);
        }

        private static bool ValidEffect(CombatEffectSnapshotV1 effect)
        {
            return effect != null
                && effect.EffectId != null
                && effect.PolicyId != null
                && effect.SourceActorId != null
                && effect.SourceLifecycleGeneration >= 0L
                && Enum.IsDefined(typeof(CombatEffectGeometryKindV1), effect.GeometryKind)
                && Enum.IsDefined(
                    typeof(CombatWorldBlockerBehaviorV1),
                    effect.WorldBlockerBehavior)
                && effect.Pierce >= 0
                && effect.MaximumHitsPerTarget > 0;
        }

        private static CombatHitRejectionCodeV1 ValidateSource(
            CombatActorSnapshotV1 source,
            CombatEffectSnapshotV1 effect)
        {
            if (source == null
                || !source.IsKnown
                || source.Identity == null
                || source.ActorId == null
                || source.FactionId == null)
            {
                return CombatHitRejectionCodeV1.UnknownSourceActor;
            }
            if (!source.IsActive)
            {
                return CombatHitRejectionCodeV1.SourceInactive;
            }
            if (source.ObservedActorId == null
                || source.ObservedActorId != source.ActorId
                || source.ActorId != effect.SourceActorId)
            {
                return CombatHitRejectionCodeV1.SourceActorMismatch;
            }
            return source.LifecycleGeneration < 0L
                || source.LifecycleGeneration != effect.SourceLifecycleGeneration
                    ? CombatHitRejectionCodeV1.StaleSourceGeneration
                    : CombatHitRejectionCodeV1.None;
        }

        private static CombatHitRejectionCodeV1 ValidateTarget(
            CombatActorSnapshotV1 target,
            long observedGeneration)
        {
            if (target == null
                || !target.IsKnown
                || target.Identity == null
                || target.ActorId == null
                || target.FactionId == null)
            {
                return CombatHitRejectionCodeV1.UnknownTargetActor;
            }
            if (target.ObservedActorId == null
                || target.ObservedActorId != target.ActorId)
            {
                return CombatHitRejectionCodeV1.TargetActorMismatch;
            }
            if (!target.IsActive)
            {
                return CombatHitRejectionCodeV1.TargetInactive;
            }
            return target.LifecycleGeneration < 0L
                || observedGeneration < 0L
                || target.LifecycleGeneration != observedGeneration
                    ? CombatHitRejectionCodeV1.StaleTargetGeneration
                    : CombatHitRejectionCodeV1.None;
        }

        private static bool ValidContact(CombatHitContactV1 contact)
        {
            if (contact == null
                || !Enum.IsDefined(typeof(CombatHitContactKindV1), contact.Kind)
                || double.IsNaN(contact.DistanceSquared)
                || double.IsInfinity(contact.DistanceSquared)
                || contact.DistanceSquared < 0d)
            {
                return false;
            }
            return contact.Kind == CombatHitContactKindV1.Actor
                ? contact.WorldBlockerId == null
                : contact.TargetActor == null && contact.WorldBlockerId != null;
        }

        private static CombatHitPolicyResultV1 ResolveWorld(
            CombatHitPolicyInputV1 input,
            CombatEffectSnapshotV1 effect,
            CombatHitHistorySnapshotV1 history)
        {
            CombatHitDispositionV1 disposition;
            switch (effect.WorldBlockerBehavior)
            {
                case CombatWorldBlockerBehaviorV1.Ignore:
                    disposition = CombatHitDispositionV1.Ignore;
                    break;
                case CombatWorldBlockerBehaviorV1.Terminate:
                    disposition = CombatHitDispositionV1.Terminate;
                    break;
                case CombatWorldBlockerBehaviorV1.Reflect:
                    disposition = CombatHitDispositionV1.Reflect;
                    break;
                default:
                    return Reject(input, CombatHitRejectionCodeV1.InvalidEffect, history);
            }
            return new CombatHitPolicyResultV1(
                input,
                disposition,
                CombatHitRejectionCodeV1.None,
                history);
        }

        private static bool RelationAllowed(
            CombatRelationRuleV1 rule,
            bool effectAllows)
        {
            switch (rule)
            {
                case CombatRelationRuleV1.AlwaysAllow:
                    return true;
                case CombatRelationRuleV1.EffectControlled:
                    return effectAllows;
                case CombatRelationRuleV1.AlwaysDeny:
                default:
                    return false;
            }
        }

        private static CombatHitPolicyResultV1 Reject(
            CombatHitPolicyInputV1 input,
            CombatHitRejectionCodeV1 code,
            CombatHitHistorySnapshotV1 history)
        {
            return new CombatHitPolicyResultV1(
                input,
                CombatHitDispositionV1.Ignore,
                code,
                history);
        }

        private static int CompareContacts(
            CombatHitContactV1 left,
            CombatHitContactV1 right)
        {
            if (ReferenceEquals(left, right))
            {
                return 0;
            }
            if (left == null)
            {
                return 1;
            }
            if (right == null)
            {
                return -1;
            }

            int distance = left.DistanceSquared.CompareTo(right.DistanceSquared);
            if (distance != 0)
            {
                return distance;
            }
            int kind = (left.Kind == CombatHitContactKindV1.WorldBlocker ? 0 : 1)
                .CompareTo(right.Kind == CombatHitContactKindV1.WorldBlocker ? 0 : 1);
            if (kind != 0)
            {
                return kind;
            }
            int id = CompareIds(left.SortId, right.SortId);
            return id != 0
                ? id
                : left.ObservedTargetGeneration.CompareTo(
                    right.ObservedTargetGeneration);
        }

        private static int CompareIds(StableId left, StableId right)
        {
            if (ReferenceEquals(left, right))
            {
                return 0;
            }
            if (left == null)
            {
                return 1;
            }
            return right == null ? -1 : left.CompareTo(right);
        }
    }

    public static class CombatActorSnapshotFactoryV1
    {
        public static CombatActorSnapshotV1 CreateKnown(
            GameplayEntityIdentity identity,
            long lifecycleGeneration,
            bool isActive,
            IList<StableId> capabilities)
        {
            return new CombatActorSnapshotV1(
                identity == null ? null : identity.EntityInstanceId,
                identity,
                lifecycleGeneration,
                true,
                isActive,
                capabilities);
        }

        public static CombatActorSnapshotV1 CreateDamageReceiver(
            GameplayEntityIdentity identity,
            long lifecycleGeneration,
            bool isActive)
        {
            return CreateKnown(
                identity,
                lifecycleGeneration,
                isActive,
                new List<StableId>
                {
                    CombatHitCapabilityIdsV1.DamageReceiver,
                });
        }

        public static CombatActorSnapshotV1 CreateUnknown(
            StableId observedActorId,
            long observedLifecycleGeneration)
        {
            return new CombatActorSnapshotV1(
                observedActorId,
                null,
                observedLifecycleGeneration,
                false,
                false,
                new List<StableId>());
        }
    }
}
