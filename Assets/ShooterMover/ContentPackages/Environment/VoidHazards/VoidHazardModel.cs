using System;
using ShooterMover.Contracts.Combat;
using ShooterMover.Domain.Common;

namespace ShooterMover.ContentPackages.Environment.VoidHazards
{
    public enum VoidHazardTargetCategory
    {
        Player = 1,
        Enemy = 2,
        Projectile = 3,
        Prop = 4
    }

    public enum VoidPlayerResponseKind
    {
        Ignore = 1,
        Damage = 2,
        InstantDeath = 3,
        Respawn = 4
    }

    public enum VoidEnemyResponseKind
    {
        Ignore = 1,
        RequestFall = 2
    }

    public enum VoidProjectileResponseKind
    {
        Ignore = 1,
        RemoveProjectile = 2
    }

    public enum VoidPropResponseKind
    {
        Ignore = 1,
        Remove = 2,
        KeepSupported = 3
    }

    public enum VoidHazardPortResult
    {
        Accepted = 1,
        DuplicateNoChange = 2,
        Rejected = 3
    }

    public enum VoidHazardContactStatus
    {
        Applied = 1,
        IgnoredByPolicy = 2,
        DuplicateContactIgnored = 3,
        SupportedPropKept = 4,
        HazardInactive = 5,
        InvalidTarget = 6,
        MissingRequiredPort = 7,
        MissingCheckpoint = 8,
        PortRejected = 9
    }

    public enum VoidHazardValidationStatus
    {
        Valid = 1,
        MissingPlacedObject = 2,
        PlacedObjectMustShareGameObject = 3,
        MissingHazardCollider = 4,
        ColliderMustBeTrigger = 5,
        ColliderMustShareGameObject = 6,
        InvalidPolicy = 7,
        InvalidDamageAmount = 8,
        MissingCheckpointPort = 9,
        InvalidCheckpointId = 10,
        InvalidPresentationPort = 11
    }

    public sealed class VoidHazardPolicy
    {
        public VoidHazardPolicy(
            VoidPlayerResponseKind playerResponse,
            double playerDamageAmount,
            StableId playerCheckpointId,
            VoidEnemyResponseKind enemyResponse,
            VoidProjectileResponseKind projectileResponse,
            VoidPropResponseKind propResponse)
        {
            if (!Enum.IsDefined(typeof(VoidPlayerResponseKind), playerResponse))
            {
                throw new ArgumentOutOfRangeException(nameof(playerResponse));
            }

            if (!Enum.IsDefined(typeof(VoidEnemyResponseKind), enemyResponse))
            {
                throw new ArgumentOutOfRangeException(nameof(enemyResponse));
            }

            if (!Enum.IsDefined(typeof(VoidProjectileResponseKind), projectileResponse))
            {
                throw new ArgumentOutOfRangeException(nameof(projectileResponse));
            }

            if (!Enum.IsDefined(typeof(VoidPropResponseKind), propResponse))
            {
                throw new ArgumentOutOfRangeException(nameof(propResponse));
            }

            if (playerResponse == VoidPlayerResponseKind.Damage
                && (double.IsNaN(playerDamageAmount)
                    || double.IsInfinity(playerDamageAmount)
                    || playerDamageAmount <= 0d))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(playerDamageAmount),
                    playerDamageAmount,
                    "Damage response requires a finite positive amount.");
            }

            if (playerResponse == VoidPlayerResponseKind.Respawn
                && playerCheckpointId == null)
            {
                throw new ArgumentNullException(
                    nameof(playerCheckpointId),
                    "Respawn response requires a typed checkpoint identity.");
            }

