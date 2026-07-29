using System;

namespace ShooterMover.UI.StrongboxOpening
{
    public sealed class DevelopmentPickupCollectionResult
    {
        public DevelopmentPickupCollectionResult(
            bool accepted,
            bool exactReplay,
            LootPickupPresentation pickup,
            string diagnostic)
        {
            Accepted = accepted;
            ExactReplay = exactReplay;
            Pickup = pickup;
            Diagnostic = diagnostic ?? string.Empty;
        }

        public bool Accepted { get; }
        public bool ExactReplay { get; }
        public LootPickupPresentation Pickup { get; }
        public string Diagnostic { get; }
    }

    /// <summary>
    /// Disposable development authority used only to prove presenter reconstruction and
    /// accepted-collection behavior. It never talks to production holdings, RAP, BOX, or save.
    /// </summary>
    public sealed class DevelopmentPickupStateFixture
    {
        private readonly LootPickupPresentation pickup;
        private bool collected;
        private bool rejectNext;

        public DevelopmentPickupStateFixture(LootPickupPresentation pickup)
        {
            this.pickup = pickup ?? throw new ArgumentNullException(nameof(pickup));
        }

        public bool IsCollected { get { return collected; } }
        public bool RejectNext { get { return rejectNext; } }

        public LootPickupPresentation ExportAvailable()
        {
            return collected ? null : pickup;
        }

        public void RejectNextCollection()
        {
            rejectNext = true;
        }

        public DevelopmentPickupCollectionResult Collect()
        {
            if (rejectNext)
            {
                rejectNext = false;
                return new DevelopmentPickupCollectionResult(
                    false,
                    false,
                    pickup,
                    "development-pickup-fixture-rejected-once");
            }
            if (collected)
            {
                return new DevelopmentPickupCollectionResult(
                    true,
                    true,
                    pickup,
                    string.Empty);
            }

            collected = true;
            return new DevelopmentPickupCollectionResult(
                true,
                false,
                pickup,
                string.Empty);
        }
    }


}
