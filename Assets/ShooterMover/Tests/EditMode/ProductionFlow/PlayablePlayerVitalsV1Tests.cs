using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using ShooterMover.Contracts.Combat;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Domain.Common;
using ShooterMover.GameplayEntities;
using ShooterMover.UI.ProductionFlow;
using UnityEngine;
using UnityEngine.TestTools;

namespace ShooterMover.Tests.EditMode.ProductionFlow
{
    public sealed class PlayablePlayerVitalsV1Tests
    {
        [Test]
        public void DamageReplayAndConflictUseCanonicalPlayerAuthoritySemantics()
        {
            Fixture fixture = Fixture.Create();
            try
            {
                DamageReceiverCommand accepted = fixture.Damage(
                    "impact-a",
                    25d);

                DamageReceiverResult first = fixture.Vitals.ApplyDamage(accepted);
                DamageReceiverResult replay = fixture.Vitals.ApplyDamage(accepted);
                DamageReceiverResult conflict = fixture.Vitals.ApplyDamage(
                    fixture.Damage("impact-a", 30d));

                Assert.That(first.Status, Is.EqualTo(DamageReceiverStatus.Applied));
                Assert.That(replay.Status, Is.EqualTo(DamageReceiverStatus.Duplicate));
                Assert.That(
                    conflict.Status,
                    Is.EqualTo(DamageReceiverStatus.RejectedInvalid));
                Assert.That(
                    conflict.RejectionCode,
                    Is.EqualTo(DamageReceiverRejectionCode.ConflictingDuplicate));
                Assert.That(fixture.Vitals.CurrentHealth, Is.EqualTo(75d));
                Assert.That(
                    fixture.Vitals.MaximumHealth,
                    Is.EqualTo(PlayablePlayerVitals2D.ProvisionalMaximumHealth));
                Assert.That(fixture.Vitals.UsesProvisionalMaximumHealth, Is.True);
                Assert.That(fixture.Vitals.ExportSnapshot().AcceptedSequence, Is.EqualTo(1L));
                Assert.That(
                    fixture.Marker.HoldingsAuthority,
                    Is.SameAs(fixture.HoldingsAuthority));
                Assert.That(
                    fixture.Marker.LoadoutAuthority,
                    Is.SameAs(fixture.LoadoutAuthority));
            }
            finally
            {
                fixture.Dispose();
            }
        }

        [Test]
        public void LethalDamageDisablesMovementZerosVelocityAndRaisesDefeatOnce()
        {
            var returnRequest = new SequencedHubReturnRequest(true);
            Fixture fixture = Fixture.Create(returnRequest);
            try
            {
                fixture.Body.linearVelocity = new Vector2(4f, -3f);
                int defeatCount = 0;
                PlayablePlayerDefeatedFactV1 defeatedFact = null;
                fixture.Vitals.Defeated += fact =>
                {
                    defeatCount++;
                    defeatedFact = fact;
                };

                DamageReceiverCommand lethal = fixture.Damage(
                    "lethal-impact",
                    500d);

                DamageReceiverResult first = fixture.Vitals.ApplyDamage(lethal);
                DamageReceiverResult replay = fixture.Vitals.ApplyDamage(lethal);

                Assert.That(first.Status, Is.EqualTo(DamageReceiverStatus.Applied));
                Assert.That(first.DeathFact, Is.Not.Null);
                Assert.That(replay.Status, Is.EqualTo(DamageReceiverStatus.Duplicate));
                Assert.That(replay.DeathFact, Is.Null);
                Assert.That(defeatCount, Is.EqualTo(1));
                Assert.That(defeatedFact, Is.Not.Null);
                Assert.That(
                    defeatedFact.CharacterInstanceStableId,
                    Is.EqualTo(fixture.CharacterStableId));
                Assert.That(
                    defeatedFact.LethalEventStableId,
                    Is.EqualTo(lethal.EventId));
                Assert.That(fixture.Vitals.IsDefeated, Is.True);
                Assert.That(fixture.Movement.enabled, Is.False);
                Assert.That(fixture.Body.linearVelocity, Is.EqualTo(Vector2.zero));
                Assert.That(fixture.Vitals.CurrentHealth, Is.EqualTo(0d));
                Assert.That(fixture.Vitals.IsHubReturnAccepted, Is.True);
                Assert.That(returnRequest.AttemptCount, Is.EqualTo(1));
            }
            finally
            {
                fixture.Dispose();
            }
        }

