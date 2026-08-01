using System;
using System.Collections.Generic;
using System.Linq;
using ShooterMover.Application.Flow.Game;
using ShooterMover.Application.Inventory.LoadoutScreen;
using ShooterMover.Application.Rewards.Strongboxes;
using ShooterMover.Contracts.Holdings;
using ShooterMover.Contracts.Rewards;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Holdings;
using ShooterMover.Domain.Persistence.Accounts;
using ShooterMover.Domain.Rewards.Model;
using ShooterMover.Domain.Rewards.Strongboxes;
using ShooterMover.Domain.Shops;

namespace ShooterMover.Application.Persistence.SaveParts
{
    public static class GameSaveRules
    {
        public static SavePartValidationResult Validate(
            PlayerAccountSnapshot account,
            Func<StableId, string> expectedStrongboxDefinitionFingerprint = null)
        {
            SavePartValidationResult aggregate =
                PlayerAccountAggregateCodec.Validate(account);
            if (!aggregate.Succeeded) return aggregate;
            for (int slot = 0; slot < account.CharacterSlots.Count; slot++)
            {
                CharacterInstanceSnapshot character = account.CharacterSlots[slot];
                if (character == null) continue;
                SavePartValidationResult result = ValidateCharacter(
                    character,
                    expectedStrongboxDefinitionFingerprint);
                if (!result.Succeeded) return result;
            }
            return SavePartValidationResult.Accept();
        }

