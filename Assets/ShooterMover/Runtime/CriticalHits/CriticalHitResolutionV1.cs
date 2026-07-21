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
    public static class CriticalHitPolicyIdsV1
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
    public sealed class CriticalHitPolicyDefinitionV1
    {
        public CriticalHitPolicyDefinitionV1(
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
            Fingerprint = CriticalHitFingerprintV1.Hash(ToCanonicalString());
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
            CriticalHitFingerprintV1.Append(
                builder,
                "schema",
                "critical-hit-policy-definition.v1");
            CriticalHitFingerprintV1.AppendId(builder, "policy", PolicyId);
            CriticalHitFingerprintV1.Append(
                builder,
                "can-crit",
                CanCrit ? "1" : "0");
            CriticalHitFingerprintV1.AppendNullableDecimal(
                builder,
                "chance-override",
                CriticalChanceOverride);
            CriticalHitFingerprintV1.AppendDecimal(
                builder,
                "chance-flat",
                CriticalChanceFlatModifier);
            CriticalHitFingerprintV1.AppendDecimal(
                builder,
                "chance-multiplier",
                CriticalChanceMultiplier);
            CriticalHitFingerprintV1.AppendNullableDecimal(
                builder,
                "multiplier-override",
                CriticalMultiplierOverride);
            CriticalHitFingerprintV1.AppendDecimal(
                builder,
                "multiplier-flat",
                CriticalMultiplierFlatModifier);
            CriticalHitFingerprintV1.AppendDecimal(
                builder,
                "multiplier-multiplier",
                CriticalMultiplierMultiplier);
            return builder.ToString();
        }
    }

    public sealed class CriticalHitPolicyRegistryV1
    {
        private readonly IReadOnlyDictionary<StableId, CriticalHitPolicyDefinitionV1>
            definitionsById;

        public CriticalHitPolicyRegistryV1(
            IEnumerable<CriticalHitPolicyDefinitionV1> definitions)
        {
            List<CriticalHitPolicyDefinitionV1> items = (definitions
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

            Definitions = new ReadOnlyCollection<CriticalHitPolicyDefinitionV1>(
                items.OrderBy(item => item.PolicyId).ToList());
            definitionsById = new ReadOnlyDictionary<
                StableId,
                CriticalHitPolicyDefinitionV1>(
                    Definitions.ToDictionary(item => item.PolicyId));
            Fingerprint = CriticalHitFingerprintV1.Hash(
                string.Join(";", Definitions.Select(item => item.Fingerprint)));
        }

        public IReadOnlyList<CriticalHitPolicyDefinitionV1> Definitions { get; }
        public string Fingerprint { get; }

        public bool TryResolve(
            StableId policyId,
            out CriticalHitPolicyDefinitionV1 definition)
        {
            if (policyId == null)
            {
                definition = null;
                return false;
            }
            return definitionsById.TryGetValue(policyId, out definition);
        }

        public static CriticalHitPolicyRegistryV1 CreateDefault()
        {
            return new CriticalHitPolicyRegistryV1(
                new[]
                {
                    new CriticalHitPolicyDefinitionV1(
                        CriticalHitPolicyIdsV1.Normal,
                        true),
                    new CriticalHitPolicyDefinitionV1(
                        CriticalHitPolicyIdsV1.CannotCrit,
                        false),
                    new CriticalHitPolicyDefinitionV1(
                        CriticalHitPolicyIdsV1.Guaranteed,
                        true,
                        criticalChanceOverride: 1m),
                    new CriticalHitPolicyDefinitionV1(
                        CriticalHitPolicyIdsV1.ModifiedChance,
                        true,
                        criticalChanceMultiplier: 0.5m),
                    new CriticalHitPolicyDefinitionV1(
                        CriticalHitPolicyIdsV1.ModifiedMultiplier,
                        true,
                        criticalMultiplierMultiplier: 1.5m),
                });
        }
    }

    /// <summary>
    /// Immutable execution facts projected from the concrete weapon/attack/effect
    /// definition. EquipmentInstanceId is optional for non-equipment attacks.
    /// </summary>
    public sealed class CriticalHitEffectFactsV1
    {
        public CriticalHitEffectFactsV1(
            StableId effectDefinitionId,
            StableId criticalPolicyId,
            StableId equipmentInstanceId = null)
        {
            EffectDefinitionId = effectDefinitionId;
            CriticalPolicyId = criticalPolicyId;
            EquipmentInstanceId = equipmentInstanceId;
            Fingerprint = CriticalHitFingerprintV1.Hash(ToCanonicalString());
        }

        public StableId EffectDefinitionId { get; }
        public StableId CriticalPolicyId { get; }
        public StableId EquipmentInstanceId { get; }
        public bool HasEquipmentInstance { get { return EquipmentInstanceId != null; } }
        public string Fingerprint { get; }

        public string ToCanonicalString()
        {
            var builder = new StringBuilder();
            CriticalHitFingerprintV1.Append(
                builder,
                "schema",
                "critical-hit-effect-facts.v1");
            CriticalHitFingerprintV1.AppendId(
                builder,
                "effect-definition",
                EffectDefinitionId);
            CriticalHitFingerprintV1.AppendId(
                builder,
                "critical-policy",
                CriticalPolicyId);
            CriticalHitFingerprintV1.AppendId(
                builder,
                "equipment-instance",
                EquipmentInstanceId);
            return builder.ToString();
        }
    }

    public enum CriticalHitResolutionStatusV1
    {
        Applied = 1,
        Duplicate = 2,
        Rejected = 3,
        ConflictingDuplicate = 4,
    }

    public enum CriticalHitRejectionCodeV1
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
    public sealed class CriticalHitResolutionCommandV1
    {
        public CriticalHitResolutionCommandV1(
            StableId operationId,
            string deterministicSeed,
            long shotSequence,
            int hitOrdinal,
            decimal baseDamage,
            CombatChannel channel,
            RunCombatProfileV1 runCombatProfile,
            CriticalHitEffectFactsV1 effectFacts,
            CombatHitPolicyResultV1 acceptedHit)
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
            Fingerprint = CriticalHitFingerprintV1.Hash(ToCanonicalString());
        }

        /// <summary>
        /// Compatibility overload for callers that predate explicit hit ordinals.
        /// Such calls represent the first hit/contact in the shot.
        /// </summary>
        public CriticalHitResolutionCommandV1(
            StableId operationId,
            string deterministicSeed,
            long hitSequence,
            decimal baseDamage,
            CombatChannel channel,
            RunCombatProfileV1 runCombatProfile,
            CriticalHitEffectFactsV1 effectFacts,
            CombatHitPolicyResultV1 acceptedHit)
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
        public RunCombatProfileV1 RunCombatProfile { get; }
        public CriticalHitEffectFactsV1 EffectFacts { get; }
        public CombatHitPolicyResultV1 AcceptedHit { get; }
        public string Fingerprint { get; }

        public string ToCanonicalString()
        {
            var builder = new StringBuilder();
            CriticalHitFingerprintV1.Append(
                builder,
                "schema",
                "critical-hit-command.v1");
            CriticalHitFingerprintV1.AppendId(builder, "operation", OperationId);
            CriticalHitFingerprintV1.Append(
                builder,
                "seed",
                DeterministicSeed ?? string.Empty);
            CriticalHitFingerprintV1.Append(
                builder,
                "shot-sequence",
                ShotSequence.ToString(CultureInfo.InvariantCulture));
            CriticalHitFingerprintV1.Append(
                builder,
                "hit-ordinal",
                HitOrdinal.ToString(CultureInfo.InvariantCulture));
            CriticalHitFingerprintV1.AppendDecimal(builder, "base-damage", BaseDamage);
            CriticalHitFingerprintV1.Append(
                builder,
                "channel",
                ((int)Channel).ToString(CultureInfo.InvariantCulture));
            CriticalHitFingerprintV1.Append(
                builder,
                "run-id",
                RunCombatProfile == null ? string.Empty : RunCombatProfile.RunId);
            CriticalHitFingerprintV1.Append(
                builder,
                "run-context",
                RunCombatProfile == null
                    ? string.Empty
                    : RunCombatProfile.RunContextFingerprint);
            CriticalHitFingerprintV1.Append(
                builder,
                "run-profile",
                RunCombatProfile == null
                    ? string.Empty
                    : RunCombatProfile.Fingerprint);
            CriticalHitFingerprintV1.Append(
                builder,
                "effect-facts",
                EffectFacts == null ? string.Empty : EffectFacts.Fingerprint);
            CriticalHitFingerprintV1.AppendAcceptedHit(builder, AcceptedHit);
            return builder.ToString();
        }
    }

    public sealed class CriticalHitPolicyApplicationV1
    {
        internal CriticalHitPolicyApplicationV1(
            CriticalHitPolicyDefinitionV1 definition,
            RunCombatProfileV1 profile)
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
            Fingerprint = CriticalHitFingerprintV1.Hash(ToCanonicalString());
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
            CriticalHitFingerprintV1.Append(
                builder,
                "schema",
                "critical-hit-policy-application.v1");
            CriticalHitFingerprintV1.AppendId(builder, "policy", PolicyId);
            CriticalHitFingerprintV1.Append(
                builder,
                "policy-fingerprint",
                PolicyFingerprint);
            CriticalHitFingerprintV1.Append(
                builder,
                "can-crit",
                CanCrit ? "1" : "0");
            CriticalHitFingerprintV1.AppendDecimal(
                builder,
                "profile-chance",
                ProfileCriticalChance);
            CriticalHitFingerprintV1.AppendDecimal(
                builder,
                "profile-multiplier",
                ProfileCriticalMultiplier);
            CriticalHitFingerprintV1.AppendDecimal(
                builder,
                "effective-chance",
                EffectiveCriticalChance);
            CriticalHitFingerprintV1.AppendDecimal(
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
    public sealed class CriticalHitRollDomainV1
    {
        internal CriticalHitRollDomainV1(
            CriticalHitResolutionCommandV1 command,
            CriticalHitPolicyApplicationV1 policyApplication)
        {
            CommandFingerprint = command.Fingerprint;
            PolicyApplicationFingerprint = policyApplication.Fingerprint;
            ShotSequence = command.ShotSequence;
            HitOrdinal = command.HitOrdinal;
            string canonical = BuildCanonicalString(command, policyApplication);
            byte[] digest = CriticalHitFingerprintV1.HashBytes(canonical);
            Fingerprint = CriticalHitFingerprintV1.ToHex(digest);
            RollSample = CriticalHitFingerprintV1.ToUnitInterval(digest);
        }

        public string CommandFingerprint { get; }
        public string PolicyApplicationFingerprint { get; }
        public long ShotSequence { get; }
        public int HitOrdinal { get; }
        public string Fingerprint { get; }
        public decimal RollSample { get; }

        private static string BuildCanonicalString(
            CriticalHitResolutionCommandV1 command,
            CriticalHitPolicyApplicationV1 policyApplication)
        {
            CombatHitPolicyInputV1 input = command.AcceptedHit.Input;
            CombatActorSnapshotV1 source = input.SourceActor;
            CombatActorSnapshotV1 target = input.Contact.TargetActor;
            CombatEffectSnapshotV1 effect = input.Effect;

            var builder = new StringBuilder();
            CriticalHitFingerprintV1.Append(builder, "schema", "critical-hit-roll.v1");
            CriticalHitFingerprintV1.Append(
                builder,
                "command-fingerprint",
                command.Fingerprint);
            CriticalHitFingerprintV1.AppendId(
                builder,
                "operation",
                command.OperationId);
            CriticalHitFingerprintV1.Append(
                builder,
                "seed",
                command.DeterministicSeed);
            CriticalHitFingerprintV1.Append(
                builder,
                "shot-sequence",
                command.ShotSequence.ToString(CultureInfo.InvariantCulture));
            CriticalHitFingerprintV1.Append(
                builder,
                "hit-ordinal",
                command.HitOrdinal.ToString(CultureInfo.InvariantCulture));
            CriticalHitFingerprintV1.Append(
                builder,
                "run-id",
                command.RunCombatProfile.RunId);
            CriticalHitFingerprintV1.Append(
                builder,
                "run-context",
                command.RunCombatProfile.RunContextFingerprint);
            CriticalHitFingerprintV1.Append(
                builder,
                "run-profile",
                command.RunCombatProfile.Fingerprint);
            CriticalHitFingerprintV1.AppendId(
                builder,
                "equipment-instance",
                command.EffectFacts.EquipmentInstanceId);
            CriticalHitFingerprintV1.AppendId(
                builder,
                "effect-definition",
                command.EffectFacts.EffectDefinitionId);
            CriticalHitFingerprintV1.AppendId(
                builder,
                "critical-policy",
                command.EffectFacts.CriticalPolicyId);
            CriticalHitFingerprintV1.Append(
                builder,
                "critical-policy-application",
                policyApplication.Fingerprint);
            CriticalHitFingerprintV1.AppendId(
                builder,
                "source-actor",
                source.ActorId);
            CriticalHitFingerprintV1.Append(
                builder,
                "source-generation",
                source.LifecycleGeneration.ToString(CultureInfo.InvariantCulture));
            CriticalHitFingerprintV1.AppendId(
                builder,
                "source-participant",
                source.Identity.Ownership.RunParticipantId);
            CriticalHitFingerprintV1.AppendId(
                builder,
                "source-character",
                source.Identity.Ownership.SourceCharacterId);
            CriticalHitFingerprintV1.AppendId(
                builder,
                "source-faction",
                source.FactionId);
            CriticalHitFingerprintV1.AppendId(
                builder,
                "target-actor",
                target.ActorId);
            CriticalHitFingerprintV1.Append(
                builder,
                "target-generation",
                target.LifecycleGeneration.ToString(CultureInfo.InvariantCulture));
            CriticalHitFingerprintV1.AppendId(
                builder,
                "target-participant",
                target.Identity.Ownership.RunParticipantId);
            CriticalHitFingerprintV1.AppendId(
                builder,
                "target-character",
                target.Identity.Ownership.SourceCharacterId);
            CriticalHitFingerprintV1.AppendId(
                builder,
                "target-faction",
                target.FactionId);
            CriticalHitFingerprintV1.AppendId(
                builder,
                "effect-instance",
                effect.EffectId);
            CriticalHitFingerprintV1.AppendId(
                builder,
                "hit-policy",
                effect.PolicyId);
            CriticalHitFingerprintV1.Append(
                builder,
                "geometry",
                ((int)effect.GeometryKind).ToString(CultureInfo.InvariantCulture));
            CriticalHitFingerprintV1.AppendDecimal(
                builder,
                "base-damage",
                command.BaseDamage);
            CriticalHitFingerprintV1.Append(
                builder,
                "channel",
                ((int)command.Channel).ToString(CultureInfo.InvariantCulture));
            return builder.ToString();
        }
    }

    public sealed class CriticalHitResolvedDamageV1
    {
        internal CriticalHitResolvedDamageV1(
            CriticalHitResolutionCommandV1 command,
            CriticalHitPolicyApplicationV1 policyApplication,
            CriticalHitRollDomainV1 rollDomain,
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
            Fingerprint = CriticalHitFingerprintV1.Hash(ToCanonicalString());
        }

        public string CommandFingerprint { get; }
        public string RunId { get; }
        public string RunCombatProfileFingerprint { get; }
        public string EffectFactsFingerprint { get; }
        public long ShotSequence { get; }
        public int HitOrdinal { get; }
        public CriticalHitPolicyApplicationV1 PolicyApplication { get; }
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
            CriticalHitFingerprintV1.Append(
                builder,
                "schema",
                "critical-hit-resolved-damage.v1");
            CriticalHitFingerprintV1.Append(builder, "command", CommandFingerprint);
            CriticalHitFingerprintV1.Append(builder, "run-id", RunId);
            CriticalHitFingerprintV1.Append(
                builder,
                "run-profile",
                RunCombatProfileFingerprint);
            CriticalHitFingerprintV1.Append(
                builder,
                "effect-facts",
                EffectFactsFingerprint);
            CriticalHitFingerprintV1.Append(
                builder,
                "shot-sequence",
                ShotSequence.ToString(CultureInfo.InvariantCulture));
            CriticalHitFingerprintV1.Append(
                builder,
                "hit-ordinal",
                HitOrdinal.ToString(CultureInfo.InvariantCulture));
            CriticalHitFingerprintV1.Append(
                builder,
                "policy-application",
                PolicyApplication.Fingerprint);
            CriticalHitFingerprintV1.Append(
                builder,
                "roll-domain",
                RollDomainFingerprint);
            CriticalHitFingerprintV1.AppendDecimal(
                builder,
                "roll-sample",
                RollSample);
            CriticalHitFingerprintV1.Append(
                builder,
                "critical",
                IsCritical ? "1" : "0");
            CriticalHitFingerprintV1.AppendDecimal(
                builder,
                "base-damage",
                BaseDamage);
            CriticalHitFingerprintV1.AppendDecimal(
                builder,
                "outgoing-multiplier",
                OutgoingDamageMultiplier);
            CriticalHitFingerprintV1.AppendDecimal(
                builder,
                "ordinary-damage",
                OrdinaryDamage);
            CriticalHitFingerprintV1.AppendDecimal(
                builder,
                "final-damage",
                FinalDamage);
            return builder.ToString();
        }
    }

    public sealed class CriticalHitResolutionResultV1
    {
        internal CriticalHitResolutionResultV1(
            CriticalHitResolutionStatusV1 status,
            CriticalHitRejectionCodeV1 rejectionCode,
            CriticalHitResolutionCommandV1 command,
            CriticalHitResolvedDamageV1 resolvedDamage)
        {
            Status = status;
            RejectionCode = rejectionCode;
            Command = command;
            ResolvedDamage = resolvedDamage;
            Fingerprint = CriticalHitFingerprintV1.Hash(ToCanonicalString());
        }

        public CriticalHitResolutionStatusV1 Status { get; }
        public CriticalHitRejectionCodeV1 RejectionCode { get; }
        public CriticalHitResolutionCommandV1 Command { get; }
        public CriticalHitResolvedDamageV1 ResolvedDamage { get; }
        public string Fingerprint { get; }
        public bool HasResolvedDamage { get { return ResolvedDamage != null; } }
        public bool IsReplay
        {
            get { return Status == CriticalHitResolutionStatusV1.Duplicate; }
        }
        public bool CanDispatchDamageCommand
        {
            get
            {
                return HasResolvedDamage
                    && ResolvedDamage.HasPositiveDamage
                    && (Status == CriticalHitResolutionStatusV1.Applied
                        || Status == CriticalHitResolutionStatusV1.Duplicate);
            }
        }

        private string ToCanonicalString()
        {
            var builder = new StringBuilder();
            CriticalHitFingerprintV1.Append(
                builder,
                "schema",
                "critical-hit-resolution-result.v1");
            CriticalHitFingerprintV1.Append(
                builder,
                "status",
                ((int)Status).ToString(CultureInfo.InvariantCulture));
            CriticalHitFingerprintV1.Append(
                builder,
                "rejection",
                ((int)RejectionCode).ToString(CultureInfo.InvariantCulture));
            CriticalHitFingerprintV1.Append(
                builder,
                "command",
                Command == null ? string.Empty : Command.Fingerprint);
            CriticalHitFingerprintV1.Append(
                builder,
                "resolved",
                ResolvedDamage == null
                    ? string.Empty
                    : ResolvedDamage.Fingerprint);
            return builder.ToString();
        }
    }

    public interface ICriticalHitResolutionAuthorityV1
    {
        CriticalHitResolutionResultV1 Resolve(
            CriticalHitResolutionCommandV1 command);
    }

    /// <summary>
    /// Run-local deterministic authority. It owns only operation replay state and
    /// immutable critical outcomes; health mutation remains downstream.
    /// </summary>
    public sealed class CriticalHitResolutionAuthorityV1 :
        ICriticalHitResolutionAuthorityV1
    {
        private sealed class LedgerEntry
        {
            internal LedgerEntry(
                string commandFingerprint,
                CriticalHitResolvedDamageV1 resolvedDamage)
            {
                CommandFingerprint = commandFingerprint;
                ResolvedDamage = resolvedDamage;
            }

            internal string CommandFingerprint { get; }
            internal CriticalHitResolvedDamageV1 ResolvedDamage { get; }
        }

        private readonly object gate = new object();
        private readonly Dictionary<StableId, LedgerEntry> ledger =
            new Dictionary<StableId, LedgerEntry>();
        private readonly CriticalHitPolicyRegistryV1 policyRegistry;
        private int appliedResolutionCount;

        public CriticalHitResolutionAuthorityV1()
            : this(CriticalHitPolicyRegistryV1.CreateDefault())
        {
        }

        public CriticalHitResolutionAuthorityV1(
            CriticalHitPolicyRegistryV1 policyRegistry)
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

        public CriticalHitResolutionResultV1 Resolve(
            CriticalHitResolutionCommandV1 command)
        {
            lock (gate)
            {
                if (command == null)
                {
                    return Rejected(
                        null,
                        CriticalHitRejectionCodeV1.MissingCommand);
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
                        return new CriticalHitResolutionResultV1(
                            CriticalHitResolutionStatusV1.Duplicate,
                            CriticalHitRejectionCodeV1.None,
                            command,
                            existing.ResolvedDamage);
                    }

                    return new CriticalHitResolutionResultV1(
                        CriticalHitResolutionStatusV1.ConflictingDuplicate,
                        CriticalHitRejectionCodeV1.ConflictingDuplicate,
                        command,
                        null);
                }

                CriticalHitPolicyDefinitionV1 definition;
                CriticalHitRejectionCodeV1 rejection = Validate(
                    command,
                    out definition);
                if (rejection != CriticalHitRejectionCodeV1.None)
                {
                    return Rejected(command, rejection);
                }

                CriticalHitPolicyApplicationV1 policyApplication;
                CriticalHitRollDomainV1 domain;
                decimal ordinaryDamage;
                decimal finalDamage;
                bool isCritical;
                try
                {
                    policyApplication = new CriticalHitPolicyApplicationV1(
                        definition,
                        command.RunCombatProfile);
                    domain = new CriticalHitRollDomainV1(
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
                        CriticalHitRejectionCodeV1.ResolvedDamageOverflow);
                }

                CriticalHitResolvedDamageV1 resolved =
                    new CriticalHitResolvedDamageV1(
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

                return new CriticalHitResolutionResultV1(
                    CriticalHitResolutionStatusV1.Applied,
                    CriticalHitRejectionCodeV1.None,
                    command,
                    resolved);
            }
        }

        private CriticalHitRejectionCodeV1 Validate(
            CriticalHitResolutionCommandV1 command,
            out CriticalHitPolicyDefinitionV1 definition)
        {
            definition = null;
            if (command.OperationId == null)
            {
                return CriticalHitRejectionCodeV1.MissingOperationId;
            }
            if (string.IsNullOrWhiteSpace(command.DeterministicSeed))
            {
                return CriticalHitRejectionCodeV1.MissingDeterministicSeed;
            }
            if (command.ShotSequence < 0L)
            {
                return CriticalHitRejectionCodeV1.InvalidShotSequence;
            }
            if (command.HitOrdinal < 0)
            {
                return CriticalHitRejectionCodeV1.InvalidHitOrdinal;
            }
            if (command.BaseDamage <= 0m)
            {
                return CriticalHitRejectionCodeV1.InvalidBaseDamage;
            }
            if (!Enum.IsDefined(typeof(CombatChannel), command.Channel)
                || command.Channel == CombatChannel.System)
            {
                return CriticalHitRejectionCodeV1.InvalidDamageChannel;
            }
            if (command.RunCombatProfile == null)
            {
                return CriticalHitRejectionCodeV1.MissingRunCombatProfile;
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
                return CriticalHitRejectionCodeV1.InvalidRunCombatProfile;
            }
            if (command.EffectFacts == null)
            {
                return CriticalHitRejectionCodeV1.MissingEffectFacts;
            }
            if (command.EffectFacts.EffectDefinitionId == null)
            {
                return CriticalHitRejectionCodeV1.MissingEffectDefinitionId;
            }
            if (command.EffectFacts.CriticalPolicyId == null)
            {
                return CriticalHitRejectionCodeV1.MissingCriticalPolicyId;
            }
            if (!policyRegistry.TryResolve(
                command.EffectFacts.CriticalPolicyId,
                out definition))
            {
                return CriticalHitRejectionCodeV1.UnknownCriticalPolicy;
            }
            if (command.AcceptedHit == null
                || !command.AcceptedHit.DamageEligible)
            {
                return CriticalHitRejectionCodeV1.HitNotDamageEligible;
            }
            if (!ValidAcceptedHitFacts(command.AcceptedHit))
            {
                return CriticalHitRejectionCodeV1.InvalidAcceptedHitFacts;
            }

            return CriticalHitRejectionCodeV1.None;
        }

        private static bool ValidAcceptedHitFacts(
            CombatHitPolicyResultV1 acceptedHit)
        {
            CombatHitPolicyInputV1 input = acceptedHit.Input;
            return input != null
                && input.SourceActor != null
                && input.SourceActor.Identity != null
                && input.SourceActor.ActorId != null
                && input.SourceActor.Identity.Ownership != null
                && input.Effect != null
                && input.Effect.EffectId != null
                && input.Effect.PolicyId != null
                && Enum.IsDefined(
                    typeof(CombatEffectGeometryKindV1),
                    input.Effect.GeometryKind)
                && input.Contact != null
                && input.Contact.Kind == CombatHitContactKindV1.Actor
                && input.Contact.TargetActor != null
                && input.Contact.TargetActor.Identity != null
                && input.Contact.TargetActor.ActorId != null
                && input.Contact.TargetActor.LifecycleGeneration >= 0L;
        }

        private static CriticalHitResolutionResultV1 Rejected(
            CriticalHitResolutionCommandV1 command,
            CriticalHitRejectionCodeV1 rejection)
        {
            return new CriticalHitResolutionResultV1(
                CriticalHitResolutionStatusV1.Rejected,
                rejection,
                command,
                null);
        }
    }

    public static class CriticalHitDamageCommandAdapterV1
    {
        public static bool TryCreate(
            CriticalHitResolutionResultV1 resolution,
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

            return CombatHitDamageCommandAdapterV1.TryCreate(
                resolution.Command.AcceptedHit,
                resolution.Command.OperationId,
                amount,
                resolution.Command.Channel,
                out command);
        }
    }

    internal static class CriticalHitFingerprintV1
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
            CombatHitPolicyResultV1 acceptedHit)
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

            CombatHitPolicyInputV1 input = acceptedHit.Input;
            if (input == null)
            {
                Append(builder, "hit-input", string.Empty);
                return;
            }

            CombatActorSnapshotV1 source = input.SourceActor;
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

            CombatEffectSnapshotV1 effect = input.Effect;
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

            CombatHitContactV1 contact = input.Contact;
            Append(
                builder,
                "hit-contact-kind",
                contact == null
                    ? string.Empty
                    : ((int)contact.Kind).ToString(
                        CultureInfo.InvariantCulture));
            CombatActorSnapshotV1 target = contact == null
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
