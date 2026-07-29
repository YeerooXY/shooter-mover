using System;
using System.Collections.Generic;
using ShooterMover.Application.Economy.Money;
using ShooterMover.Application.Economy.Scrap;
using ShooterMover.Application.Holdings;
using ShooterMover.Application.Inventory.LoadoutScreen;
using ShooterMover.Application.Persistence.Components;
using ShooterMover.Application.Progression.Experience;
using ShooterMover.Application.Progression.Skills;
using ShooterMover.Application.Rewards.CollectedRunTransfers;
using ShooterMover.Application.Rewards.Strongboxes;
using ShooterMover.Contracts.Holdings;
using ShooterMover.Contracts.Progression.Experience;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Economy.Money;
using ShooterMover.Domain.Economy.Scrap;
using ShooterMover.Domain.Persistence.Accounts;
using ShooterMover.Domain.Progression.Context;
using ShooterMover.Domain.Progression.Experience;
using ShooterMover.Domain.Progression.Skills;
using ShooterMover.Domain.Rewards.Strongboxes;

namespace ShooterMover.Application.Flow.Production
{
    internal static class CharacterStateAdapters
    {
        public static List<ISaveComponentBridge> Create(
            PlayerLoadoutLive loadout,
            PlayerExperienceState experience,
            PlayerExperienceCurve experienceCurve,
            ProgressionContext progressionContext,
            MoneyWalletActions money,
            ScrapWalletActions scrap,
            StableId scrapAuthorityId,
            StableId scrapCurrencyId,
            RankedSkillAllocationState skills,
            string skillProfileId,
            CharacterStrongboxLive strongboxes)
        {
            var adapters = new List<ISaveComponentBridge>
            {
                Experience(
                    experience,
                    experienceCurve,
                    progressionContext),
                Holdings(loadout),
                WeaponHoldingsSaveComponent.CreateAdapter(
                    loadout.WeaponHoldings),
                WeaponMountLoadoutSaveComponent.CreateAdapter(
                    loadout.MountLoadoutAuthority),
                Money(money),
                Scrap(scrap, scrapAuthorityId, scrapCurrencyId),
                Skills(skills, skillProfileId),
                Loadout(loadout),
                GeneratedEquipmentAugmentSignatureSaveComponent.CreateAdapter(
                    strongboxes.AugmentSignatures),
                Strongboxes(strongboxes),
            };
            adapters.AddRange(
                CollectedRunRewardLiveRegistry
                    .CreateSaveAdapters(
                        loadout.RoutePayload.SelectedCharacterStableId));
            return adapters;
        }

        public static TSnapshot DecodeRequired<TSnapshot>(
            CharacterInstanceSnapshot character,
            SaveComponentDefinition definition,
            ISaveComponentPayloadCodec<TSnapshot> codec)
            where TSnapshot : class
        {
            SaveComponentSnapshot component;
            if (!character.TryGetComponent(
                definition.ComponentStableId,
                out component))
            {
                throw new InvalidOperationException(
                    "Required character component is missing: "
                        + definition.ComponentStableId);
            }

            TSnapshot snapshot;
            string rejectionCode;
            if (!codec.TryDecode(
                component.CanonicalPayload,
                out snapshot,
                out rejectionCode))
            {
                throw new InvalidOperationException(
                    "Required character component is corrupt: "
                        + definition.ComponentStableId
                        + ":"
                        + rejectionCode);
            }
            return snapshot;
        }

        private static ISaveComponentBridge Experience(
            PlayerExperienceState authority,
            PlayerExperienceCurve curve,
            ProgressionContext context)
        {
            return KnownSaveComponentAdapters.PlayerExperience(
                authority.ExportSnapshot,
                snapshot =>
                {
                    var verifier = new PlayerExperienceState(
                        curve,
                        context);
                    PlayerExperienceImportResult result =
                        verifier.TryImport(snapshot);
                    return result.Status
                            == PlayerExperienceImportStatus.Imported
                        || result.Status
                            == PlayerExperienceImportStatus.DuplicateNoChange
                        ? SaveComponentValidationResult.Accept()
                        : SaveComponentValidationResult.Reject(
                            result.RejectionCode);
                },
                snapshot =>
                {
                    PlayerExperienceImportResult result =
                        authority.TryImport(snapshot);
                    return result.Status
                            == PlayerExperienceImportStatus.Imported
                        || result.Status
                            == PlayerExperienceImportStatus.DuplicateNoChange
                        ? SaveComponentApplyResult.Applied()
                        : SaveComponentApplyResult.Rejected(
                            result.RejectionCode);
                });
        }

