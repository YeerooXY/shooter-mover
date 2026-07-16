using System;
using System.Collections.Generic;
using NUnit.Framework;
using ShooterMover.Application.Economy.Money;
using ShooterMover.Application.Economy.Scrap;
using ShooterMover.Application.Holdings;
using ShooterMover.Application.Rewards.Application;
using ShooterMover.Contracts.Equipment;
using ShooterMover.Contracts.Holdings;
using ShooterMover.Contracts.Rewards;
using ShooterMover.Contracts.Rewards.Application;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Economy.Money;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Rewards.Model;

namespace ShooterMover.Tests.EditMode.Rewards.Application
{
    public sealed class RewardApplicationIntegrationTests
    {
        private static readonly StableId RapAuthority = Id("authority.reward-application");
        private static readonly StableId ScrapAuthority = Id("authority.scrap");
        private static readonly StableId ScrapCurrency = Id("currency.scrap");
        private static readonly StableId HoldingsAuthority = Id("holdings.player-profile");

        [Test]
        public void RealMoneyAuthorityAppliesExactlyOnce()
        {
            RealFixture fixture = new RealFixture();
            RewardCommitCommandV1 commit = Commit(
                Value("grant.money", RewardGrantKindV1.Money, MoneyWalletIdsV1.CurrencyStableId, 31L));
            fixture.Service.Commit(commit);
            RewardClaimCommandV1 claim = fixture.Claim(commit);

            Assert.That(fixture.Service.Claim(claim).Status,
                Is.EqualTo(RewardApplicationResultStatusV1.Applied));
            Assert.That(fixture.Service.Claim(claim).Status,
                Is.EqualTo(RewardApplicationResultStatusV1.AlreadyAppliedNoChange));
            Assert.That(fixture.Money.Balance, Is.EqualTo(31L));
            Assert.That(fixture.Money.Sequence, Is.EqualTo(1L));
        }

        [Test]
        public void RealScrapAuthorityAppliesExactlyOnce()
        {
            RealFixture fixture = new RealFixture();
            RewardCommitCommandV1 commit = Commit(
                Value("grant.scrap", RewardGrantKindV1.Scrap, ScrapCurrency, 17L));
            fixture.Service.Commit(commit);
            RewardClaimCommandV1 claim = fixture.Claim(commit);

            Assert.That(fixture.Service.Claim(claim).Status,
                Is.EqualTo(RewardApplicationResultStatusV1.Applied));
            Assert.That(fixture.Service.Claim(claim).Status,
                Is.EqualTo(RewardApplicationResultStatusV1.AlreadyAppliedNoChange));
            Assert.That(fixture.Scrap.Balance, Is.EqualTo(17L));
            Assert.That(fixture.Scrap.Sequence, Is.EqualTo(1L));
        }

        [Test]
        public void RealHoldingsAuthorityOwnsStrongboxOnce()
        {
            RealFixture fixture = new RealFixture();
            StableId instanceId = Id("strongbox-instance.integration");
            RewardCommitCommandV1 commit = Commit(
                RewardGrantApplicationPayloadV1.ForStrongboxes(
                    RewardGrantV1.Create(
                        Id("grant.strongbox"),
                        RewardGrantKindV1.Strongbox,
                        Id("strongbox-definition.tier-one"),
                        1L),
                    new[] { instanceId }));
            fixture.Service.Commit(commit);

            Assert.That(fixture.Service.Claim(fixture.Claim(commit)).Status,
                Is.EqualTo(RewardApplicationResultStatusV1.Applied));
            UniqueHoldingSnapshotV1 holding;
            Assert.That(fixture.Holdings.TryGetUnique(instanceId, out holding), Is.True);
            Assert.That(holding.RewardKind, Is.EqualTo(RewardGrantKindV1.Strongbox));
        }

