using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using ShooterMover.Application.Progression.Context;
using ShooterMover.Contracts.Progression.Context;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Progression.Context;

namespace ShooterMover.Tests.EditMode.Progression.Context
{
    public sealed class ProgressionContextTests
    {
        private static readonly StableId NormalDifficulty =
            StableId.Parse("difficulty.normal");

        [Test]
        public void Create_AcceptsLowAndVeryHighLevelsWithoutGameWideCap()
        {
            ProgressionContext low = ProgressionContext.Create(
                0,
                0,
                NormalDifficulty,
                0);
            ProgressionContext high = ProgressionContext.Create(
                int.MaxValue,
                int.MaxValue,
                StableId.Parse("difficulty.extreme"),
                int.MaxValue);

            Assert.That(low.CharacterLevel, Is.Zero);
            Assert.That(low.RegionLevel, Is.Zero);
            Assert.That(high.CharacterLevel, Is.EqualTo(int.MaxValue));
            Assert.That(high.RegionLevel, Is.EqualTo(int.MaxValue));
            Assert.That(high.DifficultyValue, Is.EqualTo(int.MaxValue));
        }

        [TestCase(-1, 0, 0, ProgressionContextValidationCode.CharacterLevelNegative)]
        [TestCase(0, -1, 0, ProgressionContextValidationCode.RegionLevelNegative)]
        [TestCase(0, 0, -1, ProgressionContextValidationCode.DifficultyValueNegative)]
        public void TryCreate_NegativeValuesRejectExplicitly(
            int characterLevel,
            int regionLevel,
            int difficultyValue,
            ProgressionContextValidationCode expectedCode)
        {
            ProgressionContext context;
            ProgressionContextValidationResult validation;

            bool accepted = ProgressionContext.TryCreate(
                characterLevel,
                regionLevel,
                NormalDifficulty,
                difficultyValue,
                null,
                out context,
                out validation);

            Assert.That(accepted, Is.False);
            Assert.That(context, Is.Null);
            Assert.That(validation.IsValid, Is.False);
            Assert.That(validation.Code, Is.EqualTo(expectedCode));
        }

        [Test]
        public void TryCreate_MissingDifficultyRejectsExplicitly()
        {
            ProgressionContext context;
            ProgressionContextValidationResult validation;

            bool accepted = ProgressionContext.TryCreate(
                1,
                1,
                null,
                0,
                null,
                out context,
                out validation);

            Assert.That(accepted, Is.False);
            Assert.That(context, Is.Null);
            Assert.That(
                validation.Code,
                Is.EqualTo(ProgressionContextValidationCode.DifficultyIdentityMissing));
        }

        [Test]
        public void TryCreate_NullTagEntryRejectsWithoutRepair()
        {
            ProgressionContext context;
            ProgressionContextValidationResult validation;

            bool accepted = ProgressionContext.TryCreate(
                1,
                2,
                NormalDifficulty,
                0,
                new StableId[] { StableId.Parse("progression-tag.valid"), null },
                out context,
                out validation);

            Assert.That(accepted, Is.False);
            Assert.That(context, Is.Null);
            Assert.That(
                validation.Code,
                Is.EqualTo(ProgressionContextValidationCode.ProgressionTagMissing));
        }

        [Test]
        public void Tags_AreDeduplicatedAndOrderedCanonically()
        {
            ProgressionContext context = ProgressionContext.Create(
                3,
                4,
                NormalDifficulty,
                1,
                new[]
                {
                    StableId.Parse("progression-tag.zulu"),
                    StableId.Parse("progression-tag.alpha"),
                    StableId.Parse("progression-tag.zulu"),
                });

            Assert.That(context.ProgressionTags.Count, Is.EqualTo(2));
            Assert.That(
                context.ProgressionTags[0],
                Is.EqualTo(StableId.Parse("progression-tag.alpha")));
            Assert.That(
                context.ProgressionTags[1],
                Is.EqualTo(StableId.Parse("progression-tag.zulu")));
        }

        [Test]
        public void InputTagCollectionMutation_DoesNotMutateContext()
        {
            var tags = new List<StableId>
            {
                StableId.Parse("progression-tag.alpha"),
            };
            ProgressionContext context = ProgressionContext.Create(
                3,
                4,
                NormalDifficulty,
                1,
                tags);

            tags.Clear();

            Assert.That(context.ProgressionTags.Count, Is.EqualTo(1));
            Assert.That(
                context.ProgressionTags[0],
                Is.EqualTo(StableId.Parse("progression-tag.alpha")));
        }

