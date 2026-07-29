using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using ShooterMover.Combat.HitPolicy;
using ShooterMover.Contracts.Combat;
using ShooterMover.Domain.Characters.Stats;
using ShooterMover.Domain.Common;
using ShooterMover.GameplayEntities;

namespace ShooterMover.Combat.CriticalHits
{
    public static class CriticalHitPolicyIds
    {
        public static readonly StableId Normal =
            StableId.Parse("critical-hit-policy.normal-v1");
        public static readonly StableId CannotCrit =
            StableId.Parse("critical-hit-policy.cannot-crit-v1");
        public static readonly StableId Guaranteed =
            StableId.Parse("critical-hit-policy.guaranteed-v1");
        public static readonly StableId ModifiedChance =
            StableId.Parse("critical-hit-policy.modified-chance-v1");
        public static readonly StableId ModifiedMultiplier =
            StableId.Parse("critical-hit-policy.modified-multiplier-v1");
    }

    /// <summary>
    /// Immutable critical rules authored by a weapon, attack, or effect definition.
    /// Geometry is intentionally absent: execution facts select the policy.
    /// </summary>
    public sealed class CriticalHitPolicyDefinition
    {
        public CriticalHitPolicyDefinition(
            StableId policyId,
            bool canCrit,
            decimal? criticalChanceOverride = null,
            decimal criticalChanceFlatModifier = 0m,
            decimal criticalChanceMultiplier = 1m,
            decimal? criticalMultiplierOverride = null,
            decimal criticalMultiplierFlatModifier = 0m,
            decimal criticalMultiplierMultiplier = 1m)
        {
            PolicyId = policyId ?? throw new ArgumentNullException(nameof(policyId));
            if (criticalChanceOverride.HasValue
                && (criticalChanceOverride.Value < 0m
                    || criticalChanceOverride.Value > 1m))
            {
                throw new ArgumentOutOfRangeException(nameof(criticalChanceOverride));
            }
            if (criticalChanceMultiplier < 0m)
            {
                throw new ArgumentOutOfRangeException(nameof(criticalChanceMultiplier));
            }
            if (criticalMultiplierOverride.HasValue
                && criticalMultiplierOverride.Value < 1m)
            {
                throw new ArgumentOutOfRangeException(nameof(criticalMultiplierOverride));
            }
            if (criticalMultiplierMultiplier <= 0m)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(criticalMultiplierMultiplier));
            }

