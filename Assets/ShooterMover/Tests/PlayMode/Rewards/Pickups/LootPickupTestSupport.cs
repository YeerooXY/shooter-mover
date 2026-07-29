using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using ShooterMover.Application.Rewards.Application;
using ShooterMover.Application.Rewards.Generation;
using ShooterMover.Content.Definitions.Rewards;
using ShooterMover.Contracts.Rewards;
using ShooterMover.Contracts.Rewards.Application;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Progression.Context;
using ShooterMover.Domain.Rewards.Application;
using ShooterMover.Domain.Rewards.Model;
using ShooterMover.UnityAdapters.Authoring;
using ShooterMover.UnityAdapters.Rewards.Pickups;
using ShooterMover.UnityAdapters.Rewards.Sources;
using UnityEngine;
using UnityEngine.TestTools;

namespace ShooterMover.Tests.PlayMode.Rewards.Pickups
{
    public abstract class LootPickupPlayModeTestBase
    {
        private readonly List<UnityEngine.Object> created = new List<UnityEngine.Object>();

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            for (int index = created.Count - 1; index >= 0; index--)
            {
                UnityEngine.Object value = created[index];
                if (value != null)
                {
                    UnityEngine.Object.Destroy(value);
                }
            }

            created.Clear();
            yield return null;
        }

        protected TestStateSet CreateAuthoritySet()
        {
            RecordingRewardChildState money = new RecordingRewardChildState(
                StableId.Parse("authority.money"));
            RecordingRewardChildState scrap = new RecordingRewardChildState(
                StableId.Parse("authority.scrap"));
            RecordingRewardChildState holdings = new RecordingRewardChildState(
                StableId.Parse("authority.holdings"));
            RewardApplicationActions service = new RewardApplicationActions(
                StableId.Parse("authority.rap"),
                money,
                scrap,
                holdings);
            GameObject authorityObject = Track(new GameObject("PickupAuthority"));
            LootPickupState adapter =
                authorityObject.AddComponent<LootPickupState>();
            adapter.ConfigureForTests(
                service,
                money.AuthorityStableId,
                scrap.AuthorityStableId,
                holdings.AuthorityStableId);
            return new TestStateSet(adapter, money, scrap, holdings);
        }

        protected GameplayScene CreateScope(string runId)
        {
            GameObject root = Track(new GameObject("PickupScope"));
            GameplayScene scope = root.AddComponent<GameplayScene>();
            scope.ConfigureForTests(
                "scope.pickups",
                "scope.gameplay",
                "projection.pickups",
                runId,
                0L);
            return scope;
        }

        protected LootSpawner CreateFactory(
            TestStateSet authorities,
            GameplayScene scope,
            ILootPickupEquipmentPayloadResolver equipmentResolver = null)
        {
            GameObject factoryObject = Track(new GameObject("PickupFactory"));
            factoryObject.transform.SetParent(scope.transform);
            LootSpawner factory =
                factoryObject.AddComponent<LootSpawner>();
            factory.ConfigureForTests(
                new RewardGenerationActions(),
                ProgressionContext.Create(
                    10,
                    10,
                    StableId.Parse("difficulty.normal"),
                    1),
                123456789UL,
                1,
                authorities.Adapter,
                scope,
                null,
                equipmentResolver);
            return factory;
        }

        protected LootPickup CreateConfiguredPickup(
            TestStateSet authorities,
            GameplayScene scope,
            RewardCommitCommand command)
        {
            Assert.That(
                authorities.Adapter.Commit(command).Status,
                Is.EqualTo(RewardApplicationResultStatus.Generated));
            return CreatePickupProjection(authorities, scope, command);
        }

        protected LootPickup CreatePickupProjection(
            TestStateSet authorities,
            GameplayScene scope,
            RewardCommitCommand command,
            bool registerForRestart = true)
        {
            GameObject value = Track(new GameObject("PickupProjection"));
            value.transform.SetParent(scope.transform);
            LootPickup pickup = value.AddComponent<LootPickup>();
            pickup.ConfigureForTests(
                LootPickupPayload.Create(command),
                authorities.Adapter,
                scope,
                0.75f,
                new LootPickupPresentationStyle[0],
                registerForRestart);
            return pickup;
        }

        protected LootSourceResolvedPreview CreatePreview(
            string suffix,
            RewardGrantKind? kind,
            string contentId,
            long quantity = 1L)
        {
            bool explicitNoDrop = !kind.HasValue;
            RewardGrantAuthoring[] guaranteed = explicitNoDrop
                ? new RewardGrantAuthoring[0]
                : new[]
                {
                    new RewardGrantAuthoring(
                        "grant." + suffix,
                        kind.Value,
                        contentId,
                        quantity,
                        quantity)
                };
            RewardProfileDefinitionAsset asset = Track(
                RewardProfileDefinitionAsset.CreateRuntime(
                    "profile." + suffix,
                    explicitNoDrop,
                    guaranteed,
                    new IndependentRewardRollAuthoring[0],
                    new ExclusiveRewardGroupAuthoring[0]));
            RewardProfile profile = asset.BuildProfile();
            RewardOperationRequest operation = RewardOperationRequest.Create(
                StableId.Parse("run." + suffix),
                StableId.Parse("source." + suffix),
                StableId.Parse("operation." + suffix),
                StableId.Parse("commitment." + suffix),
                profile.ProfileStableId,
                profile.Fingerprint);
            return new LootSourceResolvedPreview(
                LootSourceOverrideAuthoringMode.Inherit,
                profile,
                profile,
                operation,
                StableId.Parse("restart." + suffix),
                RewardApplication.Fingerprint("preview=" + suffix));
        }

