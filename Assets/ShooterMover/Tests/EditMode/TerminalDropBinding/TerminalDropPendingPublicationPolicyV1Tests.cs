#if UNITY_EDITOR
using System;
using System.Reflection;
using NUnit.Framework;
using ShooterMover.Application.Rewards.Drops;
using ShooterMover.Domain.Enemies.Catalog;
using ShooterMover.EnemyRuntimeComposition;
using ShooterMover.TerminalDropBinding;

namespace ShooterMover.Tests.EditMode.TerminalDropBinding
{
    public sealed class TerminalDropPendingPublicationPolicyV1Tests
    {
        private sealed class RejectOncePending :
            IGeneratedTerminalDropPendingAdmissionV1
        {
            private readonly PendingTerminalDropAdmissionAuthorityV1 inner;
            private bool rejected;

            public RejectOncePending(PendingTerminalDropAdmissionAuthorityV1 inner)
            {
                this.inner = inner;
            }

            public PendingTerminalDropAdmissionResultV1 Admit(
                GeneratedTerminalDropResultV1 result)
            {
                if (!rejected)
                {
                    rejected = true;
                    return PendingTerminalDropAdmissionResultV1.Rejected(
                        "pending-publication-rejected-for-test");
                }
                return inner.Admit(result);
            }
        }

        [Test]
        public void Validate_RejectsRejectedGenerationBatch()
        {
            var batch = new TerminalPersonalRewardBatchV1(
                TerminalPersonalRewardBatchStatusV1.Rejected,
                null,
                Array.Empty<GeneratedTerminalDropResultV1>(),
                "generation-rejected-for-test");

            InvalidOperationException error =
                Assert.Throws<InvalidOperationException>(() =>
                    TerminalDropPendingPublicationPolicyV1.Validate(
                        batch,
                        Array.Empty<PendingTerminalDropAdmissionResultV1>()));

            StringAssert.Contains("generation-rejected-for-test", error.Message);
        }

        [Test]
        public void Validate_AcceptsExactExplicitNoDrop()
        {
            var batch = new TerminalPersonalRewardBatchV1(
                TerminalPersonalRewardBatchStatusV1.ExplicitNoDrop,
                null,
                Array.Empty<GeneratedTerminalDropResultV1>(),
                string.Empty);

            Assert.DoesNotThrow(() =>
                TerminalDropPendingPublicationPolicyV1.Validate(
                    batch,
                    Array.Empty<PendingTerminalDropAdmissionResultV1>()));
        }

        [Test]
        public void StrictConsumer_RejectedPendingPublicationThrowsThenRetrySucceeds()
        {
            TerminalDropGenerationAuthorityV1 authority;
            EnemyDeathFactV1 death;
            CreateExistingEnemyPipeline(out authority, out death);
            var durable = new PendingTerminalDropAdmissionAuthorityV1();
            var consumer = new EnemyTerminalDropFactConsumerV1(
                authority,
                new RejectOncePending(durable),
                null,
                true);

            InvalidOperationException error =
                Assert.Throws<InvalidOperationException>(() =>
                    consumer.Consume(death));

            StringAssert.Contains("not admitted", error.Message);
            Assert.That(durable.PendingBatchCount, Is.EqualTo(0));

            Assert.DoesNotThrow(() => consumer.Consume(death));
            Assert.That(durable.PendingBatchCount, Is.EqualTo(1));
            Assert.That(consumer.LastAdmission.Status,
                Is.EqualTo(PendingTerminalDropAdmissionStatusV1.Accepted));
        }

        [Test]
        public void RollbackAccepted_RemovesOnlyExactNewAdmissionAndIsIdempotent()
        {
            GeneratedTerminalDropResultV1 generated = GenerateExistingResult();
            var pending = new PendingTerminalDropAdmissionAuthorityV1();
            PendingTerminalDropAdmissionResultV1 accepted =
                pending.Admit(generated);

            Assert.That(accepted.Status,
                Is.EqualTo(PendingTerminalDropAdmissionStatusV1.Accepted));
            Assert.That(pending.PendingBatchCount, Is.EqualTo(1));

            string diagnostic;
            Assert.That(
                pending.TryRollbackAccepted(accepted, out diagnostic),
                Is.True,
                diagnostic);
            Assert.That(pending.PendingBatchCount, Is.EqualTo(0));

            Assert.That(
                pending.TryRollbackAccepted(accepted, out diagnostic),
                Is.True,
                diagnostic);
            Assert.That(pending.PendingBatchCount, Is.EqualTo(0));
        }

