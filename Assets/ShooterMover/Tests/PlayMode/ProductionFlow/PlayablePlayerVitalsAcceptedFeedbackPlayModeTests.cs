using System.Collections;
using NUnit.Framework;
using ShooterMover.Contracts.Combat;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Domain.Common;
using ShooterMover.GameplayEntities;
using ShooterMover.UI.ProductionFlow;
using UnityEngine;
using UnityEngine.TestTools;

namespace ShooterMover.Tests.PlayMode.ProductionFlow
{
    public sealed class PlayablePlayerVitalsAcceptedFeedbackPlayModeTests
    {
        [UnityTest]
        public IEnumerator AcceptedDamageAloneMutatesHealthAndStartsHitFeedback()
        {
            var player = new GameObject("Player Vitals Feedback Test Player");
            Rigidbody2D body = player.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;

            PlayablePlayerMarker2D marker = player.AddComponent<
                PlayablePlayerMarker2D>();
            StableId characterStableId = StableId.Parse(
                "character-instance.player-vitals-feedback-playmode-test");
            PlayerRouteProfilePayloadV1 route =
                PlayerRouteProfilePayloadV1.Create(
                    characterStableId,
                    StableId.Parse(
                        "loadout-profile.player-vitals-feedback-playmode-test"),
                    new StableId[PlayerRouteProfilePayloadV1.WeaponSlotCount]);
            marker.Bind(
                characterStableId,
                StableId.Parse("class.striker"),
                route,
                new object(),
                new object());

            PlayableTopDownMovement2D movement = player.AddComponent<
                PlayableTopDownMovement2D>();
            movement.Bind(body, 6f);

            SpriteRenderer renderer = player.AddComponent<SpriteRenderer>();
            var baseColor = new Color(0.2f, 0.4f, 0.6f, 1f);
            renderer.color = baseColor;

            PlayablePlayerVitals2D vitals = player.AddComponent<
                PlayablePlayerVitals2D>();
            vitals.Bind(
                marker,
                body,
                movement,
                new UnexpectedHubReturnRequest());

            StableId eventStableId = StableId.Parse(
                "event.player-vitals-feedback-accepted-hit");
            var acceptedCommand = new DamageReceiverCommand(
                eventStableId,
                StableId.Parse("actor.player-vitals-feedback-enemy"),
                StableId.Parse("participant.player-vitals-feedback-enemy"),
                vitals.Identity.EntityInstanceId,
                25d,
                CombatChannel.Kinetic,
                vitals.LifecycleGeneration);

            DamageReceiverResult accepted = vitals.ApplyDamage(acceptedCommand);

            Assert.That(accepted.Status, Is.EqualTo(DamageReceiverStatus.Applied));
            Assert.That(vitals.CurrentHealth, Is.EqualTo(75d));
            Assert.That(vitals.MaximumHealth, Is.EqualTo(100d));
            AssertColor(renderer.color, Color.white);

            yield return new WaitForSecondsRealtime(0.18f);

            AssertColor(renderer.color, baseColor);

            DamageReceiverResult replay = vitals.ApplyDamage(acceptedCommand);

            Assert.That(replay.Status, Is.EqualTo(DamageReceiverStatus.Duplicate));
            Assert.That(vitals.CurrentHealth, Is.EqualTo(75d));
            AssertColor(renderer.color, baseColor);

            var conflictingCommand = new DamageReceiverCommand(
                eventStableId,
                acceptedCommand.SourceActorId,
                acceptedCommand.SourceRunParticipantId,
                acceptedCommand.TargetActorId,
                30d,
                acceptedCommand.Channel,
                acceptedCommand.LifecycleGeneration);
            DamageReceiverResult conflict = vitals.ApplyDamage(conflictingCommand);

            Assert.That(
                conflict.Status,
                Is.EqualTo(DamageReceiverStatus.RejectedInvalid));
            Assert.That(
                conflict.RejectionCode,
                Is.EqualTo(DamageReceiverRejectionCode.ConflictingDuplicate));
            Assert.That(vitals.CurrentHealth, Is.EqualTo(75d));
            AssertColor(renderer.color, baseColor);

            yield return null;

            AssertColor(renderer.color, baseColor);
            Object.Destroy(player);
            yield return null;
        }

        private static void AssertColor(Color actual, Color expected)
        {
            Assert.That(actual.r, Is.EqualTo(expected.r).Within(0.001f));
            Assert.That(actual.g, Is.EqualTo(expected.g).Within(0.001f));
            Assert.That(actual.b, Is.EqualTo(expected.b).Within(0.001f));
            Assert.That(actual.a, Is.EqualTo(expected.a).Within(0.001f));
        }

        private sealed class UnexpectedHubReturnRequest :
            IPlayablePlayerHubReturnRequestV1
        {
            public bool TryReturnToHub(
                PlayablePlayerMarker2D player,
                out string rejectionCode)
            {
                Assert.Fail("Non-lethal feedback validation requested a Hub return.");
                rejectionCode = "unexpected-player-vitals-hub-return";
                return false;
            }
        }
    }
}
