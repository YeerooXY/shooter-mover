using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShooterMover.Contracts.Identity;
using ShooterMover.Contracts.Mission;
using ShooterMover.Domain.Common;

namespace ShooterMover.Tests.EditMode.Contracts
{
    public sealed class MissionEnvelopeTests
    {
        private const string DefinitionFingerprint =
            "sha256:8c1e3a5f7b9d0f2a4c6e8b1d3f5a7c9e0b2d4f6a8c1e3b5d7f9a0c2e4b6d8f1a";
        private const string OtherDefinitionFingerprint =
            "sha256:1d3f5a7c9e0b2d4f6a8c1e3b5d7f9a0c2e4b6d8f1a3c5e7b9d0f2a4c6e8b1d3f";

        [Test]
        public void MissionSequence_OrdersRelatesAndAdvancesDeterministically()
        {
            MissionSequence initial = MissionSequence.Initial;
            MissionSequence first = initial.Next();
            MissionSequence second = first.Next();

            Assert.That(initial.Value, Is.EqualTo(0L));
            Assert.That(first.Value, Is.EqualTo(1L));
            Assert.That(second.Value, Is.EqualTo(2L));
            Assert.That(initial < first, Is.True);
            Assert.That(second > first, Is.True);
            Assert.That(initial.RelateTo(first), Is.EqualTo(MissionSequenceRelation.Stale));
            Assert.That(first.RelateTo(first), Is.EqualTo(MissionSequenceRelation.Current));
            Assert.That(second.RelateTo(first), Is.EqualTo(MissionSequenceRelation.Future));
            Assert.Throws<ArgumentOutOfRangeException>(() => new MissionSequence(-1L));
        }

        [Test]
        public void PayloadVersion_BindsMissionSchemaToIdentityV1()
        {
            MissionPayloadVersion first = CreateVersion(1, DefinitionFingerprint);
            MissionPayloadVersion equal = CreateVersion(1, DefinitionFingerprint);
            MissionPayloadVersion changedSchema = CreateVersion(2, DefinitionFingerprint);
            MissionPayloadVersion changedContent = CreateVersion(1, OtherDefinitionFingerprint);

            Assert.That(first, Is.EqualTo(equal));
            Assert.That(first.GetHashCode(), Is.EqualTo(equal.GetHashCode()));
            Assert.That(first, Is.Not.EqualTo(changedSchema));
            Assert.That(first, Is.Not.EqualTo(changedContent));
            Assert.That(
                first.ToCanonicalString(),
                Is.EqualTo(
                    "mission_contract_version=1\n"
                    + "content_catalog_version=1\n"
                    + "content_definition_fingerprint="
                    + DefinitionFingerprint));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new MissionPayloadVersion(
                    0,
                    ContentVersion.Create(1, DefinitionFingerprint)));
            Assert.Throws<ArgumentNullException>(() => new MissionPayloadVersion(1, null));
        }

        [Test]
        public void CommandEnvelope_CarriesRequiredIdentityVersionSequenceAndTypedPayload()
        {
            MissionPayloadVersion version = CreateVersion();
            RoomClearRequest payload = new RoomClearRequest(
                Id("room.factory-receiving"),
                Id("encounter.receiving-wave"));
            MissionCommandEnvelope command = new MissionCommandEnvelope(
                Id("command.clear-room-0001"),
                Id("run.factory-run-0001"),
                version,
                new MissionSequence(4L),
                payload);

            Assert.That(command.CommandId, Is.EqualTo(Id("command.clear-room-0001")));
            Assert.That(command.RunId, Is.EqualTo(Id("run.factory-run-0001")));
            Assert.That(command.PayloadVersion, Is.SameAs(version));
            Assert.That(command.ExpectedSequence.Value, Is.EqualTo(4L));
            Assert.That(command.CommandType, Is.EqualTo(MissionCommandType.RoomClear));
            Assert.That(command.Payload, Is.SameAs(payload));
            Assert.That(command.ToCanonicalString(), Does.Contain("command_type=room-clear"));
        }

