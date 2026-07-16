using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using ShooterMover.Contracts.Economy;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Economy.Ledger;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Holdings;
using ShooterMover.Domain.Rewards.Model;

namespace ShooterMover.Contracts.Holdings
{
    public sealed class PlayerHoldingsImportResultV1
    {
        private PlayerHoldingsImportResultV1(
            PlayerHoldingsImportStatusV1 status,
            string rejectionCode,
            long importedSequence)
        {
            Status = status;
            RejectionCode = rejectionCode;
            ImportedSequence = importedSequence;
        }

        public PlayerHoldingsImportStatusV1 Status { get; }

        public string RejectionCode { get; }

        public long ImportedSequence { get; }

        public bool Succeeded
        {
            get { return Status == PlayerHoldingsImportStatusV1.Imported; }
        }

        public static PlayerHoldingsImportResultV1 Create(
            PlayerHoldingsImportStatusV1 status,
            string rejectionCode,
            long importedSequence)
        {
            return new PlayerHoldingsImportResultV1(
                status,
                rejectionCode,
                importedSequence);
        }
    }

    public interface IPlayerHoldingsAuthorityV1
    {
        StableId AuthorityStableId { get; }

        long Sequence { get; }

        PlayerHoldingsMutationResultV1 Apply(PlayerHoldingsCommandV1 command);

        PlayerHoldingsSnapshotV1 ExportSnapshot();

        PlayerHoldingsImportResultV1 ImportSnapshot(PlayerHoldingsSnapshotV1 snapshot);
    }
}