        public static SavePartValidationResult ValidateCharacter(
            CharacterInstanceSnapshot character,
            Func<StableId, string> expectedStrongboxDefinitionFingerprint = null)
        {
            if (character == null)
            {
                return SavePartValidationResult.Reject(
                    "character-component-semantics-character-null");
            }

            PlayerHoldingsSnapshot holdings = null;
            GunInventorySnapshot gunHoldings = null;
            LoadoutSnapshot gunMountLoadout = null;
            InventoryLoadoutStateSnapshot loadout = null;
            StrongboxOpeningSnapshot strongboxes = null;
            GeneratedEquipmentAugmentSignatureSnapshot augmentSignatures = null;
            ShopReceiptSnapshot shopReceipts = null;
            string error;
            SavePartSnapshot component;
            if (character.TryGetComponent(
                GameSaveParts.PlayerHoldings().ComponentStableId,
                out component)
                && !GameSaveFormats.PlayerHoldings.TryDecode(
                    component.CanonicalPayload,
                    out holdings,
                    out error))
            {
                return SavePartValidationResult.Reject(error);
            }

            string gunHoldingsError;
            bool hasGunInventory = GunInventorySavePart.TryRead(
                character,
                out gunHoldings,
                out gunHoldingsError);
            if (!hasGunInventory
                && !string.IsNullOrEmpty(gunHoldingsError))
            {
                return SavePartValidationResult.Reject(
                    gunHoldingsError);
            }

            string gunMountError;
            bool hasLoadout =
                LoadoutSavePart.TryRead(
                    character,
                    out gunMountLoadout,
                    out gunMountError);
            if (!hasLoadout
                && !string.IsNullOrEmpty(gunMountError))
            {
                return SavePartValidationResult.Reject(
                    gunMountError);
            }
            if (hasLoadout && !hasGunInventory)
            {
                return SavePartValidationResult.Reject(
                    "gun-mount-loadout-v2-requires-canonical-holdings");
            }

            if (character.TryGetComponent(
                GameSaveParts.ExactInstanceLoadout().ComponentStableId,
                out component)
                && !GameSaveFormats.ExactInstanceLoadout.TryDecode(
                    component.CanonicalPayload,
                    out loadout,
                    out error))
            {
                return SavePartValidationResult.Reject(error);
            }
            if (character.TryGetComponent(
                GunAugmentSavePart.Definition()
                    .ComponentStableId,
                out component)
                && !GunAugmentSavePart.Codec
                    .TryDecode(
                        component.CanonicalPayload,
                        out augmentSignatures,
                        out error))
            {
                return SavePartValidationResult.Reject(error);
            }
            if (character.TryGetComponent(
                ShopPurchaseSavePart.Definition()
                    .ComponentStableId,
                out component)
                && !ShopPurchaseSavePart.Codec
                    .TryDecode(
                        component.CanonicalPayload,
                        out shopReceipts,
                        out error))
            {
                return SavePartValidationResult.Reject(error);
            }
            if (character.TryGetComponent(
                GameSaveParts.StrongboxState().ComponentStableId,
                out component)
                && !GameSaveFormats.StrongboxState.TryDecode(
                    component.CanonicalPayload,
                    out strongboxes,
                    out error))
            {
                return SavePartValidationResult.Reject(error);
            }

            if (hasLoadout)
            {
                SavePartValidationResult mountValidation =
                    ValidateCanonicalMountLoadout(
                        character,
                        gunHoldings,
                        gunMountLoadout,
                        loadout);
                if (!mountValidation.Succeeded)
                {
                    return mountValidation;
                }
            }

            if (loadout != null)
            {
                if (holdings == null)
                {
                    return SavePartValidationResult.Reject(
                        "loadout-requires-holdings-component");
                }
                var equipmentIds = new HashSet<StableId>(
                    holdings.UniqueHoldings
                        .Where(item => item.RewardKind
                            == RewardGrantKind.EquipmentReference)
                        .Select(item => item.InstanceStableId));
                for (int index = 0; index < loadout.Bindings.Count; index++)
                {
                    StableId instanceId = loadout.Bindings[index]
                        .EquipmentInstanceStableId;
                    bool gunSlot = index < InventoryLoadoutSlots.All.Count
                        && InventoryLoadoutSlots.All[index].Kind
                            == InventoryLoadoutSlotKind.Gun;

                    if (hasLoadout && gunSlot)
                    {
                        if (instanceId != null)
                        {
                            return SavePartValidationResult.Reject(
                                "legacy-gun-slot-must-be-empty-when-mount-v2-present:"
                                    + loadout.Bindings[index].SlotStableId);
                        }
                        continue;
                    }
                    if (instanceId == null)
                    {
                        continue;
                    }

                    bool present = gunSlot && gunHoldings != null
                        ? gunHoldings.Find(instanceId) != null
                        : equipmentIds.Contains(instanceId);
                    if (!present && !IsRetiredGunSaveInstance(instanceId))
                    {
                        return SavePartValidationResult.Reject(
                            gunSlot && gunHoldings != null
                                ? "loadout-gun-instance-absent-from-canonical-holdings:"
                                    + instanceId
                                : "loadout-equipment-instance-absent-from-holdings:"
                                    + instanceId);
                    }
                }
            }

            if (strongboxes != null)
            {
                if (holdings == null)
                {
                    return SavePartValidationResult.Reject(
                        "strongbox-state-requires-holdings-component");
                }
                SavePartValidationResult strongboxValidation =
                    ValidateStrongboxes(
                        holdings,
                        strongboxes,
                        expectedStrongboxDefinitionFingerprint);
                if (!strongboxValidation.Succeeded)
                {
                    return strongboxValidation;
                }
            }

            return ValidateGeneratedAugmentSignatures(
                holdings,
                strongboxes,
                shopReceipts,
                augmentSignatures);
        }

        private static SavePartValidationResult
            ValidateCanonicalMountLoadout(
                CharacterInstanceSnapshot character,
                GunInventorySnapshot gunHoldings,
                LoadoutSnapshot gunMountLoadout,
                InventoryLoadoutStateSnapshot legacyArmorLoadout)
        {
            if (legacyArmorLoadout == null)
            {
                return SavePartValidationResult.Reject(
                    "gun-mount-loadout-v2-requires-armor-loadout-component");
            }
            try
            {
                var holdingsAuthority =
                    new GunInventoryState(gunHoldings);
                GunSlots layout =
                    GunMountPolicy.ResolveLayout(
                        character.ClassDefinitionStableId);
                var authority = new LoadoutState(
                    layout,
                    holdingsAuthority,
                    gunMountLoadout);
                if (authority.ExportSnapshot().Fingerprint
                    != gunMountLoadout.Fingerprint)
                {
                    return SavePartValidationResult.Reject(
                        "gun-mount-loadout-v2-semantic-fingerprint-mismatch");
                }
            }
            catch (Exception exception)
                when (exception is ArgumentException
                    || exception is InvalidOperationException)
            {
                return SavePartValidationResult.Reject(
                    "gun-mount-loadout-v2-semantic-invalid:"
                        + exception.Message);
            }
            return SavePartValidationResult.Accept();
        }

