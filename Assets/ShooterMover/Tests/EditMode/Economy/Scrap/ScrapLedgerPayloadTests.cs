using System.Linq;
using NUnit.Framework;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Economy.Ledger;
using ShooterMover.Domain.Economy.Scrap;

namespace ShooterMover.Tests.EditMode.Economy.Scrap
{
    public sealed class ScrapLedgerPayloadTests
    {
        [Test]
        public void CanonicalPayloadIsPrintableSingleLineAndAcceptedByLedger()
        {
            StableId operationId = StableId.Parse("operation.strongbox-open-regression");
            StableId authorityId = StableId.Parse("authority.scrap-profile");
            StableId currencyId = StableId.Parse("currency.scrap");
            var provenance = new ScrapProvenance(
                ScrapIdentity.StrongboxSourceKind,
                operationId,
                StableId.Parse("strongbox.box-regression"));
            var command = new ScrapTransactionCommand(
                StableId.Parse("transaction.payload-regression"),
                operationId,
                authorityId,
                currencyId,
                ScrapMutationKind.Grant,
                7L,
                ScrapIdentity.StrongboxOpeningReason,
                provenance);

            Assert.That(command.LedgerPayload.Contains("\n"), Is.False);
            Assert.That(command.LedgerPayload.Contains("\r"), Is.False);
            Assert.That(command.LedgerPayload.All(character => character >= ' ' && character <= '~'), Is.True);

            ScrapLedgerPayload parsed;
            string rejectionCode;
            Assert.That(
                ScrapLedgerPayload.TryParse(command.LedgerPayload, out parsed, out rejectionCode),
                Is.True,
                rejectionCode);
            Assert.That(parsed.CanonicalText, Is.EqualTo(command.LedgerPayload));
            Assert.That(parsed.OperationStableId, Is.EqualTo(operationId));
            Assert.That(parsed.AuthorityStableId, Is.EqualTo(authorityId));
            Assert.That(parsed.ReasonStableId, Is.EqualTo(ScrapIdentity.StrongboxOpeningReason));
            Assert.That(parsed.Provenance.SourceKindStableId, Is.EqualTo(ScrapIdentity.StrongboxSourceKind));

            var ledger = new IdempotentLedger<ScrapLedgerVocabulary>(
                context => LedgerDecision.Accept(),
                context => LedgerDecision.Accept());
            var entry = new LedgerEntry<ScrapLedgerVocabulary>(
                ScrapIdentity.BalanceEntryType,
                currencyId,
                command.LedgerPayload);
            LedgerMutationResult<ScrapLedgerVocabulary> result = ledger.Apply(
                new LedgerMutation<ScrapLedgerVocabulary>(
                    command.TransactionStableId,
                    entry,
                    command.GetAdmissionDelta()));

            Assert.That(result.Status, Is.EqualTo(LedgerMutationStatus.Applied));
            Assert.That(ledger.GetQuantity(entry), Is.EqualTo(7L));
        }
    }
}
