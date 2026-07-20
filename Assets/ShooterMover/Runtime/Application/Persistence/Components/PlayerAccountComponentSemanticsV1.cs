using System;
using System.Collections.Generic;
using System.Linq;
using ShooterMover.Application.Inventory.LoadoutScreen;
using ShooterMover.Application.Rewards.Strongboxes;
using ShooterMover.Contracts.Holdings;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Persistence.Accounts;
using ShooterMover.Domain.Rewards.Model;

namespace ShooterMover.Application.Persistence.Components
{
    public static class PlayerAccountComponentSemanticsV1
    {
        public static SaveComponentValidationResultV1 Validate(
            PlayerAccountSnapshotV1 account,
            Func<StableId, string> expectedStrongboxDefinitionFingerprint = null)
        {
            SaveComponentValidationResultV1 aggregate =
                PlayerAccountAggregateCodecV1.Validate(account);
            if (!aggregate.Succeeded) return aggregate;
            for (int slot = 0; slot < account.CharacterSlots.Count; slot++)
            {
                CharacterInstanceSnapshotV1 character = account.CharacterSlots[slot];
                if (character == null) continue;
                SaveComponentValidationResultV1 result = ValidateCharacter(
                    character,
                    expectedStrongboxDefinitionFingerprint);
                if (!result.Succeeded) return result;
            }
            return SaveComponentValidationResultV1.Accept();
        }

        public static SaveComponentValidationResultV1 ValidateCharacter(
            CharacterInstanceSnapshotV1 character,
            Func<StableId, string> expectedStrongboxDefinitionFingerprint = null)
        {
            if (character == null)
            {
                return SaveComponentValidationResultV1.Reject(
                    "character-component-semantics-character-null");
            }

            PlayerHoldingsSnapshotV1 holdings = null;
            InventoryLoadoutAuthoritySnapshotV1 loadout = null;
            StrongboxOpeningSnapshotV1 strongboxes = null;
            string error;
            SaveComponentSnapshotV1 component;
            if (character.TryGetComponent(
                KnownSaveComponentDefinitionsV1.PlayerHoldings().ComponentStableId,
                out component)
                && !KnownSaveComponentCodecsV1.PlayerHoldings.TryDecode(
                    component.CanonicalPayload,
                    out holdings,
                    out error))
            {
                return SaveComponentValidationResultV1.Reject(error);
            }
            if (character.TryGetComponent(
                KnownSaveComponentDefinitionsV1.ExactInstanceLoadout().ComponentStableId,
                out component)
                && !KnownSaveComponentCodecsV1.ExactInstanceLoadout.TryDecode(
                    component.CanonicalPayload,
                    out loadout,
                    out error))
            {
                return SaveComponentValidationResultV1.Reject(error);
            }
            if (character.TryGetComponent(
                KnownSaveComponentDefinitionsV1.StrongboxState().ComponentStableId,
                out component)
                && !KnownSaveComponentCodecsV1.StrongboxState.TryDecode(
                    component.CanonicalPayload,
                    out strongboxes,
                    out error))
            {
                return SaveComponentValidationResultV1.Reject(error);
            }

            if (loadout != null)
            {
                if (holdings == null)
                {
                    return SaveComponentValidationResultV1.Reject(
                        "loadout-requires-holdings-component");
                }
                var equipmentIds = new HashSet<StableId>(
                    holdings.UniqueHoldings
                        .Where(item => item.RewardKind
                            == RewardGrantKindV1.EquipmentReference)
                        .Select(item => item.InstanceStableId));
                for (int index = 0; index < loadout.Bindings.Count; index++)
                {
                    StableId instanceId = loadout.Bindings[index]
                        .EquipmentInstanceStableId;
                    if (instanceId != null && !equipmentIds.Contains(instanceId))
                    {
                        return SaveComponentValidationResultV1.Reject(
                            "loadout-equipment-instance-absent-from-holdings:"
                                + instanceId);
                    }
                }
            }

            if (strongboxes != null)
            {
                if (holdings == null)
                {
                    return SaveComponentValidationResultV1.Reject(
                        "strongbox-state-requires-holdings-component");
                }
                return ValidateStrongboxes(
                    holdings,
                    strongboxes,
                    expectedStrongboxDefinitionFingerprint);
            }
            return SaveComponentValidationResultV1.Accept();
        }

