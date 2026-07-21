using ShooterMover.ContentPackages.Props.DestructibleProps;
using UnityEngine;

namespace ShooterMover.UnityAdapters.Production.Stage1
{
    /// <summary>
    /// Production asset reference for the retained default explosion animation. Combat
    /// presentation reads this asset but does not own its frames, timing, scale or sorting.
    /// </summary>
    public sealed class Stage1DefaultEnemyDeathVfxAsset2D : ScriptableObject
    {
        [SerializeField]
        private DestructiblePropDestructionAnimation animation;

        public DestructiblePropDestructionAnimation Animation
        {
            get { return animation; }
        }
    }
}
