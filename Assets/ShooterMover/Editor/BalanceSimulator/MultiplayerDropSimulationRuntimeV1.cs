using System;

namespace ShooterMover.EditorTools.BalanceSimulator
{
    /// <summary>
    /// Named multiplayer simulator boundary. Personal rolls remain delegated to the
    /// production service used by live terminal rewards; this wrapper adds no formulas.
    /// </summary>
    public sealed class MultiplayerDropSimulationRuntimeV1
    {
        private readonly DropSourceSimulationRuntimeV1 dropSourceRuntime;

        public MultiplayerDropSimulationRuntimeV1()
            : this(new DropSourceSimulationRuntimeV1())
        {
        }

        public MultiplayerDropSimulationRuntimeV1(
            DropSourceSimulationRuntimeV1 dropSourceRuntime)
        {
            this.dropSourceRuntime = dropSourceRuntime
                ?? throw new ArgumentNullException(nameof(dropSourceRuntime));
        }

        public RewardSimulationReportV1 Run(
            DropSourceSimulationRequestV1 request)
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
