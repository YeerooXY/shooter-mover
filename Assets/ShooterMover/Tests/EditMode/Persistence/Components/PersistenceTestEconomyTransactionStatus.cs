namespace ShooterMover.Tests.EditMode.Persistence.Components
{
    internal static class EconomyTransactionStatus
    {
        public static readonly ShooterMover.Contracts.Economy.EconomyTransactionStatus
            Applied = ShooterMover.Contracts.Economy.EconomyTransactionStatus.Applied;

        public static readonly ShooterMover.Contracts.Economy.EconomyTransactionStatus
            DuplicateNoChange =
                ShooterMover.Contracts.Economy.EconomyTransactionStatus
                    .ExactDuplicateNoChange;
    }
}