        [Test]
        public void RollbackAccepted_RejectsExactReplayReceipt()
        {
            GeneratedTerminalDropResultV1 generated = GenerateExistingResult();
            var pending = new PendingTerminalDropAdmissionAuthorityV1();
            PendingTerminalDropAdmissionResultV1 accepted =
                pending.Admit(generated);
            PendingTerminalDropAdmissionResultV1 replay =
                pending.Admit(generated);

            Assert.That(accepted.Status,
                Is.EqualTo(PendingTerminalDropAdmissionStatusV1.Accepted));
            Assert.That(replay.Status,
                Is.EqualTo(PendingTerminalDropAdmissionStatusV1.ExactReplay));

            string diagnostic;
            Assert.That(
                pending.TryRollbackAccepted(replay, out diagnostic),
                Is.False);
            StringAssert.Contains("receipt-invalid", diagnostic);
            Assert.That(pending.PendingBatchCount, Is.EqualTo(1));
        }

        private static GeneratedTerminalDropResultV1 GenerateExistingResult()
        {
            Type fixtureType = typeof(TerminalDropReviewBlockerTests);
            MethodInfo factory = RequireMethod(fixtureType, "PipelineAuthority");
            Type factType = fixtureType.GetNestedType(
                "PipelineFact",
                BindingFlags.NonPublic);
            Assert.That(factType, Is.Not.Null);

            var authority = (TerminalDropGenerationAuthorityV1)factory.Invoke(
                null,
                new object[] { null, null, null, null });
            object fact = Activator.CreateInstance(
                factType,
                BindingFlags.Instance | BindingFlags.Public
                    | BindingFlags.NonPublic,
                null,
                new object[] { "transaction-policy" },
                null);
            GeneratedTerminalDropResultV1 result = authority.Generate(fact);
            Assert.That(result.IsAccepted, Is.True);
            return result;
        }

        private static void CreateExistingEnemyPipeline(
            out TerminalDropGenerationAuthorityV1 authority,
            out EnemyDeathFactV1 death)
        {
            Type fixtureType = typeof(TerminalDropReviewBlockerTests);
            MethodInfo definitionFactory = RequireMethod(
                fixtureType,
                "EnemyDefinition");
            MethodInfo authorityFactory = RequireMethod(
                fixtureType,
                "EnemyAuthority");
            MethodInfo deathFactory = RequireMethod(
                fixtureType,
                "EnemyDeath");

            var definition = (EnemyDefinitionV1)definitionFactory.Invoke(
                null,
                Array.Empty<object>());
            Type contextType = fixtureType.GetNestedType(
                "EnemyContextResolver",
                BindingFlags.NonPublic);
            Type generatorType = fixtureType.GetNestedType(
                "CountingGenerator",
                BindingFlags.NonPublic);
            Assert.That(contextType, Is.Not.Null);
            Assert.That(generatorType, Is.Not.Null);

            object contexts = Activator.CreateInstance(
                contextType,
                true);
            object generator = Activator.CreateInstance(
                generatorType,
                true);
            authority = (TerminalDropGenerationAuthorityV1)authorityFactory.Invoke(
                null,
                new[] { (object)definition, contexts, generator });
            death = (EnemyDeathFactV1)deathFactory.Invoke(
                null,
                new object[] { definition });
            Assert.That(authority, Is.Not.Null);
            Assert.That(death, Is.Not.Null);
        }

        private static MethodInfo RequireMethod(Type type, string name)
        {
            MethodInfo method = type.GetMethod(
                name,
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(method, Is.Not.Null, name);
            return method;
        }
    }
}
#endif
