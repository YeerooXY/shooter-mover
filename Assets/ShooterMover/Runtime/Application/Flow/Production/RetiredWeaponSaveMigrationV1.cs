using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using ShooterMover.Application.Holdings;
using ShooterMover.Application.Inventory.LoadoutScreen;
using ShooterMover.Application.Persistence.Components;
using ShooterMover.Application.Rewards.Strongboxes;
using ShooterMover.Contracts.Holdings;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Holdings;
using ShooterMover.Domain.Persistence.Accounts;
using ShooterMover.Domain.Rewards.Model;

namespace ShooterMover.Application.Flow.Production
{
    public sealed class RetiredWeaponSaveMigrationResultV1
    {
        internal RetiredWeaponSaveMigrationResultV1(
            bool succeeded,
            bool changed,
            int migratedCharacterCount,
            string diagnostic,
            PlayerAccountSnapshotV1 account)
        {
            Succeeded = succeeded;
            Changed = changed;
            MigratedCharacterCount = migratedCharacterCount;
            Diagnostic = diagnostic ?? string.Empty;
            Account = account;
        }

        public bool Succeeded { get; }
        public bool Changed { get; }
        public int MigratedCharacterCount { get; }
        public string Diagnostic { get; }
        public PlayerAccountSnapshotV1 Account { get; }
    }

    /// <summary>
    /// Versioned decoder and cleanup boundary for deleted weapon content. Retired IDs live
    /// only here so they can be removed from old saves; they are never registered in the
    /// production weapon or equipment catalogues and are never translated to current gear.
    /// </summary>
    public static class RetiredWeaponSaveMigrationV1
    {
        public const int MigrationVersion = 1;

        private static readonly HashSet<StableId> RetiredDefinitions =
            new HashSet<StableId>
            {
                StableId.Parse("equipment.production-starter-blaster"),
                StableId.Parse("equipment.production-starter-shotgun"),
                StableId.Parse("equipment.production-starter-rocket-launcher"),
                StableId.Parse("equipment.production-starter-arc-gun"),
                StableId.Parse("equipment.production-starter-ricochet-gun"),
            };

        private static readonly HashSet<StableId> RetiredInstances =
            new HashSet<StableId>
            {
                StableId.Parse("equipment-instance.flow-draft-slot-1"),
                StableId.Parse("equipment-instance.flow-draft-slot-2"),
                StableId.Parse("equipment-instance.flow-draft-slot-3"),
                StableId.Parse("equipment-instance.flow-draft-slot-4"),
                StableId.Parse("equipment-instance.production-starter-ricochet"),
                StableId.Parse("equipment-instance.retired-starter-blaster"),
                StableId.Parse("equipment-instance.retired-starter-shotgun"),
                StableId.Parse("equipment-instance.retired-starter-rocket-launcher"),
                StableId.Parse("equipment-instance.retired-starter-arc-gun"),
                StableId.Parse("equipment-instance.retired-starter-ricochet-gun"),
            };

        private static readonly StableId MigrationSourceStableId =
            StableId.Parse("source.retired-weapon-save-migration-v1");

        public static RetiredWeaponSaveMigrationResultV1 Migrate(
            PlayerAccountSnapshotV1 account,
            Func<StableId> instanceIdFactory = null)
        {
            if (account == null)
            {
                return Failure("retired-weapon-migration-account-null", null);
            }

            try
            {
                PlayerAccountSnapshotV1 nextAccount = account;
                int migratedCharacters = 0;
                for (int slotIndex = 0;
                     slotIndex < PlayerAccountSnapshotV1.CharacterSlotCount;
                     slotIndex++)
                {
                    CharacterInstanceSnapshotV1 character =
                        nextAccount.CharacterAt(slotIndex);
                    if (character == null)
                    {
                        continue;
                    }

                    CharacterInstanceSnapshotV1 migrated;
                    bool changed;
                    string diagnostic;
                    if (!TryMigrateCharacter(
                            character,
                            instanceIdFactory,
                            out migrated,
                            out changed,
                            out diagnostic))
                    {
                        return Failure(
                            "retired-weapon-migration-character-rejected:"
                            + slotIndex
                            + ":"
                            + diagnostic,
                            account);
                    }
                    if (!changed)
                    {
                        continue;
                    }

                    nextAccount = nextAccount.WithCharacter(
                        slotIndex,
                        migrated);
                    migratedCharacters++;
                }

                return new RetiredWeaponSaveMigrationResultV1(
                    true,
                    migratedCharacters > 0,
                    migratedCharacters,
                    string.Empty,
                    nextAccount);
            }
            catch (Exception exception)
            {
                return Failure(
                    "retired-weapon-migration-threw:"
                    + exception.GetType().Name,
                    account);
            }
        }

