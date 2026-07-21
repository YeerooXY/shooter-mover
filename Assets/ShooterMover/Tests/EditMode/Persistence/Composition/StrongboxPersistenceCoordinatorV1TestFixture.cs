using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ShooterMover.Application.Economy.Money;
using ShooterMover.Application.Flow.Production;
using ShooterMover.Application.Persistence.Accounts;
using ShooterMover.Application.Persistence.Components;
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
    public sealed partial class StrongboxPersistenceCoordinatorV1Tests
    {
        private static StrongboxMissionResultApplicationCommandV1 TransferCommand(
            ProductionCharacterRuntimeGraphV1 target,
            PlayerAccountSnapshotV1 account,
            MissionResultPayloadV1 result,
            ProductionCharacterRuntimeGraphV1 source,
            string suffix)
        {
            return new StrongboxMissionResultApplicationCommandV1(
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

        private static MissionResultPayloadV1 TerminalResult(
            ProductionCharacterRuntimeGraphV1 source,
            PlayerRouteProfilePayloadV1 route,
            StableId runId,
            params MissionRunStrongboxResultV1[] boxes)
        {
            PlayerHoldingsSnapshotV1 holdings =
                source.LoadoutRuntime.Holdings.ExportSnapshot();
            StrongboxOpeningSnapshotV1 strongboxes =
                source.StrongboxAuthority.ExportSnapshot();
            return MissionResultPayloadV1.Create(
                runId,
                route,
                MissionRunCompletionStateV1.Completed,
                boxes,
                boxes.Length,
                holdings.LedgerSnapshot.Sequence,
                holdings.Fingerprint,
                strongboxes.Sequence,
                strongboxes.Fingerprint);
        }

        private static BoxFixture AddBox(
            ProductionCharacterRuntimeGraphV1 graph,
            string suffix,
            ulong seed)
        {
            StrongboxDefinitionV1 definition =
                graph.StrongboxCatalog.Definitions[0];
            StableId boxId = Id("strongbox-instance." + suffix);
            StableId grantId = Id("grant." + suffix);
            StableId sourceId = Id("source." + suffix);
            PlayerHoldingsMutationResultV1 added =
                graph.LoadoutRuntime.Holdings.Apply(
                    PlayerHoldingsCommandV1.AddStrongbox(
                        Id("transaction.add." + suffix),
                        Id("operation.add." + suffix),
                        graph.LoadoutRuntime.Holdings.AuthorityStableId,
                        definition.TierStableId,
                        boxId,
                        HoldingProvenanceV1.Create(grantId, sourceId)));
            Assert.That(added.Status,
                Is.EqualTo(PlayerHoldingsMutationStatusV1.Applied));
            PlayerHoldingsSnapshotV1 holdings =
                graph.LoadoutRuntime.Holdings.ExportSnapshot();
            var context = StrongboxInstanceContextV1.Create(
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
            Assert.That(
                graph.StrongboxAuthority.RegisterInstance(context).Status,
                Is.EqualTo(StrongboxRegistrationStatusV1.Registered));
            var collection = new MissionRunStrongboxCollectionV1(
                definition.TierStableId,
                boxId,
                grantId,
                sourceId,
                Id("operation.collect." + suffix),
                holdings.LedgerSnapshot.Sequence,
                holdings.Fingerprint);
            var result = new MissionRunStrongboxResultV1(
                collection,
                MissionRunStrongboxStateV1.Unopened,
                null,
                null);
            return new BoxFixture(context, result);
        }

        private static StrongboxOpenCommandV1 OpenCommand(
            ProductionCharacterRuntimeGraphV1 graph,
            BoxFixture box,
            string suffix)
        {
            return StrongboxOpenCommandV1.Create(
                Id("opening." + suffix),
                Id("run." + suffix),
                box.Context.InstanceStableId,
                graph.Character.CharacterInstanceStableId,
                MoneyWalletIdsV1.AuthorityStableId,
                graph.ScrapWallet.AuthorityStableId,
                graph.LoadoutRuntime.Holdings.AuthorityStableId);
        }

        private static ProductionCharacterRuntimeGraphFactoryV1 Factory()
        {
            return ProductionCharacterRuntimeGraphFactoryV1
                .CreateVerticalSliceDefaults();
        }

        private static CharacterCompositionCoordinatorV1 Composition(
            ProductionCharacterRuntimeGraphFactoryV1 factory,
            PlayerAccountSnapshotV1 initial,
            Action<PlayerAccountSnapshotV1> capture)
        {
            return new CharacterCompositionCoordinatorV1(
                new PlayerAccountSaveAuthorityV1(initial),
                factory,
                snapshot =>
                {
                    capture(snapshot);
                    return Saved(snapshot);
                });
        }

        private static CharacterInstanceSnapshotV1 StarterCharacter(
            ProductionCharacterRuntimeGraphFactoryV1 factory,
            int slotIndex,
            string suffix)
        {
            StableId characterId = Id("character-instance." + suffix);
            StableId classId = Id("loadout-profile.juggernaut");
            PlayerRouteProfilePayloadV1 route =
                PlayerRouteProfilePayloadV1.Create(
                    characterId,
                    classId,
                    new[]
                    {
                        ProductionStarterWeaponCatalogV1
                            .BlasterEquipmentInstanceStableId,
                        ProductionStarterWeaponCatalogV1
                            .ShotgunEquipmentInstanceStableId,
                        ProductionStarterWeaponCatalogV1
                            .RocketEquipmentInstanceStableId,
                        ProductionStarterWeaponCatalogV1
                            .ArcEquipmentInstanceStableId,
                    });
            ICharacterRuntimeGraphV1 graph = factory.CreateStarter(
                slotIndex,
                characterId,
                classId,
                suffix,
                route);
            IReadOnlyList<SaveComponentSnapshotV1> components =
                PlayerAccountRestoreCoordinatorV1.ExportComponents(
                    graph.SaveAdapters);
            graph.Dispose();
            return new CharacterInstanceSnapshotV1(
                characterId,
                classId,
                slotIndex,
                suffix,
                0L,
                components);
        }

        private static PlayerAccountSnapshotV1 Account(
            params CharacterInstanceSnapshotV1[] characters)
        {
            var slots = new CharacterInstanceSnapshotV1[
                PlayerAccountSnapshotV1.CharacterSlotCount];
            foreach (CharacterInstanceSnapshotV1 character in characters)
            {
                slots[character.SlotIndex] = character;
            }
            return new PlayerAccountSnapshotV1(
                Id("account.box-persist-tests"),
                0L,
                slots,
                null);
        }

        private static PlayerAccountStoreResultV1 Saved(
            PlayerAccountSnapshotV1 snapshot)
        {
            return new PlayerAccountStoreResultV1(
                PlayerAccountStoreStatusV1.Saved,
                string.Empty,
                snapshot);
        }

        private static StableId Id(string value)
        {
            return StableId.Parse(value);
        }

        private sealed class BoxFixture
        {
            public BoxFixture(
                StrongboxInstanceContextV1 context,
                MissionRunStrongboxResultV1 result)
            {
                Context = context;
                Result = result;
            }
            public StrongboxInstanceContextV1 Context { get; }
            public MissionRunStrongboxResultV1 Result { get; }
        }

    }
}
