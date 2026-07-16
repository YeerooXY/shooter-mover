using System;
using ShooterMover.Domain.Common;
using UnityEngine;

namespace ShooterMover.ContentPackages.Environment.VoidHazards
{
    /// <summary>
    /// Explicit typed classification and authority-port binding for one object that
    /// may enter a void region. Identity and category never come from tags or names.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VoidHazardTarget2D : MonoBehaviour
    {
        [SerializeField] private string targetId = "target.unassigned";
        [SerializeField] private VoidHazardTargetCategory category =
            VoidHazardTargetCategory.Player;
        [SerializeField] private bool supportedProp;

        [Header("Typed Authority Ports")]
        [SerializeField] private MonoBehaviour combatPort;
        [SerializeField] private MonoBehaviour respawnPort;
        [SerializeField] private MonoBehaviour enemyFallPort;
        [SerializeField] private MonoBehaviour projectileRemovalPort;
        [SerializeField] private MonoBehaviour propRemovalPort;

        private StableId _parsedTargetId;
        private bool _identityAttempted;

        public VoidHazardTargetCategory Category
        {
            get { return category; }
        }

        public bool IsSupportedProp
        {
            get { return supportedProp; }
        }

        public StableId TargetId
        {
            get
            {
                StableId parsed;
                if (!TryGetTargetId(out parsed))
                {
                    throw new InvalidOperationException(
                        "Void hazard target identity is not a canonical StableId.");
                }

                return parsed;
            }
        }

        public bool TryGetTargetId(out StableId parsed)
        {
            if (!_identityAttempted)
            {
                _identityAttempted = true;
                StableId.TryParse(targetId, out _parsedTargetId);
            }

            parsed = _parsedTargetId;
            return parsed != null;
        }

        public bool TryGetCombatPort(out IVoidHazardCombatPort port)
        {
            port = combatPort as IVoidHazardCombatPort;
            return port != null;
        }

        public bool TryGetRespawnPort(out IVoidHazardRespawnPort port)
        {
            port = respawnPort as IVoidHazardRespawnPort;
            return port != null;
        }

        public bool TryGetEnemyFallPort(out IVoidHazardEnemyFallPort port)
        {
            port = enemyFallPort as IVoidHazardEnemyFallPort;
            return port != null;
        }

        public bool TryGetProjectileRemovalPort(
            out IVoidHazardProjectileRemovalPort port)
        {
            port = projectileRemovalPort as IVoidHazardProjectileRemovalPort;
            return port != null;
        }

        public bool TryGetPropRemovalPort(out IVoidHazardPropRemovalPort port)
        {
            port = propRemovalPort as IVoidHazardPropRemovalPort;
            return port != null;
        }

        public void ConfigureForTests(
            string targetId,
            VoidHazardTargetCategory category,
            bool supportedProp,
            MonoBehaviour combatPort,
            MonoBehaviour respawnPort,
            MonoBehaviour enemyFallPort,
            MonoBehaviour projectileRemovalPort,
            MonoBehaviour propRemovalPort)
        {
            this.targetId = targetId;
            this.category = category;
            this.supportedProp = supportedProp;
            this.combatPort = combatPort;
            this.respawnPort = respawnPort;
            this.enemyFallPort = enemyFallPort;
            this.projectileRemovalPort = projectileRemovalPort;
            this.propRemovalPort = propRemovalPort;
            ResetIdentityCache();
        }

        private void OnValidate()
        {
            ResetIdentityCache();
        }

        private void ResetIdentityCache()
        {
            _identityAttempted = false;
            _parsedTargetId = null;
        }
    }
}
