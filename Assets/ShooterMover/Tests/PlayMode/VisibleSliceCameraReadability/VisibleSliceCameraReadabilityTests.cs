#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Movement;
using ShooterMover.Presentation.VisibleSliceCameraReadability;
using UnityEngine;

namespace ShooterMover.Tests.PlayMode.VisibleSliceCameraReadability
{
    public sealed class VisibleSliceCameraReadabilityTests
    {
        private readonly List<GameObject> spawnedObjects = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            for (int index = spawnedObjects.Count - 1; index >= 0; index--)
            {
                if (spawnedObjects[index] != null)
                {
                    UnityEngine.Object.DestroyImmediate(spawnedObjects[index]);
                }
            }

            spawnedObjects.Clear();
        }

        [Test]
        public void ReferenceFraming_IsStableAt1920x1080AndSmaller16By9()
        {
            VisibleSliceCameraConfiguration configuration = CreateConfiguration();
            Vector2 actor = new Vector2(1f, -2f);
            float referenceAspect = configuration.ResolveAspect(1920, 1080);
            float smallerAspect = configuration.ResolveAspect(1280, 720);

            VisibleSliceCameraFrame reference = VisibleSliceCameraFrameSolver.Solve(
                configuration,
                actor,
                null,
                referenceAspect);
            VisibleSliceCameraFrame smaller = VisibleSliceCameraFrameSolver.Solve(
                configuration,
                actor,
                null,
                smallerAspect);

            Assert.That(referenceAspect, Is.EqualTo(16f / 9f).Within(0.000001f));
            Assert.That(smallerAspect, Is.EqualTo(referenceAspect).Within(0.000001f));
            Assert.That(reference.Center, Is.EqualTo(smaller.Center));
            Assert.That(reference.HalfExtents, Is.EqualTo(smaller.HalfExtents));
            Assert.That(reference.HalfExtents.x, Is.EqualTo(10f).Within(0.0001f));
            Assert.That(reference.HalfExtents.y, Is.EqualTo(5.625f).Within(0.0001f));

            TestContext.WriteLine(
                "REFERENCE CAPTURE PAIR: 1920x1080 and 1280x720 => center {0}, half-extents {1}",
                reference.Center,
                reference.HalfExtents);
        }

        [Test]
        public void RoomClamp_KeepsTheOrthographicViewInsideBoundaries()
        {
            VisibleSliceCameraConfiguration configuration = CreateConfiguration();
            float aspect = configuration.ReferenceAspect;

            VisibleSliceCameraFrame upperRight = VisibleSliceCameraFrameSolver.Solve(
                configuration,
                new Vector2(100f, 100f),
                null,
                aspect);
            VisibleSliceCameraFrame lowerLeft = VisibleSliceCameraFrameSolver.Solve(
                configuration,
                new Vector2(-100f, -100f),
                null,
                aspect);

            AssertViewInside(configuration.RoomBounds, upperRight.WorldViewport);
            AssertViewInside(configuration.RoomBounds, lowerLeft.WorldViewport);
            Assert.That(upperRight.Center.x, Is.EqualTo(10f).Within(0.0001f));
            Assert.That(upperRight.Center.y, Is.EqualTo(6.375f).Within(0.0001f));
            Assert.That(lowerLeft.Center.x, Is.EqualTo(-10f).Within(0.0001f));
            Assert.That(lowerLeft.Center.y, Is.EqualTo(-6.375f).Within(0.0001f));
            Assert.That(upperRight.RoomFullyContainsView, Is.True);
            Assert.That(lowerLeft.RoomFullyContainsView, Is.True);
        }