        [Test]
        public void RepresentativeMissionRequests_AreExplicitlyTypedWithoutDecidingState()
        {
            MissionCommandPayload room = new RoomClearRequest(
                Id("room.factory-receiving"),
                Id("encounter.receiving-wave"));
            MissionCommandPayload checkpoint = new CheckpointActivationRequest(
                Id("checkpoint.teleport-a"),
                Id("room.factory-teleport-a"));
            MissionCommandPayload banking = new RewardBankingRequest(
                Id("bank.secure-storage-a"));
            MissionCommandPayload completion = new MissionCompletionRequest(
                Id("mission.factory-shutdown"));

            Assert.That(room.CommandType, Is.EqualTo(MissionCommandType.RoomClear));
            Assert.That(
                checkpoint.CommandType,
                Is.EqualTo(MissionCommandType.CheckpointActivation));
            Assert.That(banking.CommandType, Is.EqualTo(MissionCommandType.RewardBanking));
            Assert.That(
                completion.CommandType,
                Is.EqualTo(MissionCommandType.MissionCompletion));
        }

        [Test]
        public void EventEnvelope_IsExplicitlyTypedAndRequiresCommittedSequence()
        {
            MissionEventEnvelope missionEvent = new MissionEventEnvelope(
                Id("event.room-cleared-0001"),
                Id("command.clear-room-0001"),
                Id("run.factory-run-0001"),
                CreateVersion(),
                new MissionSequence(5L),
                new RoomClearedEvent(
                    Id("room.factory-receiving"),
                    Id("encounter.receiving-wave")));

            Assert.That(missionEvent.EventType, Is.EqualTo(MissionEventType.RoomCleared));
            Assert.That(missionEvent.Sequence.Value, Is.EqualTo(5L));
            Assert.That(missionEvent.CommandId, Is.EqualTo(Id("command.clear-room-0001")));
            Assert.That(missionEvent.ToCanonicalString(), Does.Contain("event_type=room-cleared"));

            Assert.Throws<ArgumentOutOfRangeException>(
                () => new MissionEventEnvelope(
                    Id("event.room-cleared-0002"),
                    Id("command.clear-room-0002"),
                    Id("run.factory-run-0001"),
                    CreateVersion(),
                    MissionSequence.Initial,
                    new RoomClearedEvent(
                        Id("room.factory-receiving"),
                        Id("encounter.receiving-wave"))));
        }

        [Test]
        public void Gate_AcceptsKnownSupportedCommandAtCurrentSequence()
        {
            MissionCommandEnvelope command = CreateRoomClearCommand(
                "command.clear-room-0001",
                7L);

            MissionCommandEvaluation result = MissionCommandGate.Evaluate(
                command,
                new MissionSequence(7L),
                CreateVersion());

            Assert.That(result.IsAccepted, Is.True);
            Assert.That(result.Rejection, Is.Null);
            Assert.That(result.Command, Is.SameAs(command));
        }

        [Test]
        public void DuplicateRequest_IsRejectedBeforeItsNowStaleSequence()
        {
            MissionCommandEnvelope accepted = CreateRoomClearCommand(
                "command.clear-room-0001",
                4L);
            MissionCommandEnvelope retry = CreateRoomClearCommand(
                "command.clear-room-0001",
                4L);

            MissionCommandEvaluation result = MissionCommandGate.Evaluate(
                retry,
                new MissionSequence(5L),
                CreateVersion(),
                accepted);

            Assert.That(result.IsAccepted, Is.False);
            Assert.That(
                result.Rejection.RejectionType,
                Is.EqualTo(MissionRejectionType.DuplicateCommand));
            Assert.That(result.Rejection.ExpectedSequence.Value, Is.EqualTo(4L));
            Assert.That(result.Rejection.CurrentSequence.Value, Is.EqualTo(5L));
        }

        [Test]
        public void RetryAfterTimeout_ProducesTheSameDeterministicDuplicateRejection()
        {
            MissionCommandEnvelope accepted = CreateRoomClearCommand(
                "command.clear-room-0001",
                4L);
            MissionCommandEnvelope retry = CreateRoomClearCommand(
                "command.clear-room-0001",
                4L);

            MissionRejectionEnvelope first = MissionCommandGate.Evaluate(
                retry,
                new MissionSequence(5L),
                CreateVersion(),
                accepted).Rejection;
            MissionRejectionEnvelope second = MissionCommandGate.Evaluate(
                retry,
                new MissionSequence(5L),
                CreateVersion(),
                accepted).Rejection;

            Assert.That(first, Is.EqualTo(second));
            Assert.That(first.GetHashCode(), Is.EqualTo(second.GetHashCode()));
            Assert.That(first.ToCanonicalString(), Is.EqualTo(second.ToCanonicalString()));
        }

