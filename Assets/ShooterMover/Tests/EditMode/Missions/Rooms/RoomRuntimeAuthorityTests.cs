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
    public sealed class RoomRuntimeAuthorityTests
    {
        [Test]
        public void ZeroOccupants_ClearImmediatelyAndEnableActiveConnectedExit()
        {
            RoomRuntimeAuthorityV1 authority = CreateAuthority("zero");

            RoomRuntimeOperationResultV1 result = Register(
                authority,
                "register-zero",
                EntryRoom,
                Array.Empty<RoomOccupantRegistrationV1>());

            RoomOccupancyProjectionV1 room = authority.GetRoomProjection(EntryRoom);
            Assert.That(result.Status, Is.EqualTo(RoomRuntimeOperationStatusV1.Applied));
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
            RoomRuntimeAuthorityV1 authority = CreateAuthority("one");
            RoomOccupantRegistrationV1 required = Occupant(
                "required-one",
                "mobile-droid",
                RoomOccupantClearRoleV1.RequiredEnemy);
            Register(authority, "register-one", EntryRoom, required);

            RoomOccupancyProjectionV1 before = authority.GetRoomProjection(EntryRoom);
            Assert.That(before.IsCleared, Is.False);
            Assert.That(before.IsExitEligible(ForwardExit), Is.False);

            RoomRuntimeOperationResultV1 terminal = Terminal(
                authority,
                "terminal-one",
                EntryRoom,
                required.EntityStableId);

            RoomOccupancyProjectionV1 after = authority.GetRoomProjection(EntryRoom);
            Assert.That(terminal.Status, Is.EqualTo(RoomRuntimeOperationStatusV1.Applied));
            Assert.That(terminal.ClearTransition, Is.Not.Null);
            Assert.That(after.IsCleared, Is.True);
            Assert.That(after.Occupants[0].IsTerminal, Is.True);
            Assert.That(after.IsExitEligible(ForwardExit), Is.True);
        }

        [Test]
        public void ManyOccupants_OnlyRequiredEnemyAndObjectiveBlockClear()
        {
            RoomRuntimeAuthorityV1 authority = CreateAuthority("many");
            RoomOccupantRegistrationV1 required = Occupant(
                "required-many",
                "enemy-type-a",
                RoomOccupantClearRoleV1.RequiredEnemy);
            RoomOccupantRegistrationV1 objective = Occupant(
                "objective-many",
                "objective-type-a",
                RoomOccupantClearRoleV1.ObjectiveEntity);
            RoomOccupantRegistrationV1 optional = Occupant(
                "optional-many",
                "enemy-type-b",
                RoomOccupantClearRoleV1.OptionalEnemy);
            RoomOccupantRegistrationV1 nonParticipant = Occupant(
                "nonparticipant-many",
                "prop-type-a",
                RoomOccupantClearRoleV1.NonParticipant);
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

            RoomRuntimeOperationResultV1 final = Terminal(
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
            RoomRuntimeAuthorityV1 authority = CreateAuthority("duplicate");
            RoomOccupantRegistrationV1 required = Occupant(
                "duplicate-target",
                "shared-definition",
                RoomOccupantClearRoleV1.RequiredEnemy);
            Register(authority, "register-duplicate", EntryRoom, required);
            var command = new ReportRoomOccupantTerminalCommandV1(
                authority.RuntimeInstanceStableId,
                Operation("terminal-duplicate"),
                authority.CurrentProjection.LifecycleGeneration,
                EntryRoom,
                required.EntityStableId);

            RoomRuntimeOperationResultV1 first = authority.ReportTerminal(command);
            long sequenceAfterFirst = authority.CurrentProjection.Sequence;
            RoomRuntimeOperationResultV1 duplicate = authority.ReportTerminal(command);

            Assert.That(first.Status, Is.EqualTo(RoomRuntimeOperationStatusV1.Applied));
            Assert.That(first.ClearTransition, Is.Not.Null);
            Assert.That(
                duplicate.Status,
                Is.EqualTo(RoomRuntimeOperationStatusV1.DuplicateNoChange));
            Assert.That(duplicate.ClearTransition, Is.Null);
            Assert.That(authority.CurrentProjection.Sequence, Is.EqualTo(sequenceAfterFirst));
            Assert.That(
                authority.GetRoomProjection(EntryRoom).Occupants[0].IsTerminal,
                Is.True);
        }

        [Test]
        public void ConflictingOperationIdentity_IsRejectedWithoutMutation()
        {
            RoomRuntimeAuthorityV1 authority = CreateAuthority("conflict");
            RoomOccupantRegistrationV1 firstOccupant = Occupant(
                "conflict-first",
                "shared-definition",
                RoomOccupantClearRoleV1.RequiredEnemy);
            RoomOccupantRegistrationV1 secondOccupant = Occupant(
                "conflict-second",
                "shared-definition",
                RoomOccupantClearRoleV1.RequiredEnemy);
            Register(
                authority,
                "register-conflict",
                EntryRoom,
                firstOccupant,
                secondOccupant);
            StableId operation = Operation("terminal-conflict");
            authority.ReportTerminal(new ReportRoomOccupantTerminalCommandV1(
                authority.RuntimeInstanceStableId,
                operation,
                1L,
                EntryRoom,
                firstOccupant.EntityStableId));
            long beforeConflict = authority.CurrentProjection.Sequence;

            RoomRuntimeOperationResultV1 conflict = authority.ReportTerminal(
                new ReportRoomOccupantTerminalCommandV1(
                    authority.RuntimeInstanceStableId,
                    operation,
                    1L,
                    EntryRoom,
                    secondOccupant.EntityStableId));

            Assert.That(conflict.Status, Is.EqualTo(RoomRuntimeOperationStatusV1.Rejected));
            Assert.That(conflict.RejectionCode, Is.EqualTo("room-operation-id-conflict"));
            Assert.That(authority.CurrentProjection.Sequence, Is.EqualTo(beforeConflict));
            RoomOccupancyProjectionV1 room = authority.GetRoomProjection(EntryRoom);
            Assert.That(FindOccupant(room, firstOccupant.EntityStableId).IsTerminal, Is.True);
            Assert.That(FindOccupant(room, secondOccupant.EntityStableId).IsTerminal, Is.False);
            Assert.That(room.IsCleared, Is.False);
        }

        [Test]
        public void LeaveAndReturn_PreservesTerminalOccupantsWithinSameRun()
        {
            RoomRuntimeAuthorityV1 authority = CreateAuthority("retained");
            RoomOccupantRegistrationV1 entryEnemy = Occupant(
                "retained-entry",
                "entry-enemy",
                RoomOccupantClearRoleV1.RequiredEnemy);
            Register(authority, "register-retained-entry", EntryRoom, entryEnemy);
            Register(
                authority,
                "register-retained-terminal",
                TerminalRoom,
                Array.Empty<RoomOccupantRegistrationV1>());
            Terminal(
                authority,
                "terminal-retained-entry",
                EntryRoom,
                entryEnemy.EntityStableId);

            Activate(authority, "activate-terminal", TerminalRoom);
            Assert.That(authority.GetRoomProjection(EntryRoom).IsActive, Is.False);
            Activate(authority, "activate-entry-return", EntryRoom);

            RoomOccupancyProjectionV1 returned = authority.GetRoomProjection(EntryRoom);
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
            RoomRuntimeAuthorityV1 authority = CreateAuthority("restart");
            RoomOccupantRegistrationV1 required = Occupant(
                "restart-required",
                "restart-definition",
                RoomOccupantClearRoleV1.RequiredEnemy);
            Register(authority, "register-restart-entry", EntryRoom, required);
            Register(
                authority,
                "register-restart-terminal",
                TerminalRoom,
                Occupant(
                    "restart-optional",
                    "optional-definition",
                    RoomOccupantClearRoleV1.OptionalEnemy));
            Terminal(
                authority,
                "terminal-before-restart",
                EntryRoom,
                required.EntityStableId);
            Activate(authority, "activate-before-restart", TerminalRoom);

            RoomRuntimeOperationResultV1 restart = authority.Restart(
                new RestartRoomRuntimeCommandV1(
                    authority.RuntimeInstanceStableId,
                    Operation("restart-runtime"),
                    1L));

            Assert.That(restart.Status, Is.EqualTo(RoomRuntimeOperationStatusV1.Applied));
            Assert.That(authority.CurrentProjection.LifecycleGeneration, Is.EqualTo(2L));
            RoomOccupancyProjectionV1 entry = authority.GetRoomProjection(EntryRoom);
            Assert.That(entry.IsActive, Is.True);
            Assert.That(entry.IsCleared, Is.False);
            Assert.That(FindOccupant(entry, required.EntityStableId).IsTerminal, Is.False);
            RoomOccupancyProjectionV1 terminal = authority.GetRoomProjection(TerminalRoom);
            Assert.That(terminal.IsActive, Is.False);
            Assert.That(terminal.IsCleared, Is.True);
            Assert.That(terminal.ConnectedExits[0].IsEligible, Is.False);

            RoomRuntimeOperationResultV1 stale = authority.ReportTerminal(
                new ReportRoomOccupantTerminalCommandV1(
                    authority.RuntimeInstanceStableId,
                    Operation("stale-after-restart"),
                    1L,
                    EntryRoom,
                    required.EntityStableId));
            Assert.That(stale.Status, Is.EqualTo(RoomRuntimeOperationStatusV1.Rejected));
            Assert.That(stale.RejectionCode, Is.EqualTo("room-runtime-generation-stale"));
        }

        [Test]
        public void MultipleRuntimeInstances_DoNotShareOccupantState()
        {
            RoomRuntimeAuthorityV1 first = CreateAuthority("instance-a");
            RoomRuntimeAuthorityV1 second = CreateAuthority("instance-b");
            RoomOccupantRegistrationV1 shared = Occupant(
                "same-entity-id",
                "same-definition-id",
                RoomOccupantClearRoleV1.RequiredEnemy);
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
            RoomRuntimeAuthorityV1 authority = CreateAuthority("identity");
            StableId sharedDefinition = Definition("identical-definition");
            RoomOccupantRegistrationV1 first = new RoomOccupantRegistrationV1(
                Entity("identity-first"),
                sharedDefinition,
                RoomOccupantClearRoleV1.RequiredEnemy);
            RoomOccupantRegistrationV1 second = new RoomOccupantRegistrationV1(
                Entity("identity-second"),
                sharedDefinition,
                RoomOccupantClearRoleV1.RequiredEnemy);
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
            RoomRuntimeAuthorityV1 authority = CreateAuthority("inactive");
            RoomOccupantRegistrationV1 terminalEnemy = Occupant(
                "inactive-terminal-enemy",
                "inactive-definition",
                RoomOccupantClearRoleV1.RequiredEnemy);
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
            RoomOccupancyProjectionV1 inactive =
                authority.GetRoomProjection(TerminalRoom);
            Assert.That(inactive.IsActive, Is.False);
            Assert.That(inactive.IsCleared, Is.True);
            Assert.That(inactive.IsExitEligible(ReturnExit), Is.False);

            Activate(authority, "activate-cleared-inactive", TerminalRoom);
            RoomOccupancyProjectionV1 active = authority.GetRoomProjection(TerminalRoom);
            Assert.That(active.IsActive, Is.True);
            Assert.That(active.IsExitEligible(ReturnExit), Is.True);
        }

        [Test]
        public void ExitEligibility_ContainsOnlyGraphConnectedExits()
        {
            RoomRuntimeAuthorityV1 authority = CreateAuthority("exits");
            Register(
                authority,
                "register-exit-entry",
                EntryRoom,
                Array.Empty<RoomOccupantRegistrationV1>());
            Register(
                authority,
                "register-exit-terminal",
                TerminalRoom,
                Array.Empty<RoomOccupantRegistrationV1>());

            RoomOccupancyProjectionV1 entry = authority.GetRoomProjection(EntryRoom);
            Assert.That(entry.ConnectedExits.Count, Is.EqualTo(1));
            Assert.That(entry.ConnectedExits[0].ExitStableId, Is.EqualTo(ForwardExit));
            Assert.That(entry.IsExitEligible(ForwardExit), Is.True);
            Assert.That(entry.IsExitEligible(ReturnExit), Is.False);

            Activate(authority, "activate-exit-terminal", TerminalRoom);
            RoomOccupancyProjectionV1 terminal = authority.GetRoomProjection(TerminalRoom);
            Assert.That(terminal.ConnectedExits.Count, Is.EqualTo(1));
            Assert.That(terminal.ConnectedExits[0].ExitStableId, Is.EqualTo(ReturnExit));
            Assert.That(terminal.IsExitEligible(ReturnExit), Is.True);
            Assert.That(terminal.IsExitEligible(ForwardExit), Is.False);
        }

        [Test]
        public void ClearRole_NotPackageOrHierarchyName_DeterminesParticipation()
        {
            RoomRuntimeAuthorityV1 authority = CreateAuthority("names");
            RoomOccupantRegistrationV1 arbitraryRequired =
                new RoomOccupantRegistrationV1(
                    StableId.Parse("prop.switch-alpha"),
                    StableId.Parse("content.decoration-alpha"),
                    RoomOccupantClearRoleV1.RequiredEnemy);
            RoomOccupantRegistrationV1 enemyLookingNonParticipant =
                new RoomOccupantRegistrationV1(
                    StableId.Parse("enemy.optional-looking"),
                    StableId.Parse("package.required-looking"),
                    RoomOccupantClearRoleV1.NonParticipant);
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
            AssertNoUnityReference(typeof(IRoomRuntimeAuthorityV1).Assembly);
            AssertNoUnityReference(typeof(RoomRuntimeAuthorityV1).Assembly);
        }

        private static StableId EntryRoom =>
            Level1RoomGraphDefinitionV1.EntryRoomStableId;

        private static StableId TerminalRoom =>
            Level1RoomGraphDefinitionV1.TerminalRoomStableId;

        private static StableId ForwardExit =>
            Level1RoomGraphDefinitionV1.ForwardExitStableId;

        private static StableId ReturnExit =>
            Level1RoomGraphDefinitionV1.ReturnExitStableId;

        private static RoomRuntimeAuthorityV1 CreateAuthority(string suffix)
        {
            return new RoomRuntimeAuthorityV1(
                StableId.Create("room-runtime", suffix),
                Level1RoomGraphDefinitionV1.Create());
        }

        private static RoomOccupantRegistrationV1 Occupant(
            string entity,
            string definition,
            RoomOccupantClearRoleV1 role)
        {
            return new RoomOccupantRegistrationV1(
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

        private static RoomRuntimeOperationResultV1 Register(
            RoomRuntimeAuthorityV1 authority,
            string operation,
            StableId room,
            params RoomOccupantRegistrationV1[] occupants)
        {
            return authority.RegisterOccupants(new RegisterRoomOccupantsCommandV1(
                authority.RuntimeInstanceStableId,
                Operation(operation),
                authority.CurrentProjection.LifecycleGeneration,
                room,
                occupants));
        }

        private static RoomRuntimeOperationResultV1 Terminal(
            RoomRuntimeAuthorityV1 authority,
            string operation,
            StableId room,
            StableId occupant)
        {
            return authority.ReportTerminal(
                new ReportRoomOccupantTerminalCommandV1(
                    authority.RuntimeInstanceStableId,
                    Operation(operation),
                    authority.CurrentProjection.LifecycleGeneration,
                    room,
                    occupant));
        }

        private static RoomRuntimeOperationResultV1 Activate(
            RoomRuntimeAuthorityV1 authority,
            string operation,
            StableId room)
        {
            return authority.ActivateRoom(new ActivateRoomCommandV1(
                authority.RuntimeInstanceStableId,
                Operation(operation),
                authority.CurrentProjection.LifecycleGeneration,
                room));
        }

        private static RoomOccupantProjectionV1 FindOccupant(
            RoomOccupancyProjectionV1 room,
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
