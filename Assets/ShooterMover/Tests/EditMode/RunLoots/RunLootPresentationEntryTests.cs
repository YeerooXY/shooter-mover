using System;
using NUnit.Framework;
using ShooterMover.Contracts.Rewards;
using ShooterMover.Domain.Common;
using ShooterMover.UnityAdapters.Rewards.RunLoots;
using UnityEngine;

namespace ShooterMover.Tests.EditMode.RunLoots
{
    public sealed class RunLootPresentationEntryTests
    {
        [Test]
        public void StrongboxRectangle_PreservesTierLabelAndPickupSize()
        {
            Texture2D texture = new Texture2D(1, 1);
            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, 1f, 1f),
                new Vector2(0.5f, 0.5f),
                1f);
            try
            {
                var entry = new RunLootPresentationEntry();
                entry.ConfigureRectangleForTests(
                    RewardGrantKind.Strongbox,
                    "strongbox-tier.black-opal",
                    null,
                    sprite,
                    new Vector3(0.85f, 0.62f, 1f),
                    new Vector2(1.2f, 1.1f),
                    "Black Opal Strongbox");

                StableId contentId;
                string diagnostic;
                Assert.That(entry.IsUsable(out diagnostic), Is.True, diagnostic);
                Assert.That(entry.TriggerShape, Is.EqualTo(RunLootTriggerShape.Rectangle));
                Assert.That(entry.TriggerSize, Is.EqualTo(new Vector2(1.2f, 1.1f)));
                Assert.That(entry.Label, Is.EqualTo("Black Opal Strongbox"));
                Assert.That(entry.TryGetContentStableId(out contentId), Is.True);
                Assert.That(contentId, Is.EqualTo(StableId.Parse("strongbox-tier.black-opal")));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(sprite);
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        [Test]
        public void StrongboxRectangle_RejectsNonPositivePickupSize()
        {
            Texture2D texture = new Texture2D(1, 1);
            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, 1f, 1f),
                new Vector2(0.5f, 0.5f),
                1f);
            try
            {
                var entry = new RunLootPresentationEntry();
                ArgumentException exception = Assert.Throws<ArgumentException>(delegate
                {
                    entry.ConfigureRectangleForTests(
                        RewardGrantKind.Strongbox,
                        "strongbox-tier.steel",
                        null,
                        sprite,
                        Vector3.one,
                        new Vector2(0f, 1.1f),
                        "Steel Strongbox");
                });

                Assert.That(
                    exception.Message,
                    Does.Contain("run-pickup-presentation-trigger-size-invalid"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(sprite);
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }
    }
}
