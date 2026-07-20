using System.ComponentModel;
using ShooterMover.Production.Level1;
using UnityEngine;

namespace ShooterMover.TestSupport.VisibleSlice
{
    /// <summary>
    /// Serialized compatibility component retained for the current Level 1 scene GUID.
    /// All implementation lives in Level1PlayerRuntimeAdapterV1.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [AddComponentMenu("")]
    [DefaultExecutionOrder(10100)]
    [DisallowMultipleComponent]
    public sealed class Stage1PlayerLiveAuthorityAdapterV1 :
        Level1PlayerRuntimeAdapterV1
    {
        Level1PlayerRuntimeAdapterV1
    {
        protected override void Awake()
        {
            base.Awake();
        }

        protected override void Update()
        {
            base.Update();
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
        }
    }
}
