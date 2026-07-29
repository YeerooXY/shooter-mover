using NUnit.Framework;
using ShooterMover.Application.Holdings;
using ShooterMover.Contracts.Economy;
using ShooterMover.Contracts.Equipment;
using ShooterMover.Contracts.Holdings;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Holdings;
using ShooterMover.Domain.Rewards.Model;

namespace ShooterMover.Tests.EditMode.Holdings
{
    public sealed class PlayerHoldingsReplayTests
    {
        private static readonly StableId AuthorityId =
            StableId.Parse("holdings.player-profile");

        [Test]
        public void ReplayAndConflictReturnTheOriginalTerminalFactAfterInterveningMutation()
        {
            var service = new PlayerHoldingsActions(
                AuthorityId,
                1000L,
                new AcceptingEquipmentValidator());
            PlayerHoldingsCommand original = AddMisc(
                "transaction.original",
                "misc.original",
                5L,
                0L);

            PlayerHoldingsMutationResult first = service.Apply(original);
            Assert.That(service.Apply(AddMisc(
                "transaction.intervening",
                "misc.intervening",
                1L,
                1L)).Status,
                Is.EqualTo(PlayerHoldingsMutationStatus.Applied));

            PlayerHoldingsMutationResult replay = service.Apply(original);
            PlayerHoldingsMutationResult conflict = service.Apply(AddMisc(
                "transaction.original",
                "misc.original",
                6L,
                0L));

            Assert.That(replay.Status,
                Is.EqualTo(PlayerHoldingsMutationStatus.ExactDuplicateNoChange));
            Assert.That(replay.OriginalStatus,
                Is.EqualTo(PlayerHoldingsMutationStatus.Applied));
            Assert.That(replay.PreviousSequence, Is.EqualTo(first.PreviousSequence));
            Assert.That(replay.CurrentSequence, Is.EqualTo(first.CurrentSequence));
            Assert.That(replay.PreviousQuantity, Is.EqualTo(first.PreviousQuantity));
            Assert.That(replay.CurrentQuantity, Is.EqualTo(first.CurrentQuantity));
            Assert.That(conflict.Status,
                Is.EqualTo(PlayerHoldingsMutationStatus.ConflictingDuplicate));
            Assert.That(conflict.OriginalStatus,
                Is.EqualTo(PlayerHoldingsMutationStatus.Applied));
            Assert.That(conflict.PreviousSequence, Is.EqualTo(first.PreviousSequence));
            Assert.That(conflict.CurrentSequence, Is.EqualTo(first.CurrentSequence));
            Assert.That(conflict.PreviousQuantity, Is.EqualTo(first.PreviousQuantity));
            Assert.That(conflict.CurrentQuantity, Is.EqualTo(first.CurrentQuantity));
            Assert.That(service.Sequence, Is.EqualTo(2L));
            Assert.That(service.GetStackQuantity(
                RewardGrantKind.Miscellaneous,
                StableId.Parse("misc.original")), Is.EqualTo(5L));
        }

        private static PlayerHoldingsCommand AddMisc(
            string transactionId,
            string itemId,
            long quantity,
            long expectedSequence)
        {
            StableId parsedTransactionId = StableId.Parse(transactionId);
            return PlayerHoldingsCommand.AddStack(
                parsedTransactionId,
                StableId.Create("operation", parsedTransactionId.Value),
                AuthorityId,
                RewardGrantKind.Miscellaneous,
                StableId.Parse(itemId),
                quantity,
                HoldingProvenance.Create(
                    StableId.Create("grant", parsedTransactionId.Value),
                    StableId.Parse("source.test")),
                expectedSequence);
        }

        private sealed class AcceptingEquipmentValidator :
            IEquipmentInstanceValidator
        {
            public EquipmentInstanceValidationResponse Validate(
                EquipmentInstanceValidationRequest request)
            {
                return new EquipmentInstanceValidationResponse(
                    true,
                    "replay-test",
                    request == null || request.Instance == null
                        ? null
                        : request.Instance.Fingerprint,
                    new EquipmentModelIssue[0]);
            }
        }
    }
}
