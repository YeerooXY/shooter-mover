using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ShooterMover.Domain.Common;
using ShooterMover.GameplayEntities;

namespace ShooterMover.Combat.HitPolicy
{
    public static class CombatHitPolicyIds
    {
        public static readonly StableId PlayerNormal =
            StableId.Parse("combat-hit-policy.player-normal-v1");
        public static readonly StableId EnemyNormal =
            StableId.Parse("combat-hit-policy.enemy-normal-v1");
        public static readonly StableId ChaoticAllFactions =
            StableId.Parse("combat-hit-policy.chaotic-all-factions-v1");
    }

    public static class CombatHitCapabilityIds
    {
        public static readonly StableId DamageReceiver =
            StableId.Parse("combat-capability.damage-receiver");
    }

    public enum CombatEffectGeometryKind
    {
        Projectile = 1,
        Explosion = 2,
        MeleeSwing = 3,
        ContactAttack = 4,
        PersistentField = 5,
        Chain = 6,
    }

    public enum CombatWorldBlockerBehavior
    {
        Ignore = 1,
        Terminate = 2,
        Reflect = 3,
    }

    public enum CombatHitContactKind
    {
        Actor = 1,
        WorldBlocker = 2,
    }

    public enum CombatRelationRule
    {
        EffectControlled = 1,
        AlwaysAllow = 2,
        AlwaysDeny = 3,
    }

    public enum CombatHitDisposition
    {
        Ignore = 1,
        Apply = 2,
        ApplyAndTerminate = 3,
        Terminate = 4,
        Reflect = 5,
    }

    public enum CombatHitRejectionCode
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

    public sealed class CombatActorSnapshot
    {
        private readonly ReadOnlyCollection<StableId> capabilities;