        private static SaveComponentValidationResultV1 ValidateStrongboxes(
            PlayerHoldingsSnapshotV1 holdings,
            StrongboxOpeningSnapshotV1 strongboxes,
            Func<StableId, string> expectedDefinitionFingerprint)
        {
            var heldBoxes = holdings.UniqueHoldings
                .Where(item => item.RewardKind == RewardGrantKindV1.Strongbox)
                .ToDictionary(item => item.InstanceStableId, item => item);
            var contexts = strongboxes.Contexts.ToDictionary(
                item => item.InstanceStableId,
                item => item);
            var openingsByBox = new Dictionary<StableId, StrongboxOpeningRecordSnapshotV1>();
            for (int index = 0; index < strongboxes.Openings.Count; index++)
            {
                StrongboxOpeningRecordSnapshotV1 opening = strongboxes.Openings[index];
                StableId boxId = opening.Command.StrongboxInstanceStableId;
                if (openingsByBox.ContainsKey(boxId))
                {
                    return SaveComponentValidationResultV1.Reject(
                        "strongbox-opening-box-identity-duplicate:" + boxId);
                }
                openingsByBox.Add(boxId, opening);
            }

            foreach (KeyValuePair<StableId, StrongboxOpeningRecordSnapshotV1> pair
                in openingsByBox)
            {
                if (!contexts.ContainsKey(pair.Key))
                {
                    return SaveComponentValidationResultV1.Reject(
                        "strongbox-opening-context-missing:" + pair.Key);
                }
            }

            foreach (KeyValuePair<StableId, UniqueHoldingSnapshotV1> pair in heldBoxes)
            {
                StrongboxInstanceContextV1 context;
                if (!contexts.TryGetValue(pair.Key, out context))
                {
                    return SaveComponentValidationResultV1.Reject(
                        "held-strongbox-registration-missing:" + pair.Key);
                }
                if (context.TierStableId != pair.Value.DefinitionStableId)
                {
                    return SaveComponentValidationResultV1.Reject(
                        "held-strongbox-tier-conflict:" + pair.Key);
                }
                if (context.CollectionProvenanceStableId
                    != pair.Value.Provenance.GrantStableId)
                {
                    return SaveComponentValidationResultV1.Reject(
                        "held-strongbox-provenance-conflict:" + pair.Key);
                }
            }

            foreach (StrongboxInstanceContextV1 context in strongboxes.Contexts)
            {
                string expected = expectedDefinitionFingerprint == null
                    ? null
                    : expectedDefinitionFingerprint(context.TierStableId);
                if (expected != null
                    && !string.Equals(
                        expected,
                        context.AlgorithmContentFingerprint,
                        StringComparison.Ordinal))
                {
                    return SaveComponentValidationResultV1.Reject(
                        "strongbox-definition-fingerprint-conflict:"
                            + context.InstanceStableId);
                }

                StrongboxOpeningRecordSnapshotV1 opening;
                if (!openingsByBox.TryGetValue(context.InstanceStableId, out opening))
                {
                    if (!heldBoxes.ContainsKey(context.InstanceStableId))
                    {
                        return SaveComponentValidationResultV1.Reject(
                            "registered-unopened-strongbox-absent-from-holdings:"
                                + context.InstanceStableId);
                    }
                    continue;
                }

                if (opening.Command.StrongboxInstanceStableId
                    != context.InstanceStableId)
                {
                    return SaveComponentValidationResultV1.Reject(
                        "strongbox-opening-instance-conflict:"
                            + context.InstanceStableId);
                }
                if (opening.GeneratedOutcome != null)
                {
                    StrongboxOpeningRequestV1 request =
                        opening.GeneratedOutcome.OpeningRequest;
                    if (request.StrongboxInstanceStableId
                            != context.InstanceStableId
                        || request.StrongboxDefinitionStableId
                            != context.TierStableId
                        || !string.Equals(
                            request.ContentFingerprint,
                            context.AlgorithmContentFingerprint,
                            StringComparison.Ordinal))
                    {
                        return SaveComponentValidationResultV1.Reject(
                            "strongbox-opening-context-conflict:"
                                + context.InstanceStableId);
                    }
                }

                bool terminalOpened = opening.Stage
                    == StrongboxOpeningStageV1.Opened;
                bool held = heldBoxes.ContainsKey(context.InstanceStableId);
                if (terminalOpened == held)
                {
                    return SaveComponentValidationResultV1.Reject(
                        terminalOpened
                            ? "opened-strongbox-still-held:"
                                + context.InstanceStableId
                            : "pending-strongbox-absent-from-holdings:"
                                + context.InstanceStableId);
                }
            }
            return SaveComponentValidationResultV1.Accept();
        }
    }
}