        [Test]
        public void RejectedReturnCanRetryAndAcceptedReturnCannotDuplicate()
        {
            var returnRequest = new SequencedHubReturnRequest(false, true);
            Fixture fixture = Fixture.Create(returnRequest);
            try
            {
                LogAssert.Expect(
                    LogType.Error,
                    "playable-player-vitals-hub-return-rejected");

                fixture.Vitals.ApplyDamage(
                    fixture.Damage("retryable-lethal", 500d));

                Assert.That(fixture.Vitals.IsDefeated, Is.True);
                Assert.That(fixture.Vitals.IsHubReturnAccepted, Is.False);
                Assert.That(fixture.Vitals.HubReturnAttemptCount, Is.EqualTo(1));
                Assert.That(returnRequest.AttemptCount, Is.EqualTo(1));
                Assert.That(
                    fixture.Vitals.Diagnostic,
                    Is.EqualTo("playable-player-vitals-hub-return-rejected"));

                Assert.That(fixture.Vitals.TryRetryHubReturn(), Is.True);
                Assert.That(fixture.Vitals.IsHubReturnAccepted, Is.True);
                Assert.That(fixture.Vitals.HubReturnAttemptCount, Is.EqualTo(2));
                Assert.That(returnRequest.AttemptCount, Is.EqualTo(2));
                Assert.That(fixture.Vitals.Diagnostic, Is.Empty);

                Assert.That(fixture.Vitals.TryRetryHubReturn(), Is.True);
                Assert.That(fixture.Vitals.TryRetryHubReturn(), Is.True);
                Assert.That(fixture.Vitals.HubReturnAttemptCount, Is.EqualTo(2));
                Assert.That(returnRequest.AttemptCount, Is.EqualTo(2));
            }
            finally
            {
                fixture.Dispose();
            }
        }

        [Test]
        public void AuthorityMismatchCannotReportSuccessOrMutateCharacterReferences()
        {
            var returnRequest = new GuardedHubReturnRequest(
                mismatchHoldingsAuthority: true);
            Fixture fixture = Fixture.Create(returnRequest);
            try
            {
                object originalHoldings = fixture.Marker.HoldingsAuthority;
                object originalLoadout = fixture.Marker.LoadoutAuthority;
                PlayerRouteProfilePayloadV1 originalRoute =
                    fixture.Marker.RoutePayload;

                LogAssert.Expect(
                    LogType.Error,
                    "playable-player-vitals-character-authority-changed");

                fixture.Vitals.ApplyDamage(
                    fixture.Damage("authority-mismatch-lethal", 500d));

                Assert.That(fixture.Vitals.IsDefeated, Is.True);
                Assert.That(fixture.Vitals.IsHubReturnAccepted, Is.False);
                Assert.That(returnRequest.AcceptedTransitionCount, Is.EqualTo(0));
                Assert.That(
                    fixture.Vitals.Diagnostic,
                    Is.EqualTo(
                        "playable-player-vitals-character-authority-changed"));
                Assert.That(
                    fixture.Marker.HoldingsAuthority,
                    Is.SameAs(originalHoldings));
                Assert.That(
                    fixture.Marker.LoadoutAuthority,
                    Is.SameAs(originalLoadout));
                Assert.That(fixture.Marker.RoutePayload, Is.SameAs(originalRoute));

                Assert.That(fixture.Vitals.TryRetryHubReturn(), Is.False);
                Assert.That(returnRequest.AcceptedTransitionCount, Is.EqualTo(0));
                Assert.That(
                    fixture.Marker.HoldingsAuthority,
                    Is.SameAs(originalHoldings));
                Assert.That(
                    fixture.Marker.LoadoutAuthority,
                    Is.SameAs(originalLoadout));
            }
            finally
            {
                fixture.Dispose();
            }
        }