        public CombatActorSnapshot(
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

    public sealed class CombatEffectSnapshot
    {
        public CombatEffectSnapshot(
            StableId effectId,
            StableId policyId,
            StableId sourceActorId,
            long sourceLifecycleGeneration,
            CombatEffectGeometryKind geometryKind,
            CombatWorldBlockerBehavior worldBlockerBehavior,
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
        public CombatEffectGeometryKind GeometryKind { get; }
        public CombatWorldBlockerBehavior WorldBlockerBehavior { get; }
        public bool AllowsSelfHit { get; }
        public bool AllowsFriendlyFire { get; }
        public int Pierce { get; }
        public int MaximumHitsPerTarget { get; }
    }

    public sealed class CombatHitContact
    {
        private CombatHitContact(
            CombatHitContactKind kind,
            CombatActorSnapshot targetActor,
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

        public CombatHitContactKind Kind { get; }
        public CombatActorSnapshot TargetActor { get; }
        public long ObservedTargetGeneration { get; }
        public StableId WorldBlockerId { get; }
        public double DistanceSquared { get; }

        public StableId SortId
        {
            get
            {
                return Kind == CombatHitContactKind.Actor
                    ? (TargetActor == null ? null : TargetActor.ActorId)
                    : WorldBlockerId;
            }
        }

        public static CombatHitContact Actor(
            CombatActorSnapshot targetActor,
            long observedTargetGeneration,
            double distanceSquared)
        {
            return new CombatHitContact(
                CombatHitContactKind.Actor,
                targetActor,
                observedTargetGeneration,
                null,
                distanceSquared);
        }

        public static CombatHitContact WorldBlocker(
            StableId worldBlockerId,
            double distanceSquared)
        {
            return new CombatHitContact(
                CombatHitContactKind.WorldBlocker,
                null,
                0L,
                worldBlockerId,
                distanceSquared);
        }
    }

    public sealed class CombatHitTargetCount
    {
        public CombatHitTargetCount(StableId targetActorId, int acceptedHitCount)
        {
            TargetActorId = targetActorId;
            AcceptedHitCount = acceptedHitCount;
        }

        public StableId TargetActorId { get; }
        public int AcceptedHitCount { get; }
    }

    public sealed class CombatHitHistorySnapshot
    {
        private readonly ReadOnlyCollection<CombatHitTargetCount> targetCounts;

        public CombatHitHistorySnapshot(
            StableId effectId,
            int acceptedActorHitCount,
            IList<CombatHitTargetCount> acceptedTargetCounts)
        {
            EffectId = effectId;
            AcceptedActorHitCount = acceptedActorHitCount;
            var copy = new List<CombatHitTargetCount>();
            if (acceptedTargetCounts != null)
            {
                for (int index = 0; index < acceptedTargetCounts.Count; index++)
                {
                    copy.Add(acceptedTargetCounts[index]);
                }
            }
            copy.Sort(CompareTargetCounts);
            targetCounts = new ReadOnlyCollection<CombatHitTargetCount>(copy);
        }

        public StableId EffectId { get; }
        public int AcceptedActorHitCount { get; }
        public IReadOnlyList<CombatHitTargetCount> TargetCounts
        {
            get { return targetCounts; }
        }

        public static CombatHitHistorySnapshot Empty(StableId effectId)
        {
            return new CombatHitHistorySnapshot(
                effectId,
                0,
                new List<CombatHitTargetCount>());
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
                CombatHitTargetCount entry = targetCounts[index];
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

        internal CombatHitHistorySnapshot WithAcceptedHit(StableId targetActorId)
        {
            var next = new List<CombatHitTargetCount>(targetCounts.Count + 1);
            bool replaced = false;
            for (int index = 0; index < targetCounts.Count; index++)
            {
                CombatHitTargetCount entry = targetCounts[index];
                if (entry.TargetActorId == targetActorId)
                {
                    next.Add(new CombatHitTargetCount(
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
                next.Add(new CombatHitTargetCount(targetActorId, 1));
            }
            return new CombatHitHistorySnapshot(
                EffectId,
                AcceptedActorHitCount + 1,
                next);
        }

        private static int CompareTargetCounts(
            CombatHitTargetCount left,
            CombatHitTargetCount right)
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

    public sealed class CombatHitPolicyDefinition
    {
        public CombatHitPolicyDefinition(
            StableId policyId,
            CombatRelationRule selfHitRule,
            CombatRelationRule friendlyFireRule,
            bool requiresDamageReceiverCapability)
        {
            PolicyId = policyId ?? throw new ArgumentNullException(nameof(policyId));
            if (!Enum.IsDefined(typeof(CombatRelationRule), selfHitRule))
            {
                throw new ArgumentOutOfRangeException(nameof(selfHitRule));
            }
            if (!Enum.IsDefined(typeof(CombatRelationRule), friendlyFireRule))
            {
                throw new ArgumentOutOfRangeException(nameof(friendlyFireRule));
            }
            SelfHitRule = selfHitRule;
            FriendlyFireRule = friendlyFireRule;
            RequiresDamageReceiverCapability = requiresDamageReceiverCapability;
        }

        public StableId PolicyId { get; }
        public CombatRelationRule SelfHitRule { get; }
        public CombatRelationRule FriendlyFireRule { get; }
        public bool RequiresDamageReceiverCapability { get; }
    }

    public sealed class CombatHitPolicyRegistry
    {
        private readonly Dictionary<StableId, CombatHitPolicyDefinition> byId;

        public CombatHitPolicyRegistry(
            IList<CombatHitPolicyDefinition> definitions)
        {
            if (definitions == null)
            {
                throw new ArgumentNullException(nameof(definitions));
            }
            byId = new Dictionary<StableId, CombatHitPolicyDefinition>();
            for (int index = 0; index < definitions.Count; index++)
            {
                CombatHitPolicyDefinition definition = definitions[index];
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
            out CombatHitPolicyDefinition definition)
        {
            if (policyId == null)
            {
                definition = null;
                return false;
            }
            return byId.TryGetValue(policyId, out definition);
        }

        public static CombatHitPolicyRegistry CreateDefault()
        {
            return new CombatHitPolicyRegistry(
                new List<CombatHitPolicyDefinition>
                {
                    new CombatHitPolicyDefinition(
                        CombatHitPolicyIds.PlayerNormal,
                        CombatRelationRule.EffectControlled,
                        CombatRelationRule.EffectControlled,
                        true),
                    new CombatHitPolicyDefinition(
                        CombatHitPolicyIds.EnemyNormal,
                        CombatRelationRule.EffectControlled,
                        CombatRelationRule.EffectControlled,
                        true),
                    new CombatHitPolicyDefinition(
                        CombatHitPolicyIds.ChaoticAllFactions,
                        CombatRelationRule.AlwaysDeny,
                        CombatRelationRule.AlwaysAllow,
                        true),
                });
        }
    }

    public sealed class CombatHitPolicyInput
    {
        public CombatHitPolicyInput(
            CombatActorSnapshot sourceActor,
            CombatEffectSnapshot effect,
            CombatHitContact contact,
            CombatHitHistorySnapshot history)
        {
            SourceActor = sourceActor;
            Effect = effect;
            Contact = contact;
            History = history;
        }

        public CombatActorSnapshot SourceActor { get; }
        public CombatEffectSnapshot Effect { get; }
        public CombatHitContact Contact { get; }
        public CombatHitHistorySnapshot History { get; }
    }

    public sealed class CombatHitPolicyResult
    {
        internal CombatHitPolicyResult(
            CombatHitPolicyInput input,
            CombatHitDisposition disposition,
            CombatHitRejectionCode rejectionCode,
            CombatHitHistorySnapshot nextHistory)
        {
            Input = input;
            Disposition = disposition;
            RejectionCode = rejectionCode;
            NextHistory = nextHistory;
        }

        public CombatHitPolicyInput Input { get; }
        public CombatHitDisposition Disposition { get; }
        public CombatHitRejectionCode RejectionCode { get; }
        public CombatHitHistorySnapshot NextHistory { get; }

        public bool DamageEligible
        {
            get
            {
                return Disposition == CombatHitDisposition.Apply
                    || Disposition == CombatHitDisposition.ApplyAndTerminate;
            }
        }

        public bool TerminatesEffect
        {
            get
            {
                return Disposition == CombatHitDisposition.ApplyAndTerminate
                    || Disposition == CombatHitDisposition.Terminate;
            }
        }

        public bool ReflectsEffect
        {
            get { return Disposition == CombatHitDisposition.Reflect; }
        }
    }

    public interface ICombatHitRules
    {
        CombatHitPolicyResult Evaluate(CombatHitPolicyInput input);
        IReadOnlyList<CombatHitContact> OrderContacts(
            IEnumerable<CombatHitContact> contacts);
    }

    public sealed class CombatHitRules : ICombatHitRules
    {
        private readonly CombatHitPolicyRegistry registry;

        public CombatHitRules(CombatHitPolicyRegistry registry)
        {
            this.registry = registry ?? throw new ArgumentNullException(nameof(registry));
        }

        public CombatHitPolicyResult Evaluate(CombatHitPolicyInput input)
        {
            if (input == null)
            {
                return Reject(null, CombatHitRejectionCode.MissingInput, null);
            }

            CombatEffectSnapshot effect = input.Effect;
            if (!ValidEffect(effect))
            {
                return Reject(input, CombatHitRejectionCode.InvalidEffect, input.History);
            }

            CombatHitPolicyDefinition definition;
            if (!registry.TryResolve(effect.PolicyId, out definition))
            {
                return Reject(input, CombatHitRejectionCode.UnknownPolicy, input.History);
            }

            CombatHitRejectionCode sourceCode = ValidateSource(
                input.SourceActor,
                effect);
            if (sourceCode != CombatHitRejectionCode.None)
            {
                return Reject(input, sourceCode, input.History);
            }

            CombatHitHistorySnapshot history = input.History;
            if (history == null
                || !history.IsValid()
                || history.EffectId != effect.EffectId)
            {
                return Reject(input, CombatHitRejectionCode.InvalidHistory, history);
            }

            CombatHitContact contact = input.Contact;
            if (!ValidContact(contact))
            {
                return Reject(input, CombatHitRejectionCode.InvalidContact, history);
            }

            if (contact.Kind == CombatHitContactKind.WorldBlocker)
            {
                return ResolveWorld(input, effect, history);
            }

            CombatActorSnapshot target = contact.TargetActor;
            CombatHitRejectionCode targetCode = ValidateTarget(
                target,
                contact.ObservedTargetGeneration);
            if (targetCode != CombatHitRejectionCode.None)
            {
                return Reject(input, targetCode, history);
            }

            if (definition.RequiresDamageReceiverCapability
                && !target.HasCapability(CombatHitCapabilityIds.DamageReceiver))
            {
                return Reject(
                    input,
                    CombatHitRejectionCode.MissingDamageReceiverCapability,
                    history);
            }

            bool self = input.SourceActor.ActorId == target.ActorId;
            if (self && !RelationAllowed(
                definition.SelfHitRule,
                effect.AllowsSelfHit))
            {
                return Reject(input, CombatHitRejectionCode.SelfHitDenied, history);
            }

            bool friendly = !self
                && input.SourceActor.FactionId == target.FactionId;
            if (friendly && !RelationAllowed(
                definition.FriendlyFireRule,
                effect.AllowsFriendlyFire))
            {
                return Reject(
                    input,
                    CombatHitRejectionCode.FriendlyFireDenied,
                    history);
            }

            int targetHitCount;
            if (!history.TryGetHitCount(target.ActorId, out targetHitCount))
            {
                return Reject(input, CombatHitRejectionCode.InvalidHistory, history);
            }
            if (targetHitCount >= effect.MaximumHitsPerTarget)
            {
                return Reject(
                    input,
                    CombatHitRejectionCode.AlreadyHitLimitReached,
                    history);
            }

            long maximumActorHits = (long)effect.Pierce + 1L;
            if (history.AcceptedActorHitCount >= maximumActorHits)
            {
                return Reject(input, CombatHitRejectionCode.PierceExhausted, history);
            }

            CombatHitHistorySnapshot next = history.WithAcceptedHit(target.ActorId);
            CombatHitDisposition disposition =
                next.AcceptedActorHitCount >= maximumActorHits
                    ? CombatHitDisposition.ApplyAndTerminate
                    : CombatHitDisposition.Apply;
            return new CombatHitPolicyResult(
                input,
                disposition,
                CombatHitRejectionCode.None,
                next);
        }

        public IReadOnlyList<CombatHitContact> OrderContacts(
            IEnumerable<CombatHitContact> contacts)
        {
            var ordered = contacts == null
                ? new List<CombatHitContact>()
                : new List<CombatHitContact>(contacts);
            ordered.Sort(CompareContacts);
            return new ReadOnlyCollection<CombatHitContact>(ordered);
        }

        private static bool ValidEffect(CombatEffectSnapshot effect)
        {
            return effect != null
                && effect.EffectId != null
                && effect.PolicyId != null
                && effect.SourceActorId != null
                && effect.SourceLifecycleGeneration >= 0L
                && Enum.IsDefined(typeof(CombatEffectGeometryKind), effect.GeometryKind)
                && Enum.IsDefined(
                    typeof(CombatWorldBlockerBehavior),
                    effect.WorldBlockerBehavior)
                && effect.Pierce >= 0
                && effect.MaximumHitsPerTarget > 0;
        }

        private static CombatHitRejectionCode ValidateSource(
            CombatActorSnapshot source,
            CombatEffectSnapshot effect)
        {
            if (source == null
                || !source.IsKnown
                || source.Identity == null
                || source.ActorId == null
                || source.FactionId == null)
            {
                return CombatHitRejectionCode.UnknownSourceActor;
            }
            if (!source.IsActive)
            {
                return CombatHitRejectionCode.SourceInactive;
            }
            if (source.ObservedActorId == null
                || source.ObservedActorId != source.ActorId
                || source.ActorId != effect.SourceActorId)
            {
                return CombatHitRejectionCode.SourceActorMismatch;
            }
            return source.LifecycleGeneration < 0L
                || source.LifecycleGeneration != effect.SourceLifecycleGeneration
                    ? CombatHitRejectionCode.StaleSourceGeneration
                    : CombatHitRejectionCode.None;
        }

        private static CombatHitRejectionCode ValidateTarget(
            CombatActorSnapshot target,
            long observedGeneration)
        {
            if (target == null
                || !target.IsKnown
                || target.Identity == null
                || target.ActorId == null
                || target.FactionId == null)
            {
                return CombatHitRejectionCode.UnknownTargetActor;
            }
            if (target.ObservedActorId == null
                || target.ObservedActorId != target.ActorId)
            {
                return CombatHitRejectionCode.TargetActorMismatch;
            }
            if (!target.IsActive)
            {
                return CombatHitRejectionCode.TargetInactive;
            }
            return target.LifecycleGeneration < 0L
                || observedGeneration < 0L
                || target.LifecycleGeneration != observedGeneration
                    ? CombatHitRejectionCode.StaleTargetGeneration
                    : CombatHitRejectionCode.None;
        }

        private static bool ValidContact(CombatHitContact contact)
        {
            if (contact == null
                || !Enum.IsDefined(typeof(CombatHitContactKind), contact.Kind)
                || double.IsNaN(contact.DistanceSquared)
                || double.IsInfinity(contact.DistanceSquared)
                || contact.DistanceSquared < 0d)
            {
                return false;
            }
            return contact.Kind == CombatHitContactKind.Actor
                ? contact.WorldBlockerId == null
                : contact.TargetActor == null && contact.WorldBlockerId != null;
        }

        private static CombatHitPolicyResult ResolveWorld(
            CombatHitPolicyInput input,
            CombatEffectSnapshot effect,
            CombatHitHistorySnapshot history)
        {
            CombatHitDisposition disposition;
            switch (effect.WorldBlockerBehavior)
            {
                case CombatWorldBlockerBehavior.Ignore:
                    disposition = CombatHitDisposition.Ignore;
                    break;
                case CombatWorldBlockerBehavior.Terminate:
                    disposition = CombatHitDisposition.Terminate;
                    break;
                case CombatWorldBlockerBehavior.Reflect:
                    disposition = CombatHitDisposition.Reflect;
                    break;
                default:
                    return Reject(input, CombatHitRejectionCode.InvalidEffect, history);
            }
            return new CombatHitPolicyResult(
                input,
                disposition,
                CombatHitRejectionCode.None,
                history);
        }

        private static bool RelationAllowed(
            CombatRelationRule rule,
            bool effectAllows)
        {
            switch (rule)
            {
                case CombatRelationRule.AlwaysAllow:
                    return true;
                case CombatRelationRule.EffectControlled:
                    return effectAllows;
                case CombatRelationRule.AlwaysDeny:
                default:
                    return false;
            }
        }

        private static CombatHitPolicyResult Reject(
            CombatHitPolicyInput input,
            CombatHitRejectionCode code,
            CombatHitHistorySnapshot history)
        {
            return new CombatHitPolicyResult(
                input,
                CombatHitDisposition.Ignore,
                code,
                history);
        }

        private static int CompareContacts(
            CombatHitContact left,
            CombatHitContact right)
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
            int kind = (left.Kind == CombatHitContactKind.WorldBlocker ? 0 : 1)
                .CompareTo(right.Kind == CombatHitContactKind.WorldBlocker ? 0 : 1);
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

    public static class CombatActorSnapshotFactory
    {
        public static CombatActorSnapshot CreateKnown(
            GameplayEntityIdentity identity,
            long lifecycleGeneration,
            bool isActive,
            IList<StableId> capabilities)
        {
            return new CombatActorSnapshot(
                identity == null ? null : identity.EntityInstanceId,
                identity,
                lifecycleGeneration,
                true,
                isActive,
                capabilities);
        }

        public static CombatActorSnapshot CreateDamageReceiver(
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
                    CombatHitCapabilityIds.DamageReceiver,
                });
        }

        public static CombatActorSnapshot CreateUnknown(
            StableId observedActorId,
            long observedLifecycleGeneration)
        {
            return new CombatActorSnapshot(
                observedActorId,
                null,
                observedLifecycleGeneration,
                false,
                false,
                new List<StableId>());
        }
    }
}
