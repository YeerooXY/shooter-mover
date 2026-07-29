using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using ShooterMover.Application.Missions.Rooms;
using ShooterMover.Content.Definitions.Missions.Rooms;
using ShooterMover.Contracts.Missions.Rooms;
using ShooterMover.Domain.Common;

namespace ShooterMover.Tests.EditMode.Missions.Rooms
{
    public sealed class RoomOccupancyStateTests
    {
        [Test]
        public void ZeroOccupants_ClearImmediatelyAndEnableActiveConnectedExit()
        {
            RoomOccupancyState authority = CreateAuthority("zero");

            RoomLiveOperationResult result = Register(
                authority,
                "register-zero",
                EntryRoom,
                Array.Empty<RoomOccupantRegistration>());

            RoomOccupancyView room = authority.GetRoomProjection(EntryRoom);
            Assert.That(result.Status, Is.EqualTo(RoomLiveOperationStatus.Applied));
            Assert.That(result.ClearTransition, Is.Not.Null);
            Assert.That(room.IsActive, Is.True);
            Assert.That(room.IsOccupancyRegistered, Is.True);
            Assert.That(room.IsCleared, Is.True);
            Assert.That(room.Occupants, Is.Empty);
            Assert.That(room.IsExitEligible(ForwardExit), Is.True);
        }

        [Test]
        public void OneRequiredOccupant_BlocksUntilItsIdentityIsTerminal()
        {
            RoomOccupancyState authority = CreateAuthority("one");
            RoomOccupantRegistration required = Occupant(
                "required-one",
                "mobile-droid",
                RoomOccupantClearRole.RequiredEnemy);
            Register(authority, "register-one", EntryRoom, required);

            RoomOccupancyView before = authority.GetRoomProjection(EntryRoom);
            Assert.That(before.IsCleared, Is.False);
            Assert.That(before.IsExitEligible(ForwardExit), Is.False);

            RoomLiveOperationResult terminal = Terminal(
                authority,
                "terminal-one",
                EntryRoom,
                required.EntityStableId);

            RoomOccupancyView after = authority.GetRoomProjection(EntryRoom);
            Assert.That(terminal.Status, Is.EqualTo(RoomLiveOperationStatus.Applied));
            Assert.That(terminal.ClearTransition, Is.Not.Null);
            Assert.That(after.IsCleared, Is.True);
            Assert.That(after.Occupants[0].IsTerminal, Is.True);
            Assert.That(after.IsExitEligible(ForwardExit), Is.True);
        }

        [Test]
        public void ManyOccupants_OnlyRequiredEnemyAndObjectiveBlockClear()
        {
            RoomOccupancyState authority = CreateAuthority("many");
            RoomOccupantRegistration required = Occupant(
                "required-many",
                "enemy-type-a",
                RoomOccupantClearRole.RequiredEnemy);
            RoomOccupantRegistration objective = Occupant(
                "objective-many",
                "objective-type-a",
                RoomOccupantClearRole.ObjectiveEntity);
            RoomOccupantRegistration optional = Occupant(
                "optional-many",
                "enemy-type-b",
                RoomOccupantClearRole.OptionalEnemy);
            RoomOccupantRegistration nonParticipant = Occupant(
                "nonparticipant-many",
                "prop-type-a",
                RoomOccupantClearRole.NonParticipant);
            Register(
                authority,
                "register-many",
                EntryRoom,
                required,
                objective,
                optional,
                nonParticipant);

            Terminal(authority, "terminal-optional", EntryRoom, optional.EntityStableId);
            Terminal(
                authority,
                "terminal-nonparticipant",
                EntryRoom,
                nonParticipant.EntityStableId);
            Assert.That(
                authority.GetRoomProjection(EntryRoom).IsCleared,
                Is.False);

            Terminal(authority, "terminal-required", EntryRoom, required.EntityStableId);
            Assert.That(
                authority.GetRoomProjection(EntryRoom).IsCleared,
                Is.False);

            RoomLiveOperationResult final = Terminal(
                authority,
                "terminal-objective",
                EntryRoom,
                objective.EntityStableId);
            Assert.That(final.ClearTransition, Is.Not.Null);
            Assert.That(
                authority.GetRoomProjection(EntryRoom).IsCleared,
                Is.True);
        }

