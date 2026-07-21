using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ShooterMover.Application.Flow.Production;
using ShooterMover.Application.Persistence.Components;
using ShooterMover.Application.Persistence.Composition;
using ShooterMover.Contracts.Holdings;
using ShooterMover.Contracts.Missions.Results;
using ShooterMover.Contracts.Rewards.Application;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Economy.Money;
using ShooterMover.Domain.Persistence.Accounts;
using ShooterMover.Domain.Rewards.Model;
using ShooterMover.Domain.Rewards.Strongboxes;

namespace ShooterMover.Application.Rewards.Strongboxes.Persistence
{
    public sealed partial class StrongboxDurableOpeningCoordinatorV1
    {
        private static string ValidateExactUnopenedState(
            ProductionCharacterRuntimeGraphV1 graph,
            MissionRunStrongboxResultV1 selected,
            StrongboxOpenCommandV1 command)
        {
            MissionRunStrongboxCollectionV1 collection = selected.Collection;
            StrongboxOpeningSnapshotV1 boxes =
                graph.StrongboxAuthority.ExportSnapshot();
            StrongboxOpeningRecordSnapshotV1 existing = boxes.Openings
                .FirstOrDefault(item => item.Command.StrongboxInstanceStableId
                    == selected.InstanceStableId);
            if (existing != null)
            {
                if (!existing.Command.Equals(command))
                {
                    return "durable-opening-existing-command-conflict";
                }
                if (existing.Stage == StrongboxOpeningStageV1.Opened
                    && existing.TerminalFact != null)
                {
                    return string.Empty;
                }
            }

            PlayerHoldingsSnapshotV1 holdings =
                graph.LoadoutRuntime.Holdings.ExportSnapshot();
            UniqueHoldingSnapshotV1 held = holdings.UniqueHoldings
                .FirstOrDefault(item => item != null
                    && item.RewardKind == RewardGrantKindV1.Strongbox
                    && item.InstanceStableId == selected.InstanceStableId);
            if (held == null)
            {
                return "durable-opening-strongbox-not-held";
            }
            if (held.DefinitionStableId != collection.DefinitionStableId
                || held.Provenance == null
                || held.Provenance.GrantStableId != collection.GrantStableId
                || held.Provenance.SourceStableId != collection.SourceStableId)
            {
                return "durable-opening-holdings-provenance-mismatch";
            }

            StrongboxInstanceContextV1 context = boxes.Contexts
                .FirstOrDefault(item => item.InstanceStableId
                    == selected.InstanceStableId);
            if (context == null
                || context.TierStableId != collection.DefinitionStableId
                || context.CollectionProvenanceStableId != collection.GrantStableId
                || context.SourceContextStableId != collection.SourceStableId)
            {
                return "durable-opening-registration-context-mismatch";
            }
            return string.Empty;
        }

        private static string Restore(
            PlayerAccountSnapshotV1 beforeAccount,
            ProductionCharacterRuntimeGraphV1 graph,
            CharacterInstanceSnapshotV1 beforeCharacter,
            string expectedFingerprint)
        {
            if (string.Equals(
                ExportComponentFingerprint(graph),
                expectedFingerprint,
                StringComparison.Ordinal))
            {
                return string.Empty;
            }

            var bindings = new List<CharacterSaveRestoreBindingV1>();
            for (int slotIndex = 0;
                slotIndex < PlayerAccountSnapshotV1.CharacterSlotCount;
                slotIndex++)
            {
                CharacterInstanceSnapshotV1 character =
                    beforeAccount.CharacterAt(slotIndex);
                if (character == null)
                {
                    continue;
                }
                bindings.Add(new CharacterSaveRestoreBindingV1(
                    slotIndex,
                    character.CharacterInstanceStableId,
                    slotIndex == beforeCharacter.SlotIndex
                        ? graph.SaveAdapters
                        : Array.Empty<ISaveComponentAdapterV1>()));
            }

            var restore = new PlayerAccountRestoreCoordinatorV1(
                validateAggregate: PlayerAccountComponentSemanticsV1.Validate);
            PlayerAccountRestoreResultV1 restored = restore.Restore(
                beforeAccount,
                bindings);
            if (restored == null || !restored.Succeeded)
            {
                return restored == null
                    ? "restore-result-null"
                    : restored.Status + ":" + restored.RejectionCode;
            }
            string actual = ExportComponentFingerprint(graph);
            return string.Equals(actual, expectedFingerprint, StringComparison.Ordinal)
                ? string.Empty
                : "restore-fingerprint-mismatch";
        }

        private static bool ComponentsMatchGraph(
            CharacterInstanceSnapshotV1 durableCharacter,
            ProductionCharacterRuntimeGraphV1 graph)
        {
            IReadOnlyList<SaveComponentSnapshotV1> exported =
                PlayerAccountRestoreCoordinatorV1.ExportComponents(
                    graph.SaveAdapters);
            for (int index = 0; index < exported.Count; index++)
            {
                SaveComponentSnapshotV1 durable;
                if (!durableCharacter.TryGetComponent(
                        exported[index].ComponentStableId,
                        out durable)
                    || !string.Equals(
                        durable.CanonicalPayload,
                        exported[index].CanonicalPayload,
                        StringComparison.Ordinal)
                    || durable.SchemaVersion != exported[index].SchemaVersion
                    || !string.Equals(
                        durable.ContentVersion,
                        exported[index].ContentVersion,
                        StringComparison.Ordinal))
                {
                    return false;
                }
            }
            return true;
        }

        private static string ExportComponentFingerprint(
            ProductionCharacterRuntimeGraphV1 graph)
        {
            IReadOnlyList<SaveComponentSnapshotV1> components =
                PlayerAccountRestoreCoordinatorV1.ExportComponents(
                    graph.SaveAdapters);
            var parts = new List<string>(components.Count);
            for (int index = 0; index < components.Count; index++)
            {
                parts.Add(components[index].ToCanonicalString());
            }
            return StrongboxCanonicalV1.Fingerprint(
                string.Join("\n", parts));
        }

        private static StrongboxOpeningResultRuntimeV1 Rejected(
            StrongboxOpenCommandV1 command,
            string rejectionCode)
        {
            return new StrongboxOpeningResultRuntimeV1(
                StrongboxOpeningRuntimeStatusV1.InvalidRequest,
                command == null ? null : command.OpeningStableId,
                0L,
                0L,
                command == null ? string.Empty : command.Fingerprint,
                null,
                null,
                null,
                null,
                null,
                rejectionCode);
        }

        private static StrongboxOpeningResultRuntimeV1 SnapshotRejected(
            StrongboxOpenCommandV1 command,
            StrongboxOpeningResultRuntimeV1 source,
            string rejectionCode)
        {
            return new StrongboxOpeningResultRuntimeV1(
                StrongboxOpeningRuntimeStatusV1.SnapshotRejected,
                command == null ? null : command.OpeningStableId,
                source == null ? 0L : source.PreviousSequence,
                source == null ? 0L : source.CurrentSequence,
                command == null ? string.Empty : command.Fingerprint,
                source == null ? null : source.GeneratedOutcome,
                source == null ? null : source.TerminalFact,
                null,
                source == null ? null : source.RewardApplicationResult,
                source == null ? null : source.ConsumeResult,
                rejectionCode);
        }

        private static string AppendRestore(string restore)
        {
            return string.IsNullOrEmpty(restore)
                ? string.Empty
                : ";restore=" + restore;
        }

    }
}
