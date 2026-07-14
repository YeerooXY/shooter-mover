using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShooterMover.Contracts.Identity;
using ShooterMover.Contracts.Mission;
using ShooterMover.Contracts.Rooms;
using ShooterMover.Domain.Common;

namespace ShooterMover.Tests.EditMode.Contracts
{
    public sealed class RoomContractTests
    {
        private const string DefinitionFingerprint =
            "sha256:8c1e3a5f7b9d0f2a4c6e8b1d3f5a7c9e0b2d4f6a8c1e3b5d7f9a0c2e4b6d8f1a";

        [Test]
        public void IdentityAndProjectionKey_AreExplicitDeterministicAndRoomBound()
        {
            RoomProjectionIdentity identity = Identity(
                "room.factory-receiving",
                "projection.factory-receiving-a");
            RoomProjectionIdentity equal = Identity(
                "room.factory-receiving",
                "projection.factory-receiving-a");
            RoomProjectionKey key = Key("room.factory-receiving", 4L);

            Assert.That(identity, Is.EqualTo(equal));
            Assert.That(identity.GetHashCode(), Is.EqualTo(equal.GetHashCode()));
            Assert.That(
                identity.ToCanonicalString(),
                Is.EqualTo(
                    "room_id=room.factory-receiving\n"
                    + "projection_id=projection.factory-receiving-a"));
            Assert.That(key.RunId, Is.EqualTo(Id("run.factory-run-0001")));
            Assert.That(key.Sequence.Value, Is.EqualTo(4L));

            RoomProjectionLifecycle lifecycle = RoomProjectionLifecycle.Create(identity);
            Assert.Throws<ArgumentException>(
                () => lifecycle.Load(Key("room.factory-cargo-sort", 4L)));
        }

        [Test]
        public void Connection_RequiresCompatibleSocketsAndIsOrderIndependent()
        {
            RoomProjectionIdentity firstRoom = Identity(
                "room.factory-receiving",
                "projection.factory-receiving-a");
            RoomProjectionIdentity secondRoom = Identity(
                "room.factory-cargo-sort",
                "projection.factory-cargo-sort-a");
            RoomSocket exit = new RoomSocket(
                firstRoom,
                Id("socket.receiving-east"),
                RoomSocketDirection.Outbound);
            RoomSocket entrance = new RoomSocket(
                secondRoom,
                Id("socket.cargo-west"),
                RoomSocketDirection.Inbound);

            RoomConnection forward = new RoomConnection(exit, entrance);
            RoomConnection reverse = new RoomConnection(entrance, exit);

            Assert.That(exit.CanConnectTo(entrance), Is.True);
            Assert.That(forward, Is.EqualTo(reverse));
            Assert.That(forward.GetOther(firstRoom), Is.EqualTo(secondRoom));
            Assert.That(forward.GetOther(secondRoom), Is.EqualTo(firstRoom));
            Assert.That(forward.Connects(firstRoom), Is.True);
            Assert.Throws<ArgumentException>(
                () => forward.GetOther(
                    Identity("room.factory-forge", "projection.factory-forge-a")));
            Assert.Throws<ArgumentException>(
                () => new RoomConnection(
                    exit,
                    new RoomSocket(
                        secondRoom,
                        Id("socket.cargo-east"),
                        RoomSocketDirection.Outbound)));
            Assert.Throws<ArgumentException>(
                () => new RoomConnection(
                    exit,
                    new RoomSocket(
                        firstRoom,
                        Id("socket.receiving-west"),
                        RoomSocketDirection.Inbound)));
        }