        public static bool IsRetiredDefinition(StableId definitionStableId)
        {
            return definitionStableId != null
                && RetiredDefinitions.Contains(definitionStableId);
        }

        private static bool TryMigrateCharacter(
            CharacterInstanceSnapshotV1 character,
            Func<StableId> instanceIdFactory,
            out CharacterInstanceSnapshotV1 migrated,
            out bool changed,
            out string diagnostic)
        {
            migrated = character;
            changed = false;
            diagnostic = string.Empty;

            SaveComponentSnapshotV1 holdingsComponent;
            SaveComponentSnapshotV1 loadoutComponent;
            if (!character.TryGetComponent(
                    KnownSaveComponentDefinitionsV1.PlayerHoldings()
                        .ComponentStableId,
                    out holdingsComponent)
                || !character.TryGetComponent(
                    KnownSaveComponentDefinitionsV1.ExactInstanceLoadout()
                        .ComponentStableId,
                    out loadoutComponent))
            {
                diagnostic = "required-inventory-component-missing";
                return false;
            }

            PlayerHoldingsSnapshotV1 holdings;
            InventoryLoadoutAuthoritySnapshotV1 loadout;
            if (!KnownSaveComponentCodecsV1.PlayerHoldings.TryDecode(
                    holdingsComponent.CanonicalPayload,
                    out holdings,
                    out diagnostic))
            {
                diagnostic = "holdings-decode-failed:" + diagnostic;
                return false;
            }
            if (!KnownSaveComponentCodecsV1.ExactInstanceLoadout.TryDecode(
                    loadoutComponent.CanonicalPayload,
                    out loadout,
                    out diagnostic))
            {
                diagnostic = "loadout-decode-failed:" + diagnostic;
                return false;
            }

            HashSet<StableId> retiredInstanceIds =
                FindRetiredEquipmentInstances(holdings);
            PlayerHoldingsSnapshotV1 currentHoldings =
                retiredInstanceIds.Count > 0
                    ? RebuildWithoutRetired(character, holdings)
                    : holdings;
            ProductionWeaponInventoryStateV1 repaired =
                ProductionWeaponOnboardingV1.Repair(
                    character.CharacterInstanceStableId,
                    character.ClassDefinitionStableId,
                    currentHoldings,
                    loadout,
                    instanceIdFactory);

            bool holdingsChanged = !string.Equals(
                repaired.Holdings.Fingerprint,
                holdings.Fingerprint,
                StringComparison.Ordinal);
            bool loadoutChanged = !string.Equals(
                repaired.Loadout.Fingerprint,
                loadout.Fingerprint,
                StringComparison.Ordinal);
            GeneratedEquipmentAugmentSignatureSnapshotV1 cleanedSignatures;
            bool signaturesChanged;
            if (!TryCleanGeneratedSignatures(
                    character,
                    retiredInstanceIds,
                    out cleanedSignatures,
                    out signaturesChanged,
                    out diagnostic))
            {
                return false;
            }
            if (!holdingsChanged && !loadoutChanged && !signaturesChanged)
            {
                return true;
            }

            if (holdingsChanged)
            {
                migrated = migrated.WithComponent(Component(
                    KnownSaveComponentDefinitionsV1.PlayerHoldings(),
                    KnownSaveComponentCodecsV1.PlayerHoldings.Encode(
                        repaired.Holdings)));
            }
            if (loadoutChanged)
            {
                migrated = migrated.WithComponent(Component(
                    KnownSaveComponentDefinitionsV1.ExactInstanceLoadout(),
                    KnownSaveComponentCodecsV1.ExactInstanceLoadout.Encode(
                        repaired.Loadout)));
            }
            if (signaturesChanged)
            {
                migrated = migrated.WithComponent(Component(
                    GeneratedEquipmentAugmentSignatureSaveComponentV1
                        .Definition(),
                    GeneratedEquipmentAugmentSignatureSaveComponentV1
                        .Codec.Encode(cleanedSignatures)));
            }
            changed = true;
            return true;
        }

