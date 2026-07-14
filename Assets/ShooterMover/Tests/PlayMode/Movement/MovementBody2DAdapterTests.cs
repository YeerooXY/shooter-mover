using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Movement;
using ShooterMover.UnityAdapters.Physics;
using UnityEngine;
using UnityEngine.TestTools;

namespace ShooterMover.Tests.PlayMode.Movement
{
    public sealed class MovementBody2DAdapterTests
    {
        private const float Tolerance = 0.00001f;
        private readonly List<GameObject> createdObjects = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            for (int index = createdObjects.Count - 1; index >= 0; index--)
            {
                if (createdObjects[index] != null)
                {
                    UnityEngine.Object.DestroyImmediate(createdObjects[index]);
                }
            }

            createdObjects.Clear();
        }

        [Test]
        public void BodyAdapter_AppliesBaseAndBurstDomainVelocityDirectly()
        {
            Rigidbody2D body = CreateBody("MT-008 body adapter");
            MovementBody2DAdapter adapter = new MovementBody2DAdapter(body);

            BaseLocomotionState locomotion = BaseLocomotionState.Create(3.25d, -4.5d);
            adapter.Apply(locomotion);
            AssertVector(body.linearVelocity, 3.25f, -4.5f);

            MovementThrusterTuningProfile tuning = BuildTuning();
            ThrusterBurstState ready = ThrusterBurstState.CreateReady(
                BaseLocomotionState.Stationary,
                tuning);
            ThrusterBankState bank = ThrusterBankState.CreateFull(tuning);

            bool activated;
            ThrusterBankState nextBank;
            ThrusterBurstState burst = ThrusterBurstStepper.TryActivate(
                ready,
                bank,
                0.6d,
                0.8d,
                tuning,
                out nextBank,
                out activated);

            Assert.That(activated, Is.True);
            Assert.That(nextBank.AvailableCharges, Is.EqualTo(bank.AvailableCharges - 1));

            adapter.Apply(burst);

            AssertVector(
                body.linearVelocity,
                (float)burst.VelocityX,
                (float)burst.VelocityY);
            AssertVector(
                adapter.ObservedVelocity,
                (float)burst.VelocityX,
                (float)burst.VelocityY);
        }

        [Test]
        public void FixedStep_ReadsCurrentAuthorityAndOverwritesObservedDrift()
        {
            Rigidbody2D body = CreateBody("MT-008 fixed step");
            MovementFixedStepDriver driver = body.gameObject.AddComponent<MovementFixedStepDriver>();
            MutableVelocitySource source = new MutableVelocitySource(4d, -2d);

            driver.Configure(body, source);
            driver.StartDriving();

            body.linearVelocity = new Vector2(90f, 90f);
            Assert.That(driver.ExecuteFixedStep(), Is.True);
            AssertVector(body.linearVelocity, 4f, -2f);

            source.Set(-7.5d, 1.25d);
            body.linearVelocity = new Vector2(-30f, 12f);
            Assert.That(driver.ExecuteFixedStep(), Is.True);
            AssertVector(body.linearVelocity, -7.5f, 1.25f);
            Assert.That(source.ReadCount, Is.EqualTo(2));
        }

        [Test]
        public void MissingAuthoritativeSample_ClearsStaleBodyVelocity()
        {
            Rigidbody2D body = CreateBody("MT-008 missing sample");
            MovementFixedStepDriver driver = body.gameObject.AddComponent<MovementFixedStepDriver>();
            MutableVelocitySource source = new MutableVelocitySource(8d, 3d);

            driver.Configure(body, source);
            driver.StartDriving();
            Assert.That(driver.ExecuteFixedStep(), Is.True);
            AssertVector(body.linearVelocity, 8f, 3f);

            source.IsAvailable = false;
            Assert.That(driver.ExecuteFixedStep(), Is.False);
            AssertVector(body.linearVelocity, 0f, 0f);
        }

