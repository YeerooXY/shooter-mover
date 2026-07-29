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
using ShooterMover.Domain.Holdings;
using ShooterMover.Domain.Rewards.Application;
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
            RewardCommitCommand commit = Commit(
                Value("grant.money", RewardGrantKind.Money, MoneyWalletIds.CurrencyStableId, 31L));
            fixture.Service.Commit(commit);
            RewardClaimCommand claim = fixture.Claim(commit);

            Assert.That(fixture.Service.Claim(claim).Status,
                Is.EqualTo(RewardApplicationResultStatus.Applied));
            Assert.That(fixture.Service.Claim(claim).Status,
                Is.EqualTo(RewardApplicationResultStatus.AlreadyAppliedNoChange));
            Assert.That(fixture.Money.Balance, Is.EqualTo(31L));
            Assert.That(fixture.Money.Sequence, Is.EqualTo(1L));
        }

        [Test]
        public void RealScrapAuthorityAppliesExactlyOnce()
        {
            RealFixture fixture = new RealFixture();
            RewardCommitCommand commit = Commit(
                Value("grant.scrap", RewardGrantKind.Scrap, ScrapCurrency, 17L));
            fixture.Service.Commit(commit);
            RewardClaimCommand claim = fixture.Claim(commit);

            Assert.That(fixture.Service.Claim(claim).Status,
                Is.EqualTo(RewardApplicationResultStatus.Applied));
            Assert.That(fixture.Service.Claim(claim).Status,
                Is.EqualTo(RewardApplicationResultStatus.AlreadyAppliedNoChange));
            Assert.That(fixture.Scrap.Balance, Is.EqualTo(17L));
            Assert.That(fixture.Scrap.Sequence, Is.EqualTo(1L));
        }

        [Test]
        public void RealHoldingsAuthorityOwnsStrongboxOnce()
        {
            RealFixture fixture = new RealFixture();
            StableId instanceId = Id("strongbox-instance.integration");
            RewardCommitCommand commit = Commit(
                RewardGrantApplicationPayload.ForStrongboxes(
                    RewardGrant.Create(
                        Id("grant.strongbox"),
                        RewardGrantKind.Strongbox,
                        Id("strongbox-definition.tier-one"),
                        1L),
                    new[] { instanceId }));
            fixture.Service.Commit(commit);

            Assert.That(fixture.Service.Claim(fixture.Claim(commit)).Status,
                Is.EqualTo(RewardApplicationResultStatus.Applied));
            UniqueHoldingSnapshot holding;
            Assert.That(fixture.Holdings.TryGetUnique(instanceId, out holding), Is.True);
            Assert.That(holding.RewardKind, Is.EqualTo(RewardGrantKind.Strongbox));
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
            RewardCommitCommand commit = Commit(
                RewardGrantApplicationPayload.ForEquipment(
                    RewardGrant.Create(
                        Id("grant.equipment"),
                        RewardGrantKind.EquipmentReference,
                        equipment.DefinitionId,
                        1L),
                    new[] { equipment }));
            fixture.Service.Commit(commit);

            Assert.That(fixture.Service.Claim(fixture.Claim(commit)).Status,
                Is.EqualTo(RewardApplicationResultStatus.Applied));
            UniqueHoldingSnapshot holding;
            Assert.That(fixture.Holdings.TryGetUnique(equipment.InstanceId, out holding), Is.True);
            Assert.That(holding.EquipmentInstance.Fingerprint, Is.EqualTo(equipment.Fingerprint));
        }

        [Test]
        public void RealHoldingsAuthorityOwnsMiscellaneousStackOnce()
        {
            RealFixture fixture = new RealFixture();
            StableId itemId = Id("misc.integration-widget");
            RewardCommitCommand commit = Commit(
                Value("grant.misc", RewardGrantKind.Miscellaneous, itemId, 8L));
            fixture.Service.Commit(commit);

            Assert.That(fixture.Service.Claim(fixture.Claim(commit)).Status,
                Is.EqualTo(RewardApplicationResultStatus.Applied));
            Assert.That(fixture.Holdings.GetStackQuantity(
                RewardGrantKind.Miscellaneous,
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
            RewardCommitCommand commit = Commit(
                Value("grant.money", RewardGrantKind.Money,
                    MoneyWalletIds.CurrencyStableId, 40L),
                RewardGrantApplicationPayload.ForEquipment(
                    RewardGrant.Create(
                        Id("grant.equipment"),
                        RewardGrantKind.EquipmentReference,
                        equipment.DefinitionId,
                        1L),
                    new[] { equipment }));
            fixture.Service.Commit(commit);

            RewardApplicationResult result = fixture.Service.Claim(fixture.Claim(commit));

            Assert.That(result.Status,
                Is.EqualTo(RewardApplicationResultStatus.ChildAuthorityRejected));
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
            RewardCommitCommand commit = Commit(
                Value("grant.money", RewardGrantKind.Money, MoneyWalletIds.CurrencyStableId, 40L),
                Value("grant.scrap", RewardGrantKind.Scrap, ScrapCurrency, 9L),
                RewardGrantApplicationPayload.ForEquipment(
                    RewardGrant.Create(
                        Id("grant.equipment"),
                        RewardGrantKind.EquipmentReference,
                        equipment.DefinitionId,
                        1L),
                    new[] { equipment }),
                RewardGrantApplicationPayload.ForStrongboxes(
                    RewardGrant.Create(
                        Id("grant.strongbox"),
                        RewardGrantKind.Strongbox,
                        Id("strongbox-definition.tier-two"),
                        1L),
                    new[] { boxInstance }));
            fixture.Service.Commit(commit);

            RewardApplicationResult result = fixture.Service.Claim(fixture.Claim(commit));

            Assert.That(result.Status, Is.EqualTo(RewardApplicationResultStatus.Applied));
            Assert.That(fixture.Money.Balance, Is.EqualTo(40L));
            Assert.That(fixture.Scrap.Balance, Is.EqualTo(9L));
            UniqueHoldingSnapshot holding;
            Assert.That(fixture.Holdings.TryGetUnique(equipment.InstanceId, out holding), Is.True);
            Assert.That(fixture.Holdings.TryGetUnique(boxInstance, out holding), Is.True);
            Assert.That(fixture.Holdings.Sequence, Is.EqualTo(2L));
        }

        private static RewardGrantApplicationPayload Value(
            string grantId,
            RewardGrantKind kind,
            StableId contentId,
            long quantity)
        {
            return RewardGrantApplicationPayload.ForValue(
                RewardGrant.Create(Id(grantId), kind, contentId, quantity));
        }

        private static RewardCommitCommand Commit(
            params RewardGrantApplicationPayload[] payloads)
        {
            RewardOperationRequest operation = RewardOperationRequest.Create(
                Id("run.integration"),
                Id("source-instance.integration"),
                Id("source-operation.integration"),
                Id("commitment.integration"),
                Id("reward-profile.integration"),
                Hash('c'));
            var grants = new List<RewardGrant>();
            for (int index = 0; index < payloads.Length; index++)
            {
                grants.Add(payloads[index].Grant);
            }

            return RewardCommitCommand.Create(
                operation,
                RewardResult.CreateGrants(
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
            return RewardApplication.Fingerprint(value.ToString());
        }

        private sealed class RealFixture
        {
            private readonly IEquipmentInstanceValidator validator;

            public RealFixture(IEquipmentInstanceValidator validator = null)
            {
                Money = new MoneyWalletActions();
                Scrap = new ScrapWalletActions(ScrapAuthority, ScrapCurrency);
                this.validator = validator ?? new AcceptingEquipmentValidator();
                Holdings = new PlayerHoldingsActions(
                    HoldingsAuthority,
                    1000L,
                    this.validator);
                Service = new RewardApplicationActions(
                    RapAuthority,
                    new MoneyRewardChildState(Money),
                    new ScrapRewardChildState(Scrap),
                    new PlayerHoldingsRewardChildState(Holdings, this.validator));
            }

            public MoneyWalletActions Money { get; }
            public ScrapWalletActions Scrap { get; }
            public PlayerHoldingsActions Holdings { get; }
            public RewardApplicationActions Service { get; }

            public RewardClaimCommand Claim(RewardCommitCommand commit)
            {
                return RewardClaimCommand.Create(
                    Id("claim.integration"),
                    commit.CommitmentStableId,
                    Id("player.integration"),
                    MoneyWalletIds.AuthorityStableId,
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