        private static ISaveComponentBridge Holdings(
            PlayerLoadoutLive runtime)
        {
            return KnownSaveComponentAdapters.PlayerHoldings(
                runtime.Holdings.ExportSnapshot,
                snapshot =>
                {
                    var verifier = new PlayerHoldingsActions(
                        runtime.Holdings.AuthorityStableId,
                        999L,
                        runtime.CatalogBridge);
                    PlayerHoldingsImportResult result =
                        verifier.ImportSnapshot(snapshot);
                    return result.Succeeded
                        ? SaveComponentValidationResult.Accept()
                        : SaveComponentValidationResult.Reject(
                            result.RejectionCode);
                },
                snapshot =>
                {
                    PlayerHoldingsImportResult result =
                        runtime.Holdings.ImportSnapshot(snapshot);
                    return result.Succeeded
                        ? SaveComponentApplyResult.Applied()
                        : SaveComponentApplyResult.Rejected(
                            result.RejectionCode);
                });
        }

        private static ISaveComponentBridge Money(
            MoneyWalletActions authority)
        {
            return KnownSaveComponentAdapters.MoneyWallet(
                () => authority.CurrentSnapshot,
                snapshot =>
                {
                    MoneyWalletImportResult result =
                        new MoneyWalletActions().ImportSnapshot(snapshot);
                    return result.Status == MoneyWalletImportStatus.Imported
                        ? SaveComponentValidationResult.Accept()
                        : SaveComponentValidationResult.Reject(
                            result.RejectionCode);
                },
                snapshot =>
                {
                    MoneyWalletImportResult result =
                        authority.ImportSnapshot(snapshot);
                    return result.Status == MoneyWalletImportStatus.Imported
                        ? SaveComponentApplyResult.Applied()
                        : SaveComponentApplyResult.Rejected(
                            result.RejectionCode);
                });
        }

        private static ISaveComponentBridge Scrap(
            ScrapWalletActions authority,
            StableId authorityId,
            StableId currencyId)
        {
            return KnownSaveComponentAdapters.ScrapWallet(
                authority.ExportSnapshot,
                snapshot =>
                {
                    ScrapSnapshotImportResult result =
                        new ScrapWalletActions(authorityId, currencyId)
                            .ImportSnapshot(snapshot);
                    return result.Succeeded
                        ? SaveComponentValidationResult.Accept()
                        : SaveComponentValidationResult.Reject(
                            result.RejectionCode);
                },
                snapshot =>
                {
                    ScrapSnapshotImportResult result =
                        authority.ImportSnapshot(snapshot);
                    return result.Succeeded
                        ? SaveComponentApplyResult.Applied()
                        : SaveComponentApplyResult.Rejected(
                            result.RejectionCode);
                });
        }

        private static ISaveComponentBridge Skills(
            RankedSkillAllocationState authority,
            string profileId)
        {
            return KnownSaveComponentAdapters.RankedSkillAllocation(
                () => authority.Get(profileId),
                snapshot => KnownSaveComponentCodecs.RankedSkillAllocation
                    .Validate(snapshot),
                snapshot =>
                {
                    authority.Seed(snapshot);
                    return authority.Get(snapshot.ProfileId).Fingerprint
                            == snapshot.Fingerprint
                        ? SaveComponentApplyResult.Applied()
                        : SaveComponentApplyResult.Rejected(
                            "ranked-skill-seed-mismatch");
                });
        }

        private static ISaveComponentBridge Loadout(
            PlayerLoadoutLive runtime)
        {
            return KnownSaveComponentAdapters.ExactInstanceLoadout(
                () => WeaponMountLoadoutView.ArmorOnly(
                    runtime.LoadoutAuthority.ExportSnapshot()),
                snapshot => KnownSaveComponentCodecs.ExactInstanceLoadout
                    .Validate(snapshot),
                snapshot =>
                {
                    InventoryLoadoutStateSnapshot compatibility =
                        WeaponMountLoadoutView
                            .ToLegacyProjection(
                                runtime.MountLayout,
                                runtime.MountLoadoutAuthority.ExportSnapshot(),
                                snapshot);
                    InventoryLoadoutImportResult result =
                        runtime.LoadoutAuthority.ImportSnapshot(compatibility);
                    return result.Succeeded
                        ? SaveComponentApplyResult.Applied()
                        : SaveComponentApplyResult.Rejected(
                            result.RejectionCode);
                });
        }

        private static ISaveComponentBridge Strongboxes(
            CharacterStrongboxLive runtime)
        {
            return KnownSaveComponentAdapters.StrongboxState(
                runtime.Authority.ExportSnapshot,
                snapshot =>
                {
                    SaveComponentValidationResult validation =
                        KnownSaveComponentCodecs.StrongboxState.Validate(
                            snapshot);
                    if (!validation.Succeeded)
                    {
                        return validation;
                    }
                    return string.Equals(
                            snapshot.DefinitionCatalogFingerprint,
                            runtime.Catalog.Fingerprint,
                            StringComparison.Ordinal)
                        ? SaveComponentValidationResult.Accept()
                        : SaveComponentValidationResult.Reject(
                            "strongbox-snapshot-catalog-mismatch");
                },
                snapshot =>
                {
                    StrongboxOpeningImportResult result =
                        runtime.Authority.ImportSnapshot(snapshot);
                    return result.Succeeded
                        ? SaveComponentApplyResult.Applied()
                        : SaveComponentApplyResult.Rejected(
                            result.RejectionCode);
                });
        }
    }
}
