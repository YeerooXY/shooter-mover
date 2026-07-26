using System.Collections;
using System.Collections.Generic;
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
    public sealed class PlayablePlayerVitalsRetryPlayModeTests
    {
        [UnityTest]
        public IEnumerator AutomaticUpdateRetriesRejectedReturnAndStopsAfterAcceptance()
        {
            var returnRequest = new SequencedHubReturnRequest(false, true);
            GameObject player = CreatePlayerPresentation(null);
            Rigidbody2D body = player.GetComponent<Rigidbody2D>();
            PlayableTopDownMovement2D movement = player.GetComponent<
                PlayableTopDownMovement2D>();
            PlayablePlayerMarker2D marker = player.GetComponent<
                PlayablePlayerMarker2D>();
            PlayablePlayerVitals2D vitals = player.AddComponent<
                PlayablePlayerVitals2D>();
            vitals.Bind(marker, body, movement, returnRequest);

            LogAssert.Expect(
                LogType.Error,
                "playable-player-vitals-hub-return-rejected");

            DamageReceiverResult lethal = vitals.ApplyDamage(
                new DamageReceiverCommand(
                    StableId.Parse("event.playmode-auto-retry-lethal"),
                    StableId.Parse("actor.playmode-enemy"),
                    StableId.Parse("participant.playmode-enemy"),
                    vitals.Identity.EntityInstanceId,
                    500d,
                    CombatChannel.Kinetic,
                    vitals.LifecycleGeneration));

            Assert.That(lethal.Status, Is.EqualTo(DamageReceiverStatus.Applied));
            Assert.That(vitals.IsDefeated, Is.True);
            Assert.That(vitals.IsHubReturnAccepted, Is.False);
            Assert.That(returnRequest.AttemptCount, Is.EqualTo(1));

            yield return new WaitForSecondsRealtime(0.4f);

            Assert.That(vitals.IsHubReturnAccepted, Is.True);
            Assert.That(vitals.HubReturnAttemptCount, Is.EqualTo(2));
            Assert.That(returnRequest.AttemptCount, Is.EqualTo(2));

            yield return new WaitForSecondsRealtime(0.4f);

            Assert.That(vitals.IsHubReturnAccepted, Is.True);
            Assert.That(vitals.HubReturnAttemptCount, Is.EqualTo(2));
            Assert.That(returnRequest.AttemptCount, Is.EqualTo(2));

            Object.Destroy(player);
            yield return null;
        }

        [UnityTest]
        public IEnumerator InstallerStartBindsExistingSpawnedPlayerExactlyOnce()
        {
            var root = new GameObject("Player Vitals Installer Test Root");
            GameObject player = CreatePlayerPresentation(root.transform);
            PlayablePlayerVitalsInstallerV1 installer = root.AddComponent<
                PlayablePlayerVitalsInstallerV1>();

            yield return null;

            Assert.That(installer, Is.Not.Null);
            PlayablePlayerVitals2D first = player.GetComponent<
                PlayablePlayerVitals2D>();
            Assert.That(first, Is.Not.Null);
            Assert.That(first.IsBound, Is.True);
            GameplayEntityIdentity firstIdentity = first.Identity;

            yield return null;

            PlayablePlayerVitals2D second = player.GetComponent<
                PlayablePlayerVitals2D>();
            Assert.That(second, Is.SameAs(first));
            Assert.That(second.Identity, Is.EqualTo(firstIdentity));

            Object.Destroy(root);
            yield return null;
        }

        private static GameObject CreatePlayerPresentation(Transform parent)
        {
            var player = new GameObject("Player Vitals PlayMode Test Player");
            if (parent != null)
            {
                player.transform.SetParent(parent, false);
            }

            Rigidbody2D body = player.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;

            PlayablePlayerMarker2D marker = player.AddComponent<
                PlayablePlayerMarker2D>();
            StableId characterStableId = StableId.Parse(
                "character-instance.player-vitals-playmode-test");
            PlayerRouteProfilePayloadV1 route =
                PlayerRouteProfilePayloadV1.Create(
                    characterStableId,
                    StableId.Parse(
                        "loadout-profile.player-vitals-playmode-test"),
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
            return player;
        }

        private sealed class SequencedHubReturnRequest :
            IPlayablePlayerHubReturnRequestV1
        {
            private readonly Queue<bool> results;

            public SequencedHubReturnRequest(params bool[] configuredResults)
            {
                results = new Queue<bool>(configuredResults);
            }

            public int AttemptCount { get; private set; }

            public bool TryReturnToHub(
                PlayablePlayerMarker2D player,
                out string rejectionCode)
            {
                AttemptCount++;
                bool accepted = results.Count > 0 && results.Dequeue();
                rejectionCode = accepted
                    ? string.Empty
                    : "playable-player-vitals-hub-return-rejected";
                return accepted;
            }
        }
    }
}
