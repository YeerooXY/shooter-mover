using System;

namespace ShooterMover.EditorTools.BalanceSimulator
{
    /// <summary>
    /// Named multiplayer simulator boundary. Personal rolls remain delegated to the
    /// production service used by live terminal rewards; this wrapper adds no formulas.
    /// </summary>
    public sealed class MultiplayerDropSimulationLive
    {
        private readonly DropSourceSimulationLive dropSourceRuntime;

        public MultiplayerDropSimulationLive()
            : this(new DropSourceSimulationLive())
        {
        }

        public MultiplayerDropSimulationLive(
            DropSourceSimulationLive dropSourceRuntime)
        {
            this.dropSourceRuntime = dropSourceRuntime
                ?? throw new ArgumentNullException(nameof(dropSourceRuntime));
        }

        public RewardSimulationReport Run(
            DropSourceSimulationRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }
            if (request.Participants.Count < 2
                || request.Participants.Count > 4)
            {
                throw new ArgumentException(
                    "Multiplayer simulation requires two to four participants.",
                    nameof(request));
            }
            return dropSourceRuntime.Run(request);
        }
    }
}
