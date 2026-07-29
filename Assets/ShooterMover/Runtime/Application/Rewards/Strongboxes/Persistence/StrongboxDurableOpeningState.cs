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
using ShooterMover.Domain.Holdings;
using ShooterMover.Domain.Persistence.Accounts;
using ShooterMover.Domain.Rewards.Model;
using ShooterMover.Domain.Rewards.Strongboxes;

namespace ShooterMover.Application.Rewards.Strongboxes.Persistence
{
    public sealed partial class StrongboxDurableOpeningFlow
    {
        private static string ValidateExactUnopenedState(
            CharacterLiveGraph graph,
            MissionRunStrongboxResult selected,
            StrongboxOpenCommand command)
        {
            MissionRunStrongboxCollection collection = selected.Collection;
            StrongboxOpeningSnapshot boxes =
                graph.StrongboxAuthority.ExportSnapshot();
            StrongboxOpeningRecordSnapshot existing = boxes.Openings
                .FirstOrDefault(item => item.Command.StrongboxInstanceStableId
                    == selected.InstanceStableId);
            if (existing != null)
            {
                if (!existing.Command.Equals(command))
                {
                    return "durable-opening-existing-command-conflict";
                }
                if (existing.Stage == StrongboxOpeningStage.Opened
                    && existing.TerminalFact != null)
                {
                    return string.Empty;
                }
            }

            PlayerHoldingsSnapshot holdings =
                graph.LoadoutRuntime.Holdings.ExportSnapshot();
            UniqueHoldingSnapshot held = holdings.UniqueHoldings
                .FirstOrDefault(item => item != null
                    && item.RewardKind == RewardGrantKind.Strongbox
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

            StrongboxInstanceContext context = boxes.Contexts
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
            PlayerAccountSnapshot beforeAccount,
            CharacterLiveGraph graph,
            CharacterInstanceSnapshot beforeCharacter,
            string expectedFingerprint)
        {
            try
            {
                if (beforeAccount == null
                    || graph == null
                    || beforeCharacter == null
                    || string.IsNullOrWhiteSpace(expectedFingerprint))
                {
                    return "restore-precondition-missing";
                }

                if (string.Equals(
                    ExportComponentFingerprint(graph),
                    expectedFingerprint,
                    StringComparison.Ordinal))
                {
                    return string.Empty;
                }

                var bindings = new List<CharacterSaveRestoreBinding>();
                for (int slotIndex = 0;
                    slotIndex < PlayerAccountSnapshot.CharacterSlotCount;
                    slotIndex++)
                {
                    CharacterInstanceSnapshot character =
                        beforeAccount.CharacterAt(slotIndex);
                    if (character == null)
                    {
                        continue;
                    }
                    bindings.Add(new CharacterSaveRestoreBinding(
                        slotIndex,
                        character.CharacterInstanceStableId,
                        slotIndex == beforeCharacter.SlotIndex
                            ? graph.SaveAdapters
                            : Array.Empty<ISaveComponentBridge>()));
                }

                var restore = new PlayerAccountRestoreFlow(
                    validateAggregate: snapshot =>
                        PlayerAccountComponentSemantics.Validate(snapshot));
                PlayerAccountRestoreResult restored = restore.Restore(
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
            catch (Exception exception)
            {
                return "restore-exception-"
                    + exception.GetType().Name.ToLowerInvariant();
            }
        }

        private static string RestoreIfCaptured(
            PlayerAccountSnapshot beforeAccount,
            CharacterLiveGraph graph,
            CharacterInstanceSnapshot beforeCharacter,
            string expectedFingerprint)
        {
            return beforeAccount == null
                || graph == null
                || beforeCharacter == null
                || string.IsNullOrWhiteSpace(expectedFingerprint)
                ? string.Empty
                : Restore(
                    beforeAccount,
                    graph,
                    beforeCharacter,
                    expectedFingerprint);
        }

        private static bool ComponentsMatchGraph(
            CharacterInstanceSnapshot durableCharacter,
            CharacterLiveGraph graph)
        {
            IReadOnlyList<SaveComponentSnapshot> exported =
                PlayerAccountRestoreFlow.ExportComponents(
                    graph.SaveAdapters);
            for (int index = 0; index < exported.Count; index++)
            {
                SaveComponentSnapshot durable;
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
            CharacterLiveGraph graph)
        {
            IReadOnlyList<SaveComponentSnapshot> components =
                PlayerAccountRestoreFlow.ExportComponents(
                    graph.SaveAdapters);
            var parts = new List<string>(components.Count);
            for (int index = 0; index < components.Count; index++)
            {
                parts.Add(components[index].ToCanonicalString());
            }
            return Strongbox.Fingerprint(
                string.Join("\n", parts));
        }

        private static StrongboxOpeningResultLive Rejected(
            StrongboxOpenCommand command,
            string rejectionCode)
        {
            return new StrongboxOpeningResultLive(
                StrongboxOpeningLiveStatus.InvalidRequest,
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

        private static StrongboxOpeningResultLive SnapshotRejected(
            StrongboxOpenCommand command,
            StrongboxOpeningResultLive source,
            string rejectionCode)
        {
            return new StrongboxOpeningResultLive(
                StrongboxOpeningLiveStatus.SnapshotRejected,
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