            CanCrit = canCrit;
            CriticalChanceOverride = criticalChanceOverride;
            CriticalChanceFlatModifier = criticalChanceFlatModifier;
            CriticalChanceMultiplier = criticalChanceMultiplier;
            CriticalMultiplierOverride = criticalMultiplierOverride;
            CriticalMultiplierFlatModifier = criticalMultiplierFlatModifier;
            CriticalMultiplierMultiplier = criticalMultiplierMultiplier;
            Fingerprint = CriticalHitFingerprint.Hash(ToCanonicalString());
        }

        public StableId PolicyId { get; }
        public bool CanCrit { get; }
        public decimal? CriticalChanceOverride { get; }
        public decimal CriticalChanceFlatModifier { get; }
        public decimal CriticalChanceMultiplier { get; }
        public decimal? CriticalMultiplierOverride { get; }
        public decimal CriticalMultiplierFlatModifier { get; }
        public decimal CriticalMultiplierMultiplier { get; }
        public string Fingerprint { get; }

        public decimal ResolveCriticalChance(decimal profileChance)
        {
            if (!CanCrit)
            {
                return 0m;
            }

            decimal value = CriticalChanceOverride
                ?? checked(
                    checked(profileChance + CriticalChanceFlatModifier)
                        * CriticalChanceMultiplier);
            if (value < 0m)
            {
                return 0m;
            }
            return value > 1m ? 1m : value;
        }

        public decimal ResolveCriticalMultiplier(decimal profileMultiplier)
        {
            if (!CanCrit)
            {
                return 1m;
            }

            decimal value = CriticalMultiplierOverride
                ?? checked(
                    checked(profileMultiplier + CriticalMultiplierFlatModifier)
                        * CriticalMultiplierMultiplier);
            return value < 1m ? 1m : value;
        }

        public string ToCanonicalString()
        {
            var builder = new StringBuilder();
            CriticalHitFingerprint.Append(
                builder,
                "schema",
                "critical-hit-policy-definition.v1");
            CriticalHitFingerprint.AppendId(builder, "policy", PolicyId);
            CriticalHitFingerprint.Append(
                builder,
                "can-crit",
                CanCrit ? "1" : "0");
            CriticalHitFingerprint.AppendNullableDecimal(
                builder,
                "chance-override",
                CriticalChanceOverride);
            CriticalHitFingerprint.AppendDecimal(
                builder,
                "chance-flat",
                CriticalChanceFlatModifier);
            CriticalHitFingerprint.AppendDecimal(
                builder,
                "chance-multiplier",
                CriticalChanceMultiplier);
            CriticalHitFingerprint.AppendNullableDecimal(
                builder,
                "multiplier-override",
                CriticalMultiplierOverride);
            CriticalHitFingerprint.AppendDecimal(
                builder,
                "multiplier-flat",
                CriticalMultiplierFlatModifier);
            CriticalHitFingerprint.AppendDecimal(
                builder,
                "multiplier-multiplier",
                CriticalMultiplierMultiplier);
            return builder.ToString();
        }
    }

    public sealed class CriticalHitPolicyRegistry
    {
        private readonly IReadOnlyDictionary<StableId, CriticalHitPolicyDefinition>
            definitionsById;

        public CriticalHitPolicyRegistry(
            IEnumerable<CriticalHitPolicyDefinition> definitions)
        {
            List<CriticalHitPolicyDefinition> items = (definitions
                ?? throw new ArgumentNullException(nameof(definitions))).ToList();
            if (items.Count == 0 || items.Any(item => item == null))
            {
                throw new ArgumentException(
                    "At least one non-null critical-hit policy is required.",
                    nameof(definitions));
            }
            if (items.Select(item => item.PolicyId).Distinct().Count()
                != items.Count)
            {
                throw new ArgumentException(
                    "Critical-hit policy identities must be unique.",
                    nameof(definitions));
            }

            Definitions = new ReadOnlyCollection<CriticalHitPolicyDefinition>(
                items.OrderBy(item => item.PolicyId).ToList());
            definitionsById = new ReadOnlyDictionary<
                StableId,
                CriticalHitPolicyDefinition>(
                    Definitions.ToDictionary(item => item.PolicyId));
            Fingerprint = CriticalHitFingerprint.Hash(
                string.Join(";", Definitions.Select(item => item.Fingerprint)));
        }

        public IReadOnlyList<CriticalHitPolicyDefinition> Definitions { get; }
        public string Fingerprint { get; }

        public bool TryResolve(
            StableId policyId,
            out CriticalHitPolicyDefinition definition)
        {
            if (policyId == null)
            {
                definition = null;
                return false;
            }
            return definitionsById.TryGetValue(policyId, out definition);
        }

        public static CriticalHitPolicyRegistry CreateDefault()
        {
            return new CriticalHitPolicyRegistry(
                new[]
                {
                    new CriticalHitPolicyDefinition(
                        CriticalHitPolicyIds.Normal,
                        true),
                    new CriticalHitPolicyDefinition(
                        CriticalHitPolicyIds.CannotCrit,
                        false),
                    new CriticalHitPolicyDefinition(
                        CriticalHitPolicyIds.Guaranteed,
                        true,
                        criticalChanceOverride: 1m),
                    new CriticalHitPolicyDefinition(
                        CriticalHitPolicyIds.ModifiedChance,
                        true,
                        criticalChanceMultiplier: 0.5m),
                    new CriticalHitPolicyDefinition(
                        CriticalHitPolicyIds.ModifiedMultiplier,
                        true,
                        criticalMultiplierMultiplier: 1.5m),
                });
        }
    }

    /// <summary>
    /// Immutable execution facts projected from the concrete weapon/attack/effect
    /// definition. EquipmentInstanceId is optional for non-equipment attacks.
    /// </summary>
    public sealed class CriticalHitEffectFacts
    {
        public CriticalHitEffectFacts(
            StableId effectDefinitionId,
            StableId criticalPolicyId,
            StableId equipmentInstanceId = null)
        {
            EffectDefinitionId = effectDefinitionId;
            CriticalPolicyId = criticalPolicyId;
            EquipmentInstanceId = equipmentInstanceId;
            Fingerprint = CriticalHitFingerprint.Hash(ToCanonicalString());
        }

        public StableId EffectDefinitionId { get; }
        public StableId CriticalPolicyId { get; }
        public StableId EquipmentInstanceId { get; }
        public bool HasEquipmentInstance { get { return EquipmentInstanceId != null; } }
        public string Fingerprint { get; }

        public string ToCanonicalString()
        {
            var builder = new StringBuilder();
            CriticalHitFingerprint.Append(
                builder,
                "schema",
                "critical-hit-effect-facts.v1");
            CriticalHitFingerprint.AppendId(
                builder,
                "effect-definition",
                EffectDefinitionId);
            CriticalHitFingerprint.AppendId(
                builder,
                "critical-policy",
                CriticalPolicyId);
            CriticalHitFingerprint.AppendId(
                builder,
                "equipment-instance",
                EquipmentInstanceId);
            return builder.ToString();
        }
    }

    public enum CriticalHitResolutionStatus
    {
        Applied = 1,
        Duplicate = 2,
        Rejected = 3,
        ConflictingDuplicate = 4,
    }

    public enum CriticalHitRejectionCode
    {
        None = 0,
        MissingCommand = 1,
        MissingOperationId = 2,
        MissingDeterministicSeed = 3,
        InvalidShotSequence = 4,
        InvalidHitSequence = InvalidShotSequence,
        InvalidBaseDamage = 5,
        InvalidDamageChannel = 6,
        MissingRunCombatProfile = 7,
        InvalidRunCombatProfile = 8,
        HitNotDamageEligible = 9,
        InvalidAcceptedHitFacts = 10,
        ResolvedDamageOverflow = 11,
        ConflictingDuplicate = 12,
        MissingEffectFacts = 13,
        MissingEffectDefinitionId = 14,
        MissingCriticalPolicyId = 15,
        UnknownCriticalPolicy = 16,
        InvalidHitOrdinal = 17,
    }

    /// <summary>
    /// Immutable input to the critical-hit boundary. ShotSequence identifies the fire
    /// operation; HitOrdinal identifies one pellet/contact/target evaluation within it.
    /// </summary>
    public sealed class CriticalHitResolutionCommand
    {
        public CriticalHitResolutionCommand(
            StableId operationId,
            string deterministicSeed,
            long shotSequence,
            int hitOrdinal,
            decimal baseDamage,
            CombatChannel channel,
            RunCombatProfile runCombatProfile,
            CriticalHitEffectFacts effectFacts,
            CombatHitPolicyResult acceptedHit)
        {
            OperationId = operationId;
            DeterministicSeed = deterministicSeed == null
                ? null
                : deterministicSeed.Trim();
            ShotSequence = shotSequence;
            HitOrdinal = hitOrdinal;
            BaseDamage = baseDamage;
            Channel = channel;
            RunCombatProfile = runCombatProfile;
            EffectFacts = effectFacts;
            AcceptedHit = acceptedHit;
            Fingerprint = CriticalHitFingerprint.Hash(ToCanonicalString());
        }

        /// <summary>
        /// Compatibility overload for callers that predate explicit hit ordinals.
        /// Such calls represent the first hit/contact in the shot.
        /// </summary>
        public CriticalHitResolutionCommand(
            StableId operationId,
            string deterministicSeed,
            long hitSequence,
            decimal baseDamage,
            CombatChannel channel,
            RunCombatProfile runCombatProfile,
            CriticalHitEffectFacts effectFacts,
            CombatHitPolicyResult acceptedHit)
            : this(
                operationId,
                deterministicSeed,
                hitSequence,
                0,
                baseDamage,
                channel,
                runCombatProfile,
                effectFacts,
                acceptedHit)
        {
        }

        public StableId OperationId { get; }
        public string DeterministicSeed { get; }
        public long ShotSequence { get; }
        public int HitOrdinal { get; }
        public long HitSequence { get { return ShotSequence; } }
        public decimal BaseDamage { get; }
        public CombatChannel Channel { get; }
        public RunCombatProfile RunCombatProfile { get; }
        public CriticalHitEffectFacts EffectFacts { get; }
        public CombatHitPolicyResult AcceptedHit { get; }
        public string Fingerprint { get; }

        public string ToCanonicalString()
        {
            var builder = new StringBuilder();
            CriticalHitFingerprint.Append(
                builder,
                "schema",
                "critical-hit-command.v1");
            CriticalHitFingerprint.AppendId(builder, "operation", OperationId);
            CriticalHitFingerprint.Append(
                builder,
                "seed",
                DeterministicSeed ?? string.Empty);
            CriticalHitFingerprint.Append(
                builder,
                "shot-sequence",
                ShotSequence.ToString(CultureInfo.InvariantCulture));
            CriticalHitFingerprint.Append(
                builder,
                "hit-ordinal",
                HitOrdinal.ToString(CultureInfo.InvariantCulture));
            CriticalHitFingerprint.AppendDecimal(builder, "base-damage", BaseDamage);
            CriticalHitFingerprint.Append(
                builder,
                "channel",
                ((int)Channel).ToString(CultureInfo.InvariantCulture));
            CriticalHitFingerprint.Append(
                builder,
                "run-id",
                RunCombatProfile == null ? string.Empty : RunCombatProfile.RunId);
            CriticalHitFingerprint.Append(
                builder,
                "run-context",
                RunCombatProfile == null
                    ? string.Empty
                    : RunCombatProfile.RunContextFingerprint);
            CriticalHitFingerprint.Append(
                builder,
                "run-profile",
                RunCombatProfile == null
                    ? string.Empty
                    : RunCombatProfile.Fingerprint);
            CriticalHitFingerprint.Append(
                builder,
                "effect-facts",
                EffectFacts == null ? string.Empty : EffectFacts.Fingerprint);
            CriticalHitFingerprint.AppendAcceptedHit(builder, AcceptedHit);
            return builder.ToString();
        }
    }

    public sealed class CriticalHitPolicyApplication
    {
        internal CriticalHitPolicyApplication(
            CriticalHitPolicyDefinition definition,
            RunCombatProfile profile)
        {
            PolicyId = definition.PolicyId;
            PolicyFingerprint = definition.Fingerprint;
            CanCrit = definition.CanCrit;
            ProfileCriticalChance = profile.CriticalChance;
            ProfileCriticalMultiplier = profile.CriticalMultiplier;
            EffectiveCriticalChance = definition.ResolveCriticalChance(
                profile.CriticalChance);
            EffectiveCriticalMultiplier = definition.ResolveCriticalMultiplier(
                profile.CriticalMultiplier);
            Fingerprint = CriticalHitFingerprint.Hash(ToCanonicalString());
        }

        public StableId PolicyId { get; }
        public string PolicyFingerprint { get; }
        public bool CanCrit { get; }
        public decimal ProfileCriticalChance { get; }
        public decimal ProfileCriticalMultiplier { get; }
        public decimal EffectiveCriticalChance { get; }
        public decimal EffectiveCriticalMultiplier { get; }
        public string Fingerprint { get; }

        public string ToCanonicalString()
        {
            var builder = new StringBuilder();
            CriticalHitFingerprint.Append(
                builder,
                "schema",
                "critical-hit-policy-application.v1");
            CriticalHitFingerprint.AppendId(builder, "policy", PolicyId);
            CriticalHitFingerprint.Append(
                builder,
                "policy-fingerprint",
                PolicyFingerprint);
            CriticalHitFingerprint.Append(
                builder,
                "can-crit",
                CanCrit ? "1" : "0");
            CriticalHitFingerprint.AppendDecimal(
                builder,
                "profile-chance",
                ProfileCriticalChance);
            CriticalHitFingerprint.AppendDecimal(
                builder,
                "profile-multiplier",
                ProfileCriticalMultiplier);
            CriticalHitFingerprint.AppendDecimal(
                builder,
                "effective-chance",
                EffectiveCriticalChance);
            CriticalHitFingerprint.AppendDecimal(
                builder,
                "effective-multiplier",
                EffectiveCriticalMultiplier);
            return builder.ToString();
        }
    }

    /// <summary>
    /// Exact immutable hash domain for one critical roll. The command fingerprint is
    /// included in addition to explicit identity fields so accepted-hit facts and history
    /// counters cannot be accidentally omitted from deterministic separation.
    /// </summary>
    public sealed class CriticalHitRollDomain
    {
        internal CriticalHitRollDomain(
            CriticalHitResolutionCommand command,
            CriticalHitPolicyApplication policyApplication)
        {
            CommandFingerprint = command.Fingerprint;
            PolicyApplicationFingerprint = policyApplication.Fingerprint;
            ShotSequence = command.ShotSequence;
            HitOrdinal = command.HitOrdinal;
            string canonical = BuildCanonicalString(command, policyApplication);
            byte[] digest = CriticalHitFingerprint.HashBytes(canonical);
            Fingerprint = CriticalHitFingerprint.ToHex(digest);
            RollSample = CriticalHitFingerprint.ToUnitInterval(digest);
        }

        public string CommandFingerprint { get; }
        public string PolicyApplicationFingerprint { get; }
        public long ShotSequence { get; }
        public int HitOrdinal { get; }
        public string Fingerprint { get; }
        public decimal RollSample { get; }

        private static string BuildCanonicalString(
            CriticalHitResolutionCommand command,
            CriticalHitPolicyApplication policyApplication)
        {
            CombatHitPolicyInput input = command.AcceptedHit.Input;
            CombatActorSnapshot source = input.SourceActor;
            CombatActorSnapshot target = input.Contact.TargetActor;
            CombatEffectSnapshot effect = input.Effect;

            var builder = new StringBuilder();
            CriticalHitFingerprint.Append(builder, "schema", "critical-hit-roll.v1");
            CriticalHitFingerprint.Append(
                builder,
                "command-fingerprint",
                command.Fingerprint);
            CriticalHitFingerprint.AppendId(
                builder,
                "operation",
                command.OperationId);
            CriticalHitFingerprint.Append(
                builder,
                "seed",
                command.DeterministicSeed);
            CriticalHitFingerprint.Append(
                builder,
                "shot-sequence",
                command.ShotSequence.ToString(CultureInfo.InvariantCulture));
            CriticalHitFingerprint.Append(
                builder,
                "hit-ordinal",
                command.HitOrdinal.ToString(CultureInfo.InvariantCulture));
            CriticalHitFingerprint.Append(
                builder,
                "run-id",
                command.RunCombatProfile.RunId);
            CriticalHitFingerprint.Append(
                builder,
                "run-context",
                command.RunCombatProfile.RunContextFingerprint);
            CriticalHitFingerprint.Append(
                builder,
                "run-profile",
                command.RunCombatProfile.Fingerprint);
            CriticalHitFingerprint.AppendId(
                builder,
                "equipment-instance",
                command.EffectFacts.EquipmentInstanceId);
            CriticalHitFingerprint.AppendId(
                builder,
                "effect-definition",
                command.EffectFacts.EffectDefinitionId);
            CriticalHitFingerprint.AppendId(
                builder,
                "critical-policy",
                command.EffectFacts.CriticalPolicyId);
            CriticalHitFingerprint.Append(
                builder,
                "critical-policy-application",
                policyApplication.Fingerprint);
            CriticalHitFingerprint.AppendId(
                builder,
                "source-actor",
                source.ActorId);
            CriticalHitFingerprint.Append(
                builder,
                "source-generation",
                source.LifecycleGeneration.ToString(CultureInfo.InvariantCulture));
            CriticalHitFingerprint.AppendId(
                builder,
                "source-participant",
                source.Identity.Ownership.RunParticipantId);
            CriticalHitFingerprint.AppendId(
                builder,
                "source-character",
                source.Identity.Ownership.SourceCharacterId);
            CriticalHitFingerprint.AppendId(
                builder,
                "source-faction",
                source.FactionId);
            CriticalHitFingerprint.AppendId(
                builder,
                "target-actor",
                target.ActorId);
            CriticalHitFingerprint.Append(
                builder,
                "target-generation",
                target.LifecycleGeneration.ToString(CultureInfo.InvariantCulture));
            CriticalHitFingerprint.AppendId(
                builder,
                "target-participant",
                target.Identity.Ownership.RunParticipantId);
            CriticalHitFingerprint.AppendId(
                builder,
                "target-character",
                target.Identity.Ownership.SourceCharacterId);
            CriticalHitFingerprint.AppendId(
                builder,
                "target-faction",
                target.FactionId);
            CriticalHitFingerprint.AppendId(
                builder,
                "effect-instance",
                effect.EffectId);
            CriticalHitFingerprint.AppendId(
                builder,
                "hit-policy",
                effect.PolicyId);
            CriticalHitFingerprint.Append(
                builder,
                "geometry",
                ((int)effect.GeometryKind).ToString(CultureInfo.InvariantCulture));
            CriticalHitFingerprint.AppendDecimal(
                builder,
                "base-damage",
                command.BaseDamage);
            CriticalHitFingerprint.Append(
                builder,
                "channel",
                ((int)command.Channel).ToString(CultureInfo.InvariantCulture));
            return builder.ToString();
        }
    }

    public sealed class CriticalHitResolvedDamage
    {
        internal CriticalHitResolvedDamage(
            CriticalHitResolutionCommand command,
            CriticalHitPolicyApplication policyApplication,
            CriticalHitRollDomain rollDomain,
            bool isCritical,
            decimal ordinaryDamage,
            decimal finalDamage)
        {
            CommandFingerprint = command.Fingerprint;
            RunId = command.RunCombatProfile.RunId;
            RunCombatProfileFingerprint = command.RunCombatProfile.Fingerprint;
            EffectFactsFingerprint = command.EffectFacts.Fingerprint;
            ShotSequence = command.ShotSequence;
            HitOrdinal = command.HitOrdinal;
            PolicyApplication = policyApplication;
            RollDomainFingerprint = rollDomain.Fingerprint;
            RollSample = rollDomain.RollSample;
            IsCritical = isCritical;
            BaseDamage = command.BaseDamage;
            OutgoingDamageMultiplier =
                command.RunCombatProfile.OutgoingDamageMultiplier;
            OrdinaryDamage = ordinaryDamage;
            FinalDamage = finalDamage;
            Fingerprint = CriticalHitFingerprint.Hash(ToCanonicalString());
        }

        public string CommandFingerprint { get; }
        public string RunId { get; }
        public string RunCombatProfileFingerprint { get; }
        public string EffectFactsFingerprint { get; }
        public long ShotSequence { get; }
        public int HitOrdinal { get; }
        public CriticalHitPolicyApplication PolicyApplication { get; }
        public string RollDomainFingerprint { get; }
        public decimal RollSample { get; }
        public bool IsCritical { get; }
        public decimal BaseDamage { get; }
        public decimal OutgoingDamageMultiplier { get; }
        public decimal CriticalChance
        {
            get { return PolicyApplication.EffectiveCriticalChance; }
        }
        public decimal CriticalMultiplier
        {
            get { return PolicyApplication.EffectiveCriticalMultiplier; }
        }
        public decimal OrdinaryDamage { get; }
        public decimal FinalDamage { get; }
        public string Fingerprint { get; }
        public bool HasPositiveDamage { get { return FinalDamage > 0m; } }

        public string ToCanonicalString()
        {
            var builder = new StringBuilder();
            CriticalHitFingerprint.Append(
                builder,
                "schema",
                "critical-hit-resolved-damage.v1");
            CriticalHitFingerprint.Append(builder, "command", CommandFingerprint);
            CriticalHitFingerprint.Append(builder, "run-id", RunId);
            CriticalHitFingerprint.Append(
                builder,
                "run-profile",
                RunCombatProfileFingerprint);
            CriticalHitFingerprint.Append(
                builder,
                "effect-facts",
                EffectFactsFingerprint);
            CriticalHitFingerprint.Append(
                builder,
                "shot-sequence",
                ShotSequence.ToString(CultureInfo.InvariantCulture));
            CriticalHitFingerprint.Append(
                builder,
                "hit-ordinal",
                HitOrdinal.ToString(CultureInfo.InvariantCulture));
            CriticalHitFingerprint.Append(
                builder,
                "policy-application",
                PolicyApplication.Fingerprint);
            CriticalHitFingerprint.Append(
                builder,
                "roll-domain",
                RollDomainFingerprint);
            CriticalHitFingerprint.AppendDecimal(
                builder,
                "roll-sample",
                RollSample);
            CriticalHitFingerprint.Append(
                builder,
                "critical",
                IsCritical ? "1" : "0");
            CriticalHitFingerprint.AppendDecimal(
                builder,
                "base-damage",
                BaseDamage);
            CriticalHitFingerprint.AppendDecimal(
                builder,
                "outgoing-multiplier",
                OutgoingDamageMultiplier);
            CriticalHitFingerprint.AppendDecimal(
                builder,
                "ordinary-damage",
                OrdinaryDamage);
            CriticalHitFingerprint.AppendDecimal(
                builder,
                "final-damage",
                FinalDamage);
            return builder.ToString();
        }
    }

    public sealed class CriticalHitResolutionResult
    {
        internal CriticalHitResolutionResult(
            CriticalHitResolutionStatus status,
            CriticalHitRejectionCode rejectionCode,
            CriticalHitResolutionCommand command,
            CriticalHitResolvedDamage resolvedDamage)
        {
            Status = status;
            RejectionCode = rejectionCode;
            Command = command;
            ResolvedDamage = resolvedDamage;
            Fingerprint = CriticalHitFingerprint.Hash(ToCanonicalString());
        }

        public CriticalHitResolutionStatus Status { get; }
        public CriticalHitRejectionCode RejectionCode { get; }
        public CriticalHitResolutionCommand Command { get; }
        public CriticalHitResolvedDamage ResolvedDamage { get; }
        public string Fingerprint { get; }
        public bool HasResolvedDamage { get { return ResolvedDamage != null; } }
        public bool IsReplay
        {
            get { return Status == CriticalHitResolutionStatus.Duplicate; }
        }
        public bool CanDispatchDamageCommand
        {
            get
            {
                return HasResolvedDamage
                    && ResolvedDamage.HasPositiveDamage
                    && (Status == CriticalHitResolutionStatus.Applied
                        || Status == CriticalHitResolutionStatus.Duplicate);
            }
        }

        private string ToCanonicalString()
        {
            var builder = new StringBuilder();
            CriticalHitFingerprint.Append(
                builder,
                "schema",
                "critical-hit-resolution-result.v1");
            CriticalHitFingerprint.Append(
                builder,
                "status",
                ((int)Status).ToString(CultureInfo.InvariantCulture));
            CriticalHitFingerprint.Append(
                builder,
                "rejection",
                ((int)RejectionCode).ToString(CultureInfo.InvariantCulture));
            CriticalHitFingerprint.Append(
                builder,
                "command",
                Command == null ? string.Empty : Command.Fingerprint);
            CriticalHitFingerprint.Append(
                builder,
                "resolved",
                ResolvedDamage == null
                    ? string.Empty
                    : ResolvedDamage.Fingerprint);
            return builder.ToString();
        }
    }

    public interface ICriticalHitResolutionState
    {
        CriticalHitResolutionResult Resolve(
            CriticalHitResolutionCommand command);
    }

    /// <summary>
    /// Run-local deterministic authority. It owns only operation replay state and
    /// immutable critical outcomes; health mutation remains downstream.
    /// </summary>
    public sealed class CriticalHitResolutionState :
        ICriticalHitResolutionState
    {
        private sealed class LedgerEntry
        {
            internal LedgerEntry(
                string commandFingerprint,
                CriticalHitResolvedDamage resolvedDamage)
            {
                CommandFingerprint = commandFingerprint;
                ResolvedDamage = resolvedDamage;
            }

            internal string CommandFingerprint { get; }
            internal CriticalHitResolvedDamage ResolvedDamage { get; }
        }

        private readonly object gate = new object();
        private readonly Dictionary<StableId, LedgerEntry> ledger =
            new Dictionary<StableId, LedgerEntry>();
        private readonly CriticalHitPolicyRegistry policyRegistry;
        private int appliedResolutionCount;

        public CriticalHitResolutionState()
            : this(CriticalHitPolicyRegistry.CreateDefault())
        {
        }

        public CriticalHitResolutionState(
            CriticalHitPolicyRegistry policyRegistry)
        {
            this.policyRegistry = policyRegistry
                ?? throw new ArgumentNullException(nameof(policyRegistry));
        }

        public int AppliedResolutionCount
        {
            get
            {
                lock (gate)
                {
                    return appliedResolutionCount;
                }
            }
        }

        public CriticalHitResolutionResult Resolve(
            CriticalHitResolutionCommand command)
        {
            lock (gate)
            {
                if (command == null)
                {
                    return Rejected(
                        null,
                        CriticalHitRejectionCode.MissingCommand);
                }

                LedgerEntry existing;
                if (command.OperationId != null
                    && ledger.TryGetValue(command.OperationId, out existing))
                {
                    if (string.Equals(
                        existing.CommandFingerprint,
                        command.Fingerprint,
                        StringComparison.Ordinal))
                    {
                        return new CriticalHitResolutionResult(
                            CriticalHitResolutionStatus.Duplicate,
                            CriticalHitRejectionCode.None,
                            command,
                            existing.ResolvedDamage);
                    }

                    return new CriticalHitResolutionResult(
                        CriticalHitResolutionStatus.ConflictingDuplicate,
                        CriticalHitRejectionCode.ConflictingDuplicate,
                        command,
                        null);
                }

                CriticalHitPolicyDefinition definition;
                CriticalHitRejectionCode rejection = Validate(
                    command,
                    out definition);
                if (rejection != CriticalHitRejectionCode.None)
                {
                    return Rejected(command, rejection);
                }

                CriticalHitPolicyApplication policyApplication;
                CriticalHitRollDomain domain;
                decimal ordinaryDamage;
                decimal finalDamage;
                bool isCritical;
                try
                {
                    policyApplication = new CriticalHitPolicyApplication(
                        definition,
                        command.RunCombatProfile);
                    domain = new CriticalHitRollDomain(
                        command,
                        policyApplication);
                    decimal chance = policyApplication.EffectiveCriticalChance;
                    isCritical = policyApplication.CanCrit
                        && (chance >= 1m
                            || (chance > 0m && domain.RollSample < chance));
                    ordinaryDamage = checked(
                        command.BaseDamage
                            * command.RunCombatProfile.OutgoingDamageMultiplier);
                    finalDamage = isCritical
                        ? checked(
                            ordinaryDamage
                                * policyApplication.EffectiveCriticalMultiplier)
                        : ordinaryDamage;
                }
                catch (OverflowException)
                {
                    return Rejected(
                        command,
                        CriticalHitRejectionCode.ResolvedDamageOverflow);
                }

                CriticalHitResolvedDamage resolved =
                    new CriticalHitResolvedDamage(
                        command,
                        policyApplication,
                        domain,
                        isCritical,
                        ordinaryDamage,
                        finalDamage);
                ledger.Add(
                    command.OperationId,
                    new LedgerEntry(command.Fingerprint, resolved));
                appliedResolutionCount++;

                return new CriticalHitResolutionResult(
                    CriticalHitResolutionStatus.Applied,
                    CriticalHitRejectionCode.None,
                    command,
                    resolved);
            }
        }

        private CriticalHitRejectionCode Validate(
            CriticalHitResolutionCommand command,
            out CriticalHitPolicyDefinition definition)
        {
            definition = null;
            if (command.OperationId == null)
            {
                return CriticalHitRejectionCode.MissingOperationId;
            }
            if (string.IsNullOrWhiteSpace(command.DeterministicSeed))
            {
                return CriticalHitRejectionCode.MissingDeterministicSeed;
            }
            if (command.ShotSequence < 0L)
            {
                return CriticalHitRejectionCode.InvalidShotSequence;
            }
            if (command.HitOrdinal < 0)
            {
                return CriticalHitRejectionCode.InvalidHitOrdinal;
            }
            if (command.BaseDamage <= 0m)
            {
                return CriticalHitRejectionCode.InvalidBaseDamage;
            }
            if (!Enum.IsDefined(typeof(CombatChannel), command.Channel)
                || command.Channel == CombatChannel.System)
            {
                return CriticalHitRejectionCode.InvalidDamageChannel;
            }
            if (command.RunCombatProfile == null)
            {
                return CriticalHitRejectionCode.MissingRunCombatProfile;
            }
            if (command.RunCombatProfile.CriticalChance < 0m
                || command.RunCombatProfile.CriticalChance > 1m
                || command.RunCombatProfile.CriticalMultiplier < 1m
                || command.RunCombatProfile.OutgoingDamageMultiplier < 0m
                || string.IsNullOrWhiteSpace(command.RunCombatProfile.RunId)
                || string.IsNullOrWhiteSpace(
                    command.RunCombatProfile.RunContextFingerprint)
                || string.IsNullOrWhiteSpace(
                    command.RunCombatProfile.Fingerprint))
            {
                return CriticalHitRejectionCode.InvalidRunCombatProfile;
            }
            if (command.EffectFacts == null)
            {
                return CriticalHitRejectionCode.MissingEffectFacts;
            }
            if (command.EffectFacts.EffectDefinitionId == null)
            {
                return CriticalHitRejectionCode.MissingEffectDefinitionId;
            }
            if (command.EffectFacts.CriticalPolicyId == null)
            {
                return CriticalHitRejectionCode.MissingCriticalPolicyId;
            }
            if (!policyRegistry.TryResolve(
                command.EffectFacts.CriticalPolicyId,
                out definition))
            {
                return CriticalHitRejectionCode.UnknownCriticalPolicy;
            }
            if (command.AcceptedHit == null
                || !command.AcceptedHit.DamageEligible)
            {
                return CriticalHitRejectionCode.HitNotDamageEligible;
            }
            if (!ValidAcceptedHitFacts(command.AcceptedHit))
            {
                return CriticalHitRejectionCode.InvalidAcceptedHitFacts;
            }

            return CriticalHitRejectionCode.None;
        }

        private static bool ValidAcceptedHitFacts(
            CombatHitPolicyResult acceptedHit)
        {
            CombatHitPolicyInput input = acceptedHit.Input;
            return input != null
                && input.SourceActor != null
                && input.SourceActor.Identity != null
                && input.SourceActor.ActorId != null
                && input.SourceActor.Identity.Ownership != null
                && input.Effect != null
                && input.Effect.EffectId != null
                && input.Effect.PolicyId != null
                && Enum.IsDefined(
                    typeof(CombatEffectGeometryKind),
                    input.Effect.GeometryKind)
                && input.Contact != null
                && input.Contact.Kind == CombatHitContactKind.Actor
                && input.Contact.TargetActor != null
                && input.Contact.TargetActor.Identity != null
                && input.Contact.TargetActor.ActorId != null
                && input.Contact.TargetActor.LifecycleGeneration >= 0L;
        }

        private static CriticalHitResolutionResult Rejected(
            CriticalHitResolutionCommand command,
            CriticalHitRejectionCode rejection)
        {
            return new CriticalHitResolutionResult(
                CriticalHitResolutionStatus.Rejected,
                rejection,
                command,
                null);
        }
    }

    public static class CriticalHitDamageCommandBridge
    {
        public static bool TryCreate(
            CriticalHitResolutionResult resolution,
            out DamageReceiverCommand command)
        {
            command = null;
            if (resolution == null
                || !resolution.CanDispatchDamageCommand
                || resolution.Command == null
                || resolution.Command.OperationId == null)
            {
                return false;
            }

            double amount = (double)resolution.ResolvedDamage.FinalDamage;
            if (double.IsNaN(amount)
                || double.IsInfinity(amount)
                || amount <= 0d)
            {
                return false;
            }

            return CombatHitDamageCommandBridge.TryCreate(
                resolution.Command.AcceptedHit,
                resolution.Command.OperationId,
                amount,
                resolution.Command.Channel,
                out command);
        }
    }

    internal static class CriticalHitFingerprint
    {
        internal static void Append(
            StringBuilder builder,
            string key,
            string value)
        {
            string safeKey = key ?? string.Empty;
            string safeValue = value ?? string.Empty;
            builder.Append(safeKey.Length.ToString(CultureInfo.InvariantCulture))
                .Append(':')
                .Append(safeKey)
                .Append('=')
                .Append(safeValue.Length.ToString(CultureInfo.InvariantCulture))
                .Append(':')
                .Append(safeValue)
                .Append(';');
        }

        internal static void AppendId(
            StringBuilder builder,
            string key,
            StableId value)
        {
            Append(builder, key, value == null ? string.Empty : value.ToString());
        }

        internal static void AppendDecimal(
            StringBuilder builder,
            string key,
            decimal value)
        {
            Append(
                builder,
                key,
                value.ToString("G29", CultureInfo.InvariantCulture));
        }

        internal static void AppendNullableDecimal(
            StringBuilder builder,
            string key,
            decimal? value)
        {
            Append(
                builder,
                key,
                value.HasValue
                    ? value.Value.ToString("G29", CultureInfo.InvariantCulture)
                    : string.Empty);
        }

        internal static void AppendAcceptedHit(
            StringBuilder builder,
            CombatHitPolicyResult acceptedHit)
        {
            if (acceptedHit == null)
            {
                Append(builder, "hit", string.Empty);
                return;
            }

            Append(
                builder,
                "hit-disposition",
                ((int)acceptedHit.Disposition).ToString(
                    CultureInfo.InvariantCulture));
            Append(
                builder,
                "hit-rejection",
                ((int)acceptedHit.RejectionCode).ToString(
                    CultureInfo.InvariantCulture));

            CombatHitPolicyInput input = acceptedHit.Input;
            if (input == null)
            {
                Append(builder, "hit-input", string.Empty);
                return;
            }

            CombatActorSnapshot source = input.SourceActor;
            AppendId(builder, "hit-source", source == null ? null : source.ActorId);
            Append(
                builder,
                "hit-source-generation",
                source == null
                    ? string.Empty
                    : source.LifecycleGeneration.ToString(
                        CultureInfo.InvariantCulture));
            AppendId(
                builder,
                "hit-source-participant",
                source == null
                    || source.Identity == null
                    || source.Identity.Ownership == null
                        ? null
                        : source.Identity.Ownership.RunParticipantId);
            AppendId(
                builder,
                "hit-source-character",
                source == null
                    || source.Identity == null
                    || source.Identity.Ownership == null
                        ? null
                        : source.Identity.Ownership.SourceCharacterId);
            AppendId(
                builder,
                "hit-source-faction",
                source == null ? null : source.FactionId);

            CombatEffectSnapshot effect = input.Effect;
            AppendId(builder, "hit-effect", effect == null ? null : effect.EffectId);
            AppendId(
                builder,
                "hit-policy",
                effect == null ? null : effect.PolicyId);
            Append(
                builder,
                "hit-geometry",
                effect == null
                    ? string.Empty
                    : ((int)effect.GeometryKind).ToString(
                        CultureInfo.InvariantCulture));

            CombatHitContact contact = input.Contact;
            Append(
                builder,
                "hit-contact-kind",
                contact == null
                    ? string.Empty
                    : ((int)contact.Kind).ToString(
                        CultureInfo.InvariantCulture));
            CombatActorSnapshot target = contact == null
                ? null
                : contact.TargetActor;
            AppendId(builder, "hit-target", target == null ? null : target.ActorId);
            Append(
                builder,
                "hit-target-generation",
                target == null
                    ? string.Empty
                    : target.LifecycleGeneration.ToString(
                        CultureInfo.InvariantCulture));
            AppendId(
                builder,
                "hit-target-participant",
                target == null
                    || target.Identity == null
                    || target.Identity.Ownership == null
                        ? null
                        : target.Identity.Ownership.RunParticipantId);
            AppendId(
                builder,
                "hit-target-character",
                target == null
                    || target.Identity == null
                    || target.Identity.Ownership == null
                        ? null
                        : target.Identity.Ownership.SourceCharacterId);
            AppendId(
                builder,
                "hit-target-faction",
                target == null ? null : target.FactionId);
            Append(
                builder,
                "hit-observed-target-generation",
                contact == null
                    ? string.Empty
                    : contact.ObservedTargetGeneration.ToString(
                        CultureInfo.InvariantCulture));
            Append(
                builder,
                "history-accepted-count",
                input.History == null
                    ? string.Empty
                    : input.History.AcceptedActorHitCount.ToString(
                        CultureInfo.InvariantCulture));
            Append(
                builder,
                "next-history-accepted-count",
                acceptedHit.NextHistory == null
                    ? string.Empty
                    : acceptedHit.NextHistory.AcceptedActorHitCount.ToString(
                        CultureInfo.InvariantCulture));
        }

        internal static string Hash(string value)
        {
            return ToHex(HashBytes(value));
        }

        internal static byte[] HashBytes(string value)
        {
            using (SHA256 sha = SHA256.Create())
            {
                return sha.ComputeHash(
                    Encoding.UTF8.GetBytes(value ?? string.Empty));
            }
        }

        internal static string ToHex(byte[] bytes)
        {
            return BitConverter.ToString(bytes)
                .Replace("-", string.Empty)
                .ToLowerInvariant();
        }

        internal static decimal ToUnitInterval(byte[] digest)
        {
            ulong value = 0UL;
            for (int index = 0; index < 8; index++)
            {
                value = (value << 8) | digest[index];
            }
            return value / 18446744073709551616m;
        }
    }
}
