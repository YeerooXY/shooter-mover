using System;
using System.Collections.Generic;
using NUnit.Framework;
using ShooterMover.Application.Economy.Money;
using ShooterMover.Application.Economy.Scrap;
using ShooterMover.Application.Holdings;
using ShooterMover.Application.Persistence.SaveParts;
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

namespace ShooterMover.Tests.EditMode.Persistence.SaveParts
{
    public sealed class ExplicitCodecGoldenTests
    {
        [Test]
        public void PlayerExperienceCodecHasAuthoredFieldOrder()
        {
            PlayerExperienceCurve curve = Curve();
            var authority = new PlayerExperience(
                curve,
                Context(1));
            authority.Grant(new PlayerExperienceGrantRequest(
                Id("xp-source.codec-golden"),
                100L));
            AssertCanonical(
                GameSaveFormats.PlayerExperience,
                authority.ExportSnapshot(),
                "PlayerExperienceSnapshot",
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
            var authority = new PlayerHoldingsActions(
                authorityId,
                100L,
                new AcceptingEquipmentValidator());
            authority.Apply(PlayerHoldingsCommand.AddStack(
                Id("transaction.holdings.codec-golden"),
                Id("operation.holdings.codec-golden"),
                authorityId,
                RewardGrantKind.Miscellaneous,
                Id("misc.codec-golden"),
                3L,
                HoldingProvenance.Create(
                    Id("grant.holdings.codec-golden"),
                    Id("source.holdings.codec-golden")),
                0L));
            AssertCanonical(
                GameSaveFormats.PlayerHoldings,
                authority.ExportSnapshot(),
                "PlayerHoldingsSnapshot",
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
            var authority = new MoneyWalletActions();
            authority.Grant(
                Id("transaction.money.codec-golden"),
                Id("operation.money.codec-golden"),
                7L);
            AssertCanonical(
                GameSaveFormats.MoneyWallet,
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
            var authority = new ScrapWalletActions(authorityId, currencyId);
            StableId operationId = Id("operation.scrap.codec-golden");
            authority.Apply(new ScrapTransactionCommand(
                Id("transaction.scrap.codec-golden"),
                operationId,
                authorityId,
                currencyId,
                ScrapMutationKind.Grant,
                11L,
                ScrapIdentity.RewardGrantReason,
                new ScrapProvenance(
                    ScrapIdentity.LootSourceKind,
                    operationId,
                    Id("commitment.scrap.codec-golden")),
                0L));
            AssertCanonical(
                GameSaveFormats.ScrapWallet,
                authority.ExportSnapshot(),
                "ScrapSnapshot",
                "schema_version",
                "authority_id",
                "currency_id",
                "balance",
                "ledger");
        }

        [Test]
        public void RankedSkillCodecHasAuthoredFieldOrder()
        {
            var snapshot = new RankedSkillAllocationSnapshot(
                "profile.codec-golden",
                "striker",
                4L,
                "skill-schema-v2",
                "skill-content-v7",
                new Dictionary<string, int>
                {
                    { "generic.movement_speed", 2 },
                    { "striker.gun_damage", 1 },
                });
            AssertCanonical(
                GameSaveFormats.RankedSkillAllocation,
                snapshot,
                "RankedSkillAllocationSnapshot",
                "profile_id",
                "class_id",
                "version",
                "schema_version",
                "content_version",
                "ranks");
        }

        [Test]
        public void StrongboxOpeningCodecHasAuthoredFieldOrder()
        {
            string catalogFingerprint = new string('a', 64);
            StrongboxInstanceContext context =
                StrongboxInstanceContext.Create(
                    Id("strongbox.instance.codec-golden"),
                    Id("strongbox.tier.codec-golden"),
                    123UL,
                    1,
                    Context(5),
                    Id("source-context.strongbox.codec-golden"),
                    Id("grant.strongbox.codec-golden"),
                    new string('b', 64));
            StrongboxOpeningSnapshot snapshot =
                StrongboxOpeningSnapshot.CreateCanonical(
                    catalogFingerprint,
                    0L,
                    new[] { context },
                    Array.Empty<StrongboxOpeningRecordSnapshot>());
            AssertCanonical(
                GameSaveFormats.StrongboxState,
                snapshot,
                "StrongboxOpeningSnapshot",
                "schema_version",
                "definition_catalog_fingerprint",
                "sequence",
                "contexts",
                "openings");
        }

        private static void AssertCanonical<TSnapshot>(
            ISavePartFormat<TSnapshot> codec,
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

        private static PlayerExperienceCurve Curve()
        {
            return new PlayerExperienceCurve(
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