        private static HashSet<StableId> FindRetiredEquipmentInstances(
            PlayerHoldingsSnapshotV1 holdings)
        {
            var output = new HashSet<StableId>();
            for (int index = 0; index < holdings.UniqueHoldings.Count; index++)
            {
                UniqueHoldingSnapshotV1 holding = holdings.UniqueHoldings[index];
                if (holding != null
                    && holding.RewardKind
                        == RewardGrantKindV1.EquipmentReference
                    && (IsRetiredDefinition(holding.DefinitionStableId)
                        || RetiredInstances.Contains(
                            holding.InstanceStableId)))
                {
                    output.Add(holding.InstanceStableId);
                }
            }
            return output;
        }

        private static bool TryCleanGeneratedSignatures(
            CharacterInstanceSnapshotV1 character,
            HashSet<StableId> retiredInstanceIds,
            out GeneratedEquipmentAugmentSignatureSnapshotV1 cleaned,
            out bool changed,
            out string diagnostic)
        {
            cleaned = null;
            changed = false;
            diagnostic = string.Empty;
            SaveComponentSnapshotV1 component;
            if (retiredInstanceIds == null
                || retiredInstanceIds.Count == 0
                || !character.TryGetComponent(
                    GeneratedEquipmentAugmentSignatureSaveComponentV1
                        .Definition().ComponentStableId,
                    out component))
            {
                return true;
            }

            GeneratedEquipmentAugmentSignatureSnapshotV1 original;
            if (!GeneratedEquipmentAugmentSignatureSaveComponentV1.Codec
                .TryDecode(
                    component.CanonicalPayload,
                    out original,
                    out diagnostic))
            {
                diagnostic = "generated-signature-decode-failed:"
                    + diagnostic;
                return false;
            }

            var committed =
                new List<GeneratedEquipmentAugmentSignatureV1>();
            for (int index = 0; index < original.Committed.Count; index++)
            {
                GeneratedEquipmentAugmentSignatureV1 signature =
                    original.Committed[index];
                if (!retiredInstanceIds.Contains(
                        signature.EquipmentInstanceStableId))
                {
                    committed.Add(signature);
                }
            }
            var staged = new List<GeneratedEquipmentAugmentSignatureV1>();
            for (int index = 0; index < original.Staged.Count; index++)
            {
                GeneratedEquipmentAugmentSignatureV1 signature =
                    original.Staged[index];
                if (!retiredInstanceIds.Contains(
                        signature.EquipmentInstanceStableId))
                {
                    staged.Add(signature);
                }
            }

            changed = committed.Count != original.Committed.Count
                || staged.Count != original.Staged.Count;
            cleaned = changed
                ? new GeneratedEquipmentAugmentSignatureSnapshotV1(
                    committed,
                    staged)
                : original;
            return true;
        }

