using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using ShooterMover.Combat.HitPolicy;
using ShooterMover.Contracts.Combat;
using ShooterMover.Domain.Characters.Stats;
using ShooterMover.Domain.Common;
using ShooterMover.GameplayEntities;

namespace ShooterMover.Combat.CriticalHits
{
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
        InvalidHitSequence = 4,
        InvalidBaseDamage = 5,
        InvalidDamageChannel = 6,
        MissingRunCombatProfile = 7,
        InvalidRunCombatProfile = 8,
        HitNotDamageEligible = 9,
        InvalidAcceptedHitFacts = 10,
        ResolvedDamageOverflow = 11,
        ConflictingDuplicate = 12,
    }

    /// <summary>
    /// Immutable input to the critical-hit boundary. The accepted hit-policy result is
    /// deliberately retained so source, target, effect, geometry, and attribution facts
    /// cannot drift between hit eligibility and damage-command creation.
    /// </summary>
    public sealed class CriticalHitResolutionCommandV1
    {
        public CriticalHitResolutionCommandV1(
            StableId operationId,
            string deterministicSeed,
            long hitSequence,
            decimal baseDamage,
            CombatChannel channel,
            RunCombatProfileV1 runCombatProfile,
            CombatHitPolicyResultV1 acceptedHit)
        {
            OperationId = operationId;
            DeterministicSeed = deterministicSeed == null
                ? null
                : deterministicSeed.Trim();
            HitSequence = hitSequence;
            BaseDamage = baseDamage;
            Channel = channel;
            RunCombatProfile = runCombatProfile;
            AcceptedHit = acceptedHit;
            Fingerprint = CriticalHitFingerprintV1.Hash(ToCanonicalString());
        }

        public StableId OperationId { get; }

        public string DeterministicSeed { get; }

        public long HitSequence { get; }

        public decimal BaseDamage { get; }

        public CombatChannel Channel { get; }

        public RunCombatProfileV1 RunCombatProfile { get; }

        public CombatHitPolicyResultV1 AcceptedHit { get; }

        public string Fingerprint { get; }

        public string ToCanonicalString()
        {
            var builder = new StringBuilder();
            CriticalHitFingerprintV1.Append(builder, "schema", "critical-hit-command.v1");
            CriticalHitFingerprintV1.AppendId(builder, "operation", OperationId);
            CriticalHitFingerprintV1.Append(
                builder,
                "seed",
                DeterministicSeed ?? string.Empty);
            CriticalHitFingerprintV1.Append(
                builder,
                "hit-sequence",
                HitSequence.ToString(CultureInfo.InvariantCulture));
            CriticalHitFingerprintV1.AppendDecimal(builder, "base-damage", BaseDamage);
            CriticalHitFingerprintV1.Append(
                builder,
                "channel",
                ((int)Channel).ToString(CultureInfo.InvariantCulture));
            CriticalHitFingerprintV1.Append(
                builder,
                "run-profile",
                RunCombatProfile == null
                    ? string.Empty
                    : RunCombatProfile.Fingerprint);
            CriticalHitFingerprintV1.AppendAcceptedHit(builder, AcceptedHit);
            return builder.ToString();
        }
    }

    /// <summary>
    /// Exact immutable hash domain for one critical roll. The SHA-256 digest is both
    /// the domain fingerprint and the source of the unit-interval sample, so no mutable
    /// RNG state or frame timing can affect the result.
    /// </summary>
    public sealed class CriticalHitRollDomainV1
    {
        internal CriticalHitRollDomainV1(CriticalHitResolutionCommandV1 command)
        {
            CommandFingerprint = command.Fingerprint;
            string canonical = BuildCanonicalString(command);
            byte[] digest = CriticalHitFingerprintV1.HashBytes(canonical);
            Fingerprint = CriticalHitFingerprintV1.ToHex(digest);
            RollSample = CriticalHitFingerprintV1.ToUnitInterval(digest);
        }

        public string CommandFingerprint { get; }

        public string Fingerprint { get; }

        public decimal RollSample { get; }

        private static string BuildCanonicalString(
            CriticalHitResolutionCommandV1 command)
        {
            CombatHitPolicyInputV1 input = command.AcceptedHit.Input;
            CombatActorSnapshotV1 source = input.SourceActor;
            CombatActorSnapshotV1 target = input.Contact.TargetActor;
            CombatEffectSnapshotV1 effect = input.Effect;

            var builder = new StringBuilder();
            CriticalHitFingerprintV1.Append(builder, "schema", "critical-hit-roll.v1");
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
                "hit-sequence",
                command.HitSequence.ToString(CultureInfo.InvariantCulture));
            CriticalHitFingerprintV1.AppendId(
                builder,
                "source-actor",
                source.ActorId);
            CriticalHitFingerprintV1.Append(
                builder,
                "source-generation",
                source.LifecycleGeneration.ToString(
                    CultureInfo.InvariantCulture));
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
                target.LifecycleGeneration.ToString(
                    CultureInfo.InvariantCulture));
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
                "effect",
                effect.EffectId);
            CriticalHitFingerprintV1.AppendId(
                builder,
                "policy",
                effect.PolicyId);
            CriticalHitFingerprintV1.Append(
                builder,
                "geometry",
                ((int)effect.GeometryKind).ToString(
                    CultureInfo.InvariantCulture));
            CriticalHitFingerprintV1.Append(
                builder,
                "run-profile",
                command.RunCombatProfile.Fingerprint);
            CriticalHitFingerprintV1.AppendDecimal(
                builder,
                "base-damage",
                command.BaseDamage);
            CriticalHitFingerprintV1.Append(
                builder,
                "channel",
                ((int)command.Channel).ToString(
                    CultureInfo.InvariantCulture));
            return builder.ToString();
        }
    }

    public sealed class CriticalHitResolvedDamageV1
    {
        internal CriticalHitResolvedDamageV1(
            CriticalHitResolutionCommandV1 command,
            CriticalHitRollDomainV1 rollDomain,
            bool isCritical,
            decimal ordinaryDamage,
            decimal finalDamage)
        {
            CommandFingerprint = command.Fingerprint;
            RunCombatProfileFingerprint = command.RunCombatProfile.Fingerprint;
            RollDomainFingerprint = rollDomain.Fingerprint;
            RollSample = rollDomain.RollSample;
            IsCritical = isCritical;
            BaseDamage = command.BaseDamage;
            OutgoingDamageMultiplier =
                command.RunCombatProfile.OutgoingDamageMultiplier;
            CriticalChance = command.RunCombatProfile.CriticalChance;
            CriticalMultiplier = command.RunCombatProfile.CriticalMultiplier;
            OrdinaryDamage = ordinaryDamage;
            FinalDamage = finalDamage;
            Fingerprint = CriticalHitFingerprintV1.Hash(ToCanonicalString());
        }

        public string CommandFingerprint { get; }

        public string RunCombatProfileFingerprint { get; }

        public string RollDomainFingerprint { get; }

        public decimal RollSample { get; }

        public bool IsCritical { get; }

        public decimal BaseDamage { get; }

        public decimal OutgoingDamageMultiplier { get; }

        public decimal CriticalChance { get; }

        public decimal CriticalMultiplier { get; }

        public decimal OrdinaryDamage { get; }

        public decimal FinalDamage { get; }

        public string Fingerprint { get; }

        public bool HasPositiveDamage
        {
            get { return FinalDamage > 0m; }
        }

        public string ToCanonicalString()
        {
            var builder = new StringBuilder();
            CriticalHitFingerprintV1.Append(
                builder,
                "schema",
                "critical-hit-resolved-damage.v1");
            CriticalHitFingerprintV1.Append(
                builder,
                "command",
                CommandFingerprint);
            CriticalHitFingerprintV1.Append(
                builder,
                "run-profile",
                RunCombatProfileFingerprint);
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
                "critical-chance",
                CriticalChance);
            CriticalHitFingerprintV1.AppendDecimal(
                builder,
                "critical-multiplier",
                CriticalMultiplier);
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

        public bool HasResolvedDamage
        {
            get { return ResolvedDamage != null; }
        }

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
    /// immutable critical-hit outcomes; health mutation remains downstream.
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
        private int appliedResolutionCount;

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

                CriticalHitRejectionCodeV1 rejection = Validate(command);
                if (rejection != CriticalHitRejectionCodeV1.None)
                {
                    return Rejected(command, rejection);
                }

                CriticalHitRollDomainV1 domain =
                    new CriticalHitRollDomainV1(command);
                decimal chance = command.RunCombatProfile.CriticalChance;
                bool isCritical = chance >= 1m
                    || (chance > 0m && domain.RollSample < chance);

                decimal ordinaryDamage;
                decimal finalDamage;
                try
                {
                    ordinaryDamage = checked(
                        command.BaseDamage
                            * command.RunCombatProfile
                                .OutgoingDamageMultiplier);
                    finalDamage = isCritical
                        ? checked(
                            ordinaryDamage
                                * command.RunCombatProfile
                                    .CriticalMultiplier)
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

        private static CriticalHitRejectionCodeV1 Validate(
            CriticalHitResolutionCommandV1 command)
        {
            if (command.OperationId == null)
            {
                return CriticalHitRejectionCodeV1.MissingOperationId;
            }
            if (string.IsNullOrWhiteSpace(command.DeterministicSeed))
            {
                return CriticalHitRejectionCodeV1.MissingDeterministicSeed;
            }
            if (command.HitSequence < 0L)
            {
                return CriticalHitRejectionCodeV1.InvalidHitSequence;
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
                || string.IsNullOrWhiteSpace(
                    command.RunCombatProfile.Fingerprint))
            {
                return CriticalHitRejectionCodeV1.InvalidRunCombatProfile;
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

    /// <summary>
    /// Final projection into the existing health authority contract. The critical
    /// operation ID becomes the damage event ID, so exact re-dispatch is idempotent
    /// at the receiving health authority as well.
    /// </summary>
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
            AppendId(
                builder,
                "hit-source",
                source == null ? null : source.ActorId);
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
            AppendId(
                builder,
                "hit-effect",
                effect == null ? null : effect.EffectId);
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
            AppendId(
                builder,
                "hit-target",
                target == null ? null : target.ActorId);
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
            ulong numerator = 0UL;
            for (int index = 0; index < 6; index++)
            {
                numerator = (numerator << 8) | digest[index];
            }

            return numerator / 281474976710656m;
        }
    }
}
