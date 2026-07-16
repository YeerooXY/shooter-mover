using System;
using System.Collections.Generic;
using System.Globalization;
using ShooterMover.Contracts.Authoring;
using ShooterMover.Domain.Authoring;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Rewards.Model;
using UnityEngine;

namespace ShooterMover.UnityAdapters.Rewards.Sources
{
    internal static class RewardProfileCapabilityReader
    {
        private const string CapabilityIdText = "capability.reward-source-profile-v1";
        private const string FieldNamespace = "reward-profile";

        public static RewardProfileV1 BuildProfile(ScriptableObject sourceObject)
        {
            IObjectCapabilityDefinitionSource source =
                sourceObject as IObjectCapabilityDefinitionSource;
            if (source == null)
            {
                throw new InvalidOperationException(
                    "Reward profile source must implement IObjectCapabilityDefinitionSource.");
            }

            CapabilityDefinition definition = source.BuildDefinition();
            if (!definition.CapabilityId.Equals(StableId.Parse(CapabilityIdText)))
            {
                throw new InvalidOperationException(
                    $"Reward profile source returned capability '{definition.CapabilityId}', not '{CapabilityIdText}'.");
            }

            RewardProfileDispositionV1 disposition =
                (RewardProfileDispositionV1)ReadInteger(definition, "disposition");
            StableId profileId = ReadStableId(definition, "profile-id");
            RewardProfileV1 profile;
            if (disposition == RewardProfileDispositionV1.ExplicitNoDrop)
            {
                profile = RewardProfileV1.CreateExplicitNoDrop(profileId);
            }
            else if (disposition == RewardProfileDispositionV1.Configured)
            {
                List<RewardGrantSpecificationV1> guaranteed =
                    new List<RewardGrantSpecificationV1>();
                int guaranteedCount = ReadCount(definition, "guaranteed-count");
                for (int index = 0; index < guaranteedCount; index++)
                {
                    guaranteed.Add(ReadGrant(
                        definition,
                        "guaranteed-" + Index(index)));
                }

                List<IndependentRewardRollV1> independent =
                    new List<IndependentRewardRollV1>();
                int independentCount = ReadCount(definition, "independent-count");
                for (int index = 0; index < independentCount; index++)
                {
                    string prefix = "independent-" + Index(index);
                    independent.Add(
                        IndependentRewardRollV1.Create(
                            ReadStableId(definition, prefix + "-roll-id"),
                            checked((int)ReadInteger(
                                definition,
                                prefix + "-probability")),
                            ReadGrant(definition, prefix + "-grant")));
                }

                List<ExclusiveRewardGroupV1> exclusive =
                    new List<ExclusiveRewardGroupV1>();
                int exclusiveCount = ReadCount(definition, "exclusive-count");
                for (int groupIndex = 0; groupIndex < exclusiveCount; groupIndex++)
                {
                    string groupPrefix = "exclusive-" + Index(groupIndex);
                    int outcomeCount = ReadCount(
                        definition,
                        groupPrefix + "-outcome-count");
                    List<WeightedRewardOutcomeV1> outcomes =
                        new List<WeightedRewardOutcomeV1>();
                    for (int outcomeIndex = 0;
                        outcomeIndex < outcomeCount;
                        outcomeIndex++)
                    {
                        string outcomePrefix = groupPrefix
                            + "-outcome-"
                            + Index(outcomeIndex);
                        StableId outcomeId = ReadStableId(
                            definition,
                            outcomePrefix + "-outcome-id");
                        long weight = ReadInteger(
                            definition,
                            outcomePrefix + "-weight");
                        WeightedRewardOutcomeKindV1 kind =
                            (WeightedRewardOutcomeKindV1)ReadInteger(
                                definition,
                                outcomePrefix + "-kind");
                        if (kind == WeightedRewardOutcomeKindV1.Grant)
                        {
                            outcomes.Add(
                                WeightedRewardOutcomeV1.CreateGrant(
                                    outcomeId,
                                    weight,
                                    ReadGrant(definition, outcomePrefix + "-grant")));
                        }
                        else if (kind == WeightedRewardOutcomeKindV1.ExplicitNoDrop)
                        {
                            outcomes.Add(
                                WeightedRewardOutcomeV1.CreateExplicitNoDrop(
                                    outcomeId,
                                    weight));
                        }
                        else
                        {
                            throw new InvalidOperationException(
                                $"Unknown weighted reward outcome kind '{kind}'.");
                        }
                    }

                    exclusive.Add(
                        ExclusiveRewardGroupV1.Create(
                            ReadStableId(definition, groupPrefix + "-group-id"),
                            outcomes));
                }

                profile = RewardProfileV1.Create(
                    profileId,
                    guaranteed,
                    independent,
                    exclusive);
            }
            else
            {
                throw new InvalidOperationException(
                    $"Unknown reward profile disposition '{disposition}'.");
            }

            string authoredFingerprint = ReadText(
                definition,
                "profile-fingerprint");
            if (!string.Equals(
                authoredFingerprint,
                profile.Fingerprint,
                StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Reward profile capability fingerprint does not match its decoded immutable profile.");
            }

            return profile;
        }

        private static RewardGrantSpecificationV1 ReadGrant(
            CapabilityDefinition definition,
            string prefix)
        {
            int scalingCount = ReadCount(
                definition,
                prefix + "-scaling-count");
            List<RewardScalingInputDescriptorV1> scaling =
                new List<RewardScalingInputDescriptorV1>();
            for (int index = 0; index < scalingCount; index++)
            {
                string inputPrefix = prefix + "-scaling-" + Index(index);
                scaling.Add(
                    RewardScalingInputDescriptorV1.Create(
                        ReadStableId(definition, inputPrefix + "-input-id"),
                        (RewardScalingInputKindV1)ReadInteger(
                            definition,
                            inputPrefix + "-kind")));
            }

            return RewardGrantSpecificationV1.Create(
                ReadStableId(definition, prefix + "-grant-id"),
                (RewardGrantKindV1)ReadInteger(definition, prefix + "-kind"),
                ReadStableId(definition, prefix + "-content-id"),
                RewardQuantityRangeV1.Create(
                    ReadInteger(definition, prefix + "-quantity-min"),
                    ReadInteger(definition, prefix + "-quantity-max")),
                scaling);
        }

        private static int ReadCount(
            CapabilityDefinition definition,
            string fieldValue)
        {
            long value = ReadInteger(definition, fieldValue);
            if (value < 0L || value > int.MaxValue)
            {
                throw new InvalidOperationException(
                    $"Reward profile count '{fieldValue}' is out of range.");
            }

            return (int)value;
        }

        private static StableId ReadStableId(
            CapabilityDefinition definition,
            string fieldValue)
        {
            CapabilityField field = RequireField(definition, fieldValue);
            if (field.Value.Kind != CapabilityValueKind.StableId)
            {
                throw WrongKind(fieldValue, CapabilityValueKind.StableId, field.Value.Kind);
            }

            return field.Value.StableIdValue;
        }

        private static long ReadInteger(
            CapabilityDefinition definition,
            string fieldValue)
        {
            CapabilityField field = RequireField(definition, fieldValue);
            if (field.Value.Kind != CapabilityValueKind.Integer)
            {
                throw WrongKind(fieldValue, CapabilityValueKind.Integer, field.Value.Kind);
            }

            return field.Value.IntegerValue;
        }

        private static string ReadText(
            CapabilityDefinition definition,
            string fieldValue)
        {
            CapabilityField field = RequireField(definition, fieldValue);
            if (field.Value.Kind != CapabilityValueKind.Text)
            {
                throw WrongKind(fieldValue, CapabilityValueKind.Text, field.Value.Kind);
            }

            return field.Value.TextValue;
        }

        private static CapabilityField RequireField(
            CapabilityDefinition definition,
            string fieldValue)
        {
            CapabilityField field;
            StableId id = StableId.Create(FieldNamespace, fieldValue);
            if (!definition.TryGetField(id, out field))
            {
                throw new InvalidOperationException(
                    $"Reward profile capability is missing required field '{id}'.");
            }

            return field;
        }

        private static InvalidOperationException WrongKind(
            string fieldValue,
            CapabilityValueKind expected,
            CapabilityValueKind actual)
        {
            return new InvalidOperationException(
                $"Reward profile field '{fieldValue}' is {actual}, not {expected}.");
        }

        private static string Index(int value)
        {
            return value.ToString("D4", CultureInfo.InvariantCulture);
        }
    }
}