        [Test]
        public void HudSafeMargins_ConstrainLookAheadAndScreenEdgeWarnings()
        {
            VisibleSliceCameraConfiguration configuration = CreateConfiguration();
            VisibleSliceCameraFrame frame = VisibleSliceCameraFrameSolver.Solve(
                configuration,
                Vector2.zero,
                CreateBurstSnapshot(),
                configuration.ReferenceAspect);
            VisibleSliceWarningSignal signal = new VisibleSliceWarningSignal(
                "enemy.blaster-warning",
                "TURRET FIRE",
                true,
                1.2f,
                100);
            VisibleSliceWarningPresentation warning =
                VisibleSliceWarningProjector.ProjectViewport(
                    new Vector2(1.6f, -0.3f),
                    false,
                    signal,
                    configuration,
                    false);

            Assert.That(frame.UsedThrusterLookAhead, Is.True);
            Assert.That(frame.ActorInsideHudSafeViewport, Is.True);
            Assert.That(
                VisibleSliceWarningProjector.IsWithinWarningConstraints(
                    configuration,
                    warning.ViewportPosition),
                Is.True);
            Assert.That(warning.IsEdgeClamped, Is.True);
            Assert.That(warning.ViewportPosition.x, Is.EqualTo(0.92f).Within(0.0001f));
            Assert.That(warning.ViewportPosition.y, Is.EqualTo(0.10f).Within(0.0001f));
            Assert.That(warning.HasColorIndependentCue, Is.True);
        }

        [Test]
        public void Restart_ReappliesTheSameReferenceFrameWithoutAccumulation()
        {
            GameObject cameraObject = Spawn("camera");
            Camera camera = cameraObject.AddComponent<Camera>();
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
            VisibleSliceCameraRig rig = cameraObject.AddComponent<VisibleSliceCameraRig>();

            GameObject actorObject = Spawn("actor");
            actorObject.transform.position = new Vector3(2f, 1f, 0f);
            VisibleSliceCameraConfiguration configuration = CreateConfiguration();
            rig.Configure(
                camera,
                new TransformCameraFollowSource(actorObject.transform),
                new FixedThrusterStatusReader(CreateBurstSnapshot()),
                new FixedReducedEffectsSource(false),
                configuration);

            Vector2 initialCenter = rig.LastFrame.Center;
            cameraObject.transform.position = new Vector3(99f, -99f, -10f);

            Assert.That(rig.Restart(), Is.True);
            Assert.That(rig.RestartGeneration, Is.EqualTo(1L));
            Assert.That(rig.LastFrame.Center, Is.EqualTo(initialCenter));

            cameraObject.transform.position = new Vector3(-99f, 99f, -10f);
            Assert.That(rig.Restart(), Is.True);
            Assert.That(rig.RestartGeneration, Is.EqualTo(2L));
            Assert.That(rig.LastFrame.Center, Is.EqualTo(initialCenter));
            Assert.That(cameraObject.transform.position.z, Is.EqualTo(-10f));
        }

        [Test]
        public void ReducedEffects_PreservesTimingPositionShapeTextAndCountdown()
        {
            VisibleSliceCameraConfiguration configuration = CreateConfiguration();
            VisibleSliceWarningSignal signal = new VisibleSliceWarningSignal(
                "enemy.blaster-warning",
                "TURRET FIRE",
                true,
                1.4f,
                100);
            Vector2 source = new Vector2(1.4f, 0.7f);

            VisibleSliceWarningPresentation full =
                VisibleSliceWarningProjector.ProjectViewport(
                    source,
                    false,
                    signal,
                    configuration,
                    false);
            VisibleSliceWarningPresentation reduced =
                VisibleSliceWarningProjector.ProjectViewport(
                    source,
                    false,
                    signal,
                    configuration,
                    true);

            Assert.That(reduced.IsVisible, Is.EqualTo(full.IsVisible));
            Assert.That(reduced.SecondsRemaining, Is.EqualTo(full.SecondsRemaining));
            Assert.That(reduced.ViewportPosition, Is.EqualTo(full.ViewportPosition));
            Assert.That(reduced.ArrowDirection, Is.EqualTo(full.ArrowDirection));
            Assert.That(reduced.Shape, Is.EqualTo(full.Shape));
            Assert.That(reduced.Glyph, Is.EqualTo(full.Glyph));
            Assert.That(reduced.Label, Is.EqualTo(full.Label));
            Assert.That(reduced.CountdownText, Is.EqualTo(full.CountdownText));
            Assert.That(full.Motion, Is.EqualTo(VisibleSliceWarningMotion.Pulsing));
            Assert.That(reduced.Motion, Is.EqualTo(VisibleSliceWarningMotion.Static));
        }

