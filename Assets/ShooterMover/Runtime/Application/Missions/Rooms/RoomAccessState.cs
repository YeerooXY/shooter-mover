using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using ShooterMover.Contracts.Missions.Rooms;
using ShooterMover.Domain.Common;

namespace ShooterMover.Application.Missions.Rooms
{
    /// <summary>
    /// Owns deterministic access evaluation and exactly-once consumptive unlocks for one run lifecycle.
    /// Room, objective, switch, drop, and holding truth remain behind narrow query/command ports.
    /// </summary>
    public sealed class RoomAccessState
    {
        private readonly RoomAccessDefinition definition;
        private readonly IRoomAccessFactPort factPort;
        private readonly IRoomRunHoldingPort holdingPort;
        private readonly HashSet<StableId> unlockedDoors = new HashSet<StableId>();
        private readonly HashSet<StableId> consumedHoldingFacts = new HashSet<StableId>();
        private readonly Dictionary<StableId, OperationRecord> operations =
            new Dictionary<StableId, OperationRecord>();
        private long sequence;

        public RoomAccessState(
            StableId runtimeInstanceStableId,
            long lifecycleGeneration,
            RoomAccessDefinition definition,
            IRoomAccessFactPort factPort,
            IRoomRunHoldingPort holdingPort)
        {
            RuntimeInstanceStableId = runtimeInstanceStableId
                ?? throw new ArgumentNullException(nameof(runtimeInstanceStableId));
            if (lifecycleGeneration <= 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(lifecycleGeneration));
            }

            LifecycleGeneration = lifecycleGeneration;
            this.definition = definition ?? throw new ArgumentNullException(nameof(definition));
            this.factPort = factPort ?? throw new ArgumentNullException(nameof(factPort));
            this.holdingPort = holdingPort ?? throw new ArgumentNullException(nameof(holdingPort));
        }

        public StableId RuntimeInstanceStableId { get; }

        public long LifecycleGeneration { get; }

        public RoomAccessSnapshot CurrentSnapshot
        {
            get { return BuildSnapshot(); }
        }

        public bool IsConditionSatisfied(StableId conditionStableId)
        {
            if (conditionStableId == null)
            {
                throw new ArgumentNullException(nameof(conditionStableId));
            }

            RoomAccessConditionDefinition condition;
            if (!definition.TryGetCondition(conditionStableId, out condition))
            {
                throw new KeyNotFoundException(
                    "Unknown room access condition: " + conditionStableId);
            }

            EvaluationContext context = CreateEvaluationContext();
            return Evaluate(condition, context);
        }

