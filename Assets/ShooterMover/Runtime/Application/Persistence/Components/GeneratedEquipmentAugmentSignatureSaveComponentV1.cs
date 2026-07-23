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
    public static class GeneratedEquipmentAugmentSignatureSaveComponentV1
    {
        private static readonly GeneratedEquipmentAugmentSignatureComponentCodecV1
            CodecValue =
                new GeneratedEquipmentAugmentSignatureComponentCodecV1();

        public static SaveComponentDefinitionV1 Definition()
        {
            return new SaveComponentDefinitionV1(
                ShooterMover.Domain.Common.StableId.Create(
                    "save-component",
                    "generated-equipment-augment-signatures"),
                1,
                "generated-equipment-augment-signatures-explicit-v1",
                false,
                650);
        }

        public static GeneratedEquipmentAugmentSignatureComponentCodecV1 Codec
        {
            get { return CodecValue; }
        }

        public static ISaveComponentAdapterV1 CreateAdapter(
            GeneratedEquipmentAugmentSignatureAuthorityV1 authority)
        {
            if (authority == null)
            {
                throw new ArgumentNullException(nameof(authority));
            }
            return new AuthoritySnapshotSaveComponentAdapterV1<
                GeneratedEquipmentAugmentSignatureSnapshotV1>(
                    Definition(),
                    CodecValue,
                    authority.ExportDurableSnapshot,
                    CodecValue.Validate,
                    snapshot =>
                    {
                        try
                        {
                            authority.RestoreDurableSnapshot(snapshot);
                            return SaveComponentApplyResultV1.Applied();
                        }
                        catch (Exception exception)
                        {
                            return SaveComponentApplyResultV1.Rejected(
                                "generated-augment-signature-restore-exception:"
                                + exception.GetType().Name);
                        }
                    });
        }
    }

    public sealed class GeneratedEquipmentAugmentSignatureComponentCodecV1 :
        ExplicitSaveComponentCodecV1<
            GeneratedEquipmentAugmentSignatureSnapshotV1>
    {
        public GeneratedEquipmentAugmentSignatureComponentCodecV1()
            : base("generated-equipment-augment-signatures-explicit-v1")
        {
        }

        public override SaveComponentValidationResultV1 Validate(
            GeneratedEquipmentAugmentSignatureSnapshotV1 snapshot)
        {
            if (snapshot == null)
            {
                return SaveComponentValidationResultV1.Reject(
                    "generated-augment-signature-snapshot-null");
            }
            if (snapshot.SchemaVersion
                != GeneratedEquipmentAugmentSignatureSnapshotV1
                    .CurrentSchemaVersion)
            {
                return SaveComponentValidationResultV1.Reject(
                    "generated-augment-signature-schema-unsupported");
            }
            if (!string.Equals(
                    snapshot.Fingerprint,
                    RewardGenerationFingerprintV1.Compute(
                        snapshot.ToCanonicalString()),
                    StringComparison.Ordinal))
            {
                return SaveComponentValidationResultV1.Reject(
                    "generated-augment-signature-snapshot-fingerprint-mismatch");
            }
            for (int index = 0; index < snapshot.Committed.Count; index++)
            {
                if (!IsValid(snapshot.Committed[index]))
                {
                    return SaveComponentValidationResultV1.Reject(
                        "generated-augment-signature-committed-invalid");
                }
            }
            for (int index = 0; index < snapshot.Staged.Count; index++)
            {
                if (!IsValid(snapshot.Staged[index]))
                {
                    return SaveComponentValidationResultV1.Reject(
                        "generated-augment-signature-staged-invalid");
                }
            }
            return SaveComponentValidationResultV1.Accept();
        }

        protected override CanonicalNodeV1 EncodeNode(
            GeneratedEquipmentAugmentSignatureSnapshotV1 snapshot)
        {
            return CanonicalNodeV1.Object(
                CanonicalValueV1.Field(
                    "schema_version",
                    CanonicalValueV1.Int32(snapshot.SchemaVersion)),
                CanonicalValueV1.Field(
                    "committed",
                    ExplicitCodecValuesV1.EncodeList(
                        snapshot.Committed,
                        EncodeSignature)),
                CanonicalValueV1.Field(
                    "staged",
                    ExplicitCodecValuesV1.EncodeList(
                        snapshot.Staged,
                        EncodeSignature)));
        }

        protected override GeneratedEquipmentAugmentSignatureSnapshotV1 DecodeNode(
            CanonicalNodeV1 node)
        {
            var reader = new CanonicalObjectReaderV1(
                node,
                "schema_version",
                "committed",
                "staged");
            int schemaVersion = CanonicalValueV1.ReadInt32(
                reader.Next("schema_version"));
            if (schemaVersion
                != GeneratedEquipmentAugmentSignatureSnapshotV1
                    .CurrentSchemaVersion)
            {
                throw new CanonicalPayloadExceptionV1(
                    "generated-augment-signature-schema-unsupported");
            }
            return new GeneratedEquipmentAugmentSignatureSnapshotV1(
                ExplicitCodecValuesV1.DecodeList(
                    reader.Next("committed"),
                    DecodeSignature),
                ExplicitCodecValuesV1.DecodeList(
                    reader.Next("staged"),
                    DecodeSignature),
                schemaVersion);
        }

        private static CanonicalNodeV1 EncodeSignature(
            GeneratedEquipmentAugmentSignatureV1 signature)
        {
            return CanonicalNodeV1.Object(
                CanonicalValueV1.Field(
                    "equipment_instance_id",
                    ExplicitCodecValuesV1.RequiredIdNode(
                        signature.EquipmentInstanceStableId)),
                CanonicalValueV1.Field(
                    "source_strongbox_instance_id",
                    ExplicitCodecValuesV1.RequiredIdNode(
                        signature.SourceStrongboxInstanceStableId)),
                CanonicalValueV1.Field(
                    "hybrid_policy_id",
                    ExplicitCodecValuesV1.RequiredIdNode(
                        signature.HybridPolicyStableId)),
                CanonicalValueV1.Field(
                    "capacity",
                    CanonicalValueV1.Int32(signature.Capacity)),
                CanonicalValueV1.Field(
                    "shared_level",
                    CanonicalValueV1.Int32(signature.SharedLevel)),
                CanonicalValueV1.Field(
                    "hybrid_policy_fingerprint",
                    CanonicalValueV1.RequiredString(
                        signature.HybridPolicyFingerprint)),
                CanonicalValueV1.Field(
                    "algorithm_version",
                    CanonicalValueV1.Int32(signature.AlgorithmVersion)));
        }

        private static GeneratedEquipmentAugmentSignatureV1 DecodeSignature(
            CanonicalNodeV1 node)
        {
            var reader = new CanonicalObjectReaderV1(
                node,
                "equipment_instance_id",
                "source_strongbox_instance_id",
                "hybrid_policy_id",
                "capacity",
                "shared_level",
                "hybrid_policy_fingerprint",
                "algorithm_version");
            return new GeneratedEquipmentAugmentSignatureV1(
                ExplicitCodecValuesV1.RequiredId(
                    reader.Next("equipment_instance_id")),
                ExplicitCodecValuesV1.RequiredId(
                    reader.Next("source_strongbox_instance_id")),
                ExplicitCodecValuesV1.RequiredId(
                    reader.Next("hybrid_policy_id")),
                CanonicalValueV1.ReadInt32(reader.Next("capacity")),
                CanonicalValueV1.ReadInt32(reader.Next("shared_level")),
                CanonicalValueV1.ReadRequiredString(
                    reader.Next("hybrid_policy_fingerprint")),
                CanonicalValueV1.ReadInt32(
                    reader.Next("algorithm_version")));
        }

        private static bool IsValid(
            GeneratedEquipmentAugmentSignatureV1 signature)
        {
            return signature != null
                && string.Equals(
                    signature.Fingerprint,
                    RewardGenerationFingerprintV1.Compute(
                        signature.ToCanonicalString()),
                    StringComparison.Ordinal);
        }
    }
}
