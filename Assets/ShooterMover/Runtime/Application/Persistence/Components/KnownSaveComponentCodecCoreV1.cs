using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using ShooterMover.Application.Inventory.LoadoutScreen;
using ShooterMover.Application.Rewards.Generation;
using ShooterMover.Application.Rewards.Strongboxes;
using ShooterMover.Contracts.Economy;
using ShooterMover.Contracts.Holdings;
using ShooterMover.Contracts.Rewards;
using ShooterMover.Contracts.Rewards.Application;
using ShooterMover.Contracts.Progression.Experience;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Economy.Ledger;
using ShooterMover.Domain.Economy.Money;
using ShooterMover.Domain.Economy.Scrap;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Holdings;
using ShooterMover.Domain.Persistence.Accounts;
using ShooterMover.Domain.Progression.Context;
using ShooterMover.Domain.Progression.Experience;
using ShooterMover.Domain.Progression.Skills;
using ShooterMover.Domain.Rewards.Application;
using ShooterMover.Domain.Rewards.Generation;
using ShooterMover.Domain.Rewards.Model;
using ShooterMover.Domain.Rewards.Strongboxes;

namespace ShooterMover.Application.Persistence.Components
{
    public abstract class ExplicitSaveComponentCodecV1<TSnapshot> :
        ISaveComponentPayloadCodecV1<TSnapshot>
        where TSnapshot : class
    {
        protected ExplicitSaveComponentCodecV1(string contractId)
        {
            ContractId = contractId
                ?? throw new ArgumentNullException(nameof(contractId));
        }

        public string ContractId { get; }

        public string Encode(TSnapshot snapshot)
        {
            SaveComponentValidationResultV1 validation = Validate(snapshot);
            if (validation == null || !validation.Succeeded)
            {
                throw new ArgumentException(
                    validation == null
                        ? "component-codec-validation-result-null"
                        : validation.RejectionCode,
                    nameof(snapshot));
            }
            string payload = CanonicalNodeCodecV1.Encode(EncodeNode(snapshot));
            if (Encoding.UTF8.GetByteCount(payload)
                > SavePersistenceLimitsV1.MaximumComponentPayloadBytes)
            {
                throw new CanonicalPayloadExceptionV1(
                    "component-payload-too-large");
            }
            return payload;
        }

        public bool TryDecode(
            string canonicalPayload,
            out TSnapshot snapshot,
            out string rejectionCode)
        {
            snapshot = null;
            CanonicalNodeV1 node;
            if (!CanonicalNodeCodecV1.TryDecode(
                canonicalPayload,
                SavePersistenceLimitsV1.MaximumComponentPayloadBytes,
                out node,
                out rejectionCode))
            {
                return false;
            }

            try
            {
                snapshot = DecodeNode(node);
                SaveComponentValidationResultV1 validation = Validate(snapshot);
                if (validation == null || !validation.Succeeded)
                {
                    snapshot = null;
                    rejectionCode = validation == null
                        ? "component-codec-validation-result-null"
                        : validation.RejectionCode;
                    return false;
                }
                string rebuilt = CanonicalNodeCodecV1.Encode(
                    EncodeNode(snapshot));
                if (!string.Equals(
                    rebuilt,
                    canonicalPayload,
                    StringComparison.Ordinal))
                {
                    snapshot = null;
                    rejectionCode = "component-payload-not-canonical";
                    return false;
                }
                rejectionCode = string.Empty;
                return true;
            }
            catch (CanonicalPayloadExceptionV1 exception)
            {
                snapshot = null;
                rejectionCode = exception.RejectionCode;
                return false;
            }
            catch (ArgumentException)
            {
                snapshot = null;
                rejectionCode = "component-payload-semantic-invalid";
                return false;
            }
            catch (InvalidOperationException)
            {
                snapshot = null;
                rejectionCode = "component-payload-semantic-invalid";
                return false;
            }
            catch (OverflowException)
            {
                snapshot = null;
                rejectionCode = "component-payload-number-overflow";
                return false;
            }
        }

        public abstract SaveComponentValidationResultV1 Validate(
            TSnapshot snapshot);

        protected abstract CanonicalNodeV1 EncodeNode(TSnapshot snapshot);

        protected abstract TSnapshot DecodeNode(CanonicalNodeV1 node);

        protected static SaveComponentValidationResultV1 FingerprintResult(
            bool valid,
            string rejectionCode)
        {
            return valid
                ? SaveComponentValidationResultV1.Accept()
                : SaveComponentValidationResultV1.Reject(rejectionCode);
        }
    }

