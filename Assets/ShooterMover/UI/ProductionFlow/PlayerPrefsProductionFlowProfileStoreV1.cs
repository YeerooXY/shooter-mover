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
        private const string ExistsKey = Prefix + "exists";
        private const string DisplayNameKey = Prefix + "display-name";
        private const string SchemaKey = Prefix + "schema";
        private const string ContractKey = Prefix + "contract";
        private const string CharacterKey = Prefix + "character";
        private const string ProfileKey = Prefix + "loadout-profile";
        private const string FingerprintKey = Prefix + "fingerprint";

        public bool TryLoad(out ProductionFlowProfileRecordV1 record)
        {
            record = null;
            if (PlayerPrefs.GetInt(ExistsKey, 0) != 1)
            {
                return false;
            }

            var slots = new List<PlayerRouteWeaponSlotEnvelopeV1>(
                PlayerRouteProfilePayloadV1.WeaponSlotCount);
            for (int index = 0;
                index < PlayerRouteProfilePayloadV1.WeaponSlotCount;
                index++)
            {
                slots.Add(new PlayerRouteWeaponSlotEnvelopeV1(
                    PlayerPrefs.GetString(SlotKey(index)),
                    PlayerPrefs.GetString(InstanceKey(index))));
            }

            var envelope = new PlayerRouteProfileEnvelopeV1(
                PlayerPrefs.GetInt(SchemaKey, 0),
                PlayerPrefs.GetString(ContractKey),
                PlayerPrefs.GetString(CharacterKey),
                PlayerPrefs.GetString(ProfileKey),
                slots,
                PlayerPrefs.GetString(FingerprintKey));
            PlayerRouteProfileValidationResultV1 imported =
                PlayerRouteProfilePayloadV1.TryImport(envelope);
            string displayName = PlayerPrefs.GetString(DisplayNameKey);
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
            if (record == null) throw new ArgumentNullException(nameof(record));
            PlayerRouteProfileEnvelopeV1 envelope = record.Payload.ToEnvelope();

            PlayerPrefs.SetString(DisplayNameKey, record.DisplayName);
            PlayerPrefs.SetInt(SchemaKey, envelope.SchemaVersion);
            PlayerPrefs.SetString(ContractKey, envelope.ContractStableId);
            PlayerPrefs.SetString(
                CharacterKey,
                envelope.SelectedCharacterStableId);
            PlayerPrefs.SetString(
                ProfileKey,
                envelope.LoadoutProfileStableId);
            PlayerPrefs.SetString(FingerprintKey, envelope.Fingerprint);
            for (int index = 0; index < envelope.WeaponSlots.Count; index++)
            {
                PlayerPrefs.SetString(
                    SlotKey(index),
                    envelope.WeaponSlots[index].WeaponSlotStableId);
                PlayerPrefs.SetString(
                    InstanceKey(index),
                    envelope.WeaponSlots[index].EquipmentInstanceStableId);
            }

            PlayerPrefs.SetInt(ExistsKey, 1);
            PlayerPrefs.Save();
        }

        public void Clear()
        {
            PlayerPrefs.DeleteKey(ExistsKey);
            PlayerPrefs.DeleteKey(DisplayNameKey);
            PlayerPrefs.DeleteKey(SchemaKey);
            PlayerPrefs.DeleteKey(ContractKey);
            PlayerPrefs.DeleteKey(CharacterKey);
            PlayerPrefs.DeleteKey(ProfileKey);
            PlayerPrefs.DeleteKey(FingerprintKey);
            for (int index = 0;
                index < PlayerRouteProfilePayloadV1.WeaponSlotCount;
                index++)
            {
                PlayerPrefs.DeleteKey(SlotKey(index));
                PlayerPrefs.DeleteKey(InstanceKey(index));
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
    }
}
