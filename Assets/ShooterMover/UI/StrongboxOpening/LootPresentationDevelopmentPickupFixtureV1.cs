using System;

namespace ShooterMover.UI.StrongboxOpening
{
    public sealed class DevelopmentPickupCollectionResultV1
    {
        public DevelopmentPickupCollectionResultV1(
            bool accepted,
            bool exactReplay,
            LootPickupPresentationV1 pickup,
            string diagnostic)
        {
            Accepted = accepted;
            ExactReplay = exactReplay;
            Pickup = pickup;
            Diagnostic = diagnostic ?? string.Empty;
        }

        public bool Accepted { get; }
        public bool ExactReplay { get; }
        public LootPickupPresentationV1 Pickup { get; }
        public string Diagnostic { get; }
    }

    /// <summary>
    /// Disposable development authority used only to prove presenter reconstruction and
    /// accepted-collection behavior. It never talks to production holdings, RAP, BOX, or save.
    /// </summary>
    public sealed class DevelopmentPickupAuthorityFixtureV1
    {
        private readonly LootPickupPresentationV1 pickup;
        private bool collected;
        private bool rejectNext;

        public DevelopmentPickupAuthorityFixtureV1(LootPickupPresentationV1 pickup)
        {
            this.pickup = pickup ?? throw new ArgumentNullException(nameof(pickup));
        }

        public bool IsCollected { get { return collected; } }
        public bool RejectNext { get { return rejectNext; } }

        public LootPickupPresentationV1 ExportAvailable()
        {
            return collected ? null : pickup;
        }

        public void RejectNextCollection()
        {
            rejectNext = true;
        }

        public DevelopmentPickupCollectionResultV1 Collect()
        {
            if (rejectNext)
            {
                rejectNext = false;
                return new DevelopmentPickupCollectionResultV1(
                    false,
                    false,
                    pickup,
                    "development-pickup-fixture-rejected-once");
            }
            if (collected)
            {
                return new DevelopmentPickupCollectionResultV1(
                    true,
                    true,
                    pickup,
                    string.Empty);
            }

            collected = true;
            return new DevelopmentPickupCollectionResultV1(
                true,
                false,
                pickup,
                string.Empty);
        }
    }


}
