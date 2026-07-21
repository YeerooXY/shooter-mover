using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using ShooterMover.Domain.Authoring;
using ShooterMover.Domain.Common;

namespace ShooterMover.Domain.Props
{
    public sealed class PropPlacementV1
    {
        public PropPlacementV1(
            PlacedObjectIdentity identity,
            StableId definitionId)
        {
            Identity = identity ?? throw new ArgumentNullException(nameof(identity));
            DefinitionId = definitionId ?? throw new ArgumentNullException(nameof(definitionId));
        }

        public PlacedObjectIdentity Identity { get; }

        public StableId DefinitionId { get; }
    }

    public sealed class PropDamageEligibilityContextV1
    {
        internal PropDamageEligibilityContextV1(
            PropDamageCommandV1 command,
            StableId targetParticipantId,
            PropDamageAlignmentV1 targetAlignment,
            StableId policyId)
        {
            OperationId = command.OperationId;
            SourceParticipantId = command.SourceParticipantId;
            SourceFactionId = command.SourceFactionId;
            DamageChannelId = command.DamageChannelId;
            TargetParticipantId = targetParticipantId;
            TargetAlignment = targetAlignment;
            PolicyId = policyId;
        }

        public StableId OperationId { get; }
        public StableId SourceParticipantId { get; }
        public StableId SourceFactionId { get; }
        public StableId DamageChannelId { get; }
        public StableId TargetParticipantId { get; }
        public PropDamageAlignmentV1 TargetAlignment { get; }
        public StableId PolicyId { get; }
    }

    public interface IPropDamageEligibilityPolicyV1
    {
        bool CanDamage(PropDamageEligibilityContextV1 context);
    }

    public sealed class PropDamageCommandV1
    {
        public PropDamageCommandV1(
            StableId operationId,
            StableId sourceParticipantId,
            StableId sourceFactionId,
            StableId damageChannelId,
            double requestedDamage)
        {
            OperationId = operationId ?? throw new ArgumentNullException(nameof(operationId));
            SourceParticipantId = sourceParticipantId
                ?? throw new ArgumentNullException(nameof(sourceParticipantId));
            SourceFactionId = sourceFactionId
                ?? throw new ArgumentNullException(nameof(sourceFactionId));
            DamageChannelId = damageChannelId
                ?? throw new ArgumentNullException(nameof(damageChannelId));
            if (double.IsNaN(requestedDamage)
                || double.IsInfinity(requestedDamage)
                || requestedDamage <= 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(requestedDamage));
            }

            RequestedDamage = requestedDamage;
            Fingerprint = PropFingerprintV1.Compute64Hex(
                "operation=" + OperationId
                + "|source=" + SourceParticipantId
                + "|faction=" + SourceFactionId
                + "|channel=" + DamageChannelId
                + "|damage=" + requestedDamage.ToString(
                    "R",
                    CultureInfo.InvariantCulture));
        }

