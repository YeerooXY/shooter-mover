using System;
using System.Collections.Generic;
using ShooterMover.Application.Flow.Production;
using ShooterMover.Contracts.Flow.Session;
using UnityEngine;

namespace ShooterMover.UI.ProductionFlow
{
    public sealed class PlayerPrefsProductionFlowProfileStoreV1 :
        IProductionFlowProfileStoreV1
    {
        private const string Prefix = "shooter-mover.flow-ui-001.profile.";
        private const int ProfileSlotCount = 6;
        private const string ExistsKey = Prefix + "exists";
        private const string DisplayNameKey = Prefix + "display-name";
        private const string SchemaKey = Prefix + "schema";
        private const string ContractKey = Prefix + "contract";
        private const string CharacterKey = Prefix + "character";
        private const string ProfileKey = Prefix + "loadout-profile";
        private const string FingerprintKey = Prefix + "fingerprint";
        private const string UnboundEquipmentMarker = "unbound-position";

        public bool TryLoad(out ProductionFlowProfileRecordV1 record)
        {
            return TryLoad(0, out record);
        }

        public bool TryLoad(
            int slotIndex,
            out ProductionFlowProfileRecordV1 record)
        {
            ValidateSlotIndex(slotIndex);
            record = null;
            if (PlayerPrefs.GetInt(ExistsKeyFor(slotIndex), 0) != 1)
            {
                return false;
            }

            var slots = new List<PlayerRouteWeaponSlotEnvelopeV1>(
                PlayerRouteProfilePayloadV1.WeaponSlotCount);
            for (int index = 0;
                index < PlayerRouteProfilePayloadV1.WeaponSlotCount;
                index++)
            {
                string equipmentInstanceStableId =
                    PlayerPrefs.GetString(
                        WeaponInstanceKey(slotIndex, index));
                if (string.Equals(
                    equipmentInstanceStableId,
                    UnboundEquipmentMarker,
                    StringComparison.Ordinal))
                {
                    equipmentInstanceStableId = null;
                }

                slots.Add(new PlayerRouteWeaponSlotEnvelopeV1(
                    PlayerPrefs.GetString(
                        WeaponSlotKey(slotIndex, index)),
                    equipmentInstanceStableId));
            }

            var envelope = new PlayerRouteProfileEnvelopeV1(
                PlayerPrefs.GetInt(SchemaKeyFor(slotIndex), 0),
                PlayerPrefs.GetString(ContractKeyFor(slotIndex)),
                PlayerPrefs.GetString(CharacterKeyFor(slotIndex)),
                PlayerPrefs.GetString(ProfileKeyFor(slotIndex)),
                slots,
                PlayerPrefs.GetString(FingerprintKeyFor(slotIndex)));
            PlayerRouteProfileValidationResultV1 imported =
                PlayerRouteProfilePayloadV1.TryImport(envelope);
            string displayName = PlayerPrefs.GetString(
                DisplayNameKeyFor(slotIndex));
            if (!imported.IsValid || string.IsNullOrWhiteSpace(displayName))
            {
                Clear();
                return false;
            }

            record = new ProductionFlowProfileRecordV1(
                displayName,
                imported.Payload);
            return true;
        }

        public void Save(ProductionFlowProfileRecordV1 record)
        {
            Save(0, record);
        }

        public void Save(
            int slotIndex,
            ProductionFlowProfileRecordV1 record)
        {
            ValidateSlotIndex(slotIndex);
            if (record == null)
            {
                throw new ArgumentNullException(nameof(record));
            }
            PlayerRouteProfilePayloadV1 normalizedPayload =
                ProductionWeaponMountPolicyV1.NormalizeRoutePayload(
                    record.Payload);
            PlayerRouteProfileEnvelopeV1 envelope =
                normalizedPayload.ToEnvelope();

            PlayerPrefs.SetString(
                DisplayNameKeyFor(slotIndex),
                record.DisplayName);
            PlayerPrefs.SetInt(
                SchemaKeyFor(slotIndex),
                envelope.SchemaVersion);
            PlayerPrefs.SetString(
                ContractKeyFor(slotIndex),
                envelope.ContractStableId);
            PlayerPrefs.SetString(
                CharacterKeyFor(slotIndex),
                envelope.SelectedCharacterStableId);
            PlayerPrefs.SetString(
                ProfileKeyFor(slotIndex),
                envelope.LoadoutProfileStableId);
            PlayerPrefs.SetString(
                FingerprintKeyFor(slotIndex),
                envelope.Fingerprint);
            for (int index = 0;
                index < envelope.WeaponSlots.Count;
                index++)
            {
                PlayerPrefs.SetString(
                    WeaponSlotKey(slotIndex, index),
                    envelope.WeaponSlots[index].WeaponSlotStableId);
                PlayerPrefs.SetString(
                    WeaponInstanceKey(slotIndex, index),
                    envelope.WeaponSlots[index]
                        .EquipmentInstanceStableId
                        ?? UnboundEquipmentMarker);
            }

            PlayerPrefs.SetInt(ExistsKeyFor(slotIndex), 1);
            PlayerPrefs.Save();
        }

        public void Clear()
        {
            for (int slotIndex = 0;
                slotIndex < ProfileSlotCount;
                slotIndex++)
            {
                PlayerPrefs.DeleteKey(ExistsKeyFor(slotIndex));
                PlayerPrefs.DeleteKey(DisplayNameKeyFor(slotIndex));
                PlayerPrefs.DeleteKey(SchemaKeyFor(slotIndex));
                PlayerPrefs.DeleteKey(ContractKeyFor(slotIndex));
                PlayerPrefs.DeleteKey(CharacterKeyFor(slotIndex));
                PlayerPrefs.DeleteKey(ProfileKeyFor(slotIndex));
                PlayerPrefs.DeleteKey(FingerprintKeyFor(slotIndex));
                for (int index = 0;
                    index < PlayerRouteProfilePayloadV1.WeaponSlotCount;
                    index++)
                {
                    PlayerPrefs.DeleteKey(
                        WeaponSlotKey(slotIndex, index));
                    PlayerPrefs.DeleteKey(
                        WeaponInstanceKey(slotIndex, index));
                }
            }
            PlayerPrefs.Save();
        }

        private static string SlotKey(int index)
        {
            return Prefix + "slot-" + index + "-id";
        }

        private static string InstanceKey(int index)
        {
            return Prefix + "slot-" + index + "-instance";
        }

        private static string ProfileSlotPrefix(int slotIndex)
        {
            return Prefix + "character-slot-" + slotIndex + ".";
        }

        private static string ExistsKeyFor(int slotIndex)
        {
            return slotIndex == 0
                ? ExistsKey
                : ProfileSlotPrefix(slotIndex) + "exists";
        }

        private static string DisplayNameKeyFor(int slotIndex)
        {
            return slotIndex == 0
                ? DisplayNameKey
                : ProfileSlotPrefix(slotIndex) + "display-name";
        }

        private static string SchemaKeyFor(int slotIndex)
        {
            return slotIndex == 0
                ? SchemaKey
                : ProfileSlotPrefix(slotIndex) + "schema";
        }

        private static string ContractKeyFor(int slotIndex)
        {
            return slotIndex == 0
                ? ContractKey
                : ProfileSlotPrefix(slotIndex) + "contract";
        }

        private static string CharacterKeyFor(int slotIndex)
        {
            return slotIndex == 0
                ? CharacterKey
                : ProfileSlotPrefix(slotIndex) + "character";
        }

        private static string ProfileKeyFor(int slotIndex)
        {
            return slotIndex == 0
                ? ProfileKey
                : ProfileSlotPrefix(slotIndex) + "loadout-profile";
        }

        private static string FingerprintKeyFor(int slotIndex)
        {
            return slotIndex == 0
                ? FingerprintKey
                : ProfileSlotPrefix(slotIndex) + "fingerprint";
        }

        private static string WeaponSlotKey(
            int profileSlot,
            int weaponSlot)
        {
            return profileSlot == 0
                ? SlotKey(weaponSlot)
                : ProfileSlotPrefix(profileSlot)
                    + "weapon-slot-"
                    + weaponSlot
                    + "-id";
        }

        private static string WeaponInstanceKey(
            int profileSlot,
            int weaponSlot)
        {
            return profileSlot == 0
                ? InstanceKey(weaponSlot)
                : ProfileSlotPrefix(profileSlot)
                    + "weapon-slot-"
                    + weaponSlot
                    + "-instance";
        }

        private static void ValidateSlotIndex(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= ProfileSlotCount)
            {
                throw new ArgumentOutOfRangeException(nameof(slotIndex));
            }
        }
    }
}
