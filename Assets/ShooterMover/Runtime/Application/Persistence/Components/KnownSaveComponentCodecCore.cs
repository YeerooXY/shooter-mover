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
    public abstract class ExplicitSaveComponentCodec<TSnapshot> :
        ISaveComponentPayloadCodec<TSnapshot>
        where TSnapshot : class
    {
        protected ExplicitSaveComponentCodec(string contractId)
        {
            ContractId = contractId
                ?? throw new ArgumentNullException(nameof(contractId));
        }

        public string ContractId { get; }

        public string Encode(TSnapshot snapshot)
        {
            SaveComponentValidationResult validation = Validate(snapshot);
            if (validation == null || !validation.Succeeded)
            {
                throw new ArgumentException(
                    validation == null
                        ? "component-codec-validation-result-null"
                        : validation.RejectionCode,
                    nameof(snapshot));
            }
            string payload = NodeCodec.Encode(EncodeNode(snapshot));
            if (Encoding.UTF8.GetByteCount(payload)
                > SavePersistenceLimits.MaximumComponentPayloadBytes)
            {
                throw new PayloadException(
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
            Node node;
            if (!NodeCodec.TryDecode(
                canonicalPayload,
                SavePersistenceLimits.MaximumComponentPayloadBytes,
                out node,
                out rejectionCode))
            {
                return false;
            }

            try
            {
                snapshot = DecodeNode(node);
                SaveComponentValidationResult validation = Validate(snapshot);
                if (validation == null || !validation.Succeeded)
                {
                    snapshot = null;
                    rejectionCode = validation == null
                        ? "component-codec-validation-result-null"
                        : validation.RejectionCode;
                    return false;
                }
                string rebuilt = NodeCodec.Encode(
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
            catch (PayloadException exception)
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

        public abstract SaveComponentValidationResult Validate(
            TSnapshot snapshot);

        protected abstract Node EncodeNode(TSnapshot snapshot);

        protected abstract TSnapshot DecodeNode(Node node);

        protected static SaveComponentValidationResult FingerprintResult(
            bool valid,
            string rejectionCode)
        {
            return valid
                ? SaveComponentValidationResult.Accept()
                : SaveComponentValidationResult.Reject(rejectionCode);
        }
    }

    public static class KnownSaveComponentCodecs
    {
        public static readonly PlayerExperienceComponentCodec
            PlayerExperience = new PlayerExperienceComponentCodec();

        public static readonly PlayerHoldingsComponentCodec
            PlayerHoldings = new PlayerHoldingsComponentCodec();

        public static readonly MoneyWalletComponentCodec
            MoneyWallet = new MoneyWalletComponentCodec();

        public static readonly ScrapWalletComponentCodec
            ScrapWallet = new ScrapWalletComponentCodec();

        public static readonly RankedSkillAllocationComponentCodec
            RankedSkillAllocation =
                new RankedSkillAllocationComponentCodec();

        public static readonly ExactInstanceLoadoutComponentCodec
            ExactInstanceLoadout =
                new ExactInstanceLoadoutComponentCodec();

        public static readonly StrongboxOpeningComponentCodec
            StrongboxState = new StrongboxOpeningComponentCodec();
    }

    internal static class ExplicitCodecValues
    {
        public static StableId RequiredId(Node node)
        {
            StableId output;
            if (!StableId.TryParse(
                Value.ReadRequiredString(node),
                out output))
            {
                throw new PayloadException(
                    "component-stable-id-invalid");
            }
            return output;
        }

        public static StableId OptionalId(Node node)
        {
            string value = Value.ReadOptionalString(node);
            if (value == null) return null;
            StableId output;
            if (!StableId.TryParse(value, out output))
            {
                throw new PayloadException(
                    "component-stable-id-invalid");
            }
            return output;
        }

        public static Node Id(StableId value)
        {
            return value == null
                ? Node.Null()
                : Node.ScalarValue(value.ToString());
        }

        public static Node RequiredIdNode(StableId value)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            return Node.ScalarValue(value.ToString());
        }

        public static TEnum EnumValue<TEnum>(Node node)
            where TEnum : struct
        {
            int numeric = Value.ReadInt32(node);
            TEnum value = (TEnum)Enum.ToObject(typeof(TEnum), numeric);
            if (!Enum.IsDefined(typeof(TEnum), value))
            {
                throw new PayloadException(
                    "component-enum-invalid");
            }
            return value;
        }

        public static Node EnumNode<TEnum>(TEnum value)
            where TEnum : struct
        {
            return Value.Int32(Convert.ToInt32(
                value,
                CultureInfo.InvariantCulture));
        }

        public static List<T> DecodeList<T>(
            Node node,
            Func<Node, T> decode)
        {
            IReadOnlyList<Node> values =
                Value.ReadList(node);
            var output = new List<T>(values.Count);
            for (int index = 0; index < values.Count; index++)
            {
                output.Add(decode(values[index]));
            }
            return output;
        }

        public static Node EncodeList<T>(
            IEnumerable<T> values,
            Func<T, Node> encode)
        {
            var output = new List<Node>();
            foreach (T value in values ?? throw new ArgumentNullException(nameof(values)))
            {
                output.Add(encode(value));
            }
            return Node.List(output);
        }

        public static Node OptionalObject<T>(
            T value,
            Func<T, Node> encode)
            where T : class
        {
            return value == null ? Node.Null() : encode(value);
        }

        public static T OptionalObjectValue<T>(
            Node node,
            Func<Node, T> decode)
            where T : class
        {
            return node.Kind == NodeKind.Null
                ? null
                : decode(node);
        }
    }

    public static class PlayerAccountAggregateCodec
    {
        public static string Encode(PlayerAccountSnapshot account)
        {
            SaveComponentValidationResult validation = Validate(account);
            if (!validation.Succeeded)
            {
                throw new ArgumentException(validation.RejectionCode, nameof(account));
            }
            string payload = NodeCodec.Encode(EncodeAccount(account));
            if (Encoding.UTF8.GetByteCount(payload)
                > SavePersistenceLimits.MaximumAccountPayloadBytes)
            {
                throw new PayloadException(
                    "account-payload-too-large");
            }
            return payload;
        }

        public static bool TryDecode(
            string payload,
            out PlayerAccountSnapshot account,
            out string rejectionCode)
        {
            account = null;
            Node node;
            if (!NodeCodec.TryDecode(
                payload,
                SavePersistenceLimits.MaximumAccountPayloadBytes,
                out node,
                out rejectionCode))
            {
                return false;
            }
            try
            {
                account = DecodeAccount(node);
                SaveComponentValidationResult validation = Validate(account);
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
            catch (PayloadException exception)
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

        public static SaveComponentValidationResult Validate(
            PlayerAccountSnapshot account)
        {
            if (account == null)
            {
                return SaveComponentValidationResult.Reject(
                    "account-snapshot-null");
            }
            if (account.SchemaVersion != PlayerAccountSnapshot.CurrentSchemaVersion)
            {
                return SaveComponentValidationResult.Reject(
                    "account-snapshot-schema-unsupported");
            }
            if (!string.Equals(
                account.Fingerprint,
                PlayerAccountSnapshotFingerprint.Hash(
                    account.ToCanonicalString()),
                StringComparison.Ordinal))
            {
                return SaveComponentValidationResult.Reject(
                    "account-snapshot-fingerprint-mismatch");
            }
            if (account.CharacterSlots.Count
                != PlayerAccountSnapshot.CharacterSlotCount)
            {
                return SaveComponentValidationResult.Reject(
                    "account-character-slot-count-invalid");
            }

            for (int slot = 0; slot < account.CharacterSlots.Count; slot++)
            {
                CharacterInstanceSnapshot character = account.CharacterSlots[slot];
                if (character == null) continue;
                if (character.SlotIndex != slot
                    || !string.Equals(
                        character.Fingerprint,
                        PlayerAccountSnapshotFingerprint.Hash(
                            character.ToCanonicalString()),
                        StringComparison.Ordinal))
                {
                    return SaveComponentValidationResult.Reject(
                        "character-snapshot-fingerprint-mismatch");
                }
                foreach (SaveComponentSnapshot component in
                    character.Components.Values)
                {
                    SaveComponentValidationResult componentValidation =
                        ValidateComponent(component);
                    if (!componentValidation.Succeeded)
                    {
                        return componentValidation;
                    }
                }
            }
            foreach (SaveComponentSnapshot component in
                account.AccountComponents.Values)
            {
                SaveComponentValidationResult componentValidation =
                    ValidateComponent(component);
                if (!componentValidation.Succeeded)
                {
                    return componentValidation;
                }
            }

            return KnownSaveComponentVersionGuard
                .ValidateKnownComponents(account);
        }

        private static SaveComponentValidationResult ValidateComponent(
            SaveComponentSnapshot component)
        {
            if (component == null)
            {
                return SaveComponentValidationResult.Reject(
                    "save-component-null");
            }
            if (!string.Equals(
                component.Fingerprint,
                PlayerAccountSnapshotFingerprint.Hash(
                    component.ToCanonicalString()),
                StringComparison.Ordinal))
            {
                return SaveComponentValidationResult.Reject(
                    "save-component-wrapper-fingerprint-mismatch");
            }
            if (Encoding.UTF8.GetByteCount(component.CanonicalPayload)
                > SavePersistenceLimits.MaximumComponentPayloadBytes)
            {
                return SaveComponentValidationResult.Reject(
                    "component-payload-too-large");
            }
            return SaveComponentValidationResult.Accept();
        }

        private static Node EncodeAccount(PlayerAccountSnapshot account)
        {
            return Node.Object(
                Value.Field("schema_version", Value.Int32(account.SchemaVersion)),
                Value.Field("account_id", ExplicitCodecValues.RequiredIdNode(account.AccountStableId)),
                Value.Field("revision", Value.Int64(account.Revision)),
                Value.Field("character_slots", ExplicitCodecValues.EncodeList(
                    account.CharacterSlots,
                    character => ExplicitCodecValues.OptionalObject(character, EncodeCharacter))),
                Value.Field("account_components", EncodeComponents(account.AccountComponents.Values)));
        }

        private static PlayerAccountSnapshot DecodeAccount(Node node)
        {
            var reader = new ObjectReader(
                node,
                "schema_version",
                "account_id",
                "revision",
                "character_slots",
                "account_components");
            int schema = Value.ReadInt32(reader.Next("schema_version"));
            if (schema != PlayerAccountSnapshot.CurrentSchemaVersion)
            {
                throw new PayloadException(
                    "account-snapshot-schema-unsupported");
            }
            StableId accountId = ExplicitCodecValues.RequiredId(reader.Next("account_id"));
            long revision = Value.ReadInt64(reader.Next("revision"));
            List<CharacterInstanceSnapshot> slots = ExplicitCodecValues.DecodeList(
                reader.Next("character_slots"),
                characterNode => ExplicitCodecValues.OptionalObjectValue(
                    characterNode,
                    DecodeCharacter));
            if (slots.Count != PlayerAccountSnapshot.CharacterSlotCount)
            {
                throw new PayloadException(
                    "account-character-slot-count-invalid");
            }
            List<SaveComponentSnapshot> components = DecodeComponents(
                reader.Next("account_components"));
            return new PlayerAccountSnapshot(
                accountId,
                revision,
                slots,
                components);
        }

        private static Node EncodeCharacter(
            CharacterInstanceSnapshot character)
        {
            return Node.Object(
                Value.Field("character_id", ExplicitCodecValues.RequiredIdNode(character.CharacterInstanceStableId)),
                Value.Field("class_id", ExplicitCodecValues.RequiredIdNode(character.ClassDefinitionStableId)),
                Value.Field("slot_index", Value.Int32(character.SlotIndex)),
                Value.Field("display_name", Value.RequiredString(character.DisplayName)),
                Value.Field("revision", Value.Int64(character.Revision)),
                Value.Field("components", EncodeComponents(character.Components.Values)));
        }

        private static CharacterInstanceSnapshot DecodeCharacter(
            Node node)
        {
            var reader = new ObjectReader(
                node,
                "character_id",
                "class_id",
                "slot_index",
                "display_name",
                "revision",
                "components");
            return new CharacterInstanceSnapshot(
                ExplicitCodecValues.RequiredId(reader.Next("character_id")),
                ExplicitCodecValues.RequiredId(reader.Next("class_id")),
                Value.ReadInt32(reader.Next("slot_index")),
                Value.ReadRequiredString(reader.Next("display_name")),
                Value.ReadInt64(reader.Next("revision")),
                DecodeComponents(reader.Next("components")));
        }

        private static Node EncodeComponents(
            IEnumerable<SaveComponentSnapshot> components)
        {
            return ExplicitCodecValues.EncodeList(
                components.OrderBy(
                    item => item.ComponentStableId.ToString(),
                    StringComparer.Ordinal),
                component => Node.Object(
                    Value.Field("component_id", ExplicitCodecValues.RequiredIdNode(component.ComponentStableId)),
                    Value.Field("schema_version", Value.Int32(component.SchemaVersion)),
                    Value.Field("content_version", Value.RequiredString(component.ContentVersion)),
                    Value.Field("payload", Value.RequiredString(component.CanonicalPayload))));
        }

        private static List<SaveComponentSnapshot> DecodeComponents(
            Node node)
        {
            return ExplicitCodecValues.DecodeList(
                node,
                componentNode =>
                {
                    var reader = new ObjectReader(
                        componentNode,
                        "component_id",
                        "schema_version",
                        "content_version",
                        "payload");
                    StableId componentId = ExplicitCodecValues.RequiredId(
                        reader.Next("component_id"));
                    int schemaVersion = Value.ReadInt32(
                        reader.Next("schema_version"));
                    string contentVersion = Value.ReadRequiredString(
                        reader.Next("content_version"));
                    string payload = Value.ReadRequiredString(
                        reader.Next("payload"));
                    if (Encoding.UTF8.GetByteCount(payload)
                        > SavePersistenceLimits.MaximumComponentPayloadBytes)
                    {
                        throw new PayloadException(
                            "component-payload-too-large");
                    }
                    return new SaveComponentSnapshot(
                        componentId,
                        schemaVersion,
                        contentVersion,
                        payload);
                });
        }
    }

}