        [Test]
        public void DuplicateTerminalNotification_IsIdempotent()
        {
            RoomOccupancyState authority = CreateAuthority("duplicate");
            RoomOccupantRegistration required = Occupant(
                "duplicate-target",
                "shared-definition",
                RoomOccupantClearRole.RequiredEnemy);
            Register(authority, "register-duplicate", EntryRoom, required);
            var command = new ReportRoomOccupantTerminalCommand(
                authority.RuntimeInstanceStableId,
                Operation("terminal-duplicate"),
                authority.CurrentProjection.LifecycleGeneration,
                EntryRoom,
                required.EntityStableId);

            RoomLiveOperationResult first = authority.ReportTerminal(command);
            long sequenceAfterFirst = authority.CurrentProjection.Sequence;
            RoomLiveOperationResult duplicate = authority.ReportTerminal(command);

            Assert.That(first.Status, Is.EqualTo(RoomLiveOperationStatus.Applied));
            Assert.That(first.ClearTransition, Is.Not.Null);
            Assert.That(
                duplicate.Status,
                Is.EqualTo(RoomLiveOperationStatus.DuplicateNoChange));
            Assert.That(duplicate.ClearTransition, Is.Null);
            Assert.That(authority.CurrentProjection.Sequence, Is.EqualTo(sequenceAfterFirst));
            Assert.That(
                authority.GetRoomProjection(EntryRoom).Occupants[0].IsTerminal,
                Is.True);
        }

        [Test]
        public void ConflictingOperationIdentity_IsRejectedWithoutMutation()
        {
            RoomOccupancyState authority = CreateAuthority("conflict");
            RoomOccupantRegistration firstOccupant = Occupant(
                "conflict-first",
                "shared-definition",
                RoomOccupantClearRole.RequiredEnemy);
            RoomOccupantRegistration secondOccupant = Occupant(
                "conflict-second",
                "shared-definition",
                RoomOccupantClearRole.RequiredEnemy);
            Register(
                authority,
                "register-conflict",
                EntryRoom,
                firstOccupant,
                secondOccupant);
            StableId operation = Operation("terminal-conflict");
            authority.ReportTerminal(new ReportRoomOccupantTerminalCommand(
                authority.RuntimeInstanceStableId,
                operation,
                1L,
                EntryRoom,
                firstOccupant.EntityStableId));
            long beforeConflict = authority.CurrentProjection.Sequence;

            RoomLiveOperationResult conflict = authority.ReportTerminal(
                new ReportRoomOccupantTerminalCommand(
                    authority.RuntimeInstanceStableId,
                    operation,
                    1L,
                    EntryRoom,
                    secondOccupant.EntityStableId));

            Assert.That(conflict.Status, Is.EqualTo(RoomLiveOperationStatus.Rejected));
            Assert.That(conflict.RejectionCode, Is.EqualTo("room-operation-id-conflict"));
            Assert.That(authority.CurrentProjection.Sequence, Is.EqualTo(beforeConflict));
            RoomOccupancyView room = authority.GetRoomProjection(EntryRoom);
            Assert.That(FindOccupant(room, firstOccupant.EntityStableId).IsTerminal, Is.True);
            Assert.That(FindOccupant(room, secondOccupant.EntityStableId).IsTerminal, Is.False);
            Assert.That(room.IsCleared, Is.False);
        }