        [Test]
        public void ConfigureStartStopAndRestart_AreIdempotent()
        {
            Rigidbody2D body = CreateBody("MT-008 lifecycle");
            MovementFixedStepDriver driver = body.gameObject.AddComponent<MovementFixedStepDriver>();
            MutableVelocitySource source = new MutableVelocitySource(2d, 5d);

            driver.Configure(body, source);
            driver.Configure(body, source);
            Assert.That(driver.IsConfigured, Is.True);

            driver.StartDriving();
            driver.StartDriving();
            Assert.That(driver.IsDriving, Is.True);
            Assert.That(driver.ExecuteFixedStep(), Is.True);
            AssertVector(body.linearVelocity, 2f, 5f);

            driver.StopDriving();
            driver.StopDriving();
            Assert.That(driver.IsDriving, Is.False);
            AssertVector(body.linearVelocity, 0f, 0f);

            source.Set(-3d, -6d);
            driver.StartDriving();
            Assert.That(driver.ExecuteFixedStep(), Is.True);
            AssertVector(body.linearVelocity, -3f, -6f);

            MutableVelocitySource differentSource = new MutableVelocitySource(0d, 0d);
            Assert.Throws<InvalidOperationException>(
                () => driver.Configure(body, differentSource));
        }

        [Test]
        public void StartDriving_DoesNotApplyBeforeAnExplicitFixedStep()
        {
            Rigidbody2D body = CreateBody("MT-008 fixed boundary");
            MovementFixedStepDriver driver = body.gameObject.AddComponent<MovementFixedStepDriver>();
            MutableVelocitySource source = new MutableVelocitySource(11d, -9d);

            driver.Configure(body, source);
            driver.StartDriving();

            AssertVector(body.linearVelocity, 0f, 0f);
            Assert.That(source.ReadCount, Is.Zero);

            Assert.That(driver.ExecuteFixedStep(), Is.True);
            AssertVector(body.linearVelocity, 11f, -9f);
            Assert.That(source.ReadCount, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator DisableMidBurst_ClearsImmediatelyAndResumesFromFreshAuthority()
        {
            Rigidbody2D body = CreateBody("MT-008 disable lifecycle");
            MovementFixedStepDriver driver = body.gameObject.AddComponent<MovementFixedStepDriver>();
            MutableVelocitySource source = new MutableVelocitySource(20d, -5d);

            driver.Configure(body, source);
            driver.StartDriving();
            Assert.That(driver.ExecuteFixedStep(), Is.True);
            AssertVector(body.linearVelocity, 20f, -5f);

            driver.enabled = false;
            Assert.That(driver.IsDriving, Is.False);
            AssertVector(body.linearVelocity, 0f, 0f);

            source.Set(-4d, 7d);
            yield return new WaitForFixedUpdate();
            AssertVector(body.linearVelocity, 0f, 0f);

            driver.enabled = true;
            Assert.That(driver.IsDriving, Is.True);
            AssertVector(body.linearVelocity, 0f, 0f);

            yield return new WaitForFixedUpdate();
            AssertVector(body.linearVelocity, -4f, 7f);
        }

        [Test]
        public void OutOfFloatBounds_IsRejectedBeforeBodyMutation()
        {
            Rigidbody2D body = CreateBody("MT-008 bounds");
            MovementBody2DAdapter adapter = new MovementBody2DAdapter(body);
            body.linearVelocity = new Vector2(1f, -2f);

            Assert.Throws<ArgumentOutOfRangeException>(
                () => adapter.ApplyAuthoritativeVelocity(double.MaxValue, 0d));
            AssertVector(body.linearVelocity, 1f, -2f);

            Assert.Throws<ArgumentOutOfRangeException>(
                () => adapter.ApplyAuthoritativeVelocity(0d, double.NaN));
            AssertVector(body.linearVelocity, 1f, -2f);
        }

        [Test]
        public void AdapterSurface_ContainsRigidbody2DAndNo3DPhysicsTypes()
        {
            FieldInfo bodyField = typeof(MovementBody2DAdapter).GetField(
                "body",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(bodyField, Is.Not.Null);
            Assert.That(bodyField.FieldType, Is.EqualTo(typeof(Rigidbody2D)));

            Type[] inspectedTypes =
            {
                typeof(MovementBody2DAdapter),
                typeof(MovementFixedStepDriver),
                typeof(IAuthoritativeMovementVelocitySource),
            };

            for (int typeIndex = 0; typeIndex < inspectedTypes.Length; typeIndex++)
            {
                AssertDeclaredMembersContainNo3DPhysics(inspectedTypes[typeIndex]);
            }
        }

        private Rigidbody2D CreateBody(string name)
        {
            GameObject gameObject = new GameObject(name);
            createdObjects.Add(gameObject);

            Rigidbody2D body = gameObject.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            return body;
        }

        private static void AssertDeclaredMembersContainNo3DPhysics(Type type)
        {
            BindingFlags flags = BindingFlags.Instance
                | BindingFlags.Static
                | BindingFlags.Public
                | BindingFlags.NonPublic
                | BindingFlags.DeclaredOnly;

            FieldInfo[] fields = type.GetFields(flags);
            for (int index = 0; index < fields.Length; index++)
            {
                AssertNot3DPhysicsType(fields[index].FieldType, type, fields[index].Name);
            }

            PropertyInfo[] properties = type.GetProperties(flags);
            for (int index = 0; index < properties.Length; index++)
            {
                AssertNot3DPhysicsType(properties[index].PropertyType, type, properties[index].Name);
            }

            MethodInfo[] methods = type.GetMethods(flags);
            for (int methodIndex = 0; methodIndex < methods.Length; methodIndex++)
            {
                MethodInfo method = methods[methodIndex];
                AssertNot3DPhysicsType(method.ReturnType, type, method.Name + " return");

                ParameterInfo[] parameters = method.GetParameters();
                for (int parameterIndex = 0; parameterIndex < parameters.Length; parameterIndex++)
                {
                    AssertNot3DPhysicsType(
                        parameters[parameterIndex].ParameterType,
                        type,
                        method.Name + "." + parameters[parameterIndex].Name);
                }
            }
        }

        private static void AssertNot3DPhysicsType(
            Type candidate,
            Type declaringType,
            string memberName)
        {
            if (candidate.IsByRef)
            {
                candidate = candidate.GetElementType();
            }

            Type[] forbiddenTypes =
            {
                typeof(Rigidbody),
                typeof(Collider),
                typeof(Collision),
                typeof(Joint),
                typeof(UnityEngine.Physics),
                typeof(PhysicsScene),
                typeof(RaycastHit),
                typeof(ContactPoint),
                typeof(Vector3),
            };

            for (int index = 0; index < forbiddenTypes.Length; index++)
            {
                Assert.That(
                    candidate,
                    Is.Not.EqualTo(forbiddenTypes[index]),
                    declaringType.FullName + "." + memberName + " references a 3D physics type.");
            }
        }

        private static void AssertVector(Vector2 actual, float expectedX, float expectedY)
        {
            Assert.That(actual.x, Is.EqualTo(expectedX).Within(Tolerance));
            Assert.That(actual.y, Is.EqualTo(expectedY).Within(Tolerance));
        }

        private static MovementThrusterTuningProfile BuildTuning()
        {
            return MovementThrusterTuningProfile.Create(
                MovementThrusterTuningProfile.CurrentProfileVersion,
                StableId.Parse("tuning.mt-008-tests"),
                12d,
                50d,
                60d,
                90d,
                1.25d,
                2,
                1,
                1.75d,
                2.5d,
                0.3d,
                0.1d,
                0.05d,
                120d,
                0.04d,
                0.2d,
                0.75d,
                2d,
                0.8d,
                0.15d,
                5d,
                4,
                0.8d,
                0.9d,
                0.1d,
                0.5d,
                0.02d,
                128);
        }

        private sealed class MutableVelocitySource : IAuthoritativeMovementVelocitySource
        {
            private double velocityX;
            private double velocityY;

            public MutableVelocitySource(double velocityX, double velocityY)
            {
                Set(velocityX, velocityY);
                IsAvailable = true;
            }

            public bool IsAvailable { get; set; }

            public int ReadCount { get; private set; }

            public bool TryReadVelocity(out double velocityX, out double velocityY)
            {
                ReadCount++;
                velocityX = this.velocityX;
                velocityY = this.velocityY;
                return IsAvailable;
            }

            public void Set(double velocityX, double velocityY)
            {
                this.velocityX = velocityX;
                this.velocityY = velocityY;
            }
        }
    }
}
