using ShooterMover.UnityAdapters.Production.Level1;
using UnityEngine;

namespace ShooterMover.TestSupport.VisibleSlice
{
    /// <summary>
    /// Serialized compatibility shell retained until the Stage1VisibleSlice scene root is
    /// renamed with its Unity GUID and scene references. All runtime behavior lives in the
    /// canonical Level1PlayerRuntimeSceneAdapterV1 production component.
    /// </summary>
    [DefaultExecutionOrder(10100)]
    [DisallowMultipleComponent]
    public sealed class Stage1PlayerLiveAuthorityAdapterV1 :
        Level1PlayerRuntimeSceneAdapterV1
    {
    }
}
