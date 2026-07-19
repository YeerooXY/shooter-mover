using System;
using ShooterMover.Domain.Common;

namespace ShooterMover.ContentPackages.Environment.VoidHazards
{
    public sealed partial class VoidHazardAuthoring2D
    {
        public VoidHazardContactResult HandleContactEnter(VoidHazardTarget2D target)
        {
            if (!_isReady || !_acceptContacts)
            {
                return Result(
                    VoidHazardContactStatus.HazardInactive,
                    target == null ? VoidHazardTargetCategory.Player : target.Category,
                    null,
                    null,
                    "The void hazard is not accepting contacts.");
            }

            if (target == null
                || !Enum.IsDefined(typeof(VoidHazardTargetCategory), target.Category))
            {
                return Result(
                    VoidHazardContactStatus.InvalidTarget,
                    VoidHazardTargetCategory.Player,
                    null,
                    null,
                    "Contact requires an explicitly classified target.");
            }

            StableId targetId;
            if (!target.TryGetTargetId(out targetId))
            {
                return Result(
                    VoidHazardContactStatus.InvalidTarget,
                    target.Category,
                    null,
                    null,
                    "Target identity is not a canonical StableId.");
            }

            int contactCount;
            if (_activeContacts.TryGetValue(targetId, out contactCount))
            {
                _activeContacts[targetId] = contactCount + 1;
                VoidHazardContactResult duplicate = Result(
                    VoidHazardContactStatus.DuplicateContactIgnored,
                    target.Category,
                    null,
                    null,
                    "A duplicate active contact produced no second request.");
                Present(targetId, target.Category, duplicate);
                return duplicate;
            }

            _activeContacts.Add(targetId, 1);
            StableId eventId = CreateEventId(
                _restartParticipantId,
                targetId,
                placedObject.BoundScope.AttemptGeneration,
                _contactOrdinal++);
            VoidHazardContactResult routed = RouteContact(target, targetId, eventId);
            Present(targetId, target.Category, routed);
            return routed;
        }

        public bool HandleContactExit(VoidHazardTarget2D target)
        {
            StableId targetId;
            if (target == null || !target.TryGetTargetId(out targetId))
            {
                return false;
            }

            int count;
            if (!_activeContacts.TryGetValue(targetId, out count))
            {
                return false;
            }

            if (count <= 1)
            {
                return _activeContacts.Remove(targetId);
            }

            _activeContacts[targetId] = count - 1;
            return true;
        }

        private VoidHazardContactResult RouteContact(
            VoidHazardTarget2D target,
            StableId targetId,
            StableId eventId)
        {
            switch (target.Category)
            {
                case VoidHazardTargetCategory.Player:
                    return RoutePlayer(target, targetId, eventId);
                case VoidHazardTargetCategory.Enemy:
                    return RouteEnemy(target, targetId, eventId);
                case VoidHazardTargetCategory.Projectile:
                    return RouteProjectile(target, targetId, eventId);
                case VoidHazardTargetCategory.Prop:
                    return RouteProp(target, targetId, eventId);
                default:
                    return Result(
                        VoidHazardContactStatus.InvalidTarget,
                        target.Category,
                        eventId,
                        null,
                        "Target category is not declared.");
            }
        }

