using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Modifiers;

namespace ShooterMover.Application.Runs.Session
{
    public enum RunConditionDeliveryStatusV1
    {
        Applied = 1,
        ExactReplay = 2,
        Rejected = 3,
        ConflictingDuplicate = 4,
        WrongRun = 5,
        StaleLifecycle = 6,
        RunEnded = 7,
    }

    public enum RunConditionAdvanceStatusV1
    {
        Applied = 1,
        ExactReplay = 2,
        Rejected = 3,
        ConflictingDuplicate = 4,
        WrongRun = 5,
        StaleLifecycle = 6,
        RunEnded = 7,
    }

    public sealed class RunConditionGameplayFactCommandV1
    {
        public RunConditionGameplayFactCommandV1(
            StableId operationStableId,
            object sourceFact,
            StableId runStableId,
            long runLifecycleGeneration,
            StableId sourceActorStableId,
            StableId subjectParticipantStableId,
            StableId sourceCharacterStableId,
            long sourceActorLifecycleGeneration,
            long authoritativeTick)
        {
            OperationStableId = operationStableId
                ?? throw new ArgumentNullException(nameof(operationStableId));
            SourceFact = sourceFact
                ?? throw new ArgumentNullException(nameof(sourceFact));
            RunStableId = runStableId
                ?? throw new ArgumentNullException(nameof(runStableId));
            if (runLifecycleGeneration <= 0L)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(runLifecycleGeneration));
            }
            SourceActorStableId = sourceActorStableId
                ?? throw new ArgumentNullException(nameof(sourceActorStableId));
            SubjectParticipantStableId = subjectParticipantStableId
                ?? throw new ArgumentNullException(
                    nameof(subjectParticipantStableId));
            SourceCharacterStableId = sourceCharacterStableId
                ?? throw new ArgumentNullException(
                    nameof(sourceCharacterStableId));
            if (sourceActorLifecycleGeneration <= 0L)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(sourceActorLifecycleGeneration));
            }
            if (authoritativeTick < 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(authoritativeTick));
            }

            RunLifecycleGeneration = runLifecycleGeneration;
            SourceActorLifecycleGeneration = sourceActorLifecycleGeneration;
            AuthoritativeTick = authoritativeTick;
            Fingerprint = RunConditionHashV1.Hash(ToCanonicalString());
        }

        public StableId OperationStableId { get; }
        public object SourceFact { get; }
        public StableId RunStableId { get; }
        public long RunLifecycleGeneration { get; }
        public StableId SourceActorStableId { get; }
        public StableId SubjectParticipantStableId { get; }
        public StableId SourceCharacterStableId { get; }
        public long SourceActorLifecycleGeneration { get; }
        public long AuthoritativeTick { get; }
        public string Fingerprint { get; }

        public string ToCanonicalString()
        {
            return OperationStableId + "|"
                + (SourceFact.GetType().FullName ?? SourceFact.GetType().Name)
                + "|" + RunStableId + "|"
                + RunLifecycleGeneration.ToString(CultureInfo.InvariantCulture)
                + "|" + SourceActorStableId + "|"
                + SubjectParticipantStableId + "|"
                + SourceCharacterStableId + "|"
                + SourceActorLifecycleGeneration.ToString(
                    CultureInfo.InvariantCulture)
                + "|" + AuthoritativeTick.ToString(CultureInfo.InvariantCulture);
        }
    }

    public sealed class RunConditionAdvanceCommandV1
    {
        public RunConditionAdvanceCommandV1(
            StableId operationStableId,
            StableId runStableId,
            long runLifecycleGeneration,
            long authoritativeTick)
        {
            OperationStableId = operationStableId
                ?? throw new ArgumentNullException(nameof(operationStableId));
            RunStableId = runStableId
                ?? throw new ArgumentNullException(nameof(runStableId));
            if (runLifecycleGeneration <= 0L)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(runLifecycleGeneration));
            }
            if (authoritativeTick < 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(authoritativeTick));
            }
            RunLifecycleGeneration = runLifecycleGeneration;
            AuthoritativeTick = authoritativeTick;
            Fingerprint = RunConditionHashV1.Hash(ToCanonicalString());
        }

        public StableId OperationStableId { get; }
        public StableId RunStableId { get; }
        public long RunLifecycleGeneration { get; }
        public long AuthoritativeTick { get; }
        public string Fingerprint { get; }

        public string ToCanonicalString()
        {
            return OperationStableId + "|" + RunStableId + "|"
                + RunLifecycleGeneration.ToString(CultureInfo.InvariantCulture)
                + "|" + AuthoritativeTick.ToString(CultureInfo.InvariantCulture);
        }
    }

    public sealed class RunConditionParticipantSnapshotV1
    {
        private readonly ReadOnlyCollection<string> activeConditionIds;

        public RunConditionParticipantSnapshotV1(
            StableId participantStableId,
            StableId characterStableId,
            StableId actorStableId,
            long actorLifecycleGeneration,
            long latestConditionTick,
            IEnumerable<string> activeConditionIds,
            int activeEffectCount,
            string statusEffectFingerprint,
            RuntimeModifierSnapshotV1 modifierProjection)
        {
            ParticipantStableId = participantStableId
                ?? throw new ArgumentNullException(nameof(participantStableId));
            CharacterStableId = characterStableId
                ?? throw new ArgumentNullException(nameof(characterStableId));
            ActorStableId = actorStableId
                ?? throw new ArgumentNullException(nameof(actorStableId));
            if (actorLifecycleGeneration <= 0L || latestConditionTick < 0L)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(actorLifecycleGeneration));
            }
            if (activeEffectCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(activeEffectCount));
            }
            if (string.IsNullOrWhiteSpace(statusEffectFingerprint))
            {
                throw new ArgumentException(
                    "A status-effect snapshot fingerprint is required.",
                    nameof(statusEffectFingerprint));
            }
            List<string> ids = (activeConditionIds ?? Array.Empty<string>())
                .Select(value => (value ?? string.Empty).Trim())
                .ToList();
            if (ids.Any(string.IsNullOrWhiteSpace)
                || ids.Distinct(StringComparer.Ordinal).Count() != ids.Count)
            {
                throw new ArgumentException(
                    "Active condition identities must be non-empty and unique.",
                    nameof(activeConditionIds));
            }

            ActorLifecycleGeneration = actorLifecycleGeneration;
            LatestConditionTick = latestConditionTick;
            ActiveEffectCount = activeEffectCount;
            StatusEffectFingerprint = statusEffectFingerprint.Trim();
            ModifierProjection = modifierProjection
                ?? throw new ArgumentNullException(nameof(modifierProjection));
            ids.Sort(StringComparer.Ordinal);
            this.activeConditionIds = new ReadOnlyCollection<string>(ids);
            Fingerprint = RunConditionHashV1.Hash(ToCanonicalString());
        }

        public StableId ParticipantStableId { get; }
        public StableId CharacterStableId { get; }
        public StableId ActorStableId { get; }
        public long ActorLifecycleGeneration { get; }
        public long LatestConditionTick { get; }
        public IReadOnlyList<string> ActiveConditionIds
        {
            get { return activeConditionIds; }
        }
        public int ActiveEffectCount { get; }
        public string StatusEffectFingerprint { get; }
        public RuntimeModifierSnapshotV1 ModifierProjection { get; }
        public string Fingerprint { get; }

        public string ToCanonicalString()
        {
            return ParticipantStableId + "|" + CharacterStableId + "|"
                + ActorStableId + "|"
                + ActorLifecycleGeneration.ToString(CultureInfo.InvariantCulture)
                + "|" + LatestConditionTick.ToString(CultureInfo.InvariantCulture)
                + "|" + string.Join(";", activeConditionIds) + "|"
                + ActiveEffectCount.ToString(CultureInfo.InvariantCulture) + "|"
                + StatusEffectFingerprint + "|" + ModifierProjection.Fingerprint;
        }
    }

    public sealed class RunConditionRuntimeSnapshotV1
    {
        private readonly ReadOnlyCollection<RunConditionParticipantSnapshotV1>
            participants;

        public RunConditionRuntimeSnapshotV1(
            StableId runStableId,
            long lifecycleGeneration,
            long authoritativeTick,
            string definitionFingerprint,
            IEnumerable<RunConditionParticipantSnapshotV1> participants,
            int acceptedFactCount)
        {
            RunStableId = runStableId
                ?? throw new ArgumentNullException(nameof(runStableId));
            if (lifecycleGeneration <= 0L || authoritativeTick < 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(lifecycleGeneration));
            }
            if (string.IsNullOrWhiteSpace(definitionFingerprint))
            {
                throw new ArgumentException(
                    "A condition definition fingerprint is required.",
                    nameof(definitionFingerprint));
            }
            if (acceptedFactCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(acceptedFactCount));
            }
            List<RunConditionParticipantSnapshotV1> items = (participants
                ?? throw new ArgumentNullException(nameof(participants))).ToList();
            if (items.Count < 1 || items.Any(item => item == null)
                || items.Select(item => item.ParticipantStableId)
                    .Distinct().Count() != items.Count)
            {
                throw new ArgumentException(
                    "At least one unique condition participant is required.",
                    nameof(participants));
            }
            items.Sort((left, right) => string.Compare(
                left.ParticipantStableId.ToString(),
                right.ParticipantStableId.ToString(),
                StringComparison.Ordinal));

            LifecycleGeneration = lifecycleGeneration;
            AuthoritativeTick = authoritativeTick;
            DefinitionFingerprint = definitionFingerprint.Trim();
            AcceptedFactCount = acceptedFactCount;
            this.participants =
                new ReadOnlyCollection<RunConditionParticipantSnapshotV1>(items);
            Fingerprint = RunConditionHashV1.Hash(ToCanonicalString());
        }

        public StableId RunStableId { get; }
        public long LifecycleGeneration { get; }
        public long AuthoritativeTick { get; }
        public string DefinitionFingerprint { get; }
        public IReadOnlyList<RunConditionParticipantSnapshotV1> Participants
        {
            get { return participants; }
        }
        public int AcceptedFactCount { get; }
        public string Fingerprint { get; }

        public string ToCanonicalString()
        {
            return RunStableId + "|"
                + LifecycleGeneration.ToString(CultureInfo.InvariantCulture)
                + "|" + AuthoritativeTick.ToString(CultureInfo.InvariantCulture)
                + "|" + DefinitionFingerprint + "|"
                + AcceptedFactCount.ToString(CultureInfo.InvariantCulture)
                + "|" + string.Join(";", participants.Select(item => item.Fingerprint));
        }
    }

    public sealed class RunConditionDeliveryResultV1
    {
        public RunConditionDeliveryResultV1(
            RunConditionDeliveryStatusV1 status,
            RunConditionGameplayFactCommandV1 command,
            string diagnosticCode,
            RunConditionRuntimeSnapshotV1 snapshot,
            string downstreamResultFingerprint)
        {
            if (!Enum.IsDefined(typeof(RunConditionDeliveryStatusV1), status))
            {
                throw new ArgumentOutOfRangeException(nameof(status));
            }
            Status = status;
            Command = command;
            DiagnosticCode = diagnosticCode ?? string.Empty;
            Snapshot = snapshot;
            DownstreamResultFingerprint = downstreamResultFingerprint
                ?? string.Empty;
            Fingerprint = RunConditionHashV1.Hash(
                ((int)Status).ToString(CultureInfo.InvariantCulture) + "|"
                + (Command == null ? string.Empty : Command.Fingerprint) + "|"
                + DiagnosticCode + "|"
                + (Snapshot == null ? string.Empty : Snapshot.Fingerprint) + "|"
                + DownstreamResultFingerprint);
        }

        public RunConditionDeliveryStatusV1 Status { get; }
        public RunConditionGameplayFactCommandV1 Command { get; }
        public string DiagnosticCode { get; }
        public RunConditionRuntimeSnapshotV1 Snapshot { get; }
        public string DownstreamResultFingerprint { get; }
        public string Fingerprint { get; }
        public bool Succeeded
        {
            get
            {
                return Status == RunConditionDeliveryStatusV1.Applied
                    || Status == RunConditionDeliveryStatusV1.ExactReplay;
            }
        }
    }

    public sealed class RunConditionAdvanceResultV1
    {
        public RunConditionAdvanceResultV1(
            RunConditionAdvanceStatusV1 status,
            RunConditionAdvanceCommandV1 command,
            string diagnosticCode,
            RunConditionRuntimeSnapshotV1 snapshot)
        {
            if (!Enum.IsDefined(typeof(RunConditionAdvanceStatusV1), status))
            {
                throw new ArgumentOutOfRangeException(nameof(status));
            }
            Status = status;
            Command = command;
            DiagnosticCode = diagnosticCode ?? string.Empty;
            Snapshot = snapshot;
            Fingerprint = RunConditionHashV1.Hash(
                ((int)Status).ToString(CultureInfo.InvariantCulture) + "|"
                + (Command == null ? string.Empty : Command.Fingerprint) + "|"
                + DiagnosticCode + "|"
                + (Snapshot == null ? string.Empty : Snapshot.Fingerprint));
        }

        public RunConditionAdvanceStatusV1 Status { get; }
        public RunConditionAdvanceCommandV1 Command { get; }
        public string DiagnosticCode { get; }
        public RunConditionRuntimeSnapshotV1 Snapshot { get; }
        public string Fingerprint { get; }
        public bool Succeeded
        {
            get
            {
                return Status == RunConditionAdvanceStatusV1.Applied
                    || Status == RunConditionAdvanceStatusV1.ExactReplay;
            }
        }
    }

    public interface IRunConditionRuntimePortV1 :
        IRunConditionalFactRuntimePortV1
    {
        void Bind(RunSessionAggregateV1 aggregate);
        RunConditionDeliveryResultV1 Deliver(
            RunConditionGameplayFactCommandV1 command);
        RunConditionAdvanceResultV1 Advance(
            RunConditionAdvanceCommandV1 command);
        RunConditionRuntimeSnapshotV1 ExportConditionSnapshot();
        RuntimeModifierSnapshotV1 ExportModifierProjection(
            StableId participantStableId);
    }

    internal static class RunConditionHashV1
    {
        internal static string Hash(string value)
        {
            using (SHA256 sha = SHA256.Create())
            {
                return BitConverter.ToString(
                        sha.ComputeHash(Encoding.UTF8.GetBytes(value ?? string.Empty)))
                    .Replace("-", string.Empty)
                    .ToLowerInvariant();
            }
        }
    }
}
