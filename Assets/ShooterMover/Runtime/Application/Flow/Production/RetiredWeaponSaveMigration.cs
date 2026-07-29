using System;
using System.Collections.Generic;
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
    public sealed class RetiredWeaponSaveMigrationResult
    {
        internal RetiredWeaponSaveMigrationResult(
            bool succeeded,
            bool changed,
            int migratedCharacterCount,
            string diagnostic,
            PlayerAccountSnapshot account)
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
        public PlayerAccountSnapshot Account { get; }
    }

    /// <summary>
    /// Versioned decoder and cleanup boundary for deleted weapon content. Retired IDs live
    /// only here so they can be removed from old saves; they are never registered in the
    /// production weapon or equipment catalogues and are never translated to current gear.
    /// Existing holdings ledger and transaction history are retained so accepted operation
    /// identities remain replay protected after migration.
    /// </summary>
    public static class RetiredWeaponSaveMigration
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

        public static RetiredWeaponSaveMigrationResult Migrate(
            PlayerAccountSnapshot account,
            Func<StableId> instanceIdFactory = null)
        {
            if (account == null)
            {
                return Failure("retired-weapon-migration-account-null", null);
            }

            try
            {
                PlayerAccountSnapshot nextAccount = account;
                int migratedCharacters = 0;
                for (int slotIndex = 0;
                     slotIndex < PlayerAccountSnapshot.CharacterSlotCount;
                     slotIndex++)
                {
                    CharacterInstanceSnapshot character =
                        nextAccount.CharacterAt(slotIndex);
                    if (character == null)
                    {
                        continue;
                    }

                    CharacterInstanceSnapshot migrated;
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

                return new RetiredWeaponSaveMigrationResult(
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
                        + DescribeException(exception),
                    account);
            }
        }

        public static bool IsRetiredDefinition(StableId definitionStableId)
        {
            return definitionStableId != null
                && RetiredDefinitions.Contains(definitionStableId);
        }

        private static bool TryMigrateCharacter(
            CharacterInstanceSnapshot character,
            Func<StableId> instanceIdFactory,
            out CharacterInstanceSnapshot migrated,
            out bool changed,
            out string diagnostic)
        {
            migrated = character;
            changed = false;
            diagnostic = string.Empty;

            SaveComponentSnapshot holdingsComponent;
            SaveComponentSnapshot loadoutComponent;
            if (!character.TryGetComponent(
                    KnownSaveComponentDefinitions.PlayerHoldings()
                        .ComponentStableId,
                    out holdingsComponent)
                || !character.TryGetComponent(
                    KnownSaveComponentDefinitions.ExactInstanceLoadout()
                        .ComponentStableId,
                    out loadoutComponent))
            {
                diagnostic = "required-inventory-component-missing";
                return false;
            }

            PlayerHoldingsSnapshot holdings;
            InventoryLoadoutStateSnapshot loadout;
            if (!KnownSaveComponentCodecs.PlayerHoldings.TryDecode(
                    holdingsComponent.CanonicalPayload,
                    out holdings,
                    out diagnostic))
            {
                diagnostic = "holdings-decode-failed:" + diagnostic;
                return false;
            }
            if (!KnownSaveComponentCodecs.ExactInstanceLoadout.TryDecode(
                    loadoutComponent.CanonicalPayload,
                    out loadout,
                    out diagnostic))
            {
                diagnostic = "loadout-decode-failed:" + diagnostic;
                return false;
            }

            // The mount-v2 component is now the authoritative source for
            // weapon bindings. The legacy loadout component is retained as a
            // compatibility projection for armour/non-weapon slots only. A
            // previous migration could leave both representations populated,
            // which the account semantic validator correctly rejects.
            SaveComponentSnapshot mountV2Component;
            bool hasMountV2 = character.TryGetComponent(
                WeaponMountLoadoutSaveComponent.Definition()
                    .ComponentStableId,
                out mountV2Component);
            if (hasMountV2)
            {
                WeaponMountLoadoutSnapshot mountSnapshot;
                if (!WeaponMountLoadoutSaveComponent.Codec.TryDecode(
                        mountV2Component.CanonicalPayload,
                        out mountSnapshot,
                        out diagnostic))
                {
                    diagnostic = "mount-v2-decode-failed:" + diagnostic;
                    return false;
                }
            }

            HashSet<StableId> retiredInstanceIds =
                FindRetiredEquipmentInstances(holdings);
            PlayerHoldingsSnapshot currentHoldings =
                retiredInstanceIds.Count > 0
                    ? RemoveRetiredEquipmentPreservingReplay(holdings)
                    : holdings;
            LegacyWeaponInventory repaired =
                LegacyWeaponSetup.Repair(
                    character.CharacterInstanceStableId,
                    character.ClassDefinitionStableId,
                    currentHoldings,
                    loadout,
                    instanceIdFactory);

            InventoryLoadoutStateSnapshot repairedLegacyLoadout =
                hasMountV2
                    ? WeaponMountLoadoutView.ArmorOnly(
                        repaired.Loadout)
                    : repaired.Loadout;

            bool holdingsChanged = !string.Equals(
                repaired.Holdings.Fingerprint,
                holdings.Fingerprint,
                StringComparison.Ordinal);
            bool loadoutChanged = !string.Equals(
                repairedLegacyLoadout.Fingerprint,
                loadout.Fingerprint,
                StringComparison.Ordinal);
            GeneratedEquipmentAugmentSignatureSnapshot cleanedSignatures;
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
                    KnownSaveComponentDefinitions.PlayerHoldings(),
                    KnownSaveComponentCodecs.PlayerHoldings.Encode(
                        repaired.Holdings)));
            }
            if (loadoutChanged)
            {
                migrated = migrated.WithComponent(Component(
                    KnownSaveComponentDefinitions.ExactInstanceLoadout(),
                    KnownSaveComponentCodecs.ExactInstanceLoadout.Encode(
                        repairedLegacyLoadout)));
            }
            if (signaturesChanged)
            {
                migrated = migrated.WithComponent(Component(
                    GeneratedEquipmentAugmentSignatureSaveComponent
                        .Definition(),
                    GeneratedEquipmentAugmentSignatureSaveComponent
                        .Codec.Encode(cleanedSignatures)));
            }
            changed = true;
            return true;
        }

        private static HashSet<StableId> FindRetiredEquipmentInstances(
            PlayerHoldingsSnapshot holdings)
        {
            var output = new HashSet<StableId>();
            for (int index = 0; index < holdings.UniqueHoldings.Count; index++)
            {
                UniqueHoldingSnapshot holding = holdings.UniqueHoldings[index];
                if (holding != null
                    && holding.RewardKind
                        == RewardGrantKind.EquipmentReference
                    && (IsRetiredDefinition(holding.DefinitionStableId)
                        || RetiredInstances.Contains(
                            holding.InstanceStableId)))
                {
                    output.Add(holding.InstanceStableId);
                }
            }
            return output;
        }

        private static PlayerHoldingsSnapshot
            RemoveRetiredEquipmentPreservingReplay(
                PlayerHoldingsSnapshot original)
        {
            var preserved = new List<UniqueHoldingSnapshot>();
            for (int index = 0;
                 index < original.UniqueHoldings.Count;
                 index++)
            {
                UniqueHoldingSnapshot holding =
                    original.UniqueHoldings[index];
                if (holding == null)
                {
                    throw new InvalidOperationException(
                        "A holdings snapshot contains a null unique holding.");
                }

                if (holding.RewardKind
                    == RewardGrantKind.EquipmentReference)
                {
                    if (IsRetiredDefinition(holding.DefinitionStableId)
                        || RetiredInstances.Contains(
                            holding.InstanceStableId))
                    {
                        continue;
                    }

                    EquipmentValidationResult validation =
                        holding.EquipmentInstance == null
                            ? null
                            : WeaponCatalogProvider.EquipmentCatalog
                                .ValidateInstance(holding.EquipmentInstance);
                    if (holding.EquipmentInstance == null
                        || WeaponCatalogProvider.EquipmentCatalog
                            .FindEquipmentDefinition(
                                holding.DefinitionStableId) == null
                        || validation == null
                        || !validation.IsValid)
                    {
                        throw new InvalidOperationException(
                            "A non-retired equipment holding is not valid in the current catalogue: "
                            + holding.InstanceStableId);
                    }
                }

                preserved.Add(holding);
            }

            return PlayerHoldingsSnapshot.CreateCanonical(
                original.SchemaVersion,
                original.AuthorityStableId,
                original.MaximumStackQuantity,
                original.LedgerSnapshot,
                preserved,
                original.StackHoldings,
                original.Transactions);
        }

        private static bool TryCleanGeneratedSignatures(
            CharacterInstanceSnapshot character,
            HashSet<StableId> retiredInstanceIds,
            out GeneratedEquipmentAugmentSignatureSnapshot cleaned,
            out bool changed,
            out string diagnostic)
        {
            cleaned = null;
            changed = false;
            diagnostic = string.Empty;
            SaveComponentSnapshot component;
            if (retiredInstanceIds == null
                || retiredInstanceIds.Count == 0
                || !character.TryGetComponent(
                    GeneratedEquipmentAugmentSignatureSaveComponent
                        .Definition().ComponentStableId,
                    out component))
            {
                return true;
            }

            GeneratedEquipmentAugmentSignatureSnapshot original;
            if (!GeneratedEquipmentAugmentSignatureSaveComponent.Codec
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
                new List<GeneratedEquipmentAugmentSignature>();
            for (int index = 0; index < original.Committed.Count; index++)
            {
                GeneratedEquipmentAugmentSignature signature =
                    original.Committed[index];
                if (!retiredInstanceIds.Contains(
                        signature.EquipmentInstanceStableId))
                {
                    committed.Add(signature);
                }
            }
            var staged = new List<GeneratedEquipmentAugmentSignature>();
            for (int index = 0; index < original.Staged.Count; index++)
            {
                GeneratedEquipmentAugmentSignature signature =
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
                ? new GeneratedEquipmentAugmentSignatureSnapshot(
                    committed,
                    staged)
                : original;
            return true;
        }

        private static SaveComponentSnapshot Component(
            SaveComponentDefinition definition,
            string payload)
        {
            return new SaveComponentSnapshot(
                definition.ComponentStableId,
                definition.SchemaVersion,
                definition.ContentVersion,
                payload);
        }

        private static string DescribeException(Exception exception)
        {
            if (exception == null)
            {
                return "Exception";
            }
            Exception root = exception.GetBaseException() ?? exception;
            string description = exception.GetType().Name;
            if (!ReferenceEquals(root, exception))
            {
                description += "->" + root.GetType().Name;
            }
            if (string.IsNullOrWhiteSpace(root.Message))
            {
                return description;
            }
            string message = root.Message
                .Replace('\r', ' ')
                .Replace('\n', ' ')
                .Trim();
            return description + ":" + message;
        }

        private static RetiredWeaponSaveMigrationResult Failure(
            string diagnostic,
            PlayerAccountSnapshot account)
        {
            return new RetiredWeaponSaveMigrationResult(
                false,
                false,
                0,
                diagnostic,
                account);
        }
    }
}