        public RoomAccessOperationResult TryUnlock(UnlockRoomDoorCommand command)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));

            string payload = BuildOperationPayload(command);
            OperationRecord existing;
            if (operations.TryGetValue(command.OperationStableId, out existing))
            {
                if (!string.Equals(existing.Payload, payload, StringComparison.Ordinal))
                {
                    return Rejected("room-access-operation-conflict");
                }

                if (existing.Status == RoomAccessOperationStatus.Rejected)
                {
                    return new RoomAccessOperationResult(
                        RoomAccessOperationStatus.Rejected,
                        existing.RejectionCode,
                        BuildSnapshot());
                }

                return new RoomAccessOperationResult(
                    RoomAccessOperationStatus.DuplicateNoChange,
                    string.Empty,
                    BuildSnapshot());
            }

            if (command.RuntimeInstanceStableId != RuntimeInstanceStableId)
            {
                return RecordRejected(command, payload, "room-access-runtime-mismatch");
            }
            if (command.LifecycleGeneration != LifecycleGeneration)
            {
                return RecordRejected(command, payload, "room-access-lifecycle-stale");
            }

            RoomDoorAccessDefinition door;
            if (!definition.TryGetDoor(command.DoorStableId, out door))
            {
                return RecordRejected(command, payload, "room-access-door-unknown");
            }

            if (unlockedDoors.Contains(door.DoorStableId))
            {
                operations.Add(
                    command.OperationStableId,
                    new OperationRecord(
                        payload,
                        RoomAccessOperationStatus.NoChange,
                        string.Empty));
                return new RoomAccessOperationResult(
                    RoomAccessOperationStatus.NoChange,
                    string.Empty,
                    BuildSnapshot());
            }

            EvaluationContext context = CreateEvaluationContext();
            RoomAccessConditionDefinition root;
            definition.TryGetCondition(door.RootConditionStableId, out root);
            if (!Evaluate(root, context))
            {
                return RecordRejected(command, payload, "room-access-condition-unsatisfied");
            }

            if (door.ConsumeHoldingStableId != null)
            {
                StableId consumeOperationStableId = CreateConsumeOperationStableId(
                    command.OperationStableId,
                    door.DoorStableId,
                    door.ConsumeHoldingStableId);
                RoomHoldingConsumeResult consumeResult = holdingPort.Consume(
                    new RoomHoldingConsumeCommand(
                        RuntimeInstanceStableId,
                        consumeOperationStableId,
                        door.ConsumeHoldingStableId,
                        1));
                if (consumeResult == null || !consumeResult.IsAccepted)
                {
                    string code = consumeResult == null
                        || string.IsNullOrWhiteSpace(consumeResult.RejectionCode)
                        ? "room-access-holding-consume-rejected"
                        : consumeResult.RejectionCode;
                    return RecordRejected(command, payload, code);
                }

                consumedHoldingFacts.Add(door.ConsumeHoldingStableId);
            }

            unlockedDoors.Add(door.DoorStableId);
            sequence++;
            operations.Add(
                command.OperationStableId,
                new OperationRecord(
                    payload,
                    RoomAccessOperationStatus.Applied,
                    string.Empty));
            return new RoomAccessOperationResult(
                RoomAccessOperationStatus.Applied,
                string.Empty,
                BuildSnapshot());
        }

        private RoomAccessOperationResult RecordRejected(
            UnlockRoomDoorCommand command,
            string payload,
            string rejectionCode)
        {
            operations.Add(
                command.OperationStableId,
                new OperationRecord(
                    payload,
                    RoomAccessOperationStatus.Rejected,
                    rejectionCode));
            return Rejected(rejectionCode);
        }

        private RoomAccessOperationResult Rejected(string rejectionCode)
        {
            return new RoomAccessOperationResult(
                RoomAccessOperationStatus.Rejected,
                rejectionCode,
                BuildSnapshot());
        }

        private RoomAccessSnapshot BuildSnapshot()
        {
            EvaluationContext context = CreateEvaluationContext();
            var doors = new List<RoomDoorAccessView>();
            for (int index = 0; index < definition.Doors.Count; index++)
            {
                RoomDoorAccessDefinition door = definition.Doors[index];
                RoomAccessConditionDefinition root;
                definition.TryGetCondition(door.RootConditionStableId, out root);
                bool satisfied = Evaluate(root, context);
                bool unlocked = unlockedDoors.Contains(door.DoorStableId);
                bool open = unlocked
                    || (door.ConsumeHoldingStableId == null && satisfied);
                doors.Add(new RoomDoorAccessView(
                    door.RoomStableId,
                    door.DoorStableId,
                    satisfied,
                    unlocked,
                    open));
            }

            return new RoomAccessSnapshot(
                RuntimeInstanceStableId,
                definition.Fingerprint,
                LifecycleGeneration,
                sequence,
                context.SourceFingerprint,
                doors);
        }

        private EvaluationContext CreateEvaluationContext()
        {
            RoomAccessFactSnapshot facts = factPort.CurrentSnapshot;
            RoomRunHoldingSnapshot holdings = holdingPort.CurrentSnapshot;
            if (facts == null)
            {
                throw new InvalidOperationException("room-access-fact-snapshot-missing");
            }
            if (holdings == null)
            {
                throw new InvalidOperationException("room-access-holding-snapshot-missing");
            }

            return new EvaluationContext(
                facts,
                holdings,
                consumedHoldingFacts,
                BuildSourceFingerprint(facts, holdings));
        }

        private bool Evaluate(
            RoomAccessConditionDefinition condition,
            EvaluationContext context)
        {
            bool cached;
            if (context.Results.TryGetValue(condition.ConditionStableId, out cached))
            {
                return cached;
            }

            bool result;
            switch (condition.Kind)
            {
                case RoomAccessConditionKind.Always:
                    result = true;
                    break;
                case RoomAccessConditionKind.RoomEntered:
                    result = context.Facts.Contains(
                        context.Facts.EnteredRooms,
                        condition.SubjectStableId);
                    break;
                case RoomAccessConditionKind.RoomComplete:
                    result = context.Facts.Contains(
                        context.Facts.CompletedRooms,
                        condition.SubjectStableId);
                    break;
                case RoomAccessConditionKind.ExactEntityTerminal:
                    result = context.Facts.Contains(
                        context.Facts.TerminalEntities,
                        condition.SubjectStableId);
                    break;
                case RoomAccessConditionKind.HoldingPresent:
                    result = context.Holdings.GetQuantity(condition.SubjectStableId) > 0;
                    break;
                case RoomAccessConditionKind.HoldingConsumed:
                    result = context.Facts.Contains(
                            context.Facts.ConsumedHoldings,
                            condition.SubjectStableId)
                        || context.ConsumedHoldingFacts.Contains(condition.SubjectStableId);
                    break;
                case RoomAccessConditionKind.CollectedDrop:
                    result = context.Facts.Contains(
                        context.Facts.CollectedDrops,
                        condition.SubjectStableId);
                    break;
                case RoomAccessConditionKind.ObjectiveComplete:
                    result = context.Facts.Contains(
                        context.Facts.CompletedObjectives,
                        condition.SubjectStableId);
                    break;
                case RoomAccessConditionKind.SwitchActive:
                    result = context.Facts.Contains(
                        context.Facts.ActiveSwitches,
                        condition.SubjectStableId);
                    break;
                case RoomAccessConditionKind.DifficultyAtLeast:
                    result = context.Facts.Difficulty >= condition.MinimumDifficulty;
                    break;
                case RoomAccessConditionKind.All:
                    result = EvaluateAll(condition, context);
                    break;
                case RoomAccessConditionKind.Any:
                    result = EvaluateAny(condition, context);
                    break;
                case RoomAccessConditionKind.Not:
                    result = !EvaluateChild(condition.ChildConditionStableIds[0], context);
                    break;
                default:
                    throw new InvalidOperationException(
                        "room-access-condition-kind-unsupported:" + condition.Kind);
            }

            context.Results.Add(condition.ConditionStableId, result);
            return result;
        }

        private bool EvaluateAll(
            RoomAccessConditionDefinition condition,
            EvaluationContext context)
        {
            for (int index = 0; index < condition.ChildConditionStableIds.Count; index++)
            {
                if (!EvaluateChild(condition.ChildConditionStableIds[index], context))
                {
                    return false;
                }
            }
            return true;
        }

        private bool EvaluateAny(
            RoomAccessConditionDefinition condition,
            EvaluationContext context)
        {
            for (int index = 0; index < condition.ChildConditionStableIds.Count; index++)
            {
                if (EvaluateChild(condition.ChildConditionStableIds[index], context))
                {
                    return true;
                }
            }
            return false;
        }

        private bool EvaluateChild(StableId conditionStableId, EvaluationContext context)
        {
            RoomAccessConditionDefinition child;
            if (!definition.TryGetCondition(conditionStableId, out child))
            {
                throw new InvalidOperationException(
                    "room-access-condition-reference-unknown:" + conditionStableId);
            }
            return Evaluate(child, context);
        }

        private static string BuildOperationPayload(UnlockRoomDoorCommand command)
        {
            return command.RuntimeInstanceStableId
                + "|"
                + command.LifecycleGeneration.ToString(CultureInfo.InvariantCulture)
                + "|"
                + command.DoorStableId;
        }

        private string BuildSourceFingerprint(
            RoomAccessFactSnapshot facts,
            RoomRunHoldingSnapshot holdings)
        {
            var builder = new StringBuilder();
            builder.Append(facts.Fingerprint).Append('|');
            var ordered = new List<StableId>(holdings.Quantities.Keys);
            ordered.Sort();
            for (int index = 0; index < ordered.Count; index++)
            {
                if (index != 0) builder.Append(',');
                StableId id = ordered[index];
                builder.Append(id)
                    .Append('=')
                    .Append(holdings.GetQuantity(id).ToString(CultureInfo.InvariantCulture));
            }
            builder.Append('|');
            var consumed = new List<StableId>(consumedHoldingFacts);
            consumed.Sort();
            for (int index = 0; index < consumed.Count; index++)
            {
                if (index != 0) builder.Append(',');
                builder.Append(consumed[index]);
            }
            return ComputeSha256(builder.ToString());
        }

        private static StableId CreateConsumeOperationStableId(
            StableId unlockOperationStableId,
            StableId doorStableId,
            StableId holdingStableId)
        {
            string source = unlockOperationStableId
                + "|"
                + doorStableId
                + "|"
                + holdingStableId;
            string hash = ComputeSha256(source).Substring(0, 32);
            return StableId.Create("room-access-consume", "op-" + hash);
        }

        private static string ComputeSha256(string value)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(value ?? string.Empty));
                var builder = new StringBuilder(hash.Length * 2);
                for (int index = 0; index < hash.Length; index++)
                {
                    builder.Append(hash[index].ToString("x2", CultureInfo.InvariantCulture));
                }
                return builder.ToString();
            }
        }

        private sealed class EvaluationContext
        {
            public EvaluationContext(
                RoomAccessFactSnapshot facts,
                RoomRunHoldingSnapshot holdings,
                HashSet<StableId> consumedHoldingFacts,
                string sourceFingerprint)
            {
                Facts = facts;
                Holdings = holdings;
                ConsumedHoldingFacts = consumedHoldingFacts;
                SourceFingerprint = sourceFingerprint ?? string.Empty;
                Results = new Dictionary<StableId, bool>();
            }

            public RoomAccessFactSnapshot Facts { get; }
            public RoomRunHoldingSnapshot Holdings { get; }
            public HashSet<StableId> ConsumedHoldingFacts { get; }
            public string SourceFingerprint { get; }
            public Dictionary<StableId, bool> Results { get; }
        }

        private sealed class OperationRecord
        {
            public OperationRecord(
                string payload,
                RoomAccessOperationStatus status,
                string rejectionCode)
            {
                Payload = payload ?? string.Empty;
                Status = status;
                RejectionCode = rejectionCode ?? string.Empty;
            }

            public string Payload { get; }
            public RoomAccessOperationStatus Status { get; }
            public string RejectionCode { get; }
        }
    }
}
