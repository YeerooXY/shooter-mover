using System;
using System.Collections.Generic;
using System.Linq;
using ShooterMover.Application.Flow.Production;
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

namespace ShooterMover.Application.Persistence.Components
{
    public static class PlayerAccountComponentSemantics
    {
        public static SaveComponentValidationResult Validate(
            PlayerAccountSnapshot account,
            Func<StableId, string> expectedStrongboxDefinitionFingerprint = null)
        {
            SaveComponentValidationResult aggregate =
                PlayerAccountAggregateCodec.Validate(account);
            if (!aggregate.Succeeded) return aggregate;
            for (int slot = 0; slot < account.CharacterSlots.Count; slot++)
            {
                CharacterInstanceSnapshot character = account.CharacterSlots[slot];
                if (character == null) continue;
                SaveComponentValidationResult result = ValidateCharacter(
                    character,
                    expectedStrongboxDefinitionFingerprint);
                if (!result.Succeeded) return result;
            }
            return SaveComponentValidationResult.Accept();
        }

        public static SaveComponentValidationResult ValidateCharacter(
            CharacterInstanceSnapshot character,
            Func<StableId, string> expectedStrongboxDefinitionFingerprint = null)
        {
            if (character == null)
            {
                return SaveComponentValidationResult.Reject(
                    "character-component-semantics-character-null");
            }

            PlayerHoldingsSnapshot holdings = null;
            WeaponHoldingsSnapshot weaponHoldings = null;
            WeaponMountLoadoutSnapshot weaponMountLoadout = null;
            InventoryLoadoutStateSnapshot loadout = null;
            StrongboxOpeningSnapshot strongboxes = null;
            GeneratedEquipmentAugmentSignatureSnapshot augmentSignatures = null;
            string error;
            SaveComponentSnapshot component;
            if (character.TryGetComponent(
                KnownSaveComponentDefinitions.PlayerHoldings().ComponentStableId,
                out component)
                && !KnownSaveComponentCodecs.PlayerHoldings.TryDecode(
                    component.CanonicalPayload,
                    out holdings,
                    out error))
            {
                return SaveComponentValidationResult.Reject(error);
            }

            string weaponHoldingsError;
            bool hasWeaponHoldings = WeaponHoldingsSaveComponent.TryRead(
                character,
                out weaponHoldings,
                out weaponHoldingsError);
            if (!hasWeaponHoldings
                && !string.IsNullOrEmpty(weaponHoldingsError))
            {
                return SaveComponentValidationResult.Reject(
                    weaponHoldingsError);
            }

            string weaponMountError;
            bool hasWeaponMountLoadout =
                WeaponMountLoadoutSaveComponent.TryRead(
                    character,
                    out weaponMountLoadout,
                    out weaponMountError);
            if (!hasWeaponMountLoadout
                && !string.IsNullOrEmpty(weaponMountError))
            {
                return SaveComponentValidationResult.Reject(
                    weaponMountError);
            }
            if (hasWeaponMountLoadout && !hasWeaponHoldings)
            {
                return SaveComponentValidationResult.Reject(
                    "weapon-mount-loadout-v2-requires-canonical-holdings");
            }

            if (character.TryGetComponent(
                KnownSaveComponentDefinitions.ExactInstanceLoadout().ComponentStableId,
                out component)
                && !KnownSaveComponentCodecs.ExactInstanceLoadout.TryDecode(
                    component.CanonicalPayload,
                    out loadout,
                    out error))
            {
                return SaveComponentValidationResult.Reject(error);
            }
            if (character.TryGetComponent(
                GeneratedEquipmentAugmentSignatureSaveComponent.Definition()
                    .ComponentStableId,
                out component)
                && !GeneratedEquipmentAugmentSignatureSaveComponent.Codec
                    .TryDecode(
                        component.CanonicalPayload,
                        out augmentSignatures,
                        out error))
            {
                return SaveComponentValidationResult.Reject(error);
            }
            if (character.TryGetComponent(
                KnownSaveComponentDefinitions.StrongboxState().ComponentStableId,
                out component)
                && !KnownSaveComponentCodecs.StrongboxState.TryDecode(
                    component.CanonicalPayload,
                    out strongboxes,
                    out error))
            {
                return SaveComponentValidationResult.Reject(error);
            }

            if (hasWeaponMountLoadout)
            {
                SaveComponentValidationResult mountValidation =
                    ValidateCanonicalMountLoadout(
                        character,
                        weaponHoldings,
                        weaponMountLoadout,
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
                    return SaveComponentValidationResult.Reject(
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
                    bool weaponSlot = index < InventoryLoadoutSlots.All.Count
                        && InventoryLoadoutSlots.All[index].Kind
                            == InventoryLoadoutSlotKind.Weapon;

                    if (hasWeaponMountLoadout && weaponSlot)
                    {
                        if (instanceId != null)
                        {
                            return SaveComponentValidationResult.Reject(
                                "legacy-weapon-slot-must-be-empty-when-mount-v2-present:"
                                    + loadout.Bindings[index].SlotStableId);
                        }
                        continue;
                    }
                    if (instanceId == null)
                    {
                        continue;
                    }

                    bool present = weaponSlot && weaponHoldings != null
                        ? weaponHoldings.Find(instanceId) != null
                        : equipmentIds.Contains(instanceId);
                    if (!present && !IsRetiredWeaponSaveInstance(instanceId))
                    {
                        return SaveComponentValidationResult.Reject(
                            weaponSlot && weaponHoldings != null
                                ? "loadout-weapon-instance-absent-from-canonical-holdings:"
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
                    return SaveComponentValidationResult.Reject(
                        "strongbox-state-requires-holdings-component");
                }
                SaveComponentValidationResult strongboxValidation =
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
                augmentSignatures);
        }

        private static SaveComponentValidationResult
            ValidateCanonicalMountLoadout(
                CharacterInstanceSnapshot character,
                WeaponHoldingsSnapshot weaponHoldings,
                WeaponMountLoadoutSnapshot weaponMountLoadout,
                InventoryLoadoutStateSnapshot legacyArmorLoadout)
        {
            if (legacyArmorLoadout == null)
            {
                return SaveComponentValidationResult.Reject(
                    "weapon-mount-loadout-v2-requires-armor-loadout-component");
            }
            try
            {
                var holdingsAuthority =
                    new WeaponHoldingsState(weaponHoldings);
                WeaponMountLayout layout =
                    WeaponMountPolicy.ResolveLayout(
                        character.ClassDefinitionStableId);
                var authority = new WeaponMountLoadoutState(
                    layout,
                    holdingsAuthority,
                    weaponMountLoadout);
                if (authority.ExportSnapshot().Fingerprint
                    != weaponMountLoadout.Fingerprint)
                {
                    return SaveComponentValidationResult.Reject(
                        "weapon-mount-loadout-v2-semantic-fingerprint-mismatch");
                }
            }
            catch (Exception exception)
                when (exception is ArgumentException
                    || exception is InvalidOperationException)
            {
                return SaveComponentValidationResult.Reject(
                    "weapon-mount-loadout-v2-semantic-invalid:"
                        + exception.Message);
            }
            return SaveComponentValidationResult.Accept();
        }

        private static SaveComponentValidationResult ValidateStrongboxes(
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
                    return SaveComponentValidationResult.Reject(
                        "strongbox-opening-box-identity-duplicate:" + boxId);
                }
                openingsByBox.Add(boxId, opening);
            }

            foreach (KeyValuePair<StableId, StrongboxOpeningRecordSnapshot> pair
                in openingsByBox)
            {
                if (!contexts.ContainsKey(pair.Key))
                {
                    return SaveComponentValidationResult.Reject(
                        "strongbox-opening-context-missing:" + pair.Key);
                }
            }

            foreach (KeyValuePair<StableId, UniqueHoldingSnapshot> pair
                in heldBoxes)
            {
                StrongboxInstanceContext context;
                if (!contexts.TryGetValue(pair.Key, out context))
                {
                    return SaveComponentValidationResult.Reject(
                        "held-strongbox-registration-missing:" + pair.Key);
                }
                if (context.TierStableId != pair.Value.DefinitionStableId)
                {
                    return SaveComponentValidationResult.Reject(
                        "held-strongbox-tier-conflict:" + pair.Key);
                }
                if (context.CollectionProvenanceStableId
                    != pair.Value.Provenance.GrantStableId)
                {
                    return SaveComponentValidationResult.Reject(
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
                    return SaveComponentValidationResult.Reject(
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
                        return SaveComponentValidationResult.Reject(
                            "registered-unopened-strongbox-absent-from-holdings:"
                                + context.InstanceStableId);
                    }
                    continue;
                }

                if (opening.Command.StrongboxInstanceStableId
                    != context.InstanceStableId)
                {
                    return SaveComponentValidationResult.Reject(
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
                        return SaveComponentValidationResult.Reject(
                            "strongbox-opening-context-conflict:"
                                + context.InstanceStableId);
                    }
                }

                bool terminalOpened = opening.Stage
                    == StrongboxOpeningStage.Opened;
                bool held = heldBoxes.ContainsKey(context.InstanceStableId);
                if (terminalOpened == held)
                {
                    return SaveComponentValidationResult.Reject(
                        terminalOpened
                            ? "opened-strongbox-still-held:"
                                + context.InstanceStableId
                            : "pending-strongbox-absent-from-holdings:"
                                + context.InstanceStableId);
                }
            }
            return SaveComponentValidationResult.Accept();
        }

        private static SaveComponentValidationResult
            ValidateGeneratedAugmentSignatures(
                PlayerHoldingsSnapshot holdings,
                StrongboxOpeningSnapshot strongboxes,
                GeneratedEquipmentAugmentSignatureSnapshot signatures)
        {
            if (signatures == null)
            {
                return SaveComponentValidationResult.Accept();
            }
            if (holdings == null)
            {
                return SaveComponentValidationResult.Reject(
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
                    return SaveComponentValidationResult.Reject(
                        "committed-augment-signature-equipment-not-held:"
                            + signature.EquipmentInstanceStableId);
                }
                if (!OpeningContainsSignatureEquipment(strongboxes, signature))
                {
                    return SaveComponentValidationResult.Reject(
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
                    return SaveComponentValidationResult.Reject(
                        "staged-augment-signature-equipment-already-held:"
                            + signature.EquipmentInstanceStableId);
                }
                if (!OpeningContainsSignatureEquipment(strongboxes, signature))
                {
                    return SaveComponentValidationResult.Reject(
                        "staged-augment-signature-opening-payload-missing:"
                            + signature.EquipmentInstanceStableId);
                }
            }
            return SaveComponentValidationResult.Accept();
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

        private static bool IsRetiredWeaponSaveInstance(StableId instanceId)
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
