#if UNITY_EDITOR
using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShooterMover.Content.Definitions.Objects;
using ShooterMover.Content.Definitions.Rewards;
using ShooterMover.Contracts.Authoring;
using ShooterMover.Contracts.Combat;
using ShooterMover.Contracts.Rewards;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Rewards.Model;
using ShooterMover.UnityAdapters.Authoring;
using ShooterMover.UnityAdapters.Rewards.Sources;
using UnityEngine;

namespace ShooterMover.Tests.PlayMode.Props
{
    public sealed class DestructiblePropRewardBridgeTests
    {
        private static readonly Type RuntimeType = Find(
            "ShooterMover.ContentPackages.Props.DestructibleProps.DestructibleProp2D");
        private static readonly Type BridgeType = Find(
            "ShooterMover.ContentPackages.Props.DestructibleProps.DestructiblePropRewardBridge2D");

        [Test]
        public void SuccessfulRewardSourceNotification_EmitsExactlyOnce()
        {
            GameObject root = new GameObject("Rewarded Prop Scope");
            ObjectFamilyDefinitionAsset family = null;
            RewardProfileDefinitionAsset profile = null;
            try
            {
                Assert.That(RuntimeType, Is.Not.Null);
                Assert.That(BridgeType, Is.Not.Null);

                GameplaySceneScope2D scope = root.AddComponent<GameplaySceneScope2D>();
                scope.ConfigureForTests(
                    "scope.rewarded-prop",
                    "scope.gameplay",
                    "projection.rewarded-prop",
                    "run.rewarded-prop",
                    0L);
                family = CreateFamily();
                profile = CreateProfile();

                GameObject propObject = new GameObject("Arbitrary Rewarded Prop");
                propObject.transform.SetParent(root.transform, false);
                BoxCollider2D collider = propObject.AddComponent<BoxCollider2D>();
                SpriteRenderer renderer = propObject.AddComponent<SpriteRenderer>();
                PlacedObjectAuthoring2D placed =
                    propObject.AddComponent<PlacedObjectAuthoring2D>();
                placed.ConfigureForTests(
                    "placed.rewarded-prop",
                    family,
                    "variant.standard",
                    scope,
                    "scope.gameplay",
                    Array.Empty<CapabilityOverrideAuthoring>());

                PropRewardRecordingSink sink =
                    new GameObject("Prop Reward Sink").AddComponent<PropRewardRecordingSink>();
                sink.transform.SetParent(root.transform, false);
                RewardSourceAuthoring2D source =
                    propObject.AddComponent<RewardSourceAuthoring2D>();
                source.ConfigureForTests(
                    placed,
                    profile,
                    RewardSourceOverrideAuthoring.MoneyOnly(
                        "reward-override.rewarded-prop",
                        "reward-profile.rewarded-prop-instance",
                        "reward-grant.rewarded-prop-instance",
                        "currency.money",
                        2L,
                        4L),
                    sink,
                    false);

                object runtime = propObject.AddComponent(RuntimeType);
                StableId propId = StableId.Parse("placed.rewarded-prop");
                Invoke(runtime, "Configure", propId, 10d, collider, propObject);
                object bridge = propObject.AddComponent(BridgeType);
                Invoke(bridge, "Configure", runtime, source);

                HitMessage first = CreateHit("combat-event.rewarded-prop", propId);
                Invoke(runtime, "TryApplyConfirmedHit", first, 10d);
                Invoke(runtime, "TryApplyConfirmedHit", first, 10d);
                Invoke(runtime, "Restart");
                Invoke(
                    runtime,
                    "TryApplyConfirmedHit",
                    CreateHit("combat-event.rewarded-prop-after-restart", propId),
                    10d);

                Assert.That(Read(bridge, "SubmissionCount"), Is.EqualTo(1));
                Assert.That(sink.SubmissionCount, Is.EqualTo(1));
                Assert.That(sink.AcceptedPreview, Is.Not.Null);
                Assert.That(
                    sink.AcceptedPreview.OperationRequest.SourceInstanceStableId,
                    Is.EqualTo(propId));
                Assert.That(
                    Read(Read(bridge, "LastSubmission"), "Status").ToString(),
                    Is.EqualTo("Accepted"));
                Assert.That(renderer.enabled, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                if (family != null)
                {
                    UnityEngine.Object.DestroyImmediate(family);
                }

                if (profile != null)
                {
                    UnityEngine.Object.DestroyImmediate(profile);
                }
            }
        }

        private static ObjectFamilyDefinitionAsset CreateFamily()
        {
            ObjectCapabilityDefinitionAsset presentation =
                ObjectCapabilityDefinitionAsset.CreateRuntime(
                    "capability.presentation",
                    new CapabilityFieldAuthoring(
                        "field.sprite",
                        CapabilityFieldValue.FromStableId(
                            StableId.Parse("sprite.rewarded-prop"))));
            ObjectFamilyDefinitionAsset family = ObjectFamilyDefinitionAsset.CreateRuntime(
                "family.rewarded-prop",
                "Rewarded prop",
                "variant.standard",
                new[] { presentation },
                new ObjectVariantAuthoring(
                    "variant.standard",
                    null,
                    ObjectCapabilitySelectionAuthoring.Inherit(
                        "capability.presentation")));
            presentation.hideFlags = HideFlags.HideAndDontSave;
            family.hideFlags = HideFlags.HideAndDontSave;
            return family;
        }

        private static RewardProfileDefinitionAsset CreateProfile()
        {
            RewardProfileDefinitionAsset profile = RewardProfileDefinitionAsset.CreateRuntime(
                "reward-profile.rewarded-prop",
                false,
                new[]
                {
                    new RewardGrantAuthoring(
                        "reward-grant.rewarded-prop",
                        RewardGrantKindV1.Money,
                        "currency.money",
                        1L,
                        3L),
                },
                Array.Empty<IndependentRewardRollAuthoring>(),
                Array.Empty<ExclusiveRewardGroupAuthoring>());
            profile.hideFlags = HideFlags.HideAndDontSave;
            return profile;
        }

        private static HitMessage CreateHit(string eventId, StableId targetId)
        {
            return new HitMessage(
                StableId.Parse(eventId),
                StableId.Parse("actor.reward-test-player"),
                targetId,
                CombatChannel.Kinetic,
                HitResult.Confirmed);
        }

        private static object Invoke(object instance, string name, params object[] args)
        {
            MethodInfo method = instance.GetType()
                .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Single(candidate =>
                    candidate.Name == name
                    && candidate.GetParameters().Length == args.Length);
            return method.Invoke(instance, args);
        }

        private static object Read(object instance, string name)
        {
            Assert.That(instance, Is.Not.Null, name);
            PropertyInfo property = instance.GetType().GetProperty(
                name,
                BindingFlags.Public | BindingFlags.Instance);
            Assert.That(property, Is.Not.Null, name);
            return property.GetValue(instance, null);
        }

        private static Type Find(string fullName)
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(fullName, false))
                .FirstOrDefault(type => type != null);
        }
    }

    public sealed class PropRewardRecordingSink :
        MonoBehaviour,
        IRewardSourceOperationSink
    {
        public int SubmissionCount { get; private set; }
        public RewardSourceResolvedPreview AcceptedPreview { get; private set; }

        public RewardSourceSubmissionResult Submit(RewardSourceResolvedPreview preview)
        {
            SubmissionCount++;
            AcceptedPreview = preview;
            return new RewardSourceSubmissionResult(
                RewardSourceSubmissionStatus.Accepted,
                "Accepted prop reward source operation.");
        }
    }
}
#endif