        [Test]
        public void ConflictingDuplicateCommandId_IsASeparateRejection()
        {
            MissionCommandEnvelope accepted = CreateRoomClearCommand(
                "command.clear-room-0001",
                4L);
            MissionCommandEnvelope conflicting = new MissionCommandEnvelope(
                accepted.CommandId,
                accepted.RunId,
                accepted.PayloadVersion,
                accepted.ExpectedSequence,
                new RoomClearRequest(
                    Id("room.factory-cargo-sort"),
                    Id("encounter.cargo-sort-wave")));

            MissionCommandEvaluation result = MissionCommandGate.Evaluate(
                conflicting,
                new MissionSequence(5L),
                CreateVersion(),
                accepted);

            Assert.That(
                result.Rejection.RejectionType,
                Is.EqualTo(MissionRejectionType.ConflictingDuplicateCommand));
        }

        [Test]
        public void StaleAndFutureSequences_AreDistinctAndOrdered()
        {
            MissionCommandEvaluation stale = MissionCommandGate.Evaluate(
                CreateRoomClearCommand("command.clear-room-0001", 3L),
                new MissionSequence(4L),
                CreateVersion());
            MissionCommandEvaluation future = MissionCommandGate.Evaluate(
                CreateRoomClearCommand("command.clear-room-0002", 5L),
                new MissionSequence(4L),
                CreateVersion());

            Assert.That(
                stale.Rejection.RejectionType,
                Is.EqualTo(MissionRejectionType.StaleSequence));
            Assert.That(
                future.Rejection.RejectionType,
                Is.EqualTo(MissionRejectionType.FutureSequence));
            Assert.That(stale.Rejection.ExpectedSequence < stale.Rejection.CurrentSequence, Is.True);
            Assert.That(future.Rejection.ExpectedSequence > future.Rejection.CurrentSequence, Is.True);
        }

        [Test]
        public void UnsupportedVersion_PrecedesUnknownTypeAndSequenceDeterministically()
        {
            MissionCommandEnvelope command = new MissionCommandEnvelope(
                Id("command.future-0001"),
                Id("run.factory-run-0001"),
                CreateVersion(2, DefinitionFingerprint),
                new MissionSequence(1L),
                new UnknownMissionCommandPayload(Id("type.future-command")));

            MissionCommandEvaluation result = MissionCommandGate.Evaluate(
                command,
                new MissionSequence(3L),
                CreateVersion());

            Assert.That(
                result.Rejection.RejectionType,
                Is.EqualTo(MissionRejectionType.UnsupportedPayloadVersion));
        }

        [Test]
        public void UnknownCommandType_IsRejectedAfterVersionPasses()
        {
            MissionCommandEnvelope command = new MissionCommandEnvelope(
                Id("command.future-0001"),
                Id("run.factory-run-0001"),
                CreateVersion(),
                new MissionSequence(3L),
                new UnknownMissionCommandPayload(Id("type.future-command")));

            Assert.That(
                MissionCommandGate.Evaluate(
                    command,
                    new MissionSequence(3L),
                    CreateVersion()).Rejection.RejectionType,
                Is.EqualTo(MissionRejectionType.UnknownCommandType));
        }

        [Test]
        public void RejectionEnvelope_UsesDeterministicCanonicalFieldOrder()
        {
            MissionCommandEnvelope command = CreateRoomClearCommand(
                "command.clear-room-0001",
                2L);
            MissionRejectionEnvelope rejection = MissionCommandGate.Evaluate(
                command,
                new MissionSequence(3L),
                CreateVersion()).Rejection;
            string[] lines = rejection.ToCanonicalString().Split('\n');

            Assert.That(lines[0], Is.EqualTo("command_id=command.clear-room-0001"));
            Assert.That(lines[1], Is.EqualTo("run_id=run.factory-run-0001"));
            Assert.That(lines[2], Is.EqualTo("mission_contract_version=1"));
            Assert.That(lines[3], Is.EqualTo("content_catalog_version=1"));
            Assert.That(
                lines[4],
                Is.EqualTo("content_definition_fingerprint=" + DefinitionFingerprint));
            Assert.That(lines[5], Is.EqualTo("expected_sequence=2"));
            Assert.That(lines[6], Is.EqualTo("current_sequence=3"));
            Assert.That(lines[7], Is.EqualTo("command_type=room-clear"));
            Assert.That(lines[8], Is.EqualTo("rejection_type=stale-sequence"));
        }