        [Test]
        public void Lifecycle_RepeatedLoadAndRefreshAreIdempotentAndStaleRefreshIsRejected()
        {
            RoomProjectionLifecycle unloaded = RoomProjectionLifecycle.Create(
                Identity("room.factory-receiving", "projection.factory-receiving-a"));
            RoomProjectionKey initialKey = Key("room.factory-receiving", 4L);
            RoomProjectionKey refreshedKey = Key("room.factory-receiving", 5L);

            RoomProjectionTransition load = unloaded.Load(initialKey);
            Assert.That(load.Kind, Is.EqualTo(RoomProjectionTransitionKind.Applied));
            RoomProjectionLifecycle loaded = load.Next;

            RoomProjectionTransition repeatedLoad = loaded.Load(initialKey);
            Assert.That(repeatedLoad.Kind, Is.EqualTo(RoomProjectionTransitionKind.NoChange));
            Assert.That(repeatedLoad.Next, Is.SameAs(loaded));

            RoomProjectionTransition refresh = loaded.Refresh(refreshedKey);
            Assert.That(refresh.Kind, Is.EqualTo(RoomProjectionTransitionKind.Applied));
            RoomProjectionLifecycle refreshed = refresh.Next;
            Assert.That(refreshed.ActiveKey, Is.EqualTo(refreshedKey));

            RoomProjectionTransition repeatedRefresh = refreshed.Refresh(refreshedKey);
            Assert.That(
                repeatedRefresh.Kind,
                Is.EqualTo(RoomProjectionTransitionKind.NoChange));
            Assert.That(repeatedRefresh.Next, Is.SameAs(refreshed));

            RoomProjectionTransition staleRefresh = refreshed.Refresh(initialKey);
            Assert.That(
                staleRefresh.Rejection,
                Is.EqualTo(RoomProjectionTransitionRejection.StaleProjectionKey));
            Assert.That(staleRefresh.Next, Is.SameAs(refreshed));
        }

        [Test]
        public void Lifecycle_ReloadAfterCompletedUnloadRestoresProjectionWithoutDurableState()
        {
            RoomProjectionLifecycle loaded = RoomProjectionLifecycle.Create(
                    Identity("room.factory-receiving", "projection.factory-receiving-a"))
                .Load(Key("room.factory-receiving", 7L))
                .Next;
            RoomProjectionLifecycle unloading = loaded.BeginUnload().Next;
            RoomProjectionLifecycle unloaded = unloading.CompleteUnload().Next;

            Assert.That(unloaded.Phase, Is.EqualTo(RoomProjectionLifecyclePhase.Unloaded));
            Assert.That(unloaded.ActiveKey, Is.Null);

            RoomProjectionKey reloadKey = Key("room.factory-receiving", 7L);
            RoomProjectionTransition reload = unloaded.Reload(reloadKey);
            Assert.That(reload.Kind, Is.EqualTo(RoomProjectionTransitionKind.Applied));
            Assert.That(reload.Next.Phase, Is.EqualTo(RoomProjectionLifecyclePhase.Loaded));
            Assert.That(reload.Next.ActiveKey, Is.EqualTo(reloadKey));

            RoomProjectionTransition repeatedReload = reload.Next.Reload(reloadKey);
            Assert.That(
                repeatedReload.Kind,
                Is.EqualTo(RoomProjectionTransitionKind.NoChange));
        }

        [Test]
        public void Lifecycle_InterruptedUnloadCanResumeIdempotently()
        {
            RoomProjectionLifecycle loaded = RoomProjectionLifecycle.Create(
                    Identity("room.factory-receiving", "projection.factory-receiving-a"))
                .Load(Key("room.factory-receiving", 9L))
                .Next;
            RoomProjectionLifecycle unloading = loaded.BeginUnload().Next;

            Assert.That(unloading.Phase, Is.EqualTo(RoomProjectionLifecyclePhase.Unloading));
            RoomProjectionTransition resumed = unloading.ResumeAfterInterruptedUnload();
            Assert.That(resumed.Kind, Is.EqualTo(RoomProjectionTransitionKind.Applied));
            Assert.That(resumed.Next.Phase, Is.EqualTo(RoomProjectionLifecyclePhase.Loaded));
            Assert.That(resumed.Next.ActiveKey, Is.EqualTo(loaded.ActiveKey));

            RoomProjectionTransition repeated = resumed.Next.ResumeAfterInterruptedUnload();
            Assert.That(repeated.Kind, Is.EqualTo(RoomProjectionTransitionKind.NoChange));
            Assert.That(repeated.Next, Is.SameAs(resumed.Next));

            RoomProjectionTransition invalidCompletion = resumed.Next.CompleteUnload();
            Assert.That(
                invalidCompletion.Rejection,
                Is.EqualTo(RoomProjectionTransitionRejection.InvalidTransition));
        }

