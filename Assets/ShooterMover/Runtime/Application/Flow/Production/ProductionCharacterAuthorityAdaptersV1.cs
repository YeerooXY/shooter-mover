using System;
using System.Collections.Generic;
using ShooterMover.Application.Economy.Money;
using ShooterMover.Application.Economy.Scrap;
using ShooterMover.Application.Holdings;
using ShooterMover.Application.Persistence.Components;
using ShooterMover.Application.Progression.Experience;
using ShooterMover.Application.Progression.Skills;
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
    internal static class ProductionCharacterAuthorityAdaptersV1
    {
        public static List<ISaveComponentAdapterV1> Create(
            ProductionPlayerLoadoutRuntimeV1 loadout,
            PlayerExperienceAuthorityV1 experience,
            PlayerExperienceCurveV1 experienceCurve,
            ProgressionContext progressionContext,
            MoneyWalletService money,
            ScrapWalletServiceV1 scrap,
            StableId scrapAuthorityId,
            StableId scrapCurrencyId,
            RankedSkillAllocationAuthorityV2 skills,
            string skillProfileId,
            ProductionCharacterStrongboxRuntimeV1 strongboxes)
        {
            return new List<ISaveComponentAdapterV1>
            {
                Experience(
                    experience,
                    experienceCurve,
                    progressionContext),
                Holdings(loadout),
                Money(money),
                Scrap(scrap, scrapAuthorityId, scrapCurrencyId),
                Skills(skills, skillProfileId),
                Loadout(loadout),
                Strongboxes(strongboxes),
            };
        }

        public static TSnapshot DecodeRequired<TSnapshot>(
            CharacterInstanceSnapshotV1 character,
            SaveComponentDefinitionV1 definition,
            ISaveComponentPayloadCodecV1<TSnapshot> codec)
            where TSnapshot : class
        {
            SaveComponentSnapshotV1 component;
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

        private static ISaveComponentAdapterV1 Experience(
            PlayerExperienceAuthorityV1 authority,
            PlayerExperienceCurveV1 curve,
            ProgressionContext context)
        {
            return KnownSaveComponentAdaptersV1.PlayerExperience(
                authority.ExportSnapshot,
                snapshot =>
                {
                    var verifier = new PlayerExperienceAuthorityV1(
                        curve,
                        context);
                    PlayerExperienceImportResultV1 result =
                        verifier.TryImport(snapshot);
                    return result.Status
                            == PlayerExperienceImportStatusV1.Imported
                        ? SaveComponentValidationResultV1.Accept()
                        : SaveComponentValidationResultV1.Reject(
                            result.RejectionCode);
                },
                snapshot =>
                {
                    PlayerExperienceImportResultV1 result =
                        authority.TryImport(snapshot);
                    return result.Status
                            == PlayerExperienceImportStatusV1.Imported
                        ? SaveComponentApplyResultV1.Applied()
                        : SaveComponentApplyResultV1.Rejected(
                            result.RejectionCode);
                });
        }

        private static ISaveComponentAdapterV1 Holdings(
            ProductionPlayerLoadoutRuntimeV1 runtime)
        {
            return KnownSaveComponentAdaptersV1.PlayerHoldings(
                runtime.Holdings.ExportSnapshot,
                snapshot =>
                {
                    var verifier = new PlayerHoldingsService(
                        runtime.Holdings.AuthorityStableId,
                        999L,
                        runtime.CatalogAdapter);
                    PlayerHoldingsImportResultV1 result =
                        verifier.ImportSnapshot(snapshot);
                    return result.Succeeded
                        ? SaveComponentValidationResultV1.Accept()
                        : SaveComponentValidationResultV1.Reject(
                            result.RejectionCode);
                },
                snapshot =>
                {
                    PlayerHoldingsImportResultV1 result =
                        runtime.Holdings.ImportSnapshot(snapshot);
                    return result.Succeeded
                        ? SaveComponentApplyResultV1.Applied()
                        : SaveComponentApplyResultV1.Rejected(
                            result.RejectionCode);
                });
        }

        private static ISaveComponentAdapterV1 Money(
            MoneyWalletService authority)
        {
            return KnownSaveComponentAdaptersV1.MoneyWallet(
                () => authority.CurrentSnapshot,
                snapshot =>
                {
                    MoneyWalletImportResult result =
                        new MoneyWalletService().ImportSnapshot(snapshot);
                    return result.Status == MoneyWalletImportStatus.Imported
                        ? SaveComponentValidationResultV1.Accept()
                        : SaveComponentValidationResultV1.Reject(
                            result.RejectionCode);
                },
                snapshot =>
                {
                    MoneyWalletImportResult result =
                        authority.ImportSnapshot(snapshot);
                    return result.Status == MoneyWalletImportStatus.Imported
                        ? SaveComponentApplyResultV1.Applied()
                        : SaveComponentApplyResultV1.Rejected(
                            result.RejectionCode);
                });
        }

        private static ISaveComponentAdapterV1 Scrap(
            ScrapWalletServiceV1 authority,
            StableId authorityId,
            StableId currencyId)
        {
            return KnownSaveComponentAdaptersV1.ScrapWallet(
                authority.ExportSnapshot,
                snapshot =>
                {
                    ScrapSnapshotImportResultV1 result =
                        new ScrapWalletServiceV1(authorityId, currencyId)
                            .ImportSnapshot(snapshot);
                    return result.Succeeded
                        ? SaveComponentValidationResultV1.Accept()
                        : SaveComponentValidationResultV1.Reject(
                            result.RejectionCode);
                },
                snapshot =>
                {
                    ScrapSnapshotImportResultV1 result =
                        authority.ImportSnapshot(snapshot);
                    return result.Succeeded
                        ? SaveComponentApplyResultV1.Applied()
                        : SaveComponentApplyResultV1.Rejected(
                            result.RejectionCode);
                });
        }

        private static ISaveComponentAdapterV1 Skills(
            RankedSkillAllocationAuthorityV2 authority,
            string profileId)
        {
            return KnownSaveComponentAdaptersV1.RankedSkillAllocation(
                () => authority.Get(profileId),
                snapshot => KnownSaveComponentCodecsV1.RankedSkillAllocation
                    .Validate(snapshot),
                snapshot =>
                {
                    authority.Seed(snapshot);
                    return authority.Get(snapshot.ProfileId).Fingerprint
                            == snapshot.Fingerprint
                        ? SaveComponentApplyResultV1.Applied()
                        : SaveComponentApplyResultV1.Rejected(
                            "ranked-skill-seed-mismatch");
                });
        }

        private static ISaveComponentAdapterV1 Loadout(
            ProductionPlayerLoadoutRuntimeV1 runtime)
        {
            return KnownSaveComponentAdaptersV1.ExactInstanceLoadout(
                runtime.LoadoutAuthority.ExportSnapshot,
                snapshot => KnownSaveComponentCodecsV1.ExactInstanceLoadout
                    .Validate(snapshot),
                snapshot =>
                {
                    ProductionInventoryLoadoutImportResultV1 result =
                        runtime.LoadoutAuthority.ImportSnapshot(snapshot);
                    return result.Succeeded
                        ? SaveComponentApplyResultV1.Applied()
                        : SaveComponentApplyResultV1.Rejected(
                            result.RejectionCode);
                });
        }

        private static ISaveComponentAdapterV1 Strongboxes(
            ProductionCharacterStrongboxRuntimeV1 runtime)
        {
            return KnownSaveComponentAdaptersV1.StrongboxState(
                runtime.Authority.ExportSnapshot,
                snapshot =>
                {
                    SaveComponentValidationResultV1 validation =
                        KnownSaveComponentCodecsV1.StrongboxState.Validate(
                            snapshot);
                    if (!validation.Succeeded)
                    {
                        return validation;
                    }
                    return string.Equals(
                            snapshot.DefinitionCatalogFingerprint,
                            runtime.Catalog.Fingerprint,
                            StringComparison.Ordinal)
                        ? SaveComponentValidationResultV1.Accept()
                        : SaveComponentValidationResultV1.Reject(
                            "strongbox-snapshot-catalog-mismatch");
                },
                snapshot =>
                {
                    StrongboxOpeningImportResultV1 result =
                        runtime.Authority.ImportSnapshot(snapshot);
                    return result.Succeeded
                        ? SaveComponentApplyResultV1.Applied()
                        : SaveComponentApplyResultV1.Rejected(
                            result.RejectionCode);
                });
        }
    }
}