        private static SavePartValidationResult ValidateStrongboxes(
            PlayerHoldingsSnapshot holdings,
            StrongboxOpeningSnapshot strongboxes,
            Func<StableId, string> expectedDefinitionFingerprint)
        {
            var heldBoxes = holdings.UniqueHoldings
                .Where(item => item.RewardKind == RewardGrantKind.Strongbox)
                .ToDictionary(item => item.InstanceStableId, item => item);
            var contexts = strongboxes.Contexts.ToDictionary(
                item => item.InstanceStableId,
                item => item);
            var openingsByBox = new Dictionary<
                StableId,
                StrongboxOpeningRecordSnapshot>();
            for (int index = 0; index < strongboxes.Openings.Count; index++)
            {
                StrongboxOpeningRecordSnapshot opening =
                    strongboxes.Openings[index];
                StableId boxId = opening.Command.StrongboxInstanceStableId;
                if (openingsByBox.ContainsKey(boxId))
                {
                    return SavePartValidationResult.Reject(
                        "strongbox-opening-box-identity-duplicate:" + boxId);
                }
                openingsByBox.Add(boxId, opening);
            }

            foreach (KeyValuePair<StableId, StrongboxOpeningRecordSnapshot> pair
                in openingsByBox)
            {
                if (!contexts.ContainsKey(pair.Key))
                {
                    return SavePartValidationResult.Reject(
                        "strongbox-opening-context-missing:" + pair.Key);
                }
            }

            foreach (KeyValuePair<StableId, UniqueHoldingSnapshot> pair
                in heldBoxes)
            {
                StrongboxInstanceContext context;
                if (!contexts.TryGetValue(pair.Key, out context))
                {
                    return SavePartValidationResult.Reject(
                        "held-strongbox-registration-missing:" + pair.Key);
                }
                if (context.TierStableId != pair.Value.DefinitionStableId)
                {
                    return SavePartValidationResult.Reject(
                        "held-strongbox-tier-conflict:" + pair.Key);
                }
                if (context.CollectionProvenanceStableId
                    != pair.Value.Provenance.GrantStableId)
                {
                    return SavePartValidationResult.Reject(
                        "held-strongbox-provenance-conflict:" + pair.Key);
                }
            }

            foreach (StrongboxInstanceContext context in strongboxes.Contexts)
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
                    return SavePartValidationResult.Reject(
                        "strongbox-definition-fingerprint-conflict:"
                            + context.InstanceStableId);
                }

                StrongboxOpeningRecordSnapshot opening;
                if (!openingsByBox.TryGetValue(
                    context.InstanceStableId,
                    out opening))
                {
                    if (!heldBoxes.ContainsKey(context.InstanceStableId))
                    {
                        return SavePartValidationResult.Reject(
                            "registered-unopened-strongbox-absent-from-holdings:"
                                + context.InstanceStableId);
                    }
                    continue;
                }

                if (opening.Command.StrongboxInstanceStableId
                    != context.InstanceStableId)
                {
                    return SavePartValidationResult.Reject(
                        "strongbox-opening-instance-conflict:"
                            + context.InstanceStableId);
                }
                if (opening.GeneratedOutcome != null)
                {
                    StrongboxOpeningRequest request =
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
                        return SavePartValidationResult.Reject(
                            "strongbox-opening-context-conflict:"
                                + context.InstanceStableId);
                    }
                }