        [Test]
        public void ProjectionReader_RepresentsUnknownKeysExplicitly()
        {
            RoomProjectionKey knownKey = Key("room.factory-receiving", 3L);
            FakeProjectionReader reader = new FakeProjectionReader(
                knownKey,
                new TestProjection("receiving-ready"));

            RoomProjectionReadResult<TestProjection> known =
                reader.Read<TestProjection>(knownKey);
            RoomProjectionReadResult<TestProjection> unknown =
                reader.Read<TestProjection>(Key("room.factory-receiving", 4L));

            Assert.That(known.Status, Is.EqualTo(RoomProjectionReadStatus.Found));
            Assert.That(known.HasValue, Is.True);
            Assert.That(known.Value.Name, Is.EqualTo("receiving-ready"));
            Assert.That(
                unknown.Status,
                Is.EqualTo(RoomProjectionReadStatus.UnknownKey));
            Assert.That(unknown.HasValue, Is.False);
            Assert.That(unknown.Value, Is.Null);
        }

        [Test]
        public void Services_ReadProjectionAndSubmitMissionMessageWithoutDirectStateMutation()
        {
            RoomProjectionKey key = Key("room.factory-receiving", 3L);
            FakeProjectionReader reader = new FakeProjectionReader(
                key,
                new TestProjection("receiving-ready"));
            MissionPayloadVersion version = CreateVersion();
            FakeMissionCommandSubmitter submitter = new FakeMissionCommandSubmitter(
                new MissionSequence(3L),
                version);
            RoomProjectionServices services = new RoomProjectionServices(reader, submitter);
            MissionCommandEnvelope command = new MissionCommandEnvelope(
                Id("command.clear-room-0001"),
                Id("run.factory-run-0001"),
                version,
                new MissionSequence(3L),
                new RoomClearRequest(
                    Id("room.factory-receiving"),
                    Id("encounter.receiving-wave")));

            RoomProjectionReadResult<TestProjection> projection =
                services.StateReader.Read<TestProjection>(key);
            MissionCommandEvaluation evaluation = services.MissionCommands.Submit(command);

            Assert.That(projection.HasValue, Is.True);
            Assert.That(evaluation.IsAccepted, Is.True);
            Assert.That(submitter.LastCommand, Is.SameAs(command));
            Assert.That(command.CommandType, Is.EqualTo(MissionCommandType.RoomClear));
        }

        [Test]
        public void TwoAdditiveRooms_MaintainIndependentProjectionLifecycles()
        {
            RoomProjectionLifecycle first = RoomProjectionLifecycle.Create(
                    Identity("room.factory-receiving", "projection.factory-receiving-a"))
                .Load(Key("room.factory-receiving", 2L))
                .Next;
            RoomProjectionLifecycle second = RoomProjectionLifecycle.Create(
                    Identity("room.factory-cargo-sort", "projection.factory-cargo-sort-a"))
                .Load(Key("room.factory-cargo-sort", 2L))
                .Next;

            RoomProjectionLifecycle firstRefreshed = first
                .Refresh(Key("room.factory-receiving", 3L))
                .Next;

            Assert.That(firstRefreshed.Identity.RoomId, Is.Not.EqualTo(second.Identity.RoomId));
            Assert.That(firstRefreshed.ActiveKey.Sequence.Value, Is.EqualTo(3L));
            Assert.That(second.ActiveKey.Sequence.Value, Is.EqualTo(2L));
            Assert.That(second.Phase, Is.EqualTo(RoomProjectionLifecyclePhase.Loaded));
        }

