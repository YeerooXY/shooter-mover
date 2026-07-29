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
    public sealed class PlayerHoldingsImportResult
    {
        private PlayerHoldingsImportResult(
            PlayerHoldingsImportStatus status,
            string rejectionCode,
            long importedSequence)
        {
            Status = status;
            RejectionCode = rejectionCode;
            ImportedSequence = importedSequence;
        }

        public PlayerHoldingsImportStatus Status { get; }

        public string RejectionCode { get; }

        public long ImportedSequence { get; }

        public bool Succeeded
        {
            get { return Status == PlayerHoldingsImportStatus.Imported; }
        }

        public static PlayerHoldingsImportResult Create(
            PlayerHoldingsImportStatus status,
            string rejectionCode,
            long importedSequence)
        {
            return new PlayerHoldingsImportResult(
                status,
                rejectionCode,
                importedSequence);
        }
    }

    public interface IPlayerHoldingsState
    {
        StableId AuthorityStableId { get; }

        long Sequence { get; }

        PlayerHoldingsMutationResult Apply(PlayerHoldingsCommand command);

        PlayerHoldingsSnapshot ExportSnapshot();

        PlayerHoldingsImportResult ImportSnapshot(PlayerHoldingsSnapshot snapshot);
    }
}
