using System;
using System.Collections.Generic;
using System.Globalization;
using ShooterMover.Application.Rewards.Generation;
using ShooterMover.Application.Runs.Session;
using ShooterMover.Content.Definitions.Rewards;
using ShooterMover.Contracts.Rewards;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Enemies.Catalog;
using ShooterMover.Domain.Rewards.Model;
using ShooterMover.EnemyRuntimeComposition;
using ShooterMover.TerminalDropBinding;
using UnityEngine;

namespace ShooterMover.UnityAdapters.Production.Stage1
{
    public sealed partial class Stage1PlayableLoopCompositionV1
    {
        private const string Stage1DropEventContext =
            "stage1-drop-event-context-v1";

        private static readonly string Stage1DropEventContextFingerprint =
            TerminalDropCanonicalV1.Hash(Stage1DropEventContext);

        private EnemyCatalogV1 canonicalEnemyTerminalCatalog;
        private Stage1EnemyTerminalSourceContextResolverV1
            canonicalEnemyTerminalSourceContexts;

        private bool TryResolveCanonicalTerminalDropContent(
            out EnemyCatalogV1 enemyCatalog,
            out IRewardProfileResolverV1 rewardProfiles,
            out Stage1EnemyTerminalSourceContextResolverV1 enemySourceContexts,
            out string diagnostic)
        {
            enemyCatalog = null;
            rewardProfiles = null;
            enemySourceContexts = null;

            RunSessionAggregateV1 run;
            if (!TryResolveSharedRunSession(out run, out diagnostic))
            {
                return false;
            }

            try
            {
                if (canonicalEnemyTerminalCatalog == null)
                {
                    canonicalEnemyTerminalCatalog =
                        LoadProductionEnemyCatalog();
                }
                if (canonicalEnemyTerminalSourceContexts == null)
                {
                    canonicalEnemyTerminalSourceContexts =
                        new Stage1EnemyTerminalSourceContextResolverV1(
                            () => observedRunSession);
                }

                enemyCatalog = canonicalEnemyTerminalCatalog;
                enemySourceContexts = canonicalEnemyTerminalSourceContexts;
                if (enemyCatalog == null || enemySourceContexts == null)
                {
                    diagnostic = "stage1-terminal-content-missing";
                    return false;
                }
                diagnostic = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                diagnostic = "stage1-terminal-content-exception:"
                    + exception.GetType().Name
                    + ":"
                    + exception.Message;
                return false;
            }
        }

        private bool TryComposeCanonicalEnemyTerminalFacts(
            out string diagnostic)
        {
            EnemyCatalogV1 enemyCatalog;
            IRewardProfileResolverV1 ignoredProfiles;
            Stage1EnemyTerminalSourceContextResolverV1 sourceContexts;
            if (!TryResolveCanonicalTerminalDropContent(
                    out enemyCatalog,
                    out ignoredProfiles,
                    out sourceContexts,
                    out diagnostic))
            {
                return false;
            }

            Stage1EnemyTerminalFactBindingV1 binding;
            if (!TryComposeCanonicalEnemyTerminalBinding(
                    enemyCatalog,
                    sourceContexts,
                    out binding,
                    out diagnostic))
            {
                return false;
            }
            controller.ConfigureCanonicalEnemyTerminalFacts(binding);
            diagnostic = string.Empty;
            return true;
        }

        private bool TryExportCanonicalEnemyTerminalFacts(
            out IReadOnlyList<Stage1CanonicalEnemyTerminalFactV1> facts,
            out string diagnostic)
        {
            EnemyCatalogV1 enemyCatalog;
            IRewardProfileResolverV1 ignoredProfiles;
            Stage1EnemyTerminalSourceContextResolverV1 sourceContexts;
            if (!TryResolveCanonicalTerminalDropContent(
                    out enemyCatalog,
                    out ignoredProfiles,
                    out sourceContexts,
                    out diagnostic))
            {
                facts = Array.Empty<Stage1CanonicalEnemyTerminalFactV1>();
                return false;
            }

            Stage1EnemyTerminalFactBindingV1 binding;
            if (!TryComposeCanonicalEnemyTerminalBinding(
                    enemyCatalog,
                    sourceContexts,
                    out binding,
                    out diagnostic))
            {
                facts = Array.Empty<Stage1CanonicalEnemyTerminalFactV1>();
                return false;
            }
            return binding.TryExport(out facts, out diagnostic);
        }