        [Test]
        public void EqualityCanonicalTextAndFingerprint_IgnoreInputTagOrderAndDuplicates()
        {
            ProgressionContext first = ProgressionContext.Create(
                7,
                11,
                StableId.Parse("difficulty.veteran"),
                3,
                new[]
                {
                    StableId.Parse("progression-tag.zulu"),
                    StableId.Parse("progression-tag.alpha"),
                    StableId.Parse("progression-tag.zulu"),
                });
            ProgressionContext second = ProgressionContext.Create(
                7,
                11,
                StableId.Parse("difficulty.veteran"),
                3,
                new[]
                {
                    StableId.Parse("progression-tag.alpha"),
                    StableId.Parse("progression-tag.zulu"),
                });

            Assert.That(first, Is.EqualTo(second));
            Assert.That(first.GetHashCode(), Is.EqualTo(second.GetHashCode()));
            Assert.That(first.ToCanonicalString(), Is.EqualTo(second.ToCanonicalString()));
            Assert.That(first.Fingerprint, Is.EqualTo(second.Fingerprint));
            Assert.That(
                first.Fingerprint,
                Is.EqualTo(
                    "sha256:ea4b009b80c92f5e98323526b3466b761c5bac43e084041573ec88ea879f85e5"));
        }

        [Test]
        public void Snapshot_EqualityAndFingerprintIncludeSequenceDeterministically()
        {
            ProgressionContext context = ProgressionContext.Create(
                1,
                2,
                NormalDifficulty,
                0);
            ProgressionContextSnapshot first = ProgressionContextSnapshot.Create(5, context);
            ProgressionContextSnapshot equal = ProgressionContextSnapshot.Create(5, context);
            ProgressionContextSnapshot next = ProgressionContextSnapshot.Create(6, context);

            Assert.That(first, Is.EqualTo(equal));
            Assert.That(first.Fingerprint, Is.EqualTo(equal.Fingerprint));
            Assert.That(first, Is.Not.EqualTo(next));
            Assert.That(first.Fingerprint, Is.Not.EqualTo(next.Fingerprint));
        }

        [Test]
        public void DirectProvider_ReturnsExactImmutableContext()
        {
            ProgressionContext context = ProgressionContext.Create(
                4,
                5,
                NormalDifficulty,
                1);
            var provider = new DirectProgressionContextProvider(context);

            Assert.That(provider.CurrentContext, Is.SameAs(context));
        }

        [Test]
        public void SessionProvider_ValidReplacementIncrementsSequenceAndReturnsChangeFact()
        {
            ProgressionContext initial = ProgressionContext.Create(
                1,
                1,
                NormalDifficulty,
                0);
            ProgressionContext replacement = ProgressionContext.Create(
                2,
                3,
                StableId.Parse("difficulty.veteran"),
                1,
                new[] { StableId.Parse("progression-tag.chapter-two") });
            var provider = new SessionProgressionContextProvider(initial);

            ProgressionContextChangeFact change = provider.TryReplace(replacement);

            Assert.That(change.Status, Is.EqualTo(ProgressionContextReplacementStatus.Applied));
            Assert.That(change.Changed, Is.True);
            Assert.That(change.PreviousSnapshot.Sequence, Is.Zero);
            Assert.That(change.CurrentSnapshot.Sequence, Is.EqualTo(1));
            Assert.That(change.PreviousSnapshot.Context, Is.SameAs(initial));
            Assert.That(change.CurrentSnapshot.Context, Is.SameAs(replacement));
            Assert.That(provider.CurrentContext, Is.SameAs(replacement));
            Assert.That(provider.CurrentSnapshot, Is.SameAs(change.CurrentSnapshot));
        }

        [Test]
        public void SessionProvider_InvalidReplacementLeavesPreviousStateUnchanged()
        {
            ProgressionContext initial = ProgressionContext.Create(
                5,
                6,
                NormalDifficulty,
                1);
            var provider = new SessionProgressionContextProvider(initial);
            ProgressionContextSnapshot before = provider.CurrentSnapshot;

            ProgressionContextChangeFact change = provider.TryReplace(
                -1,
                6,
                NormalDifficulty,
                1);

            Assert.That(change.Status, Is.EqualTo(ProgressionContextReplacementStatus.Rejected));
            Assert.That(change.Changed, Is.False);
            Assert.That(
                change.Validation.Code,
                Is.EqualTo(ProgressionContextValidationCode.CharacterLevelNegative));
            Assert.That(change.PreviousSnapshot, Is.SameAs(before));
            Assert.That(change.CurrentSnapshot, Is.SameAs(before));
            Assert.That(provider.CurrentSnapshot, Is.SameAs(before));
            Assert.That(provider.CurrentContext, Is.SameAs(initial));
        }

