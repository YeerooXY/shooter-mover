using System.ComponentModel;
using ShooterMover.UnityAdapters.Production.Level1;
using UnityEngine;

namespace ShooterMover.TestSupport.VisibleSlice
{
    /// <summary>
    /// Runtime compatibility component retained while the Level 1 controller still exposes
    /// the historical Stage1PlayerLiveAuthorityAdapterV1 type. All player authority,
    /// projectile-impact routing, HUD, restart, and lifecycle behavior lives in
    /// Level1PlayerRuntimeSceneAdapterV1.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [AddComponentMenu("")]
    [DefaultExecutionOrder(10100)]
    [DisallowMultipleComponent]
    public sealed class Stage1PlayerLiveAuthorityAdapterV1 :
        Level1PlayerRuntimeSceneAdapterV1
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