        private bool TryComposeCanonicalEnemyTerminalBinding(
            EnemyCatalogV1 enemyCatalog,
            Stage1EnemyTerminalSourceContextResolverV1 sourceContexts,
            out Stage1EnemyTerminalFactBindingV1 binding,
            out string diagnostic)
        {
            binding = null;
            if (enemyCatalog == null || sourceContexts == null)
            {
                diagnostic = "stage1-terminal-enemy-content-missing";
                return false;
            }
            if (mobileEnemy == null || turretEnemy == null)
            {
                diagnostic = "stage1-terminal-live-enemies-missing";
                return false;
            }

            try
            {
                binding = new Stage1EnemyTerminalFactBindingV1(
                    enemyCatalog,
                    sourceContexts,
                    new[]
                    {
                        new Stage1EnemyTerminalSourceV1(
                            mobileEnemy,
                            Level1AuthorableRoomDefinitionV1.EntryRoomStableId,
                            Level1AuthorableRoomDefinitionV1
                                .MovingDroidInstanceStableId,
                            MobileDefinitionStableId,
                            () => (Vector2)controller.MobileBlasterDroid
                                .transform.position),
                        new Stage1EnemyTerminalSourceV1(
                            turretEnemy,
                            Level1AuthorableRoomDefinitionV1.TerminalRoomStableId,
                            Level1AuthorableRoomDefinitionV1
                                .TurretInstanceStableId,
                            TurretDefinitionStableId,
                            () => (Vector2)controller.TurretPackage
                                .transform.position),
                    });
                diagnostic = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                diagnostic = "stage1-terminal-enemy-binding-exception:"
                    + exception.GetType().Name
                    + ":"
                    + exception.Message;
                return false;
            }
        }

        private sealed class Stage1EnemyTerminalSourceContextResolverV1 :
            IEnemyTerminalSourceContextResolverV1
        {
            private readonly Func<RunSessionAggregateV1> runResolver;

            public Stage1EnemyTerminalSourceContextResolverV1(
                Func<RunSessionAggregateV1> runResolver)
            {
                this.runResolver = runResolver
                    ?? throw new ArgumentNullException(nameof(runResolver));
            }

            public bool TryResolve(
                EnemyDeathFactV1 fact,
                out EnemyTerminalSourceContextV1 context,
                out string diagnostic)
            {
                context = null;
                if (fact == null || fact.Identity == null)
                {
                    diagnostic = "stage1-enemy-terminal-fact-missing";
                    return false;
                }

                RunSessionAggregateV1 run = runResolver();
                if (run == null || run.IsEnded)
                {
                    diagnostic = "stage1-enemy-terminal-run-missing-or-ended";
                    return false;
                }
                if (run.RunStableId != fact.Identity.RunStableId
                    || run.LifecycleGeneration != fact.LifecycleGeneration)
                {
                    diagnostic = "stage1-enemy-terminal-run-lifecycle-mismatch";
                    return false;
                }

                RunPlayerRuntimeSnapshotV1 player =
                    run.RuntimePorts.Player.ExportSnapshot();
                if (player == null
                    || player.ParticipantStableId
                        != fact.KillerSourceParticipantStableId)
                {
                    diagnostic = "stage1-enemy-terminal-killer-not-run-player";
                    return false;
                }

                context = new EnemyTerminalSourceContextV1(
                    new ProgressionContext(
                        run.FrozenInputs.Character.CharacterLevel,
                        run.FrozenInputs.Mission.MissionLevel,
                        run.FrozenInputs.Mission.DifficultyStableId,
                        0),
                    run.StartCommand.EventModifierContextFingerprint,
                    Stage1DropEventContextFingerprint);
                diagnostic = string.Empty;
                return true;
            }
        }

