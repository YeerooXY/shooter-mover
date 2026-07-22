using System;
using System.Globalization;
using System.Text;
using ShooterMover.Domain.Common;

namespace ShooterMover.Application.Rewards.Drops
{
    public enum PersonalRewardDeliveryStateV1
    {
        Pending = 1,
        Delivered = 2,
    }

    /// <summary>
    /// Immutable run-local delivery record for one generated participant result. The
    /// result remains pending until that participant's pickup/network authority accepts
    /// it; generation and delivery are therefore separate exactly-once steps.
    /// </summary>
    public sealed class PersonalRewardDeliveryEnvelopeV1 :
        IComparable<PersonalRewardDeliveryEnvelopeV1>
    {
        private readonly string canonicalText;

        public PersonalRewardDeliveryEnvelopeV1(
            PersonalRewardGenerationResultV1 result,
            PersonalRewardDeliveryStateV1 state,
            string deliveryFingerprint)
        {
            Result = result ?? throw new ArgumentNullException(nameof(result));
            if (!result.IsSuccess)
            {
                throw new ArgumentException(
                    "Only successful personal reward results may enter delivery.",
                    nameof(result));
            }
            if (!Enum.IsDefined(typeof(PersonalRewardDeliveryStateV1), state))
            {
                throw new ArgumentOutOfRangeException(nameof(state));
            }
            if (state == PersonalRewardDeliveryStateV1.Delivered
                && string.IsNullOrWhiteSpace(deliveryFingerprint))
            {
                throw new ArgumentException(
                    "Delivered personal rewards require a delivery fingerprint.",
                    nameof(deliveryFingerprint));
            }
            State = state;
            DeliveryFingerprint = deliveryFingerprint == null
                ? string.Empty
                : deliveryFingerprint.Trim();

            var builder = new StringBuilder(
                "schema=personal-reward-delivery-envelope-v1");
            builder.Append("\noperation_id=")
                .Append(Result.Context.OperationStableId)
                .Append("\nparticipant_id=")
                .Append(Result.Context.ParticipantStableId)
                .Append("\nresult_fingerprint=")
                .Append(Result.Fingerprint)
                .Append("\nstate=")
                .Append(((int)State).ToString(CultureInfo.InvariantCulture))
                .Append("\ndelivery_fingerprint=")
                .Append(DeliveryFingerprint);
            canonicalText = builder.ToString();
            Fingerprint = RewardGenerationFingerprintV1.Compute(canonicalText);
        }

        public PersonalRewardGenerationResultV1 Result { get; }
        public PersonalRewardDeliveryStateV1 State { get; }
        public string DeliveryFingerprint { get; }
        public string Fingerprint { get; }

        public PersonalRewardDeliveryEnvelopeV1 WithDelivered(
            string deliveryFingerprint)
        {
            return new PersonalRewardDeliveryEnvelopeV1(
                Result,
                PersonalRewardDeliveryStateV1.Delivered,
                deliveryFingerprint);
        }

        public int CompareTo(PersonalRewardDeliveryEnvelopeV1 other)
        {
            if (ReferenceEquals(other, null)) return 1;
            int participant = Result.Context.ParticipantStableId.CompareTo(
                other.Result.Context.ParticipantStableId);
            return participant != 0
                ? participant
                : Result.Context.OperationStableId.CompareTo(
                    other.Result.Context.OperationStableId);
        }

        public string ToCanonicalString()
        {
            return canonicalText;
        }
    }
}
