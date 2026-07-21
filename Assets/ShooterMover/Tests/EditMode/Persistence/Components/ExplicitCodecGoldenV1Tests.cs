using System;
using System.Collections.Generic;
using NUnit.Framework;
using ShooterMover.Application.Economy.Money;
using ShooterMover.Application.Economy.Scrap;
using ShooterMover.Application.Holdings;
using ShooterMover.Application.Inventory.LoadoutScreen;
using ShooterMover.Application.Persistence.Components;
using ShooterMover.Application.Progression.Experience;
using ShooterMover.Contracts.Equipment;
using ShooterMover.Contracts.Holdings;
using ShooterMover.Contracts.Progression.Experience;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Economy.Money;
using ShooterMover.Domain.Economy.Scrap;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Progression.Context;
using ShooterMover.Domain.Progression.Curves;
using ShooterMover.Domain.Progression.Experience;
using ShooterMover.Domain.Progression.Skills;
using ShooterMover.Domain.Rewards.Model;
using ShooterMover.Domain.Rewards.Strongboxes;
using ShooterMover.Application.Rewards.Strongboxes;

namespace ShooterMover.Tests.EditMode.Persistence.Components
{
    public sealed class ExplicitCodecGoldenV1Tests
    {
        [Test]
        public void PlayerExperienceCodecHasAuthoredFieldOrder()
        {
            PlayerExperienceCurveV1 curve = Curve();
            var authority = new PlayerExperienceAuthorityV1(
                curve,
                Context(1));
            authority.Grant(new PlayerExperienceGrantRequestV1(
                Id("xp-source.codec-golden"),
                100L));
            AssertCanonical(
                KnownSaveComponentCodecsV1.PlayerExperience,
                authority.ExportSnapshot(),
                "PlayerExperienceSnapshotV1",
                "schema_version",
                "authority_id",
                "sequence",
                "curve_fingerprint",
                "cumulative_experience",
                "progression_context",
                "grants");
        }

        [Test]
        public void PlayerHoldingsCodecHasAuthoredFieldOrder()
        {
            StableId authorityId = Id("authority.holdings.codec-golden");
            var authority = new PlayerHoldingsService(
                authorityId,
                100L,
                new AcceptingEquipmentValidator());
            authority.Apply(PlayerHoldingsCommandV1.AddStack(
                Id("transaction.holdings.codec-golden"),
                Id("operation.holdings.codec-golden"),
                authorityId,
                RewardGrantKindV1.Miscellaneous,
                Id("misc.codec-golden"),
                3L,
                HoldingProvenanceV1.Create(
                    Id("grant.holdings.codec-golden"),
                    Id("source.holdings.codec-golden")),
                0L));
            AssertCanonical(
                KnownSaveComponentCodecsV1.PlayerHoldings,
                authority.ExportSnapshot(),
                "PlayerHoldingsSnapshotV1",
                "schema_version",
                "authority_id",
                "maximum_stack_quantity",
                "ledger",
                "unique_holdings",
                "stack_holdings",
                "transactions");
        }

        [Test]
        public void MoneyWalletCodecHasAuthoredFieldOrder()
        {
            var authority = new MoneyWalletService();
            authority.Grant(
                Id("transaction.money.codec-golden"),
                Id("operation.money.codec-golden"),
                7L);
            AssertCanonical(
                KnownSaveComponentCodecsV1.MoneyWallet,
                authority.CurrentSnapshot,
                "MoneyWalletSnapshot",
                "schema_version",
                "sequence",
                "contributions",
                "transactions");
        }

        [Test]
        public void ScrapWalletCodecHasAuthoredFieldOrder()
        {
            StableId authorityId = Id("authority.scrap.codec-golden");
            StableId currencyId = Id("currency.scrap");
            var authority = new ScrapWalletServiceV1(authorityId, currencyId);
            StableId operationId = Id("operation.scrap.codec-golden");
            authority.Apply(new ScrapTransactionCommandV1(
                Id("transaction.scrap.codec-golden"),
                operationId,
                authorityId,
                currencyId,
                ScrapMutationKindV1.Grant,
                11L,
                ScrapIdentityV1.RewardGrantReason,
                new ScrapProvenanceV1(
                    ScrapIdentityV1.RewardSourceKind,
                    operationId,
                    Id("commitment.scrap.codec-golden")),
                0L));
            AssertCanonical(
                KnownSaveComponentCodecsV1.ScrapWallet,
                authority.ExportSnapshot(),
                "ScrapSnapshotV1",
                "schema_version",
                "authority_id",
                "currency_id",
                "balance",
                "ledger");
        }