        [Test]
        public void LeaveAndReturn_PreservesTerminalOccupantsWithinSameRun()
        {
            RoomOccupancyState authority = CreateAuthority("retained");
            RoomOccupantRegistration entryEnemy = Occupant(
                "retained-entry",
                "entry-enemy",
                RoomOccupantClearRole.RequiredEnemy);
            Register(authority, "register-retained-entry", EntryRoom, entryEnemy);
            Register(
                authority,
                "register-retained-terminal",
                TerminalRoom,
                Array.Empty<RoomOccupantRegistration>());
            Terminal(
                authority,
                "terminal-retained-entry",
                EntryRoom,
                entryEnemy.EntityStableId);

            Activate(authority, "activate-terminal", TerminalRoom);
            Assert.That(authority.GetRoomProjection(EntryRoom).IsActive, Is.False);
            Activate(authority, "activate-entry-return", EntryRoom);

            RoomOccupancyView returned = authority.GetRoomProjection(EntryRoom);
            Assert.That(returned.IsActive, Is.True);
            Assert.That(returned.IsCleared, Is.True);
            Assert.That(returned.IsExitEligible(ForwardExit), Is.True);
            Assert.That(
                FindOccupant(returned, entryEnemy.EntityStableId).IsTerminal,
                Is.True);
        }

        [Test]
        public void Restart_IncrementsGenerationAndRestoresAuthoredInitialState()
        {
            RoomOccupancyState authority = CreateAuthority("restart");
            RoomOccupantRegistration required = Occupant(
                "restart-required",
                "restart-definition",
                RoomOccupantClearRole.RequiredEnemy);
            Register(authority, "register-restart-entry", EntryRoom, required);
            Register(
                authority,
                "register-restart-terminal",
                TerminalRoom,
                Occupant(
                    "restart-optional",
                    "optional-definition",
                    RoomOccupantClearRole.OptionalEnemy));
            Terminal(
                authority,
                "terminal-before-restart",
                EntryRoom,
                required.EntityStableId);
            Activate(authority, "activate-before-restart", TerminalRoom);

            RoomLiveOperationResult restart = authority.Restart(
                new RestartRoomLiveCommand(
                    authority.RuntimeInstanceStableId,
                    Operation("restart-runtime"),
                    1L));

            Assert.That(restart.Status, Is.EqualTo(RoomLiveOperationStatus.Applied));
            Assert.That(authority.CurrentProjection.LifecycleGeneration, Is.EqualTo(2L));
            RoomOccupancyView entry = authority.GetRoomProjection(EntryRoom);
            Assert.That(entry.IsActive, Is.True);
            Assert.That(entry.IsCleared, Is.False);
            Assert.That(FindOccupant(entry, required.EntityStableId).IsTerminal, Is.False);
            RoomOccupancyView terminal = authority.GetRoomProjection(TerminalRoom);
            Assert.That(terminal.IsActive, Is.False);
            Assert.That(terminal.IsCleared, Is.True);
            Assert.That(terminal.ConnectedExits[0].IsEligible, Is.False);

            RoomLiveOperationResult stale = authority.ReportTerminal(
                new ReportRoomOccupantTerminalCommand(
                    authority.RuntimeInstanceStableId,
                    Operation("stale-after-restart"),
                    1L,
                    EntryRoom,
                    required.EntityStableId));
            Assert.That(stale.Status, Is.EqualTo(RoomLiveOperationStatus.Rejected));
            Assert.That(stale.RejectionCode, Is.EqualTo("room-runtime-generation-stale"));
        }

        [Test]
        public void MultipleRuntimeInstances_DoNotShareOccupantState()
        {
            RoomOccupancyState first = CreateAuthority("instance-a");
            RoomOccupancyState second = CreateAuthority("instance-b");
            RoomOccupantRegistration shared = Occupant(
                "same-entity-id",
                "same-definition-id",
                RoomOccupantClearRole.RequiredEnemy);
            Register(first, "register-instance-a", EntryRoom, shared);
            Register(second, "register-instance-b", EntryRoom, shared);

            Terminal(first, "terminal-instance-a", EntryRoom, shared.EntityStableId);

            Assert.That(first.GetRoomProjection(EntryRoom).IsCleared, Is.True);
            Assert.That(second.GetRoomProjection(EntryRoom).IsCleared, Is.False);
            Assert.That(
                first.CurrentProjection.RuntimeInstanceStableId,
                Is.Not.EqualTo(second.CurrentProjection.RuntimeInstanceStableId));
        }