            PlayerResponse = playerResponse;
            PlayerDamageAmount = playerDamageAmount;
            PlayerCheckpointId = playerCheckpointId;
            EnemyResponse = enemyResponse;
            ProjectileResponse = projectileResponse;
            PropResponse = propResponse;
        }

        public VoidPlayerResponseKind PlayerResponse { get; }

        public double PlayerDamageAmount { get; }

        public StableId PlayerCheckpointId { get; }

        public VoidEnemyResponseKind EnemyResponse { get; }

        public VoidProjectileResponseKind ProjectileResponse { get; }

        public VoidPropResponseKind PropResponse { get; }
    }

    public sealed class VoidHazardValidationResult
    {
        public VoidHazardValidationResult(
            VoidHazardValidationStatus status,
            string diagnostic)
        {
            Status = status;
            Diagnostic = diagnostic ?? string.Empty;
        }

        public VoidHazardValidationStatus Status { get; }

        public string Diagnostic { get; }

        public bool IsValid
        {
            get { return Status == VoidHazardValidationStatus.Valid; }
        }

        public static VoidHazardValidationResult Valid()
        {
            return new VoidHazardValidationResult(
                VoidHazardValidationStatus.Valid,
                "Void hazard configuration is valid.");
        }

        public static VoidHazardValidationResult Failed(
            VoidHazardValidationStatus status,
            string diagnostic)
        {
            if (status == VoidHazardValidationStatus.Valid)
            {
                throw new ArgumentException(
                    "A failed validation cannot use the valid status.",
                    nameof(status));
            }

            return new VoidHazardValidationResult(status, diagnostic);
        }
    }

    public sealed class VoidHazardContactResult
    {
        public VoidHazardContactResult(
            VoidHazardContactStatus status,
            VoidHazardTargetCategory category,
            StableId eventId,
            VoidHazardPortResult? portResult,
            string diagnostic)
        {
            Status = status;
            Category = category;
            EventId = eventId;
            PortResult = portResult;
            Diagnostic = diagnostic ?? string.Empty;
        }

        public VoidHazardContactStatus Status { get; }

        public VoidHazardTargetCategory Category { get; }

        public StableId EventId { get; }

        public VoidHazardPortResult? PortResult { get; }

        public string Diagnostic { get; }

        public bool IsAccepted
        {
            get
            {
                return Status == VoidHazardContactStatus.Applied
                    || Status == VoidHazardContactStatus.IgnoredByPolicy
                    || Status == VoidHazardContactStatus.DuplicateContactIgnored
                    || Status == VoidHazardContactStatus.SupportedPropKept;
            }
        }
    }

    public sealed class VoidHazardDamageRequest
    {
        public VoidHazardDamageRequest(
            StableId eventId,
            StableId hazardId,
            StableId targetId,
            double amount)
            : this(eventId, hazardId, targetId, amount, 0L)
        {
        }

        public VoidHazardDamageRequest(
            StableId eventId,
            StableId hazardId,
            StableId targetId,
            double amount,
            long attemptGeneration)
        {
            EventId = eventId ?? throw new ArgumentNullException(nameof(eventId));
            HazardId = hazardId ?? throw new ArgumentNullException(nameof(hazardId));
            TargetId = targetId ?? throw new ArgumentNullException(nameof(targetId));

            if (double.IsNaN(amount) || double.IsInfinity(amount) || amount <= 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(amount));
            }
            if (attemptGeneration < 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(attemptGeneration));
            }

            Amount = amount;
            AttemptGeneration = attemptGeneration;
        }

        public StableId EventId { get; }

        public StableId HazardId { get; }

        public StableId TargetId { get; }

        public double Amount { get; }

        public long AttemptGeneration { get; }

        public CombatChannel Channel
        {
            get { return CombatChannel.Environmental; }
        }
    }

    public sealed class VoidHazardInstantDeathRequest
    {
        public VoidHazardInstantDeathRequest(
            StableId eventId,
            StableId hazardId,
            StableId targetId)
            : this(eventId, hazardId, targetId, 0L)
        {
        }

        public VoidHazardInstantDeathRequest(
            StableId eventId,
            StableId hazardId,
            StableId targetId,
            long attemptGeneration)
        {
            EventId = eventId ?? throw new ArgumentNullException(nameof(eventId));
            HazardId = hazardId ?? throw new ArgumentNullException(nameof(hazardId));
            TargetId = targetId ?? throw new ArgumentNullException(nameof(targetId));
            if (attemptGeneration < 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(attemptGeneration));
            }
            AttemptGeneration = attemptGeneration;
        }

        public StableId EventId { get; }

        public StableId HazardId { get; }

        public StableId TargetId { get; }

        public long AttemptGeneration { get; }

        public CombatChannel Channel
        {
            get { return CombatChannel.Environmental; }
        }
    }

    public sealed class VoidHazardRespawnDestination
    {
        public VoidHazardRespawnDestination(StableId destinationId)
        {
            DestinationId = destinationId
                ?? throw new ArgumentNullException(nameof(destinationId));
        }

        public StableId DestinationId { get; }
    }

    public sealed class VoidHazardRespawnRequest
    {
        public VoidHazardRespawnRequest(
            StableId eventId,
            StableId hazardId,
            StableId targetId,
            StableId checkpointId,
            VoidHazardRespawnDestination destination)
        {
            EventId = eventId ?? throw new ArgumentNullException(nameof(eventId));
            HazardId = hazardId ?? throw new ArgumentNullException(nameof(hazardId));
            TargetId = targetId ?? throw new ArgumentNullException(nameof(targetId));
            CheckpointId = checkpointId
                ?? throw new ArgumentNullException(nameof(checkpointId));
            Destination = destination ?? throw new ArgumentNullException(nameof(destination));
        }

        public StableId EventId { get; }

        public StableId HazardId { get; }

        public StableId TargetId { get; }

        public StableId CheckpointId { get; }

        public VoidHazardRespawnDestination Destination { get; }
    }

    public sealed class VoidHazardEnemyFallRequest
    {
        public VoidHazardEnemyFallRequest(
            StableId eventId,
            StableId hazardId,
            StableId targetId)
        {
            EventId = eventId ?? throw new ArgumentNullException(nameof(eventId));
            HazardId = hazardId ?? throw new ArgumentNullException(nameof(hazardId));
            TargetId = targetId ?? throw new ArgumentNullException(nameof(targetId));
        }

        public StableId EventId { get; }

        public StableId HazardId { get; }

        public StableId TargetId { get; }
    }

    public sealed class VoidHazardRemovalRequest
    {
        public VoidHazardRemovalRequest(
            StableId eventId,
            StableId hazardId,
            StableId targetId)
        {
            EventId = eventId ?? throw new ArgumentNullException(nameof(eventId));
            HazardId = hazardId ?? throw new ArgumentNullException(nameof(hazardId));
            TargetId = targetId ?? throw new ArgumentNullException(nameof(targetId));
        }

        public StableId EventId { get; }

        public StableId HazardId { get; }

        public StableId TargetId { get; }
    }

    public sealed class VoidHazardPresentationEvent
    {
        public VoidHazardPresentationEvent(
            StableId hazardId,
            StableId targetId,
            VoidHazardTargetCategory category,
            VoidHazardContactResult result)
        {
            HazardId = hazardId ?? throw new ArgumentNullException(nameof(hazardId));
            TargetId = targetId ?? throw new ArgumentNullException(nameof(targetId));
            Category = category;
            Result = result ?? throw new ArgumentNullException(nameof(result));
        }

        public StableId HazardId { get; }

        public StableId TargetId { get; }

        public VoidHazardTargetCategory Category { get; }

        public VoidHazardContactResult Result { get; }
    }

    public interface IVoidHazardCombatPort
    {
        VoidHazardPortResult RequestDamage(VoidHazardDamageRequest request);

        VoidHazardPortResult RequestInstantDeath(VoidHazardInstantDeathRequest request);
    }

    public interface IVoidHazardCheckpointPort
    {
        bool TryResolveCheckpoint(
            StableId checkpointId,
            out VoidHazardRespawnDestination destination);
    }

    public interface IVoidHazardRespawnPort
    {
        VoidHazardPortResult RequestRespawn(VoidHazardRespawnRequest request);
    }

    public interface IVoidHazardEnemyFallPort
    {
        VoidHazardPortResult RequestEnemyFall(VoidHazardEnemyFallRequest request);
    }

    public interface IVoidHazardProjectileRemovalPort
    {
        VoidHazardPortResult RequestProjectileRemoval(VoidHazardRemovalRequest request);
    }

    public interface IVoidHazardPropRemovalPort
    {
        VoidHazardPortResult RequestPropRemoval(VoidHazardRemovalRequest request);
    }

    public interface IVoidHazardPresentationPort
    {
        void Present(VoidHazardPresentationEvent presentationEvent);
    }
}