        [Test]
        public void RoomProjectionContracts_AreImmutableUnityFreeAndExposeNoTruthSetters()
        {
            Type[] immutableTypes =
            {
                typeof(RoomProjectionIdentity),
                typeof(RoomProjectionKey),
                typeof(RoomSocket),
                typeof(RoomConnection),
                typeof(RoomProjectionReadResult<TestProjection>),
                typeof(RoomProjectionServices),
                typeof(RoomProjectionLifecycle),
                typeof(RoomProjectionTransition),
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

            Assert.That(
                typeof(IRoomProjectionStateReader).GetMethods()
                    .Select(method => method.Name),
                Is.EquivalentTo(new[] { "Read" }));
            Assert.That(
                typeof(IRoomMissionCommandSubmitter).GetMethods()
                    .Select(method => method.Name),
                Is.EquivalentTo(new[] { "Submit" }));
            Assert.That(
                typeof(IRoomMissionCommandSubmitter).GetMethod("Submit")
                    .GetParameters()
                    .Single()
                    .ParameterType,
                Is.EqualTo(typeof(MissionCommandEnvelope)));

            string[] forbiddenAuthorityTokens =
            {
                "Clear",
                "Reward",
                "Route",
                "Checkpoint",
                "Objective",
                "Persist",
                "Save",
            };
            string[] serviceMethodNames = typeof(IRoomProjectionStateReader).GetMethods()
                .Concat(typeof(IRoomMissionCommandSubmitter).GetMethods())
                .Select(method => method.Name)
                .ToArray();
            foreach (string token in forbiddenAuthorityTokens)
            {
                Assert.That(
                    serviceMethodNames.Any(
                        name => name.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0),
                    Is.False,
                    "Room projection services must not expose a direct " + token + " mutator.");
            }

            Assert.That(
                typeof(RoomProjectionIdentity).Assembly.GetReferencedAssemblies()
                    .Any(name => name.Name.StartsWith("UnityEngine", StringComparison.Ordinal)),
                Is.False);
        }

        private static RoomProjectionIdentity Identity(string roomId, string projectionId)
        {
            return new RoomProjectionIdentity(Id(roomId), Id(projectionId));
        }

        private static RoomProjectionKey Key(string roomId, long sequence)
        {
            return new RoomProjectionKey(
                Id("run.factory-run-0001"),
                Id(roomId),
                new MissionSequence(sequence));
        }

        private static MissionPayloadVersion CreateVersion()
        {
            return new MissionPayloadVersion(
                1,
                ContentVersion.Create(1, DefinitionFingerprint));
        }

        private static StableId Id(string text)
        {
            return StableId.Parse(text);
        }

        private sealed class TestProjection
        {
            public TestProjection(string name)
            {
                Name = name;
            }

            public string Name { get; }
        }

        private sealed class FakeProjectionReader : IRoomProjectionStateReader
        {
            private readonly RoomProjectionKey knownKey;
            private readonly object value;

            public FakeProjectionReader(RoomProjectionKey knownKey, object value)
            {
                this.knownKey = knownKey;
                this.value = value;
            }

            public RoomProjectionReadResult<TProjection> Read<TProjection>(
                RoomProjectionKey key)
            {
                if (!knownKey.Equals(key))
                {
                    return RoomProjectionReadResult<TProjection>.Unknown(key);
                }

                if (!(value is TProjection))
                {
                    throw new InvalidOperationException(
                        "The requested projection type does not match the test fixture.");
                }

                return RoomProjectionReadResult<TProjection>.Found(
                    key,
                    (TProjection)value);
            }
        }

        private sealed class FakeMissionCommandSubmitter : IRoomMissionCommandSubmitter
        {
            private readonly MissionSequence currentSequence;
            private readonly MissionPayloadVersion supportedVersion;

            public FakeMissionCommandSubmitter(
                MissionSequence currentSequence,
                MissionPayloadVersion supportedVersion)
            {
                this.currentSequence = currentSequence;
                this.supportedVersion = supportedVersion;
            }

            public MissionCommandEnvelope LastCommand { get; private set; }

            public MissionCommandEvaluation Submit(MissionCommandEnvelope command)
            {
                LastCommand = command;
                return MissionCommandGate.Evaluate(
                    command,
                    currentSequence,
                    supportedVersion);
            }
        }
    }
}