        private VoidHazardContactResult RoutePlayer(
            VoidHazardTarget2D target,
            StableId targetId,
            StableId eventId)
        {
            switch (_resolvedPolicy.PlayerResponse)
            {
                case VoidPlayerResponseKind.Ignore:
                    return Ignored(target.Category, eventId);
                case VoidPlayerResponseKind.Damage:
                    IVoidHazardCombatPort damagePort;
                    if (!target.TryGetCombatPort(out damagePort))
                    {
                        return MissingPort(
                            target.Category,
                            eventId,
                            "Player damage response requires IVoidHazardCombatPort.");
                    }

                    return FromPort(
                        target.Category,
                        eventId,
                        damagePort.RequestDamage(new VoidHazardDamageRequest(
                            eventId,
                            _restartParticipantId,
                            targetId,
                            _resolvedPolicy.PlayerDamageAmount,
                            placedObject.BoundScope.AttemptGeneration)));
                case VoidPlayerResponseKind.InstantDeath:
                    IVoidHazardCombatPort deathPort;
                    if (!target.TryGetCombatPort(out deathPort))
                    {
                        return MissingPort(
                            target.Category,
                            eventId,
                            "Player instant-death response requires IVoidHazardCombatPort.");
                    }

                    return FromPort(
                        target.Category,
                        eventId,
                        deathPort.RequestInstantDeath(new VoidHazardInstantDeathRequest(
                            eventId,
                            _restartParticipantId,
                            targetId,
                            placedObject.BoundScope.AttemptGeneration)));
                case VoidPlayerResponseKind.Respawn:
                    return RouteRespawn(target, targetId, eventId);
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private VoidHazardContactResult RouteRespawn(
            VoidHazardTarget2D target,
            StableId targetId,
            StableId eventId)
        {
            IVoidHazardCheckpointPort resolver =
                checkpointPort as IVoidHazardCheckpointPort;
            IVoidHazardRespawnPort respawn;
            if (resolver == null || !target.TryGetRespawnPort(out respawn))
            {
                return MissingPort(
                    target.Category,
                    eventId,
                    "Respawn requires checkpoint and player respawn ports.");
            }

            VoidHazardRespawnDestination destination;
            if (!resolver.TryResolveCheckpoint(
                _resolvedPolicy.PlayerCheckpointId,
                out destination)
                || destination == null)
            {
                return Result(
                    VoidHazardContactStatus.MissingCheckpoint,
                    target.Category,
                    eventId,
                    null,
                    "The required checkpoint could not be resolved.");
            }

            return FromPort(
                target.Category,
                eventId,
                respawn.RequestRespawn(new VoidHazardRespawnRequest(
                    eventId,
                    _restartParticipantId,
                    targetId,
                    _resolvedPolicy.PlayerCheckpointId,
                    destination)));
        }

        private VoidHazardContactResult RouteEnemy(
            VoidHazardTarget2D target,
            StableId targetId,
            StableId eventId)
        {
            if (_resolvedPolicy.EnemyResponse == VoidEnemyResponseKind.Ignore)
            {
                return Ignored(target.Category, eventId);
            }

            IVoidHazardEnemyFallPort port;
            if (!target.TryGetEnemyFallPort(out port))
            {
                return MissingPort(
                    target.Category,
                    eventId,
                    "Enemy fall response requires IVoidHazardEnemyFallPort.");
            }

            return FromPort(
                target.Category,
                eventId,
                port.RequestEnemyFall(new VoidHazardEnemyFallRequest(
                    eventId,
                    _restartParticipantId,
                    targetId)));
        }

        private VoidHazardContactResult RouteProjectile(
            VoidHazardTarget2D target,
            StableId targetId,
            StableId eventId)
        {
            if (_resolvedPolicy.ProjectileResponse == VoidProjectileResponseKind.Ignore)
            {
                return Ignored(target.Category, eventId);
            }

            IVoidHazardProjectileRemovalPort port;
            if (!target.TryGetProjectileRemovalPort(out port))
            {
                return MissingPort(
                    target.Category,
                    eventId,
                    "Projectile removal requires IVoidHazardProjectileRemovalPort.");
            }

            return FromPort(
                target.Category,
                eventId,
                port.RequestProjectileRemoval(new VoidHazardRemovalRequest(
                    eventId,
                    _restartParticipantId,
                    targetId)));
        }

        private VoidHazardContactResult RouteProp(
            VoidHazardTarget2D target,
            StableId targetId,
            StableId eventId)
        {
            if (_resolvedPolicy.PropResponse == VoidPropResponseKind.Ignore)
            {
                return Ignored(target.Category, eventId);
            }

            if (_resolvedPolicy.PropResponse == VoidPropResponseKind.KeepSupported
                && target.IsSupportedProp)
            {
                return Result(
                    VoidHazardContactStatus.SupportedPropKept,
                    target.Category,
                    eventId,
                    null,
                    "Supported prop remains under its owning support authority.");
            }

            IVoidHazardPropRemovalPort port;
            if (!target.TryGetPropRemovalPort(out port))
            {
                return MissingPort(
                    target.Category,
                    eventId,
                    "Prop removal requires IVoidHazardPropRemovalPort.");
            }

            return FromPort(
                target.Category,
                eventId,
                port.RequestPropRemoval(new VoidHazardRemovalRequest(
                    eventId,
                    _restartParticipantId,
                    targetId)));
        }

        private static VoidHazardContactResult Ignored(
            VoidHazardTargetCategory category,
            StableId eventId)
        {
            return Result(
                VoidHazardContactStatus.IgnoredByPolicy,
                category,
                eventId,
                null,
                "The declared category policy ignores this contact.");
        }

        private static VoidHazardContactResult MissingPort(
            VoidHazardTargetCategory category,
            StableId eventId,
            string diagnostic)
        {
            return Result(
                VoidHazardContactStatus.MissingRequiredPort,
                category,
                eventId,
                null,
                diagnostic);
        }

        private static VoidHazardContactResult FromPort(
            VoidHazardTargetCategory category,
            StableId eventId,
            VoidHazardPortResult portResult)
        {
            bool accepted = portResult == VoidHazardPortResult.Accepted
                || portResult == VoidHazardPortResult.DuplicateNoChange;
            return Result(
                accepted
                    ? VoidHazardContactStatus.Applied
                    : VoidHazardContactStatus.PortRejected,
                category,
                eventId,
                portResult,
                accepted
                    ? "The typed authority accepted the hazard request."
                    : "The typed authority rejected the hazard request.");
        }

        private static VoidHazardContactResult Result(
            VoidHazardContactStatus status,
            VoidHazardTargetCategory category,
            StableId eventId,
            VoidHazardPortResult? portResult,
            string diagnostic)
        {
            return new VoidHazardContactResult(
                status,
                category,
                eventId,
                portResult,
                diagnostic);
        }
    }
}