        [Test]
        public void GrayscaleWarning_UsesTextShapeAndHighLuminanceContrast()
        {
            VisibleSliceWarningPresentation warning =
                VisibleSliceWarningProjector.ProjectViewport(
                    new Vector2(-1f, 0.5f),
                    false,
                    new VisibleSliceWarningSignal(
                        "enemy.blaster-warning",
                        "TURRET FIRE",
                        true,
                        0.8f,
                        100),
                    CreateConfiguration(),
                    true);

            Assert.That(warning.HasColorIndependentCue, Is.True);
            Assert.That(warning.Shape, Is.EqualTo(VisibleSliceWarningShape.OutlinedTriangle));
            Assert.That(warning.Glyph, Is.EqualTo("!"));
            Assert.That(warning.Label, Is.EqualTo("TURRET FIRE"));
            Assert.That(warning.CountdownText, Is.EqualTo("1"));
            Assert.That(warning.LuminanceContrastRatio, Is.GreaterThanOrEqualTo(7f));

            TestContext.WriteLine(
                "GRAYSCALE EVIDENCE: shape={0}, glyph={1}, label={2}, contrast={3:0.00}:1",
                warning.Shape,
                warning.Glyph,
                warning.Label,
                warning.LuminanceContrastRatio);
        }

        [Test]
        public void Rig_DoesNotMutateBodyPlayerTransformIntentCombatOrGlobalQuality()
        {
            int qualityBefore = QualitySettings.GetQualityLevel();
            MutationSentinel sentinel = new MutationSentinel(17, 23);

            GameObject actorObject = Spawn("movement-actor-view");
            actorObject.transform.position = new Vector3(3f, -2f, 0f);
            Rigidbody2D body = actorObject.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.linearVelocity = new Vector2(4f, -3f);
            Vector2 bodyPositionBefore = body.position;
            Vector2 velocityBefore = body.linearVelocity;
            Vector3 transformBefore = actorObject.transform.position;

            GameObject cameraObject = Spawn("camera-rig");
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
            Camera camera = cameraObject.AddComponent<Camera>();
            VisibleSliceCameraRig rig = cameraObject.AddComponent<VisibleSliceCameraRig>();
            rig.Configure(
                camera,
                new TransformCameraFollowSource(actorObject.transform),
                new FixedThrusterStatusReader(CreateBurstSnapshot()),
                new FixedReducedEffectsSource(true),
                CreateConfiguration());

            Assert.That(rig.ApplyFrameForResolution(1920, 1080), Is.True);
            Assert.That(rig.Restart(), Is.True);
            rig.ProjectWarning(
                new Vector3(30f, 5f, 0f),
                new VisibleSliceWarningSignal(
                    "enemy.blaster-warning",
                    "TURRET FIRE",
                    true,
                    1f,
                    100));

            Assert.That(body.position, Is.EqualTo(bodyPositionBefore));
            Assert.That(body.linearVelocity, Is.EqualTo(velocityBefore));
            Assert.That(actorObject.transform.position, Is.EqualTo(transformBefore));
            Assert.That(sentinel.CombatState, Is.EqualTo(17));
            Assert.That(sentinel.MovementIntentState, Is.EqualTo(23));
            Assert.That(QualitySettings.GetQualityLevel(), Is.EqualTo(qualityBefore));
        }

