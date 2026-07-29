using System;
using System.Collections.Generic;
using ShooterMover.Application.Economy.Money;
using ShooterMover.Application.Economy.Scrap;
using ShooterMover.Application.Holdings;
using ShooterMover.Application.Inventory.LoadoutScreen;
using ShooterMover.Application.Persistence.SaveParts;
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

namespace ShooterMover.Application.Flow.Game
{
    internal static class CharacterStateAdapters
    {
        public static List<ISavePart> Create(
            PlayerLoadoutLive loadout,
            PlayerExperience experience,
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
            var adapters = new List<ISavePart>
            {
                Experience(
                    experience,
                    experienceCurve,
                    progressionContext),
                Holdings(loadout),
                GunInventorySavePart.CreateAdapter(
                    loadout.GunInventory),
                LoadoutSavePart.CreateAdapter(
                    loadout.MountLoadoutAuthority),
                Money(money),
                Scrap(scrap, scrapAuthorityId, scrapCurrencyId),
                Skills(skills, skillProfileId),
                Loadout(loadout),
                GunAugmentSavePart.CreateAdapter(
                    strongboxes.AugmentSignatures),
                Strongboxes(strongboxes),
            };
            adapters.AddRange(
                RewardClaimLiveRegistry
                    .CreateSaveAdapters(
                        loadout.RoutePayload.SelectedCharacterStableId));
            return adapters;
        }

        public static TSnapshot DecodeRequired<TSnapshot>(
            CharacterInstanceSnapshot character,
            SavePartDefinition definition,
            ISavePartFormat<TSnapshot> codec)
            where TSnapshot : class
        {
            SavePartSnapshot component;
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

        private static ISavePart Experience(
            PlayerExperience authority,
            PlayerExperienceCurve curve,
            ProgressionContext context)
        {
            return KnownSavePartAdapters.PlayerExperience(
                authority.ExportSnapshot,
                snapshot =>
                {
                    var verifier = new PlayerExperience(
                        curve,
                        context);
                    PlayerExperienceImportResult result =
                        verifier.TryImport(snapshot);
                    return result.Status
                            == PlayerExperienceImportStatus.Imported
                        || result.Status
                            == PlayerExperienceImportStatus.DuplicateNoChange
                        ? SavePartValidationResult.Accept()
                        : SavePartValidationResult.Reject(
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
                        ? SavePartApplyResult.Applied()
                        : SavePartApplyResult.Rejected(
                            result.RejectionCode);
                });
        }

        private static ISavePart Holdings(
            PlayerLoadoutLive runtime)
        {
            return KnownSavePartAdapters.PlayerHoldings(
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
                        ? SavePartValidationResult.Accept()
                        : SavePartValidationResult.Reject(
                            result.RejectionCode);
                },
                snapshot =>
                {
                    PlayerHoldingsImportResult result =
                        runtime.Holdings.ImportSnapshot(snapshot);
                    return result.Succeeded
                        ? SavePartApplyResult.Applied()
                        : SavePartApplyResult.Rejected(
                            result.RejectionCode);
                });
        }

        private static ISavePart Money(
            MoneyWalletActions authority)
        {
            return KnownSavePartAdapters.MoneyWallet(
                () => authority.CurrentSnapshot,
                snapshot =>
                {
                    MoneyWalletImportResult result =
                        new MoneyWalletActions().ImportSnapshot(snapshot);
                    return result.Status == MoneyWalletImportStatus.Imported
                        ? SavePartValidationResult.Accept()
                        : SavePartValidationResult.Reject(
                            result.RejectionCode);
                },
                snapshot =>
                {
                    MoneyWalletImportResult result =
                        authority.ImportSnapshot(snapshot);
                    return result.Status == MoneyWalletImportStatus.Imported
                        ? SavePartApplyResult.Applied()
                        : SavePartApplyResult.Rejected(
                            result.RejectionCode);
                });
        }

        private static ISavePart Scrap(
            ScrapWalletActions authority,
            StableId authorityId,
            StableId currencyId)
        {
            return KnownSavePartAdapters.ScrapWallet(
                authority.ExportSnapshot,
                snapshot =>
                {
                    ScrapSnapshotImportResult result =
                        new ScrapWalletActions(authorityId, currencyId)
                            .ImportSnapshot(snapshot);
                    return result.Succeeded
                        ? SavePartValidationResult.Accept()
                        : SavePartValidationResult.Reject(
                            result.RejectionCode);
                },
                snapshot =>
                {
                    ScrapSnapshotImportResult result =
                        authority.ImportSnapshot(snapshot);
                    return result.Succeeded
                        ? SavePartApplyResult.Applied()
                        : SavePartApplyResult.Rejected(
                            result.RejectionCode);
                });
        }

        private static ISavePart Skills(
            RankedSkillAllocationState authority,
            string profileId)
        {
            return KnownSavePartAdapters.RankedSkillAllocation(
                () => authority.Get(profileId),
                snapshot => GameSaveFormats.RankedSkillAllocation
                    .Validate(snapshot),
                snapshot =>
                {
                    authority.Seed(snapshot);
                    return authority.Get(snapshot.ProfileId).Fingerprint
                            == snapshot.Fingerprint
                        ? SavePartApplyResult.Applied()
                        : SavePartApplyResult.Rejected(
                            "ranked-skill-seed-mismatch");
                });
        }

        private static ISavePart Loadout(
            PlayerLoadoutLive runtime)
        {
            return KnownSavePartAdapters.ExactInstanceLoadout(
                () => LoadoutView.ArmorOnly(
                    runtime.LoadoutAuthority.ExportSnapshot()),
                snapshot => GameSaveFormats.ExactInstanceLoadout
                    .Validate(snapshot),
                snapshot =>
                {
                    InventoryLoadoutStateSnapshot compatibility =
                        LoadoutView
                            .ToLegacyProjection(
                                runtime.MountLayout,
                                runtime.MountLoadoutAuthority.ExportSnapshot(),
                                snapshot);
                    InventoryLoadoutImportResult result =
                        runtime.LoadoutAuthority.ImportSnapshot(compatibility);
                    return result.Succeeded
                        ? SavePartApplyResult.Applied()
                        : SavePartApplyResult.Rejected(
                            result.RejectionCode);
                });
        }

        private static ISavePart Strongboxes(
            CharacterStrongboxLive runtime)
        {
            return KnownSavePartAdapters.StrongboxState(
                runtime.Authority.ExportSnapshot,
                snapshot =>
                {
                    SavePartValidationResult validation =
                        GameSaveFormats.StrongboxState.Validate(
                            snapshot);
                    if (!validation.Succeeded)
                    {
                        return validation;
                    }
                    return string.Equals(
                            snapshot.DefinitionCatalogFingerprint,
                            runtime.Catalog.Fingerprint,
                            StringComparison.Ordinal)
                        ? SavePartValidationResult.Accept()
                        : SavePartValidationResult.Reject(
                            "strongbox-snapshot-catalog-mismatch");
                },
                snapshot =>
                {
                    StrongboxOpeningImportResult result =
                        runtime.Authority.ImportSnapshot(snapshot);
                    return result.Succeeded
                        ? SavePartApplyResult.Applied()
                        : SavePartApplyResult.Rejected(
                            result.RejectionCode);
                });
        }
    }
}