        [Test]
        public void IdenticalDefinitions_WithDistinctEntityIdentitiesRemainIndependent()
        {
            RoomOccupancyState authority = CreateAuthority("identity");
            StableId sharedDefinition = Definition("identical-definition");
            RoomOccupantRegistration first = new RoomOccupantRegistration(
                Entity("identity-first"),
                sharedDefinition,
                RoomOccupantClearRole.RequiredEnemy);
            RoomOccupantRegistration second = new RoomOccupantRegistration(
                Entity("identity-second"),
                sharedDefinition,
                RoomOccupantClearRole.RequiredEnemy);
            Register(authority, "register-identities", EntryRoom, first, second);

            Terminal(authority, "terminal-identity-first", EntryRoom, first.EntityStableId);
            Assert.That(authority.GetRoomProjection(EntryRoom).IsCleared, Is.False);
            Assert.That(
                FindOccupant(
                    authority.GetRoomProjection(EntryRoom),
                    second.EntityStableId).IsTerminal,
                Is.False);

            Terminal(authority, "terminal-identity-second", EntryRoom, second.EntityStableId);
            Assert.That(authority.GetRoomProjection(EntryRoom).IsCleared, Is.True);
        }

        [Test]
        public void InactiveRoom_RetainsTerminalFactsButDoesNotEnableItsExit()
        {
            RoomOccupancyState authority = CreateAuthority("inactive");
            RoomOccupantRegistration terminalEnemy = Occupant(
                "inactive-terminal-enemy",
                "inactive-definition",
                RoomOccupantClearRole.RequiredEnemy);
            Register(
                authority,
                "register-inactive-room",
                TerminalRoom,
                terminalEnemy);

            Terminal(
                authority,
                "terminal-inactive-room",
                TerminalRoom,
                terminalEnemy.EntityStableId);
            RoomOccupancyView inactive =
                authority.GetRoomProjection(TerminalRoom);
            Assert.That(inactive.IsActive, Is.False);
            Assert.That(inactive.IsCleared, Is.True);
            Assert.That(inactive.IsExitEligible(ReturnExit), Is.False);

            Activate(authority, "activate-cleared-inactive", TerminalRoom);
            RoomOccupancyView active = authority.GetRoomProjection(TerminalRoom);
            Assert.That(active.IsActive, Is.True);
            Assert.That(active.IsExitEligible(ReturnExit), Is.True);
        }

        [Test]
        public void ExitEligibility_ContainsOnlyGraphConnectedExits()
        {
            RoomOccupancyState authority = CreateAuthority("exits");
            Register(
                authority,
                "register-exit-entry",
                EntryRoom,
                Array.Empty<RoomOccupantRegistration>());
            Register(
                authority,
                "register-exit-terminal",
                TerminalRoom,
                Array.Empty<RoomOccupantRegistration>());

            RoomOccupancyView entry = authority.GetRoomProjection(EntryRoom);
            Assert.That(entry.ConnectedExits.Count, Is.EqualTo(1));
            Assert.That(entry.ConnectedExits[0].ExitStableId, Is.EqualTo(ForwardExit));
            Assert.That(entry.IsExitEligible(ForwardExit), Is.True);
            Assert.That(entry.IsExitEligible(ReturnExit), Is.False);

            Activate(authority, "activate-exit-terminal", TerminalRoom);
            RoomOccupancyView terminal = authority.GetRoomProjection(TerminalRoom);
            Assert.That(terminal.ConnectedExits.Count, Is.EqualTo(1));
            Assert.That(terminal.ConnectedExits[0].ExitStableId, Is.EqualTo(ReturnExit));
            Assert.That(terminal.IsExitEligible(ReturnExit), Is.True);
            Assert.That(terminal.IsExitEligible(ForwardExit), Is.False);
        }

