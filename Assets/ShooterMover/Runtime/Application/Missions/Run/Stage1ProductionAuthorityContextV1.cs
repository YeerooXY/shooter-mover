using System;
using ShooterMover.Application.Missions.Results;
using ShooterMover.Application.Progression.Experience.EnemyRewards;

namespace ShooterMover.Application.Missions.Run
{
    /// <summary>
    /// Existing production authorities required to compose one Stage 1 run. This is a
    /// dependency handoff only and owns no holdings, XP, rewards, strongboxes or results.
    /// </summary>
    public sealed class Stage1ProductionAuthorityBundleV1
    {
        public Stage1ProductionAuthorityBundleV1(
            ILevelRunStableIdFactoryV1 runStableIdFactory,
            ILevelRunLoadoutResolverV1 loadoutResolver,
            EnemyExperienceRewardServiceV1 experienceRewards,
            MissionRunResultAuthorityV1 missionResults,
            MissionRunAuthorityCheckpointV1 checkpoint)
        {
            RunStableIdFactory = runStableIdFactory
                ?? throw new ArgumentNullException(nameof(runStableIdFactory));
            LoadoutResolver = loadoutResolver
                ?? throw new ArgumentNullException(nameof(loadoutResolver));
            ExperienceRewards = experienceRewards
                ?? throw new ArgumentNullException(nameof(experienceRewards));
            MissionResults = missionResults
                ?? throw new ArgumentNullException(nameof(missionResults));
            Checkpoint = checkpoint
                ?? throw new ArgumentNullException(nameof(checkpoint));
        }

        public ILevelRunStableIdFactoryV1 RunStableIdFactory { get; }
        public ILevelRunLoadoutResolverV1 LoadoutResolver { get; }
        public EnemyExperienceRewardServiceV1 ExperienceRewards { get; }
        public MissionRunResultAuthorityV1 MissionResults { get; }
        public MissionRunAuthorityCheckpointV1 Checkpoint { get; }
    }

    /// <summary>
    /// One-shot process-local dependency handoff into the Stage 1 scene. Failed startup
    /// restores the exact same bundle so a scene retry cannot manufacture new authority.
    /// </summary>
    public static class Stage1ProductionAuthorityContextV1
    {
        private static readonly object Gate = new object();
        private static Stage1ProductionAuthorityBundleV1 bundle;

        public static void Capture(Stage1ProductionAuthorityBundleV1 authorityBundle)
        {
            if (authorityBundle == null)
            {
                throw new ArgumentNullException(nameof(authorityBundle));
            }

            lock (Gate)
            {
                if (bundle != null && !ReferenceEquals(bundle, authorityBundle))
                {
                    throw new InvalidOperationException(
                        "A different Stage 1 authority bundle is already pending.");
                }

                bundle = authorityBundle;
            }
        }

        public static bool TryConsume(out Stage1ProductionAuthorityBundleV1 authorityBundle)
        {
            lock (Gate)
            {
                authorityBundle = bundle;
                bundle = null;
                return authorityBundle != null;
            }
        }

        public static bool HasPendingBundle
        {
            get
            {
                lock (Gate)
                {
                    return bundle != null;
                }
            }
        }

        public static void ClearForTests()
        {
            lock (Gate)
            {
                bundle = null;
            }
        }
    }
}
