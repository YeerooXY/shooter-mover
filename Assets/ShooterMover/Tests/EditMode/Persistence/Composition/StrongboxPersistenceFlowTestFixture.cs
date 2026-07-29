using System;
using System.Collections.Generic;
using NUnit.Framework;
using ShooterMover.Application.Economy.Money;
using ShooterMover.Application.Flow.Game;
using ShooterMover.Application.Persistence.Accounts;
using ShooterMover.Application.Persistence.SaveParts;
using ShooterMover.Application.Persistence.Composition;
using ShooterMover.Application.Rewards.Strongboxes;
using ShooterMover.Application.Rewards.Strongboxes.Persistence;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Contracts.Holdings;
using ShooterMover.Contracts.Missions.Results;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Economy.Money;
using ShooterMover.Domain.Holdings;
using ShooterMover.Domain.Persistence.Accounts;
using ShooterMover.Domain.Progression.Context;
using ShooterMover.Domain.Rewards.Model;
using ShooterMover.Domain.Rewards.Strongboxes;

namespace ShooterMover.Tests.EditMode.Persistence.Composition
{
    public sealed partial class StrongboxPersistenceFlowTests
    {
        private static StrongboxMissionResultApplicationCommand TransferCommand(
            CharacterLiveGraph target,
            PlayerAccountSnapshot account,
            MissionResultPayload result,
            CharacterLiveGraph source,
            string suffix)
        {
            return new StrongboxMissionResultApplicationCommand(
                Id("operation.box-transfer." + suffix),
                result.RunStableId,
                1L,
                result,
                target.Character.CharacterInstanceStableId,
                target.Character.Revision,
                target.Character.Fingerprint,
                account.Revision,
                source.LoadoutRuntime.Holdings.ExportSnapshot(),
                source.StrongboxAuthority.ExportSnapshot());
        }

        private static MissionResultPayload TerminalResult(
            CharacterLiveGraph source,
            PlayerRouteProfilePayload route,
            StableId runId,
            params MissionRunStrongboxResult[] boxes)
        {
            PlayerHoldingsSnapshot holdings = source.LoadoutRuntime.Holdings.ExportSnapshot();
            StrongboxOpeningSnapshot strongboxes = source.StrongboxAuthority.ExportSnapshot();
            return MissionResultPayload.Create(
                runId,
                route,
                MissionRunCompletionState.Completed,
                boxes,
                boxes.Length,
                holdings.LedgerSnapshot.Sequence,
                holdings.Fingerprint,
                strongboxes.Sequence,
                strongboxes.Fingerprint);
        }

        private static BoxFixture AddBox(
            CharacterLiveGraph graph,
            string suffix,
            ulong seed)
        {
            StrongboxDefinition definition = graph.StrongboxCatalog.Definitions[0];
            StableId boxId = Id("strongbox-instance." + suffix);
            StableId grantId = Id("grant." + suffix);
            StableId sourceId = Id("source." + suffix);
            PlayerHoldingsMutationResult added = graph.LoadoutRuntime.Holdings.Apply(
                PlayerHoldingsCommand.AddStrongbox(
                    Id("transaction.add." + suffix),
                    Id("operation.add." + suffix),
                    graph.LoadoutRuntime.Holdings.AuthorityStableId,
                    definition.TierStableId,
                    boxId,
                    HoldingProvenance.Create(grantId, sourceId)));
            Assert.That(added.Status, Is.EqualTo(PlayerHoldingsMutationStatus.Applied));
            PlayerHoldingsSnapshot holdings = graph.LoadoutRuntime.Holdings.ExportSnapshot();
            var context = StrongboxInstanceContext.Create(
                boxId,
                definition.TierStableId,
                seed,
                1,
                ProgressionContext.Create(
                    1,
                    1,
                    Id("difficulty.normal"),
                    0,
                    new[] { Id("progression-tag.campaign") }),
                sourceId,
                grantId,
                definition.Fingerprint);
            Assert.That(graph.StrongboxAuthority.RegisterInstance(context).Status,
                Is.EqualTo(StrongboxRegistrationStatus.Registered));
            var collection = new MissionRunStrongboxCollection(
                definition.TierStableId,
                boxId,
                grantId,
                sourceId,
                Id("operation.collect." + suffix),
                holdings.LedgerSnapshot.Sequence,
                holdings.Fingerprint);
            return new BoxFixture(
                context,
                new MissionRunStrongboxResult(
                    collection,
                    MissionRunStrongboxState.Unopened,
                    null,
                    null));
        }

        private static StrongboxOpenCommand OpenCommand(
            CharacterLiveGraph graph,
            BoxFixture box,
            string suffix)
        {
            return StrongboxOpenCommand.Create(
                Id("opening." + suffix),
                Id("run." + suffix),
                box.Context.InstanceStableId,
                graph.Character.CharacterInstanceStableId,
                MoneyWalletIds.AuthorityStableId,
                graph.ScrapWallet.AuthorityStableId,
                graph.LoadoutRuntime.Holdings.AuthorityStableId);
        }

        private static CharacterLiveGraphFactory Factory()
        {
            return CharacterLiveGraphFactory.CreateVerticalSliceDefaults();
        }

        private static CharacterSetupFlow Composition(
            CharacterLiveGraphFactory factory,
            PlayerAccountSnapshot initial,
            Action<PlayerAccountSnapshot> capture)
        {
            return new CharacterSetupFlow(
                new PlayerAccountSaveState(initial),
                factory,
                snapshot =>
                {
                    capture(snapshot);
                    return Saved(snapshot);
                });
        }

        private static CharacterInstanceSnapshot StarterCharacter(
            CharacterLiveGraphFactory factory,
            int slotIndex,
            string suffix)
        {
            StableId characterId = Id("character-instance." + suffix);
            StableId classId = Id("loadout-profile.juggernaut");
            PlayerRouteProfilePayload route = PlayerRouteProfilePayload.Create(
                characterId,
                classId,
                new StableId[PlayerRouteProfilePayload.GunSlotCount]);
            ICharacterLiveGraph graph = factory.CreateStarter(
                slotIndex, characterId, classId, suffix, route);
            IReadOnlyList<SavePartSnapshot> components =
                PlayerAccountRestoreFlow.ExportComponents(graph.SaveAdapters);
            graph.Dispose();
            return new CharacterInstanceSnapshot(
                characterId, classId, slotIndex, suffix, 0L, components);
        }

        private static PlayerAccountSnapshot Account(
            params CharacterInstanceSnapshot[] characters)
        {
            var slots = new CharacterInstanceSnapshot[
                PlayerAccountSnapshot.CharacterSlotCount];
            foreach (CharacterInstanceSnapshot character in characters)
                slots[character.SlotIndex] = character;
            return new PlayerAccountSnapshot(
                Id("account.box-persist-tests"), 0L, slots, null);
        }

        private static PlayerAccountStoreResult Saved(PlayerAccountSnapshot snapshot)
        {
            return new PlayerAccountStoreResult(
                PlayerAccountStoreStatus.Saved, string.Empty, snapshot);
        }

        private static StableId Id(string value) { return StableId.Parse(value); }

        private sealed class BoxFixture
        {
            public BoxFixture(StrongboxInstanceContext context, MissionRunStrongboxResult result)
            {
                Context = context;
                Result = result;
            }
            public StrongboxInstanceContext Context { get; }
            public MissionRunStrongboxResult Result { get; }
        }
    }
}
