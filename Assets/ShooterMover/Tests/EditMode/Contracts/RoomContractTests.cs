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
            RoomViewIdentity identity = Identity(
                "room.factory-receiving",
                "projection.factory-receiving-a");
            RoomViewIdentity equal = Identity(
                "room.factory-receiving",
                "projection.factory-receiving-a");
            RoomViewKey key = Key("room.factory-receiving", 4L);

            Assert.That(identity, Is.EqualTo(equal));
            Assert.That(identity.GetHashCode(), Is.EqualTo(equal.GetHashCode()));
            Assert.That(
                identity.ToCanonicalString(),
                Is.EqualTo(
                    "room_id=room.factory-receiving\n"
                    + "projection_id=projection.factory-receiving-a"));
            Assert.That(key.RunId, Is.EqualTo(Id("run.factory-run-0001")));
            Assert.That(key.Sequence.Value, Is.EqualTo(4L));

            RoomViewLifecycle lifecycle = RoomViewLifecycle.Create(identity);
            Assert.Throws<ArgumentException>(
                () => lifecycle.Load(Key("room.factory-cargo-sort", 4L)));
        }

        [Test]
        public void Connection_RequiresCompatibleSocketsAndIsOrderIndependent()
        {
            RoomViewIdentity firstRoom = Identity(
                "room.factory-receiving",
                "projection.factory-receiving-a");
            RoomViewIdentity secondRoom = Identity(
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
            RoomViewLifecycle unloaded = RoomViewLifecycle.Create(
                Identity("room.factory-receiving", "projection.factory-receiving-a"));
            RoomViewKey initialKey = Key("room.factory-receiving", 4L);
            RoomViewKey refreshedKey = Key("room.factory-receiving", 5L);

            RoomViewTransition load = unloaded.Load(initialKey);
            Assert.That(load.Kind, Is.EqualTo(RoomViewTransitionKind.Applied));
            RoomViewLifecycle loaded = load.Next;

            RoomViewTransition repeatedLoad = loaded.Load(initialKey);
            Assert.That(repeatedLoad.Kind, Is.EqualTo(RoomViewTransitionKind.NoChange));
            Assert.That(repeatedLoad.Next, Is.SameAs(loaded));

            RoomViewTransition refresh = loaded.Refresh(refreshedKey);
            Assert.That(refresh.Kind, Is.EqualTo(RoomViewTransitionKind.Applied));
            RoomViewLifecycle refreshed = refresh.Next;
            Assert.That(refreshed.ActiveKey, Is.EqualTo(refreshedKey));

            RoomViewTransition repeatedRefresh = refreshed.Refresh(refreshedKey);
            Assert.That(
                repeatedRefresh.Kind,
                Is.EqualTo(RoomViewTransitionKind.NoChange));
            Assert.That(repeatedRefresh.Next, Is.SameAs(refreshed));

            RoomViewTransition staleRefresh = refreshed.Refresh(initialKey);
            Assert.That(
                staleRefresh.Rejection,
                Is.EqualTo(RoomViewTransitionRejection.StaleProjectionKey));
            Assert.That(staleRefresh.Next, Is.SameAs(refreshed));
        }

        [Test]
        public void Lifecycle_ReloadAfterCompletedUnloadRestoresProjectionWithoutDurableState()
        {
            RoomViewLifecycle loaded = RoomViewLifecycle.Create(
                    Identity("room.factory-receiving", "projection.factory-receiving-a"))
                .Load(Key("room.factory-receiving", 7L))
                .Next;
            RoomViewLifecycle unloading = loaded.BeginUnload().Next;
            RoomViewLifecycle unloaded = unloading.CompleteUnload().Next;

            Assert.That(unloaded.Phase, Is.EqualTo(RoomViewLifecyclePhase.Unloaded));
            Assert.That(unloaded.ActiveKey, Is.Null);

            RoomViewKey reloadKey = Key("room.factory-receiving", 7L);
            RoomViewTransition reload = unloaded.Reload(reloadKey);
            Assert.That(reload.Kind, Is.EqualTo(RoomViewTransitionKind.Applied));
            Assert.That(reload.Next.Phase, Is.EqualTo(RoomViewLifecyclePhase.Loaded));
            Assert.That(reload.Next.ActiveKey, Is.EqualTo(reloadKey));

            RoomViewTransition repeatedReload = reload.Next.Reload(reloadKey);
            Assert.That(
                repeatedReload.Kind,
                Is.EqualTo(RoomViewTransitionKind.NoChange));
        }

        [Test]
        public void Lifecycle_InterruptedUnloadCanResumeIdempotently()
        {
            RoomViewLifecycle loaded = RoomViewLifecycle.Create(
                    Identity("room.factory-receiving", "projection.factory-receiving-a"))
                .Load(Key("room.factory-receiving", 9L))
                .Next;
            RoomViewLifecycle unloading = loaded.BeginUnload().Next;

            Assert.That(unloading.Phase, Is.EqualTo(RoomViewLifecyclePhase.Unloading));
            RoomViewTransition resumed = unloading.ResumeAfterInterruptedUnload();
            Assert.That(resumed.Kind, Is.EqualTo(RoomViewTransitionKind.Applied));
            Assert.That(resumed.Next.Phase, Is.EqualTo(RoomViewLifecyclePhase.Loaded));
            Assert.That(resumed.Next.ActiveKey, Is.EqualTo(loaded.ActiveKey));

            RoomViewTransition repeated = resumed.Next.ResumeAfterInterruptedUnload();
            Assert.That(repeated.Kind, Is.EqualTo(RoomViewTransitionKind.NoChange));
            Assert.That(repeated.Next, Is.SameAs(resumed.Next));

            RoomViewTransition invalidCompletion = resumed.Next.CompleteUnload();
            Assert.That(
                invalidCompletion.Rejection,
                Is.EqualTo(RoomViewTransitionRejection.InvalidTransition));
        }

        [Test]
        public void ProjectionReader_RepresentsUnknownKeysExplicitly()
        {
            RoomViewKey knownKey = Key("room.factory-receiving", 3L);
            FakeViewReader reader = new FakeViewReader(
                knownKey,
                new TestView("receiving-ready"));

            RoomViewReadResult<TestView> known =
                reader.Read<TestView>(knownKey);
            RoomViewReadResult<TestView> unknown =
                reader.Read<TestView>(Key("room.factory-receiving", 4L));

            Assert.That(known.Status, Is.EqualTo(RoomViewReadStatus.Found));
            Assert.That(known.HasValue, Is.True);
            Assert.That(known.Value.Name, Is.EqualTo("receiving-ready"));
            Assert.That(
                unknown.Status,
                Is.EqualTo(RoomViewReadStatus.UnknownKey));
            Assert.That(unknown.HasValue, Is.False);
            Assert.That(unknown.Value, Is.Null);
        }

        [Test]
        public void Services_ReadProjectionAndSubmitMissionMessageWithoutDirectStateMutation()
        {
            RoomViewKey key = Key("room.factory-receiving", 3L);
            FakeViewReader reader = new FakeViewReader(
                key,
                new TestView("receiving-ready"));
            MissionPayloadVersion version = CreateVersion();
            FakeMissionCommandSubmitter submitter = new FakeMissionCommandSubmitter(
                new MissionSequence(3L),
                version);
            RoomViewServices services = new RoomViewServices(reader, submitter);
            MissionCommandEnvelope command = new MissionCommandEnvelope(
                Id("command.clear-room-0001"),
                Id("run.factory-run-0001"),
                version,
                new MissionSequence(3L),
                new RoomClearRequest(
                    Id("room.factory-receiving"),
                    Id("encounter.receiving-wave")));

            RoomViewReadResult<TestView> projection =
                services.StateReader.Read<TestView>(key);
            MissionCommandEvaluation evaluation = services.MissionCommands.Submit(command);

            Assert.That(projection.HasValue, Is.True);
            Assert.That(evaluation.IsAccepted, Is.True);
            Assert.That(submitter.LastCommand, Is.SameAs(command));
            Assert.That(command.CommandType, Is.EqualTo(MissionCommandType.RoomClear));
        }

        [Test]
        public void TwoAdditiveRooms_MaintainIndependentProjectionLifecycles()
        {
            RoomViewLifecycle first = RoomViewLifecycle.Create(
                    Identity("room.factory-receiving", "projection.factory-receiving-a"))
                .Load(Key("room.factory-receiving", 2L))
                .Next;
            RoomViewLifecycle second = RoomViewLifecycle.Create(
                    Identity("room.factory-cargo-sort", "projection.factory-cargo-sort-a"))
                .Load(Key("room.factory-cargo-sort", 2L))
                .Next;

            RoomViewLifecycle firstRefreshed = first
                .Refresh(Key("room.factory-receiving", 3L))
                .Next;

            Assert.That(firstRefreshed.Identity.RoomId, Is.Not.EqualTo(second.Identity.RoomId));
            Assert.That(firstRefreshed.ActiveKey.Sequence.Value, Is.EqualTo(3L));
            Assert.That(second.ActiveKey.Sequence.Value, Is.EqualTo(2L));
            Assert.That(second.Phase, Is.EqualTo(RoomViewLifecyclePhase.Loaded));
        }

        [Test]
        public void RoomProjectionContracts_AreImmutableUnityFreeAndExposeNoTruthSetters()
        {
            Type[] immutableTypes =
            {
                typeof(RoomViewIdentity),
                typeof(RoomViewKey),
                typeof(RoomSocket),
                typeof(RoomConnection),
                typeof(RoomViewReadResult<TestView>),
                typeof(RoomViewServices),
                typeof(RoomViewLifecycle),
                typeof(RoomViewTransition),
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
                typeof(IRoomViewStateReader).GetMethods()
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
            string[] serviceMethodNames = typeof(IRoomViewStateReader).GetMethods()
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
                typeof(RoomViewIdentity).Assembly.GetReferencedAssemblies()
                    .Any(name => name.Name.StartsWith("UnityEngine", StringComparison.Ordinal)),
                Is.False);
        }

        private static RoomViewIdentity Identity(string roomId, string projectionId)
        {
            return new RoomViewIdentity(Id(roomId), Id(projectionId));
        }

        private static RoomViewKey Key(string roomId, long sequence)
        {
            return new RoomViewKey(
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

        private sealed class TestView
        {
            public TestView(string name)
            {
                Name = name;
            }

            public string Name { get; }
        }

        private sealed class FakeViewReader : IRoomViewStateReader
        {
            private readonly RoomViewKey knownKey;
            private readonly object value;

            public FakeViewReader(RoomViewKey knownKey, object value)
            {
                this.knownKey = knownKey;
                this.value = value;
            }

            public RoomViewReadResult<TProjection> Read<TProjection>(
                RoomViewKey key)
            {
                if (!knownKey.Equals(key))
                {
                    return RoomViewReadResult<TProjection>.Unknown(key);
                }

                if (!(value is TProjection))
                {
                    throw new InvalidOperationException(
                        "The requested projection type does not match the test fixture.");
                }

                return RoomViewReadResult<TProjection>.Found(
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
