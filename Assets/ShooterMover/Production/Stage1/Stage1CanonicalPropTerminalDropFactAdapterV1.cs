using System;
using System.Globalization;
using ShooterMover.ContentPackages.Props.DestructibleProps;
using ShooterMover.Domain.Common;
using ShooterMover.TerminalDropBinding;

namespace ShooterMover.UnityAdapters.Production.Stage1
{
    /// <summary>
    /// Pure transport adapter from the immutable Stage 1 prop fact into the generic
    /// terminal source fact. It performs no profile selection or reward calculation.
    /// </summary>
    internal sealed class Stage1CanonicalPropTerminalDropFactAdapterV1 :
        ITerminalDropFactAdapterV1
    {
        public StableId FactKindStableId
        {
            get { return TerminalDropFactKindIdsV1.PropDestruction; }
        }

        public Type FactType
        {
            get { return typeof(Stage1CanonicalPropDestructionFactV1); }
        }

        public TerminalDropAdaptationResultV1 Adapt(object terminalFact)
        {
            var fact = terminalFact as Stage1CanonicalPropDestructionFactV1;
            if (fact == null
                || fact.Destruction == null
                || fact.Provenance == null)
            {
                return TerminalDropAdaptationResultV1.Rejected(
                    TerminalDropRejectionCodeV1.InvalidTerminalFact,
                    "stage1-prop-terminal-canonical-fact-invalid");
            }

            DestructiblePropDestructionResult destruction = fact.Destruction;
            string canonical = destruction.EventId
                + "|"
                + destruction.PropId
                + "|"
                + destruction.SourceId
                + "|"
                + fact.Provenance.Definition.DefinitionId
                + "|"
                + fact.Provenance.DropProfileStableId
                + "|"
                + fact.RoomStableId
                + "|"
                + fact.TerminalPosition.x.ToString(
                    "R",
                    CultureInfo.InvariantCulture)
                + "|"
                + fact.TerminalPosition.y.ToString(
                    "R",
                    CultureInfo.InvariantCulture);
            return TerminalDropAdaptationResultV1.Accepted(
                new TerminalDropSourceFactV1(
                    TerminalDropFactKindIdsV1.PropDestruction,
                    destruction.EventId,
                    destruction.EventId,
                    fact.RunStableId,
                    fact.LifecycleGeneration,
                    destruction.PropId,
                    fact.PlacementStableId,
                    fact.LifecycleGeneration,
                    fact.Provenance.Definition.DefinitionId,
                    fact.AttributedParticipantStableId,
                    destruction.SourceId,
                    StableId.Create(
                        "damage",
                        "combat-channel-"
                            + ((int)destruction.Channel).ToString(
                                CultureInfo.InvariantCulture)),
                    fact.Provenance.DropProfileStableId,
                    Stage1ProductionFingerprintV1.Hash("source|" + canonical),
                    fact.Provenance.DefinitionFingerprint,
                    Stage1ProductionFingerprintV1.Hash("upstream|" + canonical)));
        }
    }
}