                bool terminalOpened = opening.Stage
                    == StrongboxOpeningStage.Opened;
                bool held = heldBoxes.ContainsKey(context.InstanceStableId);
                if (terminalOpened == held)
                {
                    return SavePartValidationResult.Reject(
                        terminalOpened
                            ? "opened-strongbox-still-held:"
                                + context.InstanceStableId
                            : "pending-strongbox-absent-from-holdings:"
                                + context.InstanceStableId);
                }
            }
            return SavePartValidationResult.Accept();
        }

        private static SavePartValidationResult
            ValidateGeneratedAugmentSignatures(
                PlayerHoldingsSnapshot holdings,
                StrongboxOpeningSnapshot strongboxes,
                ShopReceiptSnapshot shopReceipts,
                GeneratedEquipmentAugmentSignatureSnapshot signatures)
        {
            if (signatures == null)
            {
                return SavePartValidationResult.Accept();
            }
            if (holdings == null)
            {
                return SavePartValidationResult.Reject(
                    "generated-augment-signatures-require-holdings-component");
            }

            var heldEquipment = new HashSet<StableId>(
                holdings.UniqueHoldings
                    .Where(item => item.RewardKind
                        == RewardGrantKind.EquipmentReference)
                    .Select(item => item.InstanceStableId));

            for (int index = 0; index < signatures.Committed.Count; index++)
            {
                GeneratedEquipmentAugmentSignature signature =
                    signatures.Committed[index];
                if (!heldEquipment.Contains(
                        signature.EquipmentInstanceStableId))
                {
                    return SavePartValidationResult.Reject(
                        "committed-augment-signature-equipment-not-held:"
                            + signature.EquipmentInstanceStableId);
                }
                if (!OpeningContainsSignatureEquipment(strongboxes, signature)
                    && !ShopReceiptContainsSignatureSource(
                        shopReceipts,
                        signature))
                {
                    return SavePartValidationResult.Reject(
                        "committed-augment-signature-opening-payload-missing:"
                            + signature.EquipmentInstanceStableId);
                }
            }

            for (int index = 0; index < signatures.Staged.Count; index++)
            {
                GeneratedEquipmentAugmentSignature signature =
                    signatures.Staged[index];
                if (heldEquipment.Contains(signature.EquipmentInstanceStableId))
                {
                    return SavePartValidationResult.Reject(
                        "staged-augment-signature-equipment-already-held:"
                            + signature.EquipmentInstanceStableId);
                }
                if (!OpeningContainsSignatureEquipment(strongboxes, signature))
                {
                    return SavePartValidationResult.Reject(
                        "staged-augment-signature-opening-payload-missing:"
                            + signature.EquipmentInstanceStableId);
                }
            }
            return SavePartValidationResult.Accept();
        }

        private static bool OpeningContainsSignatureEquipment(
            StrongboxOpeningSnapshot strongboxes,
            GeneratedEquipmentAugmentSignature signature)
        {
            if (strongboxes == null || signature == null)
            {
                return false;
            }
            for (int openingIndex = 0;
                 openingIndex < strongboxes.Openings.Count;
                 openingIndex++)
            {
                StrongboxOpeningRecordSnapshot opening =
                    strongboxes.Openings[openingIndex];
                if (opening.Command.StrongboxInstanceStableId
                        != signature.SourceStrongboxInstanceStableId
                    || opening.GeneratedOutcome == null)
                {
                    continue;
                }
                for (int payloadIndex = 0;
                     payloadIndex < opening.GeneratedOutcome.Payloads.Count;
                     payloadIndex++)
                {
                    for (int equipmentIndex = 0;
                         equipmentIndex
                            < opening.GeneratedOutcome.Payloads[payloadIndex]
                                .EquipmentInstances.Count;
                         equipmentIndex++)
                    {
                        EquipmentInstance equipment =
                            opening.GeneratedOutcome.Payloads[payloadIndex]
                                .EquipmentInstances[equipmentIndex];
                        if (equipment != null
                            && equipment.InstanceId
                                == signature.EquipmentInstanceStableId)
                        {
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        private static bool ShopReceiptContainsSignatureSource(
            ShopReceiptSnapshot shopReceipts,
            GeneratedEquipmentAugmentSignature signature)
        {
            if (shopReceipts == null || signature == null)
            {
                return false;
            }
            string source = signature.SourceStrongboxInstanceStableId
                .ToString();
            if (!source.StartsWith("shopstock.", StringComparison.Ordinal))
            {
                return false;
            }
            for (int index = 0;
                 index < shopReceipts.Receipts.Count;
                 index++)
            {
                if (shopReceipts.Receipts[index].StockEntryStableId
                    == signature.SourceStrongboxInstanceStableId)
                {
                    return true;
                }
            }
            return false;
        }

        private static bool IsRetiredGunSaveInstance(StableId instanceId)
        {
            if (instanceId == null)
            {
                return false;
            }
            string value = instanceId.ToString();
            return value == "equipment-instance.flow-draft-slot-1"
                || value == "equipment-instance.flow-draft-slot-2"
                || value == "equipment-instance.flow-draft-slot-3"
                || value == "equipment-instance.flow-draft-slot-4"
                || value == "equipment-instance.production-starter-ricochet"
                || value.StartsWith(
                    "equipment-instance.retired-starter-",
                    StringComparison.Ordinal);
        }
    }
}