    public static class KnownSaveComponentCodecsV1
    {
        public static readonly PlayerExperienceComponentCodecV1
            PlayerExperience = new PlayerExperienceComponentCodecV1();

        public static readonly PlayerHoldingsComponentCodecV1
            PlayerHoldings = new PlayerHoldingsComponentCodecV1();

        public static readonly MoneyWalletComponentCodecV1
            MoneyWallet = new MoneyWalletComponentCodecV1();

        public static readonly ScrapWalletComponentCodecV1
            ScrapWallet = new ScrapWalletComponentCodecV1();

        public static readonly RankedSkillAllocationComponentCodecV1
            RankedSkillAllocation =
                new RankedSkillAllocationComponentCodecV1();

        public static readonly ExactInstanceLoadoutComponentCodecV1
            ExactInstanceLoadout =
                new ExactInstanceLoadoutComponentCodecV1();

        public static readonly StrongboxOpeningComponentCodecV1
            StrongboxState = new StrongboxOpeningComponentCodecV1();
    }

    internal static class ExplicitCodecValuesV1
    {
        public static StableId RequiredId(CanonicalNodeV1 node)
        {
            StableId output;
            if (!StableId.TryParse(
                CanonicalValueV1.ReadRequiredString(node),
                out output))
            {
                throw new CanonicalPayloadExceptionV1(
                    "component-stable-id-invalid");
            }
            return output;
        }

        public static StableId OptionalId(CanonicalNodeV1 node)
        {
            string value = CanonicalValueV1.ReadOptionalString(node);
            if (value == null) return null;
            StableId output;
            if (!StableId.TryParse(value, out output))
            {
                throw new CanonicalPayloadExceptionV1(
                    "component-stable-id-invalid");
            }
            return output;
        }

        public static CanonicalNodeV1 Id(StableId value)
        {
            return value == null
                ? CanonicalNodeV1.Null()
                : CanonicalNodeV1.ScalarValue(value.ToString());
        }

        public static CanonicalNodeV1 RequiredIdNode(StableId value)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            return CanonicalNodeV1.ScalarValue(value.ToString());
        }

        public static TEnum EnumValue<TEnum>(CanonicalNodeV1 node)
            where TEnum : struct
        {
            int numeric = CanonicalValueV1.ReadInt32(node);
            TEnum value = (TEnum)Enum.ToObject(typeof(TEnum), numeric);
            if (!Enum.IsDefined(typeof(TEnum), value))
            {
                throw new CanonicalPayloadExceptionV1(
                    "component-enum-invalid");
            }
            return value;
        }

        public static CanonicalNodeV1 EnumNode<TEnum>(TEnum value)
            where TEnum : struct
        {
            return CanonicalValueV1.Int32(Convert.ToInt32(
                value,
                CultureInfo.InvariantCulture));
        }

        public static List<T> DecodeList<T>(
            CanonicalNodeV1 node,
            Func<CanonicalNodeV1, T> decode)
        {
            IReadOnlyList<CanonicalNodeV1> values =
                CanonicalValueV1.ReadList(node);
            var output = new List<T>(values.Count);
            for (int index = 0; index < values.Count; index++)
            {
                output.Add(decode(values[index]));
            }
            return output;
        }