        [Test]
        public void PublicSurface_ContainsReadOnlyInputsAndNoForbiddenMutationAuthority()
        {
            AssertReadOnlyInterface(typeof(IVisibleSliceCameraFollowSource));
            AssertReadOnlyInterface(typeof(IVisibleSliceThrusterStatusReader));
            AssertReadOnlyInterface(typeof(IVisibleSliceReducedEffectsSource));

            FieldInfo[] fields = typeof(VisibleSliceCameraRig).GetFields(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            foreach (FieldInfo field in fields)
            {
                AssertTypeIsNotForbiddenMutationAuthority(field.FieldType, field.Name);
            }

            MethodInfo[] methods = typeof(VisibleSliceCameraRig).GetMethods(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
            foreach (MethodInfo method in methods)
            {
                AssertTypeIsNotForbiddenMutationAuthority(method.ReturnType, method.Name);
                ParameterInfo[] parameters = method.GetParameters();
                foreach (ParameterInfo parameter in parameters)
                {
                    Type parameterType = parameter.ParameterType.IsByRef
                        ? parameter.ParameterType.GetElementType()
                        : parameter.ParameterType;
                    AssertTypeIsNotForbiddenMutationAuthority(
                        parameterType,
                        method.Name + "." + parameter.Name);
                }
            }
        }

        private GameObject Spawn(string name)
        {
            GameObject instance = new GameObject(name);
            spawnedObjects.Add(instance);
            return instance;
        }

        private static VisibleSliceCameraConfiguration CreateConfiguration()
        {
            return VisibleSliceCameraConfiguration.CreateDefault(
                Rect.MinMaxRect(-20f, -12f, 20f, 12f));
        }

        private static ThrusterStatusSnapshot CreateBurstSnapshot()
        {
            return new ThrusterStatusSnapshot(
                ThrusterStatusState.Burst,
                StableId.Parse("movement.camera-test"),
                4L,
                2,
                3,
                1,
                1.5d,
                ThrusterBurstPhase.Burst,
                8d,
                0d,
                1d,
                0d,
                0d,
                0d,
                0.1d,
                0d,
                0.2d,
                0.1d);
        }

        private static void AssertViewInside(Rect room, Rect view)
        {
            Assert.That(view.xMin, Is.GreaterThanOrEqualTo(room.xMin - 0.0001f));
            Assert.That(view.yMin, Is.GreaterThanOrEqualTo(room.yMin - 0.0001f));
            Assert.That(view.xMax, Is.LessThanOrEqualTo(room.xMax + 0.0001f));
            Assert.That(view.yMax, Is.LessThanOrEqualTo(room.yMax + 0.0001f));
        }

        private static void AssertReadOnlyInterface(Type interfaceType)
        {
            Assert.That(interfaceType.IsInterface, Is.True);
            foreach (PropertyInfo property in interfaceType.GetProperties())
            {
                Assert.That(property.CanWrite, Is.False, property.Name);
            }

            foreach (MethodInfo method in interfaceType.GetMethods())
            {
                Assert.That(method.Name.StartsWith("set_", StringComparison.Ordinal), Is.False);
            }
        }

        private static void AssertTypeIsNotForbiddenMutationAuthority(Type type, string member)
        {
            Assert.That(type, Is.Not.Null, member);
            string fullName = type.FullName ?? type.Name;
            Assert.That(type, Is.Not.EqualTo(typeof(Rigidbody2D)), member);
            Assert.That(fullName, Does.Not.Contain(".Combat"), member);
            Assert.That(fullName, Does.Not.Contain("MovementIntent"), member);
            Assert.That(fullName, Does.Not.Contain("QualitySettings"), member);
            Assert.That(fullName, Does.Not.Contain("PlayerPrefs"), member);
        }

        private sealed class FixedThrusterStatusReader : IVisibleSliceThrusterStatusReader
        {
            private readonly ThrusterStatusSnapshot snapshot;

            public FixedThrusterStatusReader(ThrusterStatusSnapshot snapshot)
            {
                this.snapshot = snapshot;
            }

            public ThrusterStatusSnapshot ReadSnapshot()
            {
                return snapshot;
            }
        }

        private sealed class FixedReducedEffectsSource : IVisibleSliceReducedEffectsSource
        {
            public FixedReducedEffectsSource(bool enabled)
            {
                ReducedEffectsEnabled = enabled;
            }

            public bool ReducedEffectsEnabled { get; }
        }

        private sealed class MutationSentinel
        {
            public MutationSentinel(int combatState, int movementIntentState)
            {
                CombatState = combatState;
                MovementIntentState = movementIntentState;
            }

            public int CombatState { get; }

            public int MovementIntentState { get; }
        }
    }
}
#endif