        [Test]
        public void SessionProvider_ExactDuplicateIsNoChangeAndDoesNotIncrementSequence()
        {
            ProgressionContext initial = ProgressionContext.Create(
                8,
                9,
                NormalDifficulty,
                2,
                new[] { StableId.Parse("progression-tag.alpha") });
            ProgressionContext equalReplacement = ProgressionContext.Create(
                8,
                9,
                NormalDifficulty,
                2,
                new[]
                {
                    StableId.Parse("progression-tag.alpha"),
                    StableId.Parse("progression-tag.alpha"),
                });
            var provider = new SessionProgressionContextProvider(initial);
            ProgressionContextSnapshot before = provider.CurrentSnapshot;

            ProgressionContextChangeFact change = provider.TryReplace(equalReplacement);

            Assert.That(
                change.Status,
                Is.EqualTo(ProgressionContextReplacementStatus.DuplicateNoChange));
            Assert.That(change.Changed, Is.False);
            Assert.That(change.CurrentSnapshot, Is.SameAs(before));
            Assert.That(provider.CurrentSnapshot.Sequence, Is.Zero);
            Assert.That(provider.CurrentContext, Is.SameAs(initial));
        }

        [Test]
        public void SessionProvider_NullReplacementRejectsWithoutMutation()
        {
            ProgressionContext initial = ProgressionContext.Create(
                1,
                1,
                NormalDifficulty,
                0);
            var provider = new SessionProgressionContextProvider(initial);
            ProgressionContextSnapshot before = provider.CurrentSnapshot;

            ProgressionContextChangeFact change = provider.TryReplace(
                (ProgressionContext)null);

            Assert.That(change.Status, Is.EqualTo(ProgressionContextReplacementStatus.Rejected));
            Assert.That(
                change.Validation.Code,
                Is.EqualTo(ProgressionContextValidationCode.ContextMissing));
            Assert.That(provider.CurrentSnapshot, Is.SameAs(before));
        }

        [Test]
        public void ContextAndProviders_HaveNoUnityGlobalOrRandomCurveDependency()
        {
            Assert.That(ReferencesUnity(typeof(ProgressionContext).Assembly), Is.False);
            Assert.That(ReferencesUnity(typeof(IProgressionContextProvider).Assembly), Is.False);
            Assert.That(
                ReferencesUnity(typeof(SessionProgressionContextProvider).Assembly),
                Is.False);

            AssertPublicSurfaceHasNoForbiddenType(typeof(ProgressionContext));
            AssertPublicSurfaceHasNoForbiddenType(typeof(IProgressionContextProvider));
            AssertPublicSurfaceHasNoForbiddenType(typeof(DirectProgressionContextProvider));
            AssertPublicSurfaceHasNoForbiddenType(typeof(SessionProgressionContextProvider));

            FieldInfo[] staticProviderFields = typeof(SessionProgressionContextProvider)
                .GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(staticProviderFields, Is.Empty);
        }

        private static bool ReferencesUnity(Assembly assembly)
        {
            AssemblyName[] references = assembly.GetReferencedAssemblies();
            for (int index = 0; index < references.Length; index++)
            {
                if (references[index].Name.StartsWith("UnityEngine", StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static void AssertPublicSurfaceHasNoForbiddenType(Type type)
        {
            const BindingFlags Flags =
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;

            foreach (PropertyInfo property in type.GetProperties(Flags))
            {
                AssertTypeIsAllowed(property.PropertyType);
            }

            foreach (FieldInfo field in type.GetFields(Flags))
            {
                AssertTypeIsAllowed(field.FieldType);
            }

            foreach (MethodInfo method in type.GetMethods(Flags))
            {
                AssertTypeIsAllowed(method.ReturnType);
                ParameterInfo[] parameters = method.GetParameters();
                for (int index = 0; index < parameters.Length; index++)
                {
                    AssertTypeIsAllowed(parameters[index].ParameterType);
                }
            }
        }

        private static void AssertTypeIsAllowed(Type type)
        {
            string fullName = type.FullName ?? string.Empty;
            Assert.That(fullName, Does.Not.Contain("UnityEngine"));
            Assert.That(fullName, Does.Not.Contain(".Progression.Curves"));
            Assert.That(fullName, Does.Not.Contain("UnityEngine.Random"));
            Assert.That(fullName, Does.Not.Contain("System.Random"));

            if (!type.IsGenericType)
            {
                return;
            }

            Type[] arguments = type.GetGenericArguments();
            for (int index = 0; index < arguments.Length; index++)
            {
                AssertTypeIsAllowed(arguments[index]);
            }
        }
    }
}