        public static CanonicalNodeV1 EncodeList<T>(
            IEnumerable<T> values,
            Func<T, CanonicalNodeV1> encode)
        {
            var output = new List<CanonicalNodeV1>();
            foreach (T value in values ?? throw new ArgumentNullException(nameof(values)))
            {
                output.Add(encode(value));
            }
            return CanonicalNodeV1.List(output);
        }

        public static CanonicalNodeV1 OptionalObject<T>(
            T value,
            Func<T, CanonicalNodeV1> encode)
            where T : class
        {
            return value == null ? CanonicalNodeV1.Null() : encode(value);
        }

        public static T OptionalObjectValue<T>(
            CanonicalNodeV1 node,
            Func<CanonicalNodeV1, T> decode)
            where T : class
        {
            return node.Kind == CanonicalNodeKindV1.Null
                ? null
                : decode(node);
        }
    }

    public static class PlayerAccountAggregateCodecV1
    {
        public static string Encode(PlayerAccountSnapshotV1 account)
        {
            SaveComponentValidationResultV1 validation = Validate(account);
            if (!validation.Succeeded)
            {
                throw new ArgumentException(validation.RejectionCode, nameof(account));
            }
            string payload = CanonicalNodeCodecV1.Encode(EncodeAccount(account));
            if (Encoding.UTF8.GetByteCount(payload)
                > SavePersistenceLimitsV1.MaximumAccountPayloadBytes)
            {
                throw new CanonicalPayloadExceptionV1(
                    "account-payload-too-large");
            }
            return payload;
        }

        public static bool TryDecode(
            string payload,
            out PlayerAccountSnapshotV1 account,
            out string rejectionCode)
        {
            account = null;
            CanonicalNodeV1 node;
            if (!CanonicalNodeCodecV1.TryDecode(
                payload,
                SavePersistenceLimitsV1.MaximumAccountPayloadBytes,
                out node,
                out rejectionCode))
            {
                return false;
            }
            try
            {
                account = DecodeAccount(node);
                SaveComponentValidationResultV1 validation = Validate(account);
                if (!validation.Succeeded)
                {
                    account = null;
                    rejectionCode = validation.RejectionCode;
                    return false;
                }
                if (!string.Equals(
                    Encode(account),
                    payload,
                    StringComparison.Ordinal))
                {
                    account = null;
                    rejectionCode = "account-payload-not-canonical";
                    return false;
                }
                rejectionCode = string.Empty;
                return true;
            }
            catch (CanonicalPayloadExceptionV1 exception)
            {
                account = null;
                rejectionCode = exception.RejectionCode;
                return false;
            }
            catch (ArgumentException)
            {
                account = null;
                rejectionCode = "account-payload-semantic-invalid";
                return false;
            }
            catch (OverflowException)
            {
                account = null;
                rejectionCode = "account-payload-number-overflow";
                return false;
            }
        }

        public static SaveComponentValidationResultV1 Validate(
            PlayerAccountSnapshotV1 account)
        {
            if (account == null)
            {
                return SaveComponentValidationResultV1.Reject(
                    "account-snapshot-null");
            }
            if (account.SchemaVersion != PlayerAccountSnapshotV1.CurrentSchemaVersion)
            {
                return SaveComponentValidationResultV1.Reject(
                    "account-snapshot-schema-unsupported");
            }
            if (!string.Equals(
                account.Fingerprint,
                PlayerAccountSnapshotFingerprintV1.Hash(
                    account.ToCanonicalString()),
                StringComparison.Ordinal))
            {
                return SaveComponentValidationResultV1.Reject(
                    "account-snapshot-fingerprint-mismatch");
            }
            if (account.CharacterSlots.Count
                != PlayerAccountSnapshotV1.CharacterSlotCount)
            {
                return SaveComponentValidationResultV1.Reject(
                    "account-character-slot-count-invalid");
            }

            for (int slot = 0; slot < account.CharacterSlots.Count; slot++)
            {
                CharacterInstanceSnapshotV1 character = account.CharacterSlots[slot];
                if (character == null) continue;
                if (character.SlotIndex != slot
                    || !string.Equals(
                        character.Fingerprint,
                        PlayerAccountSnapshotFingerprintV1.Hash(
                            character.ToCanonicalString()),
                        StringComparison.Ordinal))
                {
                    return SaveComponentValidationResultV1.Reject(
                        "character-snapshot-fingerprint-mismatch");
                }
                foreach (SaveComponentSnapshotV1 component in
                    character.Components.Values)
                {
                    SaveComponentValidationResultV1 componentValidation =
                        ValidateComponent(component);
                    if (!componentValidation.Succeeded)
                    {
                        return componentValidation;
                    }
                }
            }
            foreach (SaveComponentSnapshotV1 component in
                account.AccountComponents.Values)
            {
                SaveComponentValidationResultV1 componentValidation =
                    ValidateComponent(component);
                if (!componentValidation.Succeeded)
                {
                    return componentValidation;
                }
            }
            return SaveComponentValidationResultV1.Accept();
        }

        private static SaveComponentValidationResultV1 ValidateComponent(
            SaveComponentSnapshotV1 component)
        {
            if (component == null)
            {
                return SaveComponentValidationResultV1.Reject(
                    "save-component-null");
            }
            if (!string.Equals(
                component.Fingerprint,
                PlayerAccountSnapshotFingerprintV1.Hash(
                    component.ToCanonicalString()),
                StringComparison.Ordinal))
            {
                return SaveComponentValidationResultV1.Reject(
                    "save-component-wrapper-fingerprint-mismatch");
            }
            if (Encoding.UTF8.GetByteCount(component.CanonicalPayload)
                > SavePersistenceLimitsV1.MaximumComponentPayloadBytes)
            {
                return SaveComponentValidationResultV1.Reject(
                    "component-payload-too-large");
            }
            return SaveComponentValidationResultV1.Accept();
        }

        private static CanonicalNodeV1 EncodeAccount(PlayerAccountSnapshotV1 account)
        {
            return CanonicalNodeV1.Object(
                CanonicalValueV1.Field("schema_version", CanonicalValueV1.Int32(account.SchemaVersion)),
                CanonicalValueV1.Field("account_id", ExplicitCodecValuesV1.RequiredIdNode(account.AccountStableId)),
                CanonicalValueV1.Field("revision", CanonicalValueV1.Int64(account.Revision)),
                CanonicalValueV1.Field("character_slots", ExplicitCodecValuesV1.EncodeList(
                    account.CharacterSlots,
                    character => ExplicitCodecValuesV1.OptionalObject(character, EncodeCharacter))),
                CanonicalValueV1.Field("account_components", EncodeComponents(account.AccountComponents.Values)));
        }

        private static PlayerAccountSnapshotV1 DecodeAccount(CanonicalNodeV1 node)
        {
            var reader = new CanonicalObjectReaderV1(
                node,
                "schema_version",
                "account_id",
                "revision",
                "character_slots",
                "account_components");
            int schema = CanonicalValueV1.ReadInt32(reader.Next("schema_version"));
            if (schema != PlayerAccountSnapshotV1.CurrentSchemaVersion)
            {
                throw new CanonicalPayloadExceptionV1(
                    "account-snapshot-schema-unsupported");
            }
            StableId accountId = ExplicitCodecValuesV1.RequiredId(reader.Next("account_id"));
            long revision = CanonicalValueV1.ReadInt64(reader.Next("revision"));
            List<CharacterInstanceSnapshotV1> slots = ExplicitCodecValuesV1.DecodeList(
                reader.Next("character_slots"),
                characterNode => ExplicitCodecValuesV1.OptionalObjectValue(
                    characterNode,
                    DecodeCharacter));
            if (slots.Count != PlayerAccountSnapshotV1.CharacterSlotCount)
            {
                throw new CanonicalPayloadExceptionV1(
                    "account-character-slot-count-invalid");
            }
            List<SaveComponentSnapshotV1> components = DecodeComponents(
                reader.Next("account_components"));
            return new PlayerAccountSnapshotV1(
                accountId,
                revision,
                slots,
                components);
        }

        private static CanonicalNodeV1 EncodeCharacter(
            CharacterInstanceSnapshotV1 character)
        {
            return CanonicalNodeV1.Object(
                CanonicalValueV1.Field("character_id", ExplicitCodecValuesV1.RequiredIdNode(character.CharacterInstanceStableId)),
                CanonicalValueV1.Field("class_id", ExplicitCodecValuesV1.RequiredIdNode(character.ClassDefinitionStableId)),
                CanonicalValueV1.Field("slot_index", CanonicalValueV1.Int32(character.SlotIndex)),
                CanonicalValueV1.Field("display_name", CanonicalValueV1.RequiredString(character.DisplayName)),
                CanonicalValueV1.Field("revision", CanonicalValueV1.Int64(character.Revision)),
                CanonicalValueV1.Field("components", EncodeComponents(character.Components.Values)));
        }

        private static CharacterInstanceSnapshotV1 DecodeCharacter(
            CanonicalNodeV1 node)
        {
            var reader = new CanonicalObjectReaderV1(
                node,
                "character_id",
                "class_id",
                "slot_index",
                "display_name",
                "revision",
                "components");
            return new CharacterInstanceSnapshotV1(
                ExplicitCodecValuesV1.RequiredId(reader.Next("character_id")),
                ExplicitCodecValuesV1.RequiredId(reader.Next("class_id")),
                CanonicalValueV1.ReadInt32(reader.Next("slot_index")),
                CanonicalValueV1.ReadRequiredString(reader.Next("display_name")),
                CanonicalValueV1.ReadInt64(reader.Next("revision")),
                DecodeComponents(reader.Next("components")));
        }

        private static CanonicalNodeV1 EncodeComponents(
            IEnumerable<SaveComponentSnapshotV1> components)
        {
            return ExplicitCodecValuesV1.EncodeList(
                components.OrderBy(
                    item => item.ComponentStableId.ToString(),
                    StringComparer.Ordinal),
                component => CanonicalNodeV1.Object(
                    CanonicalValueV1.Field("component_id", ExplicitCodecValuesV1.RequiredIdNode(component.ComponentStableId)),
                    CanonicalValueV1.Field("schema_version", CanonicalValueV1.Int32(component.SchemaVersion)),
                    CanonicalValueV1.Field("content_version", CanonicalValueV1.RequiredString(component.ContentVersion)),
                    CanonicalValueV1.Field("payload", CanonicalValueV1.RequiredString(component.CanonicalPayload))));
        }

        private static List<SaveComponentSnapshotV1> DecodeComponents(
            CanonicalNodeV1 node)
        {
            return ExplicitCodecValuesV1.DecodeList(
                node,
                componentNode =>
                {
                    var reader = new CanonicalObjectReaderV1(
                        componentNode,
                        "component_id",
                        "schema_version",
                        "content_version",
                        "payload");
                    StableId componentId = ExplicitCodecValuesV1.RequiredId(
                        reader.Next("component_id"));
                    int schemaVersion = CanonicalValueV1.ReadInt32(
                        reader.Next("schema_version"));
                    string contentVersion = CanonicalValueV1.ReadRequiredString(
                        reader.Next("content_version"));
                    string payload = CanonicalValueV1.ReadRequiredString(
                        reader.Next("payload"));
                    if (Encoding.UTF8.GetByteCount(payload)
                        > SavePersistenceLimitsV1.MaximumComponentPayloadBytes)
                    {
                        throw new CanonicalPayloadExceptionV1(
                            "component-payload-too-large");
                    }
                    return new SaveComponentSnapshotV1(
                        componentId,
                        schemaVersion,
                        contentVersion,
                        payload);
                });
        }
    }

}