        private static PlayerHoldingsSnapshotV1 RebuildWithoutRetired(
            CharacterInstanceSnapshotV1 character,
            PlayerHoldingsSnapshotV1 original)
        {
            var adapter = new ProductionEquipmentCatalogAdapterV1(
                ProductionWeaponCatalogProvider.EquipmentCatalog);
            var rebuilt = new PlayerHoldingsService(
                original.AuthorityStableId,
                original.MaximumStackQuantity,
                adapter);

            for (int index = 0; index < original.UniqueHoldings.Count; index++)
            {
                UniqueHoldingSnapshotV1 holding =
                    original.UniqueHoldings[index];
                if (holding.RewardKind
                    == RewardGrantKindV1.EquipmentReference)
                {
                    if (IsRetiredDefinition(holding.DefinitionStableId)
                        || RetiredInstances.Contains(
                            holding.InstanceStableId))
                    {
                        continue;
                    }
                    if (holding.EquipmentInstance == null
                        || ProductionWeaponCatalogProvider.EquipmentCatalog
                            .FindEquipmentDefinition(
                                holding.DefinitionStableId) == null
                        || !ProductionWeaponCatalogProvider.EquipmentCatalog
                            .ValidateInstance(holding.EquipmentInstance)
                            .IsValid)
                    {
                        throw new InvalidOperationException(
                            "A non-retired equipment holding is not valid in the current catalogue: "
                            + holding.InstanceStableId);
                    }
                    Apply(rebuilt, PlayerHoldingsCommandV1.AddEquipment(
                        TransactionId(character, "equipment", holding.InstanceStableId),
                        OperationId(character, "equipment", holding.InstanceStableId),
                        rebuilt.AuthorityStableId,
                        holding.EquipmentInstance,
                        holding.Provenance,
                        rebuilt.Sequence));
                    continue;
                }

                Apply(rebuilt, PlayerHoldingsCommandV1.AddStrongbox(
                    TransactionId(character, "strongbox", holding.InstanceStableId),
                    OperationId(character, "strongbox", holding.InstanceStableId),
                    rebuilt.AuthorityStableId,
                    holding.DefinitionStableId,
                    holding.InstanceStableId,
                    holding.Provenance,
                    rebuilt.Sequence));
            }

            for (int index = 0; index < original.StackHoldings.Count; index++)
            {
                StackHoldingSnapshotV1 stack = original.StackHoldings[index];
                string token = Token(
                    character,
                    "stack",
                    stack.ItemStableId);
                Apply(rebuilt, PlayerHoldingsCommandV1.AddStack(
                    StableId.Parse(
                        "transaction.retired-weapon-migration-" + token),
                    StableId.Parse(
                        "operation.retired-weapon-migration-" + token),
                    rebuilt.AuthorityStableId,
                    stack.RewardKind,
                    stack.ItemStableId,
                    stack.Quantity,
                    HoldingProvenanceV1.Create(
                        StableId.Parse(
                            "grant.retired-weapon-migration-" + token),
                        MigrationSourceStableId),
                    rebuilt.Sequence));
            }

            return rebuilt.ExportSnapshot();
        }

        private static void Apply(
            PlayerHoldingsService holdings,
            PlayerHoldingsCommandV1 command)
        {
            PlayerHoldingsMutationResultV1 result = holdings.Apply(command);
            if (result == null
                || (result.Status != PlayerHoldingsMutationStatusV1.Applied
                    && result.Status
                        != PlayerHoldingsMutationStatusV1
                            .ExactDuplicateNoChange))
            {
                throw new InvalidOperationException(
                    "Unable to rebuild a preserved holding: "
                    + (result == null
                        ? "result-null"
                        : result.RejectionCode));
            }
        }

        private static SaveComponentSnapshotV1 Component(
            SaveComponentDefinitionV1 definition,
            string payload)
        {
            return new SaveComponentSnapshotV1(
                definition.ComponentStableId,
                definition.SchemaVersion,
                definition.ContentVersion,
                payload);
        }

        private static StableId TransactionId(
            CharacterInstanceSnapshotV1 character,
            string kind,
            StableId itemStableId)
        {
            return StableId.Parse(
                "transaction.retired-weapon-migration-"
                + Token(character, kind, itemStableId));
        }

        private static StableId OperationId(
            CharacterInstanceSnapshotV1 character,
            string kind,
            StableId itemStableId)
        {
            return StableId.Parse(
                "operation.retired-weapon-migration-"
                + Token(character, kind, itemStableId));
        }

        private static string Token(
            CharacterInstanceSnapshotV1 character,
            string kind,
            StableId itemStableId)
        {
            return Hash(
                MigrationVersion
                    + "|"
                    + character.CharacterInstanceStableId
                    + "|"
                    + kind
                    + "|"
                    + itemStableId);
        }

        private static string Hash(string value)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] digest = sha.ComputeHash(
                    Encoding.UTF8.GetBytes(value ?? string.Empty));
                var builder = new StringBuilder(32);
                for (int index = 0; index < 16; index++)
                {
                    builder.Append(digest[index].ToString("x2"));
                }
                return builder.ToString();
            }
        }

        private static RetiredWeaponSaveMigrationResultV1 Failure(
            string diagnostic,
            PlayerAccountSnapshotV1 account)
        {
            return new RetiredWeaponSaveMigrationResultV1(
                false,
                false,
                0,
                diagnostic,
                account);
        }
    }
}