        [Test]
        public void OrdinaryObserverFailureDoesNotBlockLaterObserversOrHubReturn()
        {
            var returnRequest = new SequencedHubReturnRequest(true);
            Fixture fixture = Fixture.Create(returnRequest);
            try
            {
                int observed = 0;
                fixture.Vitals.Defeated += fact =>
                {
                    throw new InvalidOperationException(
                        "ordinary-defeat-observer-failure");
                };
                fixture.Vitals.Defeated += fact => observed++;

                LogAssert.Expect(
                    LogType.Exception,
                    new Regex("ordinary-defeat-observer-failure"));

                fixture.Vitals.ApplyDamage(
                    fixture.Damage("ordinary-observer-lethal", 500d));

                Assert.That(observed, Is.EqualTo(1));
                Assert.That(fixture.Vitals.IsHubReturnAccepted, Is.True);
                Assert.That(returnRequest.AttemptCount, Is.EqualTo(1));
            }
            finally
            {
                fixture.Dispose();
            }
        }

        [Test]
        public void FatalObserverExceptionIsRethrown()
        {
            var returnRequest = new SequencedHubReturnRequest(true);
            Fixture fixture = Fixture.Create(returnRequest);
            try
            {
                fixture.Vitals.Defeated += fact =>
                {
                    throw new OutOfMemoryException(
                        "fatal-defeat-observer-failure");
                };

                Assert.Throws<OutOfMemoryException>(() =>
                {
                    fixture.Vitals.ApplyDamage(
                        fixture.Damage("fatal-observer-lethal", 500d));
                });

                Assert.That(fixture.Vitals.IsDefeated, Is.True);
                Assert.That(fixture.Movement.enabled, Is.False);
                Assert.That(fixture.Body.linearVelocity, Is.EqualTo(Vector2.zero));
                Assert.That(fixture.Vitals.IsHubReturnAccepted, Is.False);
                Assert.That(returnRequest.AttemptCount, Is.EqualTo(0));
            }
            finally
            {
                fixture.Dispose();
            }
        }

        [Test]
        public void DuplicateBindingIsRejectedWithoutReplacingTheAuthority()
        {
            Fixture fixture = Fixture.Create();
            try
            {
                GameplayEntityIdentity identity = fixture.Vitals.Identity;

                InvalidOperationException exception = Assert.Throws<
                    InvalidOperationException>(() => fixture.Vitals.Bind(
                        fixture.Marker,
                        fixture.Body,
                        fixture.Movement));

                Assert.That(
                    exception.Message,
                    Is.EqualTo("playable-player-vitals-duplicate-binding"));
                Assert.That(fixture.Vitals.Identity, Is.EqualTo(identity));
                Assert.That(fixture.Vitals.CurrentHealth, Is.EqualTo(100d));
            }
            finally
            {
                fixture.Dispose();
            }
        }

