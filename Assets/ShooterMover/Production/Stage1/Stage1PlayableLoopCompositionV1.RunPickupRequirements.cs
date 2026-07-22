using UnityEngine;

namespace ShooterMover.UnityAdapters.Production.Stage1
{
    /// <summary>
    /// Unity composition requirement only. Adding the existing production Stage 1
    /// composition causes Unity to attach the focused pickup adapters without a
    /// RuntimeInitializeOnLoadMethod or global scene-loaded subscription.
    /// </summary>
    [RequireComponent(
        typeof(Stage1RunPickupBootstrap2D),
        typeof(Stage1RunPickupPropBootstrap2D))]
    public sealed partial class Stage1PlayableLoopCompositionV1
    {
    }
}