        private sealed class Stage1EnemyTerminalFactBindingV1
        {
            private readonly EnemyCatalogV1 catalog;
            private readonly ContextResolvedEnemyDeathTerminalDropFactAdapterV1
                adapter;
            private readonly Stage1EnemyTerminalSourceV1[] sources;

            public Stage1EnemyTerminalFactBindingV1(
                EnemyCatalogV1 catalog,
                IEnemyTerminalSourceContextResolverV1 sourceContexts,
                IEnumerable<Stage1EnemyTerminalSourceV1> sources)
            {
                this.catalog = catalog
                    ?? throw new ArgumentNullException(nameof(catalog));
                adapter = new ContextResolvedEnemyDeathTerminalDropFactAdapterV1(
                    sourceContexts
                        ?? throw new ArgumentNullException(nameof(sourceContexts)));
                if (sources == null)
                {
                    throw new ArgumentNullException(nameof(sources));
                }
                var copy = new List<Stage1EnemyTerminalSourceV1>();
                foreach (Stage1EnemyTerminalSourceV1 source in sources)
                {
                    if (source == null)
                    {
                        throw new ArgumentException(
                            "Stage 1 enemy terminal sources must not contain null entries.",
                            nameof(sources));
                    }
                    copy.Add(source);
                }
                this.sources = copy.ToArray();
            }

            public bool TryExport(
                out IReadOnlyList<Stage1CanonicalEnemyTerminalFactV1> facts,
                out string diagnostic)
            {
                var output = new List<Stage1CanonicalEnemyTerminalFactV1>();
                for (int index = 0; index < sources.Length; index++)
                {
                    Stage1EnemyTerminalSourceV1 source = sources[index];
                    EnemyDefinitionV1 definition;
                    if (!catalog.TryGet(source.DefinitionStableId, out definition)
                        || definition == null)
                    {
                        facts = Array.Empty<Stage1CanonicalEnemyTerminalFactV1>();
                        diagnostic = "stage1-enemy-terminal-definition-missing:"
                            + source.DefinitionStableId;
                        return false;
                    }

                    EnemyDeathFactV1 fact;
                    string exportDiagnostic;
                    if (!source.Authority.TryExportDeathFact(
                            out fact,
                            out exportDiagnostic))
                    {
                        continue;
                    }
                    if (fact == null
                        || fact.Identity == null
                        || fact.Identity.RoomStableId != source.RoomStableId
                        || fact.Identity.PlacementStableId
                            != source.PlacementStableId)
                    {
                        facts = Array.Empty<Stage1CanonicalEnemyTerminalFactV1>();
                        diagnostic = "stage1-enemy-terminal-placement-mismatch";
                        return false;
                    }

                    TerminalDropAdaptationResultV1 adapted = adapter.Adapt(fact);
                    if (adapted == null || !adapted.Succeeded)
                    {
                        facts = Array.Empty<Stage1CanonicalEnemyTerminalFactV1>();
                        diagnostic = adapted == null
                            ? "stage1-enemy-terminal-adaptation-null"
                            : adapted.Diagnostic;
                        return false;
                    }

                    Vector2 position = source.Position();
                    output.Add(new Stage1CanonicalEnemyTerminalFactV1(
                        fact,
                        source.RoomStableId,
                        source.PlacementStableId,
                        position,
                        TerminalDropCanonicalV1.Hash(
                            source.RoomStableId
                                + "|"
                                + source.PlacementStableId
                                + "|"
                                + position.x.ToString("R", CultureInfo.InvariantCulture)
                                + "|"
                                + position.y.ToString("R", CultureInfo.InvariantCulture))));
                }
                facts = output.AsReadOnly();
                diagnostic = string.Empty;
                return true;
            }
        }
    }
}