        [Test]
        public void RealHoldingsAuthorityOwnsEquipmentOnce()
        {
            RealFixture fixture = new RealFixture();
            EquipmentInstance equipment = EquipmentInstance.Create(
                Id("equipment-instance.integration"),
                Id("equipment-definition.blaster"),
                1,
                Id("quality.common"),
                Array.Empty<AugmentInstance>());
            RewardCommitCommandV1 commit = Commit(
                RewardGrantApplicationPayloadV1.ForEquipment(
                    RewardGrantV1.Create(
                        Id("grant.equipment"),
                        RewardGrantKindV1.EquipmentReference,
                        equipment.DefinitionId,
                        1L),
                    new[] { equipment }));
            fixture.Service.Commit(commit);

            Assert.That(fixture.Service.Claim(fixture.Claim(commit)).Status,
                Is.EqualTo(RewardApplicationResultStatusV1.Applied));
            UniqueHoldingSnapshotV1 holding;
            Assert.That(fixture.Holdings.TryGetUnique(equipment.InstanceId, out holding), Is.True);
            Assert.That(holding.EquipmentInstance.Fingerprint, Is.EqualTo(equipment.Fingerprint));
        }

        [Test]
        public void RealHoldingsAuthorityOwnsMiscellaneousStackOnce()
        {
            RealFixture fixture = new RealFixture();
            StableId itemId = Id("misc.integration-widget");
            RewardCommitCommandV1 commit = Commit(
                Value("grant.misc", RewardGrantKindV1.Miscellaneous, itemId, 8L));
            fixture.Service.Commit(commit);

            Assert.That(fixture.Service.Claim(fixture.Claim(commit)).Status,
                Is.EqualTo(RewardApplicationResultStatusV1.Applied));
            Assert.That(fixture.Holdings.GetStackQuantity(
                RewardGrantKindV1.Miscellaneous,
                itemId), Is.EqualTo(8L));
        }

        [Test]
        public void InvalidEquipmentPreflightLeavesRealMoneyAndHoldingsUnchanged()
        {
            RealFixture fixture = new RealFixture(new RejectingEquipmentValidator());
            EquipmentInstance equipment = EquipmentInstance.Create(
                Id("equipment-instance.rejected-integration"),
                Id("equipment-definition.blaster"),
                1,
                Id("quality.common"),
                Array.Empty<AugmentInstance>());
            RewardCommitCommandV1 commit = Commit(
                Value("grant.money", RewardGrantKindV1.Money,
                    MoneyWalletIdsV1.CurrencyStableId, 40L),
                RewardGrantApplicationPayloadV1.ForEquipment(
                    RewardGrantV1.Create(
                        Id("grant.equipment"),
                        RewardGrantKindV1.EquipmentReference,
                        equipment.DefinitionId,
                        1L),
                    new[] { equipment }));
            fixture.Service.Commit(commit);

            RewardApplicationResultV1 result = fixture.Service.Claim(fixture.Claim(commit));

            Assert.That(result.Status,
                Is.EqualTo(RewardApplicationResultStatusV1.ChildAuthorityRejected));
            Assert.That(fixture.Money.Balance, Is.Zero);
            Assert.That(fixture.Money.Sequence, Is.Zero);
            Assert.That(fixture.Holdings.Sequence, Is.Zero);
        }

        [Test]
        public void RealMoneyScrapAndHoldingsApplyMixedRewardCompletely()
        {
            RealFixture fixture = new RealFixture();
            EquipmentInstance equipment = EquipmentInstance.Create(
                Id("equipment-instance.mixed-integration"),
                Id("equipment-definition.blaster"),
                1,
                Id("quality.common"),
                Array.Empty<AugmentInstance>());
            StableId boxInstance = Id("strongbox-instance.mixed-integration");
            RewardCommitCommandV1 commit = Commit(
                Value("grant.money", RewardGrantKindV1.Money, MoneyWalletIdsV1.CurrencyStableId, 40L),
                Value("grant.scrap", RewardGrantKindV1.Scrap, ScrapCurrency, 9L),
                RewardGrantApplicationPayloadV1.ForEquipment(
                    RewardGrantV1.Create(
                        Id("grant.equipment"),
                        RewardGrantKindV1.EquipmentReference,
                        equipment.DefinitionId,
                        1L),
                    new[] { equipment }),
                RewardGrantApplicationPayloadV1.ForStrongboxes(
                    RewardGrantV1.Create(
                        Id("grant.strongbox"),
                        RewardGrantKindV1.Strongbox,
                        Id("strongbox-definition.tier-two"),
                        1L),
                    new[] { boxInstance }));
            fixture.Service.Commit(commit);

            RewardApplicationResultV1 result = fixture.Service.Claim(fixture.Claim(commit));

            Assert.That(result.Status, Is.EqualTo(RewardApplicationResultStatusV1.Applied));
            Assert.That(fixture.Money.Balance, Is.EqualTo(40L));
            Assert.That(fixture.Scrap.Balance, Is.EqualTo(9L));
            UniqueHoldingSnapshotV1 holding;
            Assert.That(fixture.Holdings.TryGetUnique(equipment.InstanceId, out holding), Is.True);
            Assert.That(fixture.Holdings.TryGetUnique(boxInstance, out holding), Is.True);
            Assert.That(fixture.Holdings.Sequence, Is.EqualTo(2L));
        }