        protected static RewardCommitCommand CreateValueCommit(
            string suffix,
            RewardGrantKind kind,
            string contentId,
            long quantity)
        {
            RewardOperationRequest operation = CreateOperation(suffix);
            RewardGrant grant = RewardGrant.Create(
                StableId.Parse("grant." + suffix),
                kind,
                StableId.Parse(contentId),
                quantity);
            RewardResult result = RewardResult.CreateGrants(
                operation.CommitmentStableId,
                operation.SourceOperationStableId,
                new[] { grant });
            return RewardCommitCommand.Create(
                operation,
                result,
                RewardApplication.Fingerprint("generation=" + suffix),
                new[] { RewardGrantApplicationPayload.ForValue(grant) });
        }

        protected static RewardCommitCommand CreateStrongboxCommit(
            string suffix,
            string contentId,
            StableId instanceId)
        {
            RewardOperationRequest operation = CreateOperation(suffix);
            RewardGrant grant = RewardGrant.Create(
                StableId.Parse("grant." + suffix),
                RewardGrantKind.Strongbox,
                StableId.Parse(contentId),
                1L);
            RewardResult result = RewardResult.CreateGrants(
                operation.CommitmentStableId,
                operation.SourceOperationStableId,
                new[] { grant });
            return RewardCommitCommand.Create(
                operation,
                result,
                RewardApplication.Fingerprint("generation=" + suffix),
                new[]
                {
                    RewardGrantApplicationPayload.ForStrongboxes(
                        grant,
                        new[] { instanceId })
                });
        }

        protected T Track<T>(T value) where T : UnityEngine.Object
        {
            created.Add(value);
            return value;
        }

        private static RewardOperationRequest CreateOperation(string suffix)
        {
            return RewardOperationRequest.Create(
                StableId.Parse("run.pickup-tests"),
                StableId.Parse("source." + suffix),
                StableId.Parse("operation." + suffix),
                StableId.Parse("commitment." + suffix),
                StableId.Parse("profile." + suffix),
                RewardApplication.Fingerprint("profile=" + suffix));
        }

        protected sealed class TestStateSet
        {
            public TestStateSet(
                LootPickupState adapter,
                RecordingRewardChildState money,
                RecordingRewardChildState scrap,
                RecordingRewardChildState holdings)
            {
                Adapter = adapter;
                Money = money;
                Scrap = scrap;
                Holdings = holdings;
            }

            public LootPickupState Adapter { get; }
            public RecordingRewardChildState Money { get; }
            public RecordingRewardChildState Scrap { get; }
            public RecordingRewardChildState Holdings { get; }
        }

        protected sealed class FixedEquipmentPayloadResolver :
            ILootPickupEquipmentPayloadResolver
        {
            private readonly EquipmentInstance equipment;

            public FixedEquipmentPayloadResolver(EquipmentInstance equipment)
            {
                this.equipment = equipment;
            }

            public bool TryResolve(
                LootSourceResolvedPreview source,
                RewardGrant grant,
                out IReadOnlyList<EquipmentInstance> equipmentInstances,
                out string rejectionCode)
            {
                if (source == null
                    || grant == null
                    || grant.Quantity != 1L
                    || equipment == null
                    || equipment.DefinitionId != grant.ContentStableId)
                {
                    equipmentInstances = new EquipmentInstance[0];
                    rejectionCode = "test-equipment-resolution-rejected";
                    return false;
                }

                equipmentInstances = new[] { equipment };
                rejectionCode = null;
                return true;
            }
        }

        protected sealed class RecordingRewardChildState : IRewardChildState
        {
            private readonly Dictionary<StableId, RewardChildGrantCommand> applied =
                new Dictionary<StableId, RewardChildGrantCommand>();

            public RecordingRewardChildState(StableId authorityStableId)
            {
                AuthorityStableId = authorityStableId;
            }

            public StableId AuthorityStableId { get; }
            public long Sequence { get; private set; }
            public int ApplyCount { get; private set; }
            public RewardChildGrantCommand LastCommand { get; private set; }

            public RewardStatePreflightResult Preflight(
                IReadOnlyList<RewardChildGrantCommand> commands)
            {
                List<RewardStatePreflightFact> facts =
                    new List<RewardStatePreflightFact>();
                for (int index = 0; index < commands.Count; index++)
                {
                    RewardChildGrantCommand command = commands[index];
                    RewardStateAdmissionStatus status =
                        command.DestinationAuthorityStableId != AuthorityStableId
                            ? RewardStateAdmissionStatus.AuthorityMismatch
                            : applied.ContainsKey(command.TransactionStableId)
                                ? RewardStateAdmissionStatus.AlreadyApplied
                                : RewardStateAdmissionStatus.Accepted;
                    facts.Add(new RewardStatePreflightFact(
                        command.TransactionStableId,
                        status,
                        status == RewardStateAdmissionStatus.AuthorityMismatch
                            ? "recording-authority-mismatch"
                            : null));
                }

                return new RewardStatePreflightResult(facts);
            }

            public RewardChildApplyResult Apply(RewardChildGrantCommand command)
            {
                RewardChildGrantCommand prior;
                if (applied.TryGetValue(command.TransactionStableId, out prior))
                {
                    bool exact = prior.Equals(command);
                    return new RewardChildApplyResult(
                        command.TransactionStableId,
                        exact
                            ? RewardChildApplyStatus.ExactDuplicateNoChange
                            : RewardChildApplyStatus.ConflictingDuplicate,
                        exact,
                        exact ? null : "recording-transaction-conflict");
                }

                applied.Add(command.TransactionStableId, command);
                LastCommand = command;
                ApplyCount++;
                Sequence++;
                return new RewardChildApplyResult(
                    command.TransactionStableId,
                    RewardChildApplyStatus.Applied,
                    true,
                    null);
            }
        }
    }
}
