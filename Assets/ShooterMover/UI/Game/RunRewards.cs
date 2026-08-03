using UnityEngine;

namespace ShooterMover.UI.Game
{
    /// <summary>
    /// Scene placeholder retained so existing scenes do not acquire a missing-script component.
    /// Compact-enemy rewards are intentionally disconnected until a new definition-driven bridge
    /// is implemented; level traversal and direct hub return remain available through LevelGame.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    [DisallowMultipleComponent]
    public sealed class RunRewards : MonoBehaviour
    {
        public string Diagnostic
        {
            get { return "compact-enemy-rewards-not-connected"; }
        }
    }
}