        [Test]
        public void EventSequences_OrderAcceptedFactsDeterministically()
        {
            MissionEventEnvelope first = CreateRoomClearedEvent(
                "event.room-cleared-0001",
                1L);
            MissionEventEnvelope second = CreateRoomClearedEvent(
                "event.room-cleared-0002",
                2L);
            MissionEventEnvelope[] reversed = { second, first };

            MissionEventEnvelope[] ordered = reversed
                .OrderBy(value => value.Sequence)
                .ToArray();

            Assert.That(ordered[0], Is.SameAs(first));
            Assert.That(ordered[1], Is.SameAs(second));
        }

        [Test]
        public void MissionEnvelopeTypes_AreImmutableClosedAndUnityFree()
        {
            Type[] immutableTypes =
            {
                typeof(MissionSequence),
                typeof(MissionPayloadVersion),
                typeof(MissionCommandEnvelope),
                typeof(MissionEventEnvelope),
                typeof(MissionRejectionEnvelope),
                typeof(MissionCommandEvaluation),
                typeof(UnknownMissionCommandPayload),
                typeof(RoomClearRequest),
                typeof(CheckpointActivationRequest),
                typeof(RewardBankingRequest),
                typeof(MissionCompletionRequest),
                typeof(RoomClearedEvent),
                typeof(CheckpointActivatedEvent),
                typeof(RewardsBankedEvent),
                typeof(MissionCompletedEvent),
            };

            foreach (Type type in immutableTypes)
            {
                Assert.That(type.IsSealed, Is.True, type.FullName + " must be sealed.");
                foreach (PropertyInfo property in type.GetProperties(
                    BindingFlags.Instance | BindingFlags.Public))
                {
                    Assert.That(
                        property.CanWrite,
                        Is.False,
                        type.FullName + "." + property.Name + " must not be settable.");
                }
            }

            Assert.That(typeof(MissionCommandPayload).IsAbstract, Is.True);
            Assert.That(typeof(MissionEventPayload).IsAbstract, Is.True);
            Assert.That(
                typeof(MissionCommandPayload).GetConstructors(
                    BindingFlags.Instance | BindingFlags.Public),
                Is.Empty);
            Assert.That(
                typeof(MissionEventPayload).GetConstructors(
                    BindingFlags.Instance | BindingFlags.Public),
                Is.Empty);
            Assert.That(
                typeof(MissionCommandEnvelope).Assembly.GetReferencedAssemblies()
                    .Any(name => name.Name.StartsWith("UnityEngine", StringComparison.Ordinal)),
                Is.False);
        }

        private static MissionCommandEnvelope CreateRoomClearCommand(
            string commandId,
            long expectedSequence)
        {
            return new MissionCommandEnvelope(
                Id(commandId),
                Id("run.factory-run-0001"),
                CreateVersion(),
                new MissionSequence(expectedSequence),
                new RoomClearRequest(
                    Id("room.factory-receiving"),
                    Id("encounter.receiving-wave")));
        }

        private static MissionEventEnvelope CreateRoomClearedEvent(
            string eventId,
            long sequence)
        {
            return new MissionEventEnvelope(
                Id(eventId),
                Id("command.clear-room-0001"),
                Id("run.factory-run-0001"),
                CreateVersion(),
                new MissionSequence(sequence),
                new RoomClearedEvent(
                    Id("room.factory-receiving"),
                    Id("encounter.receiving-wave")));
        }

        private static MissionPayloadVersion CreateVersion(
            int contractVersion = 1,
            string fingerprint = DefinitionFingerprint)
        {
            return new MissionPayloadVersion(
                contractVersion,
                ContentVersion.Create(1, fingerprint));
        }

        private static StableId Id(string text)
        {
            return StableId.Parse(text);
        }
    }
}