        private static RewardGrantApplicationPayloadV1 Value(
            string grantId,
            RewardGrantKindV1 kind,
            StableId contentId,
            long quantity)
        {
            return RewardGrantApplicationPayloadV1.ForValue(
                RewardGrantV1.Create(Id(grantId), kind, contentId, quantity));
        }

        private static RewardCommitCommandV1 Commit(
            params RewardGrantApplicationPayloadV1[] payloads)
        {
            RewardOperationRequestV1 operation = RewardOperationRequestV1.Create(
                Id("run.integration"),
                Id("source-instance.integration"),
                Id("source-operation.integration"),
                Id("commitment.integration"),
                Id("reward-profile.integration"),
                Hash('c'));
            var grants = new List<RewardGrantV1>();
            for (int index = 0; index < payloads.Length; index++)
            {
                grants.Add(payloads[index].Grant);
            }

            return RewardCommitCommandV1.Create(
                operation,
                RewardResultV1.CreateGrants(
                    operation.CommitmentStableId,
                    operation.SourceOperationStableId,
                    grants),
                Hash('g'),
                payloads);
        }

        private static StableId Id(string value)
        {
            return StableId.Parse(value);
        }

        private static string Hash(char value)
        {
            return "sha256:" + new string(value, 64);
        }

        private sealed class RealFixture
        {
            private readonly IEquipmentInstanceValidator validator;

            public RealFixture(IEquipmentInstanceValidator validator = null)
            {
                Money = new MoneyWalletService();
                Scrap = new ScrapWalletServiceV1(ScrapAuthority, ScrapCurrency);
                this.validator = validator ?? new AcceptingEquipmentValidator();
                Holdings = new PlayerHoldingsService(
                    HoldingsAuthority,
                    1000L,
                    this.validator);
                Service = new RewardApplicationServiceV1(
                    RapAuthority,
                    new MoneyRewardChildAuthorityV1(Money),
                    new ScrapRewardChildAuthorityV1(Scrap),
                    new PlayerHoldingsRewardChildAuthorityV1(Holdings, this.validator));
            }

            public MoneyWalletService Money { get; }
            public ScrapWalletServiceV1 Scrap { get; }
            public PlayerHoldingsService Holdings { get; }
            public RewardApplicationServiceV1 Service { get; }

            public RewardClaimCommandV1 Claim(RewardCommitCommandV1 commit)
            {
                return RewardClaimCommandV1.Create(
                    Id("claim.integration"),
                    commit.CommitmentStableId,
                    Id("player.integration"),
                    MoneyWalletIdsV1.AuthorityStableId,
                    ScrapAuthority,
                    HoldingsAuthority);
            }
        }

        private sealed class RejectingEquipmentValidator : IEquipmentInstanceValidator
        {
            public EquipmentInstanceValidationResponse Validate(
                EquipmentInstanceValidationRequest request)
            {
                return new EquipmentInstanceValidationResponse(
                    false,
                    "catalog-integration",
                    request == null || request.Instance == null
                        ? null
                        : request.Instance.Fingerprint,
                    Array.Empty<EquipmentModelIssue>());
            }
        }

        private sealed class AcceptingEquipmentValidator : IEquipmentInstanceValidator
        {
            public EquipmentInstanceValidationResponse Validate(
                EquipmentInstanceValidationRequest request)
            {
                return new EquipmentInstanceValidationResponse(
                    request != null && request.Instance != null,
                    "catalog-integration",
                    request == null || request.Instance == null
                        ? null
                        : request.Instance.Fingerprint,
                    Array.Empty<EquipmentModelIssue>());
            }
        }
    }
}
