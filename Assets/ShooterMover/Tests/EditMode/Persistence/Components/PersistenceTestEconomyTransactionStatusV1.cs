namespace ShooterMover.Tests.EditMode.Persistence.Components
{
    internal static class EconomyTransactionStatusV1
    {
        public static readonly ShooterMover.Contracts.Economy.EconomyTransactionStatusV1
            Applied = ShooterMover.Contracts.Economy.EconomyTransactionStatusV1.Applied;

        public static readonly ShooterMover.Contracts.Economy.EconomyTransactionStatusV1
            DuplicateNoChange =
                ShooterMover.Contracts.Economy.EconomyTransactionStatusV1
                    .ExactDuplicateNoChange;
    }
}
