using System;
using ShooterMover.Application.Rewards.Strongboxes;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Rewards.Generation;

namespace ShooterMover.Application.Persistence.Components
{
    /// <summary>
    /// Optional character component for committed and in-flight generated augment
    /// signatures. Older character saves may omit it; newly persisted graphs retain it.
    /// </summary>
    public static class GeneratedEquipmentAugmentSignatureSaveComponent
    {
        private static readonly GeneratedEquipmentAugmentSignatureComponentCodec
            CodecValue =
                new GeneratedEquipmentAugmentSignatureComponentCodec();

        public static SaveComponentDefinition Definition()
        {
            return new SaveComponentDefinition(
                ShooterMover.Domain.Common.StableId.Create(
                    "save-component",
                    "generated-equipment-augment-signatures"),
                1,
                "generated-equipment-augment-signatures-explicit-v1",
                false,
                650);
        }

        public static GeneratedEquipmentAugmentSignatureComponentCodec Codec
        {
            get { return CodecValue; }
        }

        public static ISaveComponentBridge CreateAdapter(
            GeneratedEquipmentAugmentSignatureState authority)
        {
            if (authority == null)
            {
                throw new ArgumentNullException(nameof(authority));
            }
            return new StateSnapshotSaveComponentBridge<
                GeneratedEquipmentAugmentSignatureSnapshot>(
                    Definition(),
                    CodecValue,
                    () =>
                    {
                        lock (authority)
                        {
                            return authority.ExportDurableSnapshot();
                        }
                    },
                    CodecValue.Validate,
                    snapshot =>
                    {
                        lock (authority)
                        {
                            try
                            {
                                authority.RestoreDurableSnapshot(snapshot);
                                return SaveComponentApplyResult.Applied();
                            }
                            catch (Exception exception)
                            {
                                return SaveComponentApplyResult.Rejected(
                                    "generated-augment-signature-restore-exception:"
                                    + exception.GetType().Name);
                            }
                        }
                    });
        }
    }

    public sealed class GeneratedEquipmentAugmentSignatureComponentCodec :
        ExplicitSaveComponentCodec<
            GeneratedEquipmentAugmentSignatureSnapshot>
    {
        public GeneratedEquipmentAugmentSignatureComponentCodec()
            : base("generated-equipment-augment-signatures-explicit-v1")
        {
        }

        public override SaveComponentValidationResult Validate(
            GeneratedEquipmentAugmentSignatureSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return SaveComponentValidationResult.Reject(
                    "generated-augment-signature-snapshot-null");
            }
            if (snapshot.SchemaVersion
                != GeneratedEquipmentAugmentSignatureSnapshot
                    .CurrentSchemaVersion)
            {
                return SaveComponentValidationResult.Reject(
                    "generated-augment-signature-schema-unsupported");
            }
            if (!string.Equals(
                    snapshot.Fingerprint,
                    RewardGenerationFingerprint.Compute(
                        snapshot.ToCanonicalString()),
                    StringComparison.Ordinal))
            {
                return SaveComponentValidationResult.Reject(
                    "generated-augment-signature-snapshot-fingerprint-mismatch");
            }
            for (int index = 0; index < snapshot.Committed.Count; index++)
            {
                if (!IsValid(snapshot.Committed[index]))
                {
                    return SaveComponentValidationResult.Reject(
                        "generated-augment-signature-committed-invalid");
                }
            }
            for (int index = 0; index < snapshot.Staged.Count; index++)
            {
                if (!IsValid(snapshot.Staged[index]))
                {
                    return SaveComponentValidationResult.Reject(
                        "generated-augment-signature-staged-invalid");
                }
            }
            return SaveComponentValidationResult.Accept();
        }

        protected override Node EncodeNode(
            GeneratedEquipmentAugmentSignatureSnapshot snapshot)
        {
            return Node.Object(
                Value.Field(
                    "schema_version",
                    Value.Int32(snapshot.SchemaVersion)),
                Value.Field(
                    "committed",
                    ExplicitCodecValues.EncodeList(
                        snapshot.Committed,
                        EncodeSignature)),
                Value.Field(
                    "staged",
                    ExplicitCodecValues.EncodeList(
                        snapshot.Staged,
                        EncodeSignature)));
        }

        protected override GeneratedEquipmentAugmentSignatureSnapshot DecodeNode(
            Node node)
        {
            var reader = new ObjectReader(
                node,
                "schema_version",
                "committed",
                "staged");
            int schemaVersion = Value.ReadInt32(
                reader.Next("schema_version"));
            if (schemaVersion
                != GeneratedEquipmentAugmentSignatureSnapshot
                    .CurrentSchemaVersion)
            {
                throw new PayloadException(
                    "generated-augment-signature-schema-unsupported");
            }
            return new GeneratedEquipmentAugmentSignatureSnapshot(
                ExplicitCodecValues.DecodeList(
                    reader.Next("committed"),
                    DecodeSignature),
                ExplicitCodecValues.DecodeList(
                    reader.Next("staged"),
                    DecodeSignature),
                schemaVersion);
        }

        private static Node EncodeSignature(
            GeneratedEquipmentAugmentSignature signature)
        {
            return Node.Object(
                Value.Field(
                    "equipment_instance_id",
                    ExplicitCodecValues.RequiredIdNode(
                        signature.EquipmentInstanceStableId)),
                Value.Field(
                    "source_strongbox_instance_id",
                    ExplicitCodecValues.RequiredIdNode(
                        signature.SourceStrongboxInstanceStableId)),
                Value.Field(
                    "hybrid_policy_id",
                    ExplicitCodecValues.RequiredIdNode(
                        signature.HybridPolicyStableId)),
                Value.Field(
                    "capacity",
                    Value.Int32(signature.Capacity)),
                Value.Field(
                    "shared_level",
                    Value.Int32(signature.SharedLevel)),
                Value.Field(
                    "hybrid_policy_fingerprint",
                    Value.RequiredString(
                        signature.HybridPolicyFingerprint)),
                Value.Field(
                    "algorithm_version",
                    Value.Int32(signature.AlgorithmVersion)));
        }

        private static GeneratedEquipmentAugmentSignature DecodeSignature(
            Node node)
        {
            var reader = new ObjectReader(
                node,
                "equipment_instance_id",
                "source_strongbox_instance_id",
                "hybrid_policy_id",
                "capacity",
                "shared_level",
                "hybrid_policy_fingerprint",
                "algorithm_version");
            return new GeneratedEquipmentAugmentSignature(
                ExplicitCodecValues.RequiredId(
                    reader.Next("equipment_instance_id")),
                ExplicitCodecValues.RequiredId(
                    reader.Next("source_strongbox_instance_id")),
                ExplicitCodecValues.RequiredId(
                    reader.Next("hybrid_policy_id")),
                Value.ReadInt32(reader.Next("capacity")),
                Value.ReadInt32(reader.Next("shared_level")),
                Value.ReadRequiredString(
                    reader.Next("hybrid_policy_fingerprint")),
                Value.ReadInt32(
                    reader.Next("algorithm_version")));
        }

        private static bool IsValid(
            GeneratedEquipmentAugmentSignature signature)
        {
            return signature != null
                && string.Equals(
                    signature.Fingerprint,
                    RewardGenerationFingerprint.Compute(
                        signature.ToCanonicalString()),
                    StringComparison.Ordinal);
        }
    }
}