        [Test]
        public void ClearRole_NotPackageOrHierarchyName_DeterminesParticipation()
        {
            RoomOccupancyState authority = CreateAuthority("names");
            RoomOccupantRegistration arbitraryRequired =
                new RoomOccupantRegistration(
                    StableId.Parse("prop.switch-alpha"),
                    StableId.Parse("content.decoration-alpha"),
                    RoomOccupantClearRole.RequiredEnemy);
            RoomOccupantRegistration enemyLookingNonParticipant =
                new RoomOccupantRegistration(
                    StableId.Parse("enemy.optional-looking"),
                    StableId.Parse("package.required-looking"),
                    RoomOccupantClearRole.NonParticipant);
            Register(
                authority,
                "register-role-not-name",
                EntryRoom,
                arbitraryRequired,
                enemyLookingNonParticipant);

            Assert.That(authority.GetRoomProjection(EntryRoom).IsCleared, Is.False);
            Terminal(
                authority,
                "terminal-role-not-name",
                EntryRoom,
                arbitraryRequired.EntityStableId);
            Assert.That(authority.GetRoomProjection(EntryRoom).IsCleared, Is.True);
        }

        [Test]
        public void RuntimeAssemblies_HaveNoUnityEngineDependency()
        {
            AssertNoUnityReference(typeof(IRoomLiveState).Assembly);
            AssertNoUnityReference(typeof(RoomOccupancyState).Assembly);
        }

        private static StableId EntryRoom =>
            Level1RoomGraphDefinition.EntryRoomStableId;

        private static StableId TerminalRoom =>
            Level1RoomGraphDefinition.TerminalRoomStableId;

        private static StableId ForwardExit =>
            Level1RoomGraphDefinition.ForwardExitStableId;

        private static StableId ReturnExit =>
            Level1RoomGraphDefinition.ReturnExitStableId;

        private static RoomOccupancyState CreateAuthority(string suffix)
        {
            return new RoomOccupancyState(
                StableId.Create("room-runtime", suffix),
                Level1RoomGraphDefinition.Create());
        }

        private static RoomOccupantRegistration Occupant(
            string entity,
            string definition,
            RoomOccupantClearRole role)
        {
            return new RoomOccupantRegistration(
                Entity(entity),
                Definition(definition),
                role);
        }

        private static StableId Entity(string value)
        {
            return StableId.Create("entity", value);
        }

        private static StableId Definition(string value)
        {
            return StableId.Create("definition", value);
        }

        private static StableId Operation(string value)
        {
            return StableId.Create("operation", value);
        }

        private static RoomLiveOperationResult Register(
            RoomOccupancyState authority,
            string operation,
            StableId room,
            params RoomOccupantRegistration[] occupants)
        {
            return authority.RegisterOccupants(new RegisterRoomOccupantsCommand(
                authority.RuntimeInstanceStableId,
                Operation(operation),
                authority.CurrentProjection.LifecycleGeneration,
                room,
                occupants));
        }

        private static RoomLiveOperationResult Terminal(
            RoomOccupancyState authority,
            string operation,
            StableId room,
            StableId occupant)
        {
            return authority.ReportTerminal(
                new ReportRoomOccupantTerminalCommand(
                    authority.RuntimeInstanceStableId,
                    Operation(operation),
                    authority.CurrentProjection.LifecycleGeneration,
                    room,
                    occupant));
        }

        private static RoomLiveOperationResult Activate(
            RoomOccupancyState authority,
            string operation,
            StableId room)
        {
            return authority.ActivateRoom(new ActivateRoomCommand(
                authority.RuntimeInstanceStableId,
                Operation(operation),
                authority.CurrentProjection.LifecycleGeneration,
                room));
        }

        private static RoomOccupantView FindOccupant(
            RoomOccupancyView room,
            StableId entity)
        {
            for (int index = 0; index < room.Occupants.Count; index++)
            {
                if (room.Occupants[index].EntityStableId == entity)
                {
                    return room.Occupants[index];
                }
            }

            throw new AssertionException("Missing occupant projection: " + entity);
        }

        private static void AssertNoUnityReference(Assembly assembly)
        {
            AssemblyName[] references = assembly.GetReferencedAssemblies();
            for (int index = 0; index < references.Length; index++)
            {
                Assert.That(
                    references[index].Name,
                    Does.Not.StartWith("UnityEngine"),
                    assembly.GetName().Name + " must stay engine-independent.");
            }
        }
    }
}