        [Test]
        public void RankedSkillCodecHasAuthoredFieldOrder()
        {
            var snapshot = new RankedSkillAllocationSnapshotV2(
                "profile.codec-golden",
                "striker",
                4L,
                "skill-schema-v2",
                "skill-content-v7",
                new Dictionary<string, int>
                {
                    { "generic.movement_speed", 2 },
                    { "striker.weapon_damage", 1 },
                });
            AssertCanonical(
                KnownSaveComponentCodecsV1.RankedSkillAllocation,
                snapshot,
                "RankedSkillAllocationSnapshotV2",
                "profile_id",
                "class_id",
                "version",
                "schema_version",
                "content_version",
                "ranks");
        }

        [Test]
        public void ExactInstanceLoadoutCodecHasAuthoredFieldOrder()
        {
            var bindings = new List<InventoryLoadoutSlotBindingV1>();
            for (int index = 0; index < InventoryLoadoutSlotsV1.All.Count; index++)
            {
                InventoryLoadoutSlotDescriptorV1 slot =
                    InventoryLoadoutSlotsV1.All[index];
                bindings.Add(new InventoryLoadoutSlotBindingV1(
                    slot.SlotStableId,
                    index == 0
                        ? Id("equipment-instance.codec-golden")
                        : null));
            }
            InventoryLoadoutAuthoritySnapshotV1 snapshot =
                InventoryLoadoutAuthoritySnapshotV1.CreateCanonical(
                    1L,
                    bindings);
            AssertCanonical(
                KnownSaveComponentCodecsV1.ExactInstanceLoadout,
                snapshot,
                "InventoryLoadoutAuthoritySnapshotV1",
                "sequence",
                "bindings");
        }

        [Test]
        public void StrongboxOpeningCodecHasAuthoredFieldOrder()
        {
            string catalogFingerprint = new string('a', 64);
            StrongboxInstanceContextV1 context =
                StrongboxInstanceContextV1.Create(
                    Id("strongbox.instance.codec-golden"),
                    Id("strongbox.tier.codec-golden"),
                    123UL,
                    1,
                    Context(5),
                    Id("source-context.strongbox.codec-golden"),
                    Id("grant.strongbox.codec-golden"),
                    new string('b', 64));
            StrongboxOpeningSnapshotV1 snapshot =
                StrongboxOpeningSnapshotV1.CreateCanonical(
                    catalogFingerprint,
                    0L,
                    new[] { context },
                    Array.Empty<StrongboxOpeningRecordSnapshotV1>());
            AssertCanonical(
                KnownSaveComponentCodecsV1.StrongboxState,
                snapshot,
                "StrongboxOpeningSnapshotV1",
                "schema_version",
                "definition_catalog_fingerprint",
                "sequence",
                "contexts",
                "openings");
        }

        private static void AssertCanonical<TSnapshot>(
            ISaveComponentPayloadCodecV1<TSnapshot> codec,
            TSnapshot snapshot,
            string forbiddenClrType,
            params string[] fields)
            where TSnapshot : class
        {
            string payload = codec.Encode(snapshot);
            TSnapshot decoded;
            string rejection;
            Assert.That(codec.TryDecode(
                payload,
                out decoded,
                out rejection), Is.True, rejection);
            Assert.That(codec.Encode(decoded), Is.EqualTo(payload));
            Assert.That(payload, Does.Not.Contain(forbiddenClrType));
            Assert.That(payload, Does.Not.Contain("System."));

            int previous = -1;
            for (int index = 0; index < fields.Length; index++)
            {
                string token = "V" + fields[index].Length + ":" + fields[index];
                int position = payload.IndexOf(token, StringComparison.Ordinal);
                Assert.That(position, Is.GreaterThan(previous), fields[index]);
                previous = position;
            }
        }

        private static PlayerExperienceCurveV1 Curve()
        {
            return new PlayerExperienceCurveV1(
                100L,
                100L,
                50,
                new SoftActivationCurveParameters(0.1, 10L, 10L));
        }

        private static ProgressionContext Context(int level)
        {
            return ProgressionContext.Create(
                level,
                1,
                Id("difficulty.normal"),
                0,
                new[] { Id("progression-tag.campaign") });
        }

        private static StableId Id(string value)
        {
            return StableId.Parse(value);
        }

        private sealed class AcceptingEquipmentValidator :
            IEquipmentInstanceValidator
        {
            public EquipmentInstanceValidationResponse Validate(
                EquipmentInstanceValidationRequest request)
            {
                return new EquipmentInstanceValidationResponse(
                    request != null && request.Instance != null,
                    "codec-golden-catalog",
                    request == null || request.Instance == null
                        ? null
                        : request.Instance.Fingerprint,
                    Array.Empty<EquipmentModelIssue>());
            }
        }
    }
}
