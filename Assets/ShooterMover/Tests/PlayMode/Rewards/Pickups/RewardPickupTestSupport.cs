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
    public abstract class RewardPickupPlayModeTestBase
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

        protected TestAuthoritySet CreateAuthoritySet()
        {
            RecordingRewardChildAuthority money = new RecordingRewardChildAuthority(
                StableId.Parse("authority.money"));
            RecordingRewardChildAuthority scrap = new RecordingRewardChildAuthority(
                StableId.Parse("authority.scrap"));
            RecordingRewardChildAuthority holdings = new RecordingRewardChildAuthority(
                StableId.Parse("authority.holdings"));
            RewardApplicationServiceV1 service = new RewardApplicationServiceV1(
                StableId.Parse("authority.rap"),
                money,
                scrap,
                holdings);
            GameObject authorityObject = Track(new GameObject("PickupAuthority"));
            RewardPickupApplicationAuthority2D adapter =
                authorityObject.AddComponent<RewardPickupApplicationAuthority2D>();
            adapter.ConfigureForTests(
                service,
                money.AuthorityStableId,
                scrap.AuthorityStableId,
                holdings.AuthorityStableId);
            return new TestAuthoritySet(adapter, money, scrap, holdings);
        }

        protected GameplaySceneScope2D CreateScope(string runId)
        {
            GameObject root = Track(new GameObject("PickupScope"));
            GameplaySceneScope2D scope = root.AddComponent<GameplaySceneScope2D>();
            scope.ConfigureForTests(
                "scope.pickups",
                "scope.gameplay",
                "projection.pickups",
                runId,
                0L);
            return scope;
        }

        protected RewardPickupDropFactory2D CreateFactory(
            TestAuthoritySet authorities,
            GameplaySceneScope2D scope,
            IRewardPickupEquipmentPayloadResolverV1 equipmentResolver = null)
        {
            GameObject factoryObject = Track(new GameObject("PickupFactory"));
            factoryObject.transform.SetParent(scope.transform);
            RewardPickupDropFactory2D factory =
                factoryObject.AddComponent<RewardPickupDropFactory2D>();
            factory.ConfigureForTests(
                new RewardGenerationServiceV1(),
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

        protected RewardPickup2D CreateConfiguredPickup(
            TestAuthoritySet authorities,
            GameplaySceneScope2D scope,
            RewardCommitCommandV1 command)
        {
            Assert.That(
                authorities.Adapter.Commit(command).Status,
                Is.EqualTo(RewardApplicationResultStatusV1.Generated));
            return CreatePickupProjection(authorities, scope, command);
        }

        protected RewardPickup2D CreatePickupProjection(
            TestAuthoritySet authorities,
            GameplaySceneScope2D scope,
            RewardCommitCommandV1 command,
            bool registerForRestart = true)
        {
            GameObject value = Track(new GameObject("PickupProjection"));
            value.transform.SetParent(scope.transform);
            RewardPickup2D pickup = value.AddComponent<RewardPickup2D>();
            pickup.ConfigureForTests(
                RewardPickupPayloadV1.Create(command),
                authorities.Adapter,
                scope,
                0.75f,
                new RewardPickupPresentationStyleV1[0],
                registerForRestart);
            return pickup;
        }

        protected RewardSourceResolvedPreview CreatePreview(
            string suffix,
            RewardGrantKindV1? kind,
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
            RewardProfileV1 profile = asset.BuildProfile();
            RewardOperationRequestV1 operation = RewardOperationRequestV1.Create(
                StableId.Parse("run." + suffix),
                StableId.Parse("source." + suffix),
                StableId.Parse("operation." + suffix),
                StableId.Parse("commitment." + suffix),
                profile.ProfileStableId,
                profile.Fingerprint);
            return new RewardSourceResolvedPreview(
                RewardSourceOverrideAuthoringMode.Inherit,
                profile,
                profile,
                operation,
                StableId.Parse("restart." + suffix),
                RewardApplicationCanonicalV1.Fingerprint("preview=" + suffix));
        }

        protected static RewardCommitCommandV1 CreateValueCommit(
            string suffix,
            RewardGrantKindV1 kind,
            string contentId,
            long quantity)
        {
            RewardOperationRequestV1 operation = CreateOperation(suffix);
            RewardGrantV1 grant = RewardGrantV1.Create(
                StableId.Parse("grant." + suffix),
                kind,
                StableId.Parse(contentId),
                quantity);
            RewardResultV1 result = RewardResultV1.CreateGrants(
                operation.CommitmentStableId,
                operation.SourceOperationStableId,
                new[] { grant });
            return RewardCommitCommandV1.Create(
                operation,
                result,
                RewardApplicationCanonicalV1.Fingerprint("generation=" + suffix),
                new[] { RewardGrantApplicationPayloadV1.ForValue(grant) });
        }

        protected static RewardCommitCommandV1 CreateStrongboxCommit(
            string suffix,
            string contentId,
            StableId instanceId)
        {
            RewardOperationRequestV1 operation = CreateOperation(suffix);
            RewardGrantV1 grant = RewardGrantV1.Create(
                StableId.Parse("grant." + suffix),
                RewardGrantKindV1.Strongbox,
                StableId.Parse(contentId),
                1L);
            RewardResultV1 result = RewardResultV1.CreateGrants(
                operation.CommitmentStableId,
                operation.SourceOperationStableId,
                new[] { grant });
            return RewardCommitCommandV1.Create(
                operation,
                result,
                RewardApplicationCanonicalV1.Fingerprint("generation=" + suffix),
                new[]
                {
                    RewardGrantApplicationPayloadV1.ForStrongboxes(
                        grant,
                        new[] { instanceId })
                });
        }

        protected T Track<T>(T value) where T : UnityEngine.Object
        {
            created.Add(value);
            return value;
        }

        private static RewardOperationRequestV1 CreateOperation(string suffix)
        {
            return RewardOperationRequestV1.Create(
                StableId.Parse("run.pickup-tests"),
                StableId.Parse("source." + suffix),
                StableId.Parse("operation." + suffix),
                StableId.Parse("commitment." + suffix),
                StableId.Parse("profile." + suffix),
                RewardApplicationCanonicalV1.Fingerprint("profile=" + suffix));
        }

        protected sealed class TestAuthoritySet
        {
            public TestAuthoritySet(
                RewardPickupApplicationAuthority2D adapter,
                RecordingRewardChildAuthority money,
                RecordingRewardChildAuthority scrap,
                RecordingRewardChildAuthority holdings)
            {
                Adapter = adapter;
                Money = money;
                Scrap = scrap;
                Holdings = holdings;
            }

            public RewardPickupApplicationAuthority2D Adapter { get; }
            public RecordingRewardChildAuthority Money { get; }
            public RecordingRewardChildAuthority Scrap { get; }
            public RecordingRewardChildAuthority Holdings { get; }
        }

        protected sealed class FixedEquipmentPayloadResolver :
            IRewardPickupEquipmentPayloadResolverV1
        {
            private readonly EquipmentInstance equipment;

            public FixedEquipmentPayloadResolver(EquipmentInstance equipment)
            {
                this.equipment = equipment;
            }

            public bool TryResolve(
                RewardSourceResolvedPreview source,
                RewardGrantV1 grant,
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

        protected sealed class RecordingRewardChildAuthority : IRewardChildAuthorityV1
        {
            private readonly Dictionary<StableId, RewardChildGrantCommandV1> applied =
                new Dictionary<StableId, RewardChildGrantCommandV1>();

            public RecordingRewardChildAuthority(StableId authorityStableId)
            {
                AuthorityStableId = authorityStableId;
            }

            public StableId AuthorityStableId { get; }
            public long Sequence { get; private set; }
            public int ApplyCount { get; private set; }
            public RewardChildGrantCommandV1 LastCommand { get; private set; }

            public RewardAuthorityPreflightResultV1 Preflight(
                IReadOnlyList<RewardChildGrantCommandV1> commands)
            {
                List<RewardAuthorityPreflightFactV1> facts =
                    new List<RewardAuthorityPreflightFactV1>();
                for (int index = 0; index < commands.Count; index++)
                {
                    RewardChildGrantCommandV1 command = commands[index];
                    RewardAuthorityAdmissionStatusV1 status =
                        command.DestinationAuthorityStableId != AuthorityStableId
                            ? RewardAuthorityAdmissionStatusV1.AuthorityMismatch
                            : applied.ContainsKey(command.TransactionStableId)
                                ? RewardAuthorityAdmissionStatusV1.AlreadyApplied
                                : RewardAuthorityAdmissionStatusV1.Accepted;
                    facts.Add(new RewardAuthorityPreflightFactV1(
                        command.TransactionStableId,
                        status,
                        status == RewardAuthorityAdmissionStatusV1.AuthorityMismatch
                            ? "recording-authority-mismatch"
                            : null));
                }

                return new RewardAuthorityPreflightResultV1(facts);
            }

            public RewardChildApplyResultV1 Apply(RewardChildGrantCommandV1 command)
            {
                RewardChildGrantCommandV1 prior;
                if (applied.TryGetValue(command.TransactionStableId, out prior))
                {
                    bool exact = prior.Equals(command);
                    return new RewardChildApplyResultV1(
                        command.TransactionStableId,
                        exact
                            ? RewardChildApplyStatusV1.ExactDuplicateNoChange
                            : RewardChildApplyStatusV1.ConflictingDuplicate,
                        exact,
                        exact ? null : "recording-transaction-conflict");
                }

                applied.Add(command.TransactionStableId, command);
                LastCommand = command;
                ApplyCount++;
                Sequence++;
                return new RewardChildApplyResultV1(
                    command.TransactionStableId,
                    RewardChildApplyStatusV1.Applied,
                    true,
                    null);
            }
        }
    }
}