        private sealed class SequencedHubReturnRequest :
            IPlayablePlayerHubReturnRequestV1
        {
            private readonly Queue<bool> results;

            public SequencedHubReturnRequest(params bool[] configuredResults)
            {
                results = new Queue<bool>(
                    configuredResults == null || configuredResults.Length == 0
                        ? new[] { false }
                        : configuredResults);
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

        private sealed class GuardedHubReturnRequest :
            IPlayablePlayerHubReturnRequestV1
        {
            private readonly bool mismatchHoldingsAuthority;

            public GuardedHubReturnRequest(bool mismatchHoldingsAuthority)
            {
                this.mismatchHoldingsAuthority = mismatchHoldingsAuthority;
            }

            public int AttemptCount { get; private set; }
            public int AcceptedTransitionCount { get; private set; }

            public bool TryReturnToHub(
                PlayablePlayerMarker2D player,
                out string rejectionCode)
            {
                AttemptCount++;
                object expectedHoldings = mismatchHoldingsAuthority
                    ? new object()
                    : player.HoldingsAuthority;
                bool valid =
                    PlayablePlayerHubReturnAuthorityGuardV1.TryValidate(
                        player,
                        player.CharacterInstanceStableId,
                        player.ClassDefinitionStableId,
                        player.RoutePayload,
                        player.RoutePayload,
                        expectedHoldings,
                        player.LoadoutAuthority,
                        out rejectionCode);
                if (!valid)
                {
                    return false;
                }

                AcceptedTransitionCount++;
                return true;
            }
        }

        private sealed class Fixture : IDisposable
        {
            private Fixture(
                GameObject player,
                Rigidbody2D body,
                PlayablePlayerMarker2D marker,
                PlayableTopDownMovement2D movement,
                PlayablePlayerVitals2D vitals,
                StableId characterStableId,
                object holdingsAuthority,
                object loadoutAuthority)
            {
                Player = player;
                Body = body;
                Marker = marker;
                Movement = movement;
                Vitals = vitals;
                CharacterStableId = characterStableId;
                HoldingsAuthority = holdingsAuthority;
                LoadoutAuthority = loadoutAuthority;
            }

            public GameObject Player { get; }
            public Rigidbody2D Body { get; }
            public PlayablePlayerMarker2D Marker { get; }
            public PlayableTopDownMovement2D Movement { get; }
            public PlayablePlayerVitals2D Vitals { get; }
            public StableId CharacterStableId { get; }
            public object HoldingsAuthority { get; }
            public object LoadoutAuthority { get; }

            public static Fixture Create(
                IPlayablePlayerHubReturnRequestV1 hubReturnRequest = null)
            {
                var player = new GameObject("Player Vitals Test Player");
                Rigidbody2D body = player.AddComponent<Rigidbody2D>();
                body.gravityScale = 0f;
                PlayablePlayerMarker2D marker = player.AddComponent<
                    PlayablePlayerMarker2D>();
                StableId characterStableId = StableId.Parse(
                    "character-instance.player-vitals-test");
                StableId classStableId = StableId.Parse("class.striker");
                PlayerRouteProfilePayloadV1 route =
                    PlayerRouteProfilePayloadV1.Create(
                        characterStableId,
                        StableId.Parse("loadout-profile.player-vitals-test"),
                        new StableId[PlayerRouteProfilePayloadV1.WeaponSlotCount]);
                object holdingsAuthority = new object();
                object loadoutAuthority = new object();
                marker.Bind(
                    characterStableId,
                    classStableId,
                    route,
                    holdingsAuthority,
                    loadoutAuthority);

                PlayableTopDownMovement2D movement = player.AddComponent<
                    PlayableTopDownMovement2D>();
                movement.Bind(body, 6f);
                PlayablePlayerVitals2D vitals = player.AddComponent<
                    PlayablePlayerVitals2D>();
                vitals.Bind(
                    marker,
                    body,
                    movement,
                    hubReturnRequest
                    ?? new SequencedHubReturnRequest(true));

                return new Fixture(
                    player,
                    body,
                    marker,
                    movement,
                    vitals,
                    characterStableId,
                    holdingsAuthority,
                    loadoutAuthority);
            }

            public DamageReceiverCommand Damage(string eventSuffix, double amount)
            {
                return new DamageReceiverCommand(
                    StableId.Create("event", eventSuffix),
                    StableId.Parse("actor.enemy-test"),
                    null,
                    Vitals.Identity.EntityInstanceId,
                    amount,
                    CombatChannel.Kinetic,
                    Vitals.LifecycleGeneration);
            }

            public void Dispose()
            {
                if (Player != null)
                {
                    UnityEngine.Object.DestroyImmediate(Player);
                }
            }
        }
    }
}
