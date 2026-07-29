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

        public static RewardProfile BuildProfile(ScriptableObject sourceObject)
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

            RewardProfileDisposition disposition =
                (RewardProfileDisposition)ReadInteger(definition, "disposition");
            StableId profileId = ReadStableId(definition, "profile-id");
            RewardProfile profile;
            if (disposition == RewardProfileDisposition.ExplicitNoDrop)
            {
                profile = RewardProfile.CreateExplicitNoDrop(profileId);
            }
            else if (disposition == RewardProfileDisposition.Configured)
            {
                List<RewardGrantSpecification> guaranteed =
                    new List<RewardGrantSpecification>();
                int guaranteedCount = ReadCount(definition, "guaranteed-count");
                for (int index = 0; index < guaranteedCount; index++)
                {
                    guaranteed.Add(ReadGrant(
                        definition,
                        "guaranteed-" + Index(index)));
                }

                List<IndependentRewardRoll> independent =
                    new List<IndependentRewardRoll>();
                int independentCount = ReadCount(definition, "independent-count");
                for (int index = 0; index < independentCount; index++)
                {
                    string prefix = "independent-" + Index(index);
                    independent.Add(
                        IndependentRewardRoll.Create(
                            ReadStableId(definition, prefix + "-roll-id"),
                            checked((int)ReadInteger(
                                definition,
                                prefix + "-probability")),
                            ReadGrant(definition, prefix + "-grant")));
                }

                List<ExclusiveRewardGroup> exclusive =
                    new List<ExclusiveRewardGroup>();
                int exclusiveCount = ReadCount(definition, "exclusive-count");
                for (int groupIndex = 0; groupIndex < exclusiveCount; groupIndex++)
                {
                    string groupPrefix = "exclusive-" + Index(groupIndex);
                    int outcomeCount = ReadCount(
                        definition,
                        groupPrefix + "-outcome-count");
                    List<WeightedRewardOutcome> outcomes =
                        new List<WeightedRewardOutcome>();
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
                        WeightedRewardOutcomeKind kind =
                            (WeightedRewardOutcomeKind)ReadInteger(
                                definition,
                                outcomePrefix + "-kind");
                        if (kind == WeightedRewardOutcomeKind.Grant)
                        {
                            outcomes.Add(
                                WeightedRewardOutcome.CreateGrant(
                                    outcomeId,
                                    weight,
                                    ReadGrant(definition, outcomePrefix + "-grant")));
                        }
                        else if (kind == WeightedRewardOutcomeKind.ExplicitNoDrop)
                        {
                            outcomes.Add(
                                WeightedRewardOutcome.CreateExplicitNoDrop(
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
                        ExclusiveRewardGroup.Create(
                            ReadStableId(definition, groupPrefix + "-group-id"),
                            outcomes));
                }

                profile = RewardProfile.Create(
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

        private static RewardGrantSpecification ReadGrant(
            CapabilityDefinition definition,
            string prefix)
        {
            int scalingCount = ReadCount(
                definition,
                prefix + "-scaling-count");
            List<RewardScalingInputDescriptor> scaling =
                new List<RewardScalingInputDescriptor>();
            for (int index = 0; index < scalingCount; index++)
            {
                string inputPrefix = prefix + "-scaling-" + Index(index);
                scaling.Add(
                    RewardScalingInputDescriptor.Create(
                        ReadStableId(definition, inputPrefix + "-input-id"),
                        (RewardScalingInputKind)ReadInteger(
                            definition,
                            inputPrefix + "-kind")));
            }

            return RewardGrantSpecification.Create(
                ReadStableId(definition, prefix + "-grant-id"),
                (RewardGrantKind)ReadInteger(definition, prefix + "-kind"),
                ReadStableId(definition, prefix + "-content-id"),
                RewardQuantityRange.Create(
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