        public StableId OperationId { get; }
        public StableId SourceParticipantId { get; }
        public StableId SourceFactionId { get; }
        public StableId DamageChannelId { get; }
        public double RequestedDamage { get; }
        public string Fingerprint { get; }
    }

    public enum PropDamageStatusV1
    {
        Applied = 0,
        Destroyed = 1,
        DuplicateNoChange = 2,
        RejectedConflictingReplay = 3,
        RejectedNoCombatAuthority = 4,
        RejectedByPolicy = 5,
        RejectedIndestructible = 6,
        RejectedTerminal = 7
    }

    public static class PropFactKindIdsV1
    {
        public static readonly StableId Terminal =
            StableId.Parse("fact-kind.prop-terminal");
        public static readonly StableId ExplosionRequest =
            StableId.Parse("fact-kind.prop-explosion");
        public static readonly StableId DropRequest =
            StableId.Parse("fact-kind.prop-drop-request");
        public static readonly StableId ObjectiveOnDestroy =
            StableId.Parse("fact-kind.prop-objective-destroy");
        public static readonly StableId Interaction =
            StableId.Parse("fact-kind.prop-interaction");
        public static readonly StableId SwitchOn =
            StableId.Parse("fact-kind.prop-switch-on");
        public static readonly StableId SwitchOff =
            StableId.Parse("fact-kind.prop-switch-off");
        public static readonly StableId ObjectiveOnInteraction =
            StableId.Parse("fact-kind.prop-objective-interact");
    }

    internal static class PropFactIdentityV1
    {
        private const string FactNamespace = "prop-fact";

        public static StableId Derive(
            StableId rootOperationId,
            StableId propParticipantId,
            StableId factKindId,
            StableId profileOrFactId)
        {
            if (rootOperationId == null)
            {
                throw new ArgumentNullException(nameof(rootOperationId));
            }

            if (propParticipantId == null)
            {
                throw new ArgumentNullException(nameof(propParticipantId));
            }

            if (factKindId == null)
            {
                throw new ArgumentNullException(nameof(factKindId));
            }

            string fingerprint = PropFingerprintV1.Compute64Hex(
                "root=" + rootOperationId
                + "|prop=" + propParticipantId
                + "|kind=" + factKindId
                + "|value=" + (profileOrFactId == null
                    ? "none"
                    : profileOrFactId.ToString()));
            return StableId.Create(
                FactNamespace,
                factKindId.Value + "-" + fingerprint);
        }
    }

    public sealed class PropTerminalFactV1
    {
        internal PropTerminalFactV1(
            StableId factId,
            StableId kindId,
            PropDamageCommandV1 command,
            StableId propParticipantId,
            StableId propDefinitionId)
        {
            FactId = factId ?? throw new ArgumentNullException(nameof(factId));
            KindId = kindId ?? throw new ArgumentNullException(nameof(kindId));
            PropParticipantId = propParticipantId
                ?? throw new ArgumentNullException(nameof(propParticipantId));
            PropDefinitionId = propDefinitionId
                ?? throw new ArgumentNullException(nameof(propDefinitionId));
            SourceParticipantId = command.SourceParticipantId;
            SourceFactionId = command.SourceFactionId;
            DamageChannelId = command.DamageChannelId;
            Fingerprint = PropFingerprintV1.Compute64Hex(
                "kind=" + KindId
                + "|fact=" + FactId
                + "|prop=" + PropParticipantId
                + "|definition=" + PropDefinitionId
                + "|source=" + SourceParticipantId
                + "|faction=" + SourceFactionId
                + "|channel=" + DamageChannelId);
        }

        public StableId FactId { get; }
        public StableId KindId { get; }
        public StableId PropParticipantId { get; }
        public StableId PropDefinitionId { get; }
        public StableId SourceParticipantId { get; }
        public StableId SourceFactionId { get; }
        public StableId DamageChannelId { get; }
        public string Fingerprint { get; }
    }

    public sealed class PropTriggeredFactV1
    {
        internal PropTriggeredFactV1(
            StableId factId,
            StableId kindId,
            StableId profileOrFactId,
            StableId propParticipantId,
            StableId sourceParticipantId)
        {
            FactId = factId ?? throw new ArgumentNullException(nameof(factId));
            KindId = kindId ?? throw new ArgumentNullException(nameof(kindId));
            ProfileOrFactId = profileOrFactId
                ?? throw new ArgumentNullException(nameof(profileOrFactId));
            PropParticipantId = propParticipantId
                ?? throw new ArgumentNullException(nameof(propParticipantId));
            SourceParticipantId = sourceParticipantId
                ?? throw new ArgumentNullException(nameof(sourceParticipantId));
            Fingerprint = PropFingerprintV1.Compute64Hex(
                "kind=" + KindId
                + "|fact=" + FactId
                + "|value=" + ProfileOrFactId
                + "|prop=" + PropParticipantId
                + "|source=" + SourceParticipantId);
        }

        public StableId FactId { get; }
        public StableId KindId { get; }
        public StableId ProfileOrFactId { get; }
        public StableId PropParticipantId { get; }
        public StableId SourceParticipantId { get; }
        public string Fingerprint { get; }
    }

    public sealed class PropFactBatchV1
    {
        internal PropFactBatchV1(
            PropTerminalFactV1 terminal,
            PropTriggeredFactV1 explosion,
            PropTriggeredFactV1 dropRequest,
            PropTriggeredFactV1 objective)
        {
            Terminal = terminal;
            Explosion = explosion;
            DropRequest = dropRequest;
            Objective = objective;
        }

        public static PropFactBatchV1 Empty { get; } =
            new PropFactBatchV1(null, null, null, null);

        public PropTerminalFactV1 Terminal { get; }
        public PropTriggeredFactV1 Explosion { get; }
        public PropTriggeredFactV1 DropRequest { get; }
        public PropTriggeredFactV1 Objective { get; }

        public bool IsEmpty
        {
            get
            {
                return Terminal == null
                    && Explosion == null
                    && DropRequest == null
                    && Objective == null;
            }
        }
    }

    public sealed class PropRuntimeSnapshotV1
    {
        internal PropRuntimeSnapshotV1(
            StableId participantId,
            StableId definitionId,
            StableId presentationId,
            bool solid,
            bool hasCombatAuthority,
            bool terminal,
            double? maximumHealth,
            double? currentHealth,
            bool blocksRoomClear,
            StableId switchId,
            bool? switchActive)
        {
            ParticipantId = participantId;
            DefinitionId = definitionId;
            PresentationId = presentationId;
            IsSolid = solid;
            HasCombatAuthority = hasCombatAuthority;
            IsTerminal = terminal;
            MaximumHealth = maximumHealth;
            CurrentHealth = currentHealth;
            BlocksRoomClear = blocksRoomClear;
            SwitchId = switchId;
            SwitchActive = switchActive;
            Fingerprint = PropFingerprintV1.Compute64Hex(
                "schema=1|participant=" + ParticipantId
                + "|definition=" + DefinitionId
                + "|presentation=" + PresentationId
                + "|solid=" + (solid ? "1" : "0")
                + "|combat=" + (hasCombatAuthority ? "1" : "0")
                + "|terminal=" + (terminal ? "1" : "0")
                + "|max=" + NullableNumber(maximumHealth)
                + "|current=" + NullableNumber(currentHealth)
                + "|room-clear=" + (blocksRoomClear ? "1" : "0")
                + "|switch=" + (switchId == null ? "none" : switchId.ToString())
                + "|switch-active="
                + (switchActive.HasValue
                    ? (switchActive.Value ? "1" : "0")
                    : "none"));
        }

        public int SchemaVersion
        {
            get { return 1; }
        }

        public StableId ParticipantId { get; }
        public StableId DefinitionId { get; }
        public StableId PresentationId { get; }
        public bool IsSolid { get; }
        public bool HasCombatAuthority { get; }
        public bool IsTerminal { get; }
        public double? MaximumHealth { get; }
        public double? CurrentHealth { get; }
        public bool BlocksRoomClear { get; }
        public StableId SwitchId { get; }
        public bool? SwitchActive { get; }
        public string Fingerprint { get; }

        private static string NullableNumber(double? value)
        {
            return value.HasValue
                ? value.Value.ToString("R", CultureInfo.InvariantCulture)
                : "none";
        }
    }

    public sealed class PropDamageResultV1
    {
        internal PropDamageResultV1(
            PropDamageStatusV1 status,
            double previousHealth,
            double currentHealth,
            double appliedDamage,
            PropFactBatchV1 facts,
            PropRuntimeSnapshotV1 snapshot,
            string diagnostic)
        {
            Status = status;
            PreviousHealth = previousHealth;
            CurrentHealth = currentHealth;
            AppliedDamage = appliedDamage;
            Facts = facts ?? PropFactBatchV1.Empty;
            Snapshot = snapshot;
            Diagnostic = diagnostic ?? string.Empty;
        }

        public PropDamageStatusV1 Status { get; }
        public double PreviousHealth { get; }
        public double CurrentHealth { get; }
        public double AppliedDamage { get; }
        public PropFactBatchV1 Facts { get; }
        public PropRuntimeSnapshotV1 Snapshot { get; }
        public string Diagnostic { get; }
    }

    public sealed class PropInteractionCommandV1
    {
        public PropInteractionCommandV1(
            StableId operationId,
            StableId sourceParticipantId)
        {
            OperationId = operationId ?? throw new ArgumentNullException(nameof(operationId));
            SourceParticipantId = sourceParticipantId
                ?? throw new ArgumentNullException(nameof(sourceParticipantId));
            Fingerprint = PropFingerprintV1.Compute64Hex(
                "operation=" + OperationId + "|source=" + SourceParticipantId);
        }

        public StableId OperationId { get; }
        public StableId SourceParticipantId { get; }
        public string Fingerprint { get; }
    }

    public enum PropInteractionStatusV1
    {
        Applied = 0,
        DuplicateNoChange = 1,
        RejectedConflictingReplay = 2,
        RejectedNotInteractable = 3,
        RejectedTerminal = 4
    }

    public sealed class PropInteractionResultV1
    {
        internal PropInteractionResultV1(
            PropInteractionStatusV1 status,
            PropTriggeredFactV1 interaction,
            PropTriggeredFactV1 switchFact,
            PropTriggeredFactV1 objective,
            PropRuntimeSnapshotV1 snapshot,
            string diagnostic)
        {
            Status = status;
            Interaction = interaction;
            SwitchFact = switchFact;
            Objective = objective;
            Snapshot = snapshot;
            Diagnostic = diagnostic ?? string.Empty;
        }

        public PropInteractionStatusV1 Status { get; }
        public PropTriggeredFactV1 Interaction { get; }
        public PropTriggeredFactV1 SwitchFact { get; }
        public PropTriggeredFactV1 Objective { get; }
        public PropRuntimeSnapshotV1 Snapshot { get; }
        public string Diagnostic { get; }
    }
}
