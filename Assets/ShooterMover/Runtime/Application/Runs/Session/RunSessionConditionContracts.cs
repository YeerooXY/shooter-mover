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
    public enum RunConditionDeliveryStatus
    {
        Applied = 1,
        ExactReplay = 2,
        Rejected = 3,
        ConflictingDuplicate = 4,
        WrongRun = 5,
        StaleLifecycle = 6,
        RunEnded = 7,
    }

    public enum RunConditionAdvanceStatus
    {
        Applied = 1,
        ExactReplay = 2,
        Rejected = 3,
        ConflictingDuplicate = 4,
        WrongRun = 5,
        StaleLifecycle = 6,
        RunEnded = 7,
    }

    public sealed class RunConditionGameplayFactCommand
    {
        public RunConditionGameplayFactCommand(
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
            Fingerprint = RunConditionHash.Hash(ToCanonicalString());
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

    public sealed class RunConditionAdvanceCommand
    {
        public RunConditionAdvanceCommand(
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
            Fingerprint = RunConditionHash.Hash(ToCanonicalString());
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

    public sealed class RunConditionParticipantSnapshot
    {
        private readonly ReadOnlyCollection<string> activeConditionIds;

        public RunConditionParticipantSnapshot(
            StableId participantStableId,
            StableId characterStableId,
            StableId actorStableId,
            long actorLifecycleGeneration,
            long latestConditionTick,
            IEnumerable<string> activeConditionIds,
            int activeEffectCount,
            string statusEffectFingerprint,
            LiveModifierSnapshot modifierProjection)
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
            Fingerprint = RunConditionHash.Hash(ToCanonicalString());
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
        public LiveModifierSnapshot ModifierProjection { get; }
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

    public sealed class RunConditionLiveSnapshot
    {
        private readonly ReadOnlyCollection<RunConditionParticipantSnapshot>
            participants;

        public RunConditionLiveSnapshot(
            StableId runStableId,
            long lifecycleGeneration,
            long authoritativeTick,
            string definitionFingerprint,
            IEnumerable<RunConditionParticipantSnapshot> participants,
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
            List<RunConditionParticipantSnapshot> items = (participants
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
                new ReadOnlyCollection<RunConditionParticipantSnapshot>(items);
            Fingerprint = RunConditionHash.Hash(ToCanonicalString());
        }

        public StableId RunStableId { get; }
        public long LifecycleGeneration { get; }
        public long AuthoritativeTick { get; }
        public string DefinitionFingerprint { get; }
        public IReadOnlyList<RunConditionParticipantSnapshot> Participants
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

    public sealed class RunConditionDeliveryResult
    {
        public RunConditionDeliveryResult(
            RunConditionDeliveryStatus status,
            RunConditionGameplayFactCommand command,
            string diagnosticCode,
            RunConditionLiveSnapshot snapshot,
            string downstreamResultFingerprint)
        {
            if (!Enum.IsDefined(typeof(RunConditionDeliveryStatus), status))
            {
                throw new ArgumentOutOfRangeException(nameof(status));
            }
            Status = status;
            Command = command;
            DiagnosticCode = diagnosticCode ?? string.Empty;
            Snapshot = snapshot;
            DownstreamResultFingerprint = downstreamResultFingerprint
                ?? string.Empty;
            Fingerprint = RunConditionHash.Hash(
                ((int)Status).ToString(CultureInfo.InvariantCulture) + "|"
                + (Command == null ? string.Empty : Command.Fingerprint) + "|"
                + DiagnosticCode + "|"
                + (Snapshot == null ? string.Empty : Snapshot.Fingerprint) + "|"
                + DownstreamResultFingerprint);
        }

        public RunConditionDeliveryStatus Status { get; }
        public RunConditionGameplayFactCommand Command { get; }
        public string DiagnosticCode { get; }
        public RunConditionLiveSnapshot Snapshot { get; }
        public string DownstreamResultFingerprint { get; }
        public string Fingerprint { get; }
        public bool Succeeded
        {
            get
            {
                return Status == RunConditionDeliveryStatus.Applied
                    || Status == RunConditionDeliveryStatus.ExactReplay;
            }
        }
    }

    public sealed class RunConditionAdvanceResult
    {
        public RunConditionAdvanceResult(
            RunConditionAdvanceStatus status,
            RunConditionAdvanceCommand command,
            string diagnosticCode,
            RunConditionLiveSnapshot snapshot)
        {
            if (!Enum.IsDefined(typeof(RunConditionAdvanceStatus), status))
            {
                throw new ArgumentOutOfRangeException(nameof(status));
            }
            Status = status;
            Command = command;
            DiagnosticCode = diagnosticCode ?? string.Empty;
            Snapshot = snapshot;
            Fingerprint = RunConditionHash.Hash(
                ((int)Status).ToString(CultureInfo.InvariantCulture) + "|"
                + (Command == null ? string.Empty : Command.Fingerprint) + "|"
                + DiagnosticCode + "|"
                + (Snapshot == null ? string.Empty : Snapshot.Fingerprint));
        }

        public RunConditionAdvanceStatus Status { get; }
        public RunConditionAdvanceCommand Command { get; }
        public string DiagnosticCode { get; }
        public RunConditionLiveSnapshot Snapshot { get; }
        public string Fingerprint { get; }
        public bool Succeeded
        {
            get
            {
                return Status == RunConditionAdvanceStatus.Applied
                    || Status == RunConditionAdvanceStatus.ExactReplay;
            }
        }
    }

    public interface IRunConditionLivePort :
        IRunConditionalFactLivePort
    {
        void Bind(RunSessionAggregate aggregate);
        RunConditionDeliveryResult Deliver(
            RunConditionGameplayFactCommand command);
        RunConditionAdvanceResult Advance(
            RunConditionAdvanceCommand command);
        RunConditionLiveSnapshot ExportConditionSnapshot();
        LiveModifierSnapshot ExportModifierProjection(
            StableId participantStableId);
    }

    internal static class RunConditionHash
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
