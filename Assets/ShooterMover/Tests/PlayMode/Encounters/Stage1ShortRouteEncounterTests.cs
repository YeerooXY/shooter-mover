#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace ShooterMover.Tests.PlayMode.Encounters
{
    public sealed class Stage1ShortRouteEncounterTests
    {
        private const string SessionTypeName =
            "ShooterMover.ContentPackages.Encounters.Stage1ShortRoute.Stage1ShortRouteSession";
        private const string PackageRoot =
            "Assets/ShooterMover/ContentPackages/Encounters/Stage1ShortRoute/";
        private const string CompositionSourcePath =
            PackageRoot + "Stage1ShortRouteComposition.cs";
        private const string SessionSourcePath =
            PackageRoot + "Stage1ShortRouteSession.cs";
        private const string RouteMapPath =
            PackageRoot + "STAGE1_SHORT_ROUTE_COMPOSITION.md";
        private const string RouteFixtureSourcePath =
            "Assets/ShooterMover/TestSupport/EvidenceHarness/Stage1ShortRouteFixture.cs";
        private const string RouteScenePath =
            "Assets/ShooterMover/Tests/PlayMode/EvidenceHarness/Scenes/Stage1ShortRouteShell.unity";
        private const string LoadoutSourcePath =
            "Assets/ShooterMover/ContentPackages/Weapons/Stage1Loadouts/Stage1WeaponLoadoutFixtures.cs";

        private static readonly string[] ExpectedOrder =
        {
            "route.start",
            "route.arena-entry",
            "route.connector",
            "route.review-end",
            "route.restart",
        };

        [Test]
        public void ProjectionAndRoomOrder_AreDeterministicAndMatchTheReadOnlyShell()
        {
            object first = CreateSession("run.en011-projection-a");
            object second = CreateSession("run.en011-projection-a");

            Assert.That(GetStringArray(first, "GetRoomOrder"), Is.EqualTo(ExpectedOrder));
            Assert.That(GetStringArray(second, "GetRoomOrder"), Is.EqualTo(ExpectedOrder));
            Assert.That(
                GetProperty<string>(first, "CompositionFingerprint"),
                Is.EqualTo(GetProperty<string>(second, "CompositionFingerprint")));

            foreach (string markerId in ExpectedOrder)
            {
                string firstProjection = InvokeString(first, "GetProjectionCanonical", markerId);
                string secondProjection = InvokeString(second, "GetProjectionCanonical", markerId);
                Assert.That(firstProjection, Is.EqualTo(secondProjection), markerId);
                Assert.That(firstProjection, Does.Contain("marker_id=" + markerId));
                Assert.That(firstProjection, Does.Contain("generation=1"));
                Assert.That(firstProjection, Does.Contain("mission_sequence=0"));
            }

            string fixtureSource = ReadProjectFile(RouteFixtureSourcePath);
            foreach (string markerId in ExpectedOrder)
            {
                Assert.That(fixtureSource, Does.Contain("\"" + markerId + "\""));
            }
        }

        [Test]
        public void OrdinaryPressure_CoversAllValidatedRolesAndOneEliteEndpoint()
        {
            object session = CreateSession("run.en011-roster");
            var ordinaryRoles = new HashSet<string>(StringComparer.Ordinal);
            foreach (string markerId in ExpectedOrder.Take(3))
            {
                foreach (string roleId in GetStringArray(
                    session,
                    "GetRoomParticipantRoleIds",
                    markerId))
                {
                    ordinaryRoles.Add(roleId);
                }
            }

            Assert.That(
                ordinaryRoles.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                Is.EqualTo(
                    new[]
                    {
                        "enemy.blaster-turret",
                        "enemy.mobile-blaster-droid",
                        "enemy.pursuer-drone",
                        "enemy.ram-droid",
                    }));
            Assert.That(
                GetStringArray(session, "GetRoomParticipantRoleIds", "route.review-end"),
                Is.EqualTo(new[] { "enemy.four-blaster-elite" }));
            Assert.That(
                GetStringArray(session, "GetRoomParticipantRoleIds", "route.restart"),
                Is.Empty);
        }

        [Test]
        public void Retreat_IsAllowedForOrdinaryPressureAndRejectedByEliteLockdown()
        {
            object session = CreateSession("run.en011-retreat");
            AssertApplied(InvokeInstance(session, "EnterNextRoom"));
            AssertApplied(InvokeInstance(session, "CompleteCurrentEncounter"));
            AssertApplied(InvokeInstance(session, "EnterNextRoom"));
            AssertApplied(InvokeInstance(session, "RegisterProjectile", "projectile.en011-retreat-live"));

            string[] firstArenaActors = GetStringArray(session, "GetActiveEnemyIds");
            object retreat = InvokeInstance(session, "RetreatCurrentRoom");
            AssertDisposition(retreat, "Applied");
            Assert.That(GetProperty<string>(session, "CurrentMarkerId"), Is.EqualTo("route.start"));
            Assert.That(GetProperty<int>(session, "CompletionEventCount"), Is.EqualTo(1));
            AssertRuntimeTokensCleared(session);

            AssertApplied(InvokeInstance(session, "EnterNextRoom"));
            string[] secondArenaActors = GetStringArray(session, "GetActiveEnemyIds");
            Assert.That(secondArenaActors, Is.Not.EqualTo(firstArenaActors));
            Assert.That(secondArenaActors.Intersect(firstArenaActors), Is.Empty);
            AssertApplied(InvokeInstance(session, "CompleteCurrentEncounter"));

            AssertApplied(InvokeInstance(session, "EnterNextRoom"));
            AssertApplied(InvokeInstance(session, "CompleteCurrentEncounter"));
            AssertApplied(InvokeInstance(session, "EnterNextRoom"));
            Assert.That(GetProperty<string>(session, "CurrentMarkerId"), Is.EqualTo("route.review-end"));
            Assert.That(GetProperty<string>(session, "CurrentLockdownState"), Is.EqualTo("Engaged"));

            object eliteRetreat = InvokeInstance(session, "RetreatCurrentRoom");
            AssertDisposition(eliteRetreat, "Rejected");
            Assert.That(GetProperty<string>(eliteRetreat, "Reason"), Is.EqualTo("lockdown-active"));
            Assert.That(GetProperty<string>(session, "CurrentMarkerId"), Is.EqualTo("route.review-end"));
            Assert.That(GetProperty<int>(session, "ActiveEnemyCount"), Is.EqualTo(1));
        }

        [Test]
        public void EliteCompletion_ReleasesLockdownAndRecordsCompletionOnce()
        {
            object session = CreateSession("run.en011-elite");
            for (int index = 0; index < 3; index++)
            {
                AssertApplied(InvokeInstance(session, "EnterNextRoom"));
                AssertApplied(InvokeInstance(session, "CompleteCurrentEncounter"));
            }

            AssertApplied(InvokeInstance(session, "EnterNextRoom"));
            Assert.That(GetProperty<string>(session, "CurrentRoomKind"), Is.EqualTo("EliteEndpoint"));
            Assert.That(GetProperty<string>(session, "CurrentLockdownState"), Is.EqualTo("Engaged"));

            object firstCompletion = InvokeInstance(session, "CompleteCurrentEncounter");
            AssertDisposition(firstCompletion, "Applied");
            Assert.That(GetProperty<string>(session, "CurrentLockdownState"), Is.EqualTo("Released"));
            Assert.That(GetProperty<string>(session, "CurrentEncounterPhase"), Is.EqualTo("Completed"));
            Assert.That(GetProperty<int>(session, "CompletionEventCount"), Is.EqualTo(4));
            AssertRuntimeTokensCleared(session);

            object repeatedCompletion = InvokeInstance(session, "CompleteCurrentEncounter");
            AssertDisposition(repeatedCompletion, "NoChange");
            Assert.That(
                GetProperty<string>(repeatedCompletion, "Reason"),
                Is.EqualTo("completion-already-recorded"));
            Assert.That(GetProperty<int>(session, "CompletionEventCount"), Is.EqualTo(4));

            AssertApplied(InvokeInstance(session, "EnterNextRoom"));
            Assert.That(GetProperty<string>(session, "CurrentMarkerId"), Is.EqualTo("route.restart"));
            Assert.That(GetProperty<string>(session, "CurrentRoomKind"), Is.EqualTo("ProjectionOnly"));
            Assert.That(
                GetProperty<string>(InvokeInstance(session, "CompleteCurrentEncounter"), "Disposition").ToString(),
                Is.EqualTo("Rejected"));
        }

        [Test]
        public void Hazards_AreBoundedGeometryAndTextWarnings()
        {
            object session = CreateSession("run.en011-hazards");
            Assert.That(InvokeString(session, "GetRoomHazardCanonical", "route.start"), Is.Empty);
            Assert.That(InvokeString(session, "GetRoomHazardCanonical", "route.review-end"), Is.Empty);

            string sweep = InvokeString(
                session,
                "GetRoomHazardCanonical",
                "route.arena-entry");
            string gate = InvokeString(
                session,
                "GetRoomHazardCanonical",
                "route.connector");
            AssertHazard(
                sweep,
                "warning_glyph=ChevronSweep",
                "warning_text=CHEVRON SWEEP",
                "telegraph_ticks=45",
                "damage_per_hit=4");
            AssertHazard(
                gate,
                "warning_glyph=DoubleBarGate",
                "warning_text=DOUBLE BAR GATE",
                "telegraph_ticks=60",
                "damage_per_hit=6");

            AssertApplied(InvokeInstance(session, "EnterNextRoom"));
            Assert.That(GetProperty<int>(session, "ActiveHazardCount"), Is.Zero);
            AssertApplied(InvokeInstance(session, "CompleteCurrentEncounter"));
            AssertApplied(InvokeInstance(session, "EnterNextRoom"));
            Assert.That(GetProperty<int>(session, "ActiveHazardCount"), Is.EqualTo(1));
            Assert.That(
                GetStringArray(session, "GetActiveHazardIds"),
                Is.EqualTo(new[] { "hazard.stage1-short-route-chevron-sweep" }));
            AssertApplied(InvokeInstance(session, "CompleteCurrentEncounter"));
            Assert.That(GetProperty<int>(session, "ActiveHazardCount"), Is.Zero);
        }

        [Test]
        public void RapidRestart_RestoresFrozenRouteWithoutStaleRuntimeTokens()
        {
            object session = CreateSession("run.en011-restart");
            string fingerprint = GetProperty<string>(session, "CompositionFingerprint");
            string[] order = GetStringArray(session, "GetRoomOrder");
            string projectionBefore = InvokeString(session, "GetProjectionCanonical", "route.start");

            AssertApplied(InvokeInstance(session, "EnterNextRoom"));
            AssertApplied(InvokeInstance(session, "CompleteCurrentEncounter"));
            AssertApplied(InvokeInstance(session, "EnterNextRoom"));
            string[] staleActors = GetStringArray(session, "GetActiveEnemyIds");
            AssertApplied(InvokeInstance(session, "RegisterProjectile", "projectile.en011-stale-a"));
            AssertApplied(InvokeInstance(session, "RegisterProjectile", "projectile.en011-stale-b"));
            Assert.That(GetProperty<int>(session, "ActiveEnemyCount"), Is.EqualTo(2));
            Assert.That(GetProperty<int>(session, "ActiveHazardCount"), Is.EqualTo(1));
            Assert.That(GetProperty<int>(session, "ActiveProjectileCount"), Is.EqualTo(2));

            object restart = InvokeInstance(session, "Restart");
            AssertDisposition(restart, "Applied");
            Assert.That(GetProperty<int>(session, "Generation"), Is.EqualTo(2));
            Assert.That(GetProperty<string>(session, "CurrentMarkerId"), Is.Empty);
            Assert.That(GetProperty<int>(session, "CompletionEventCount"), Is.Zero);
            AssertRuntimeTokensCleared(session);
            Assert.That(GetProperty<string>(session, "CompositionFingerprint"), Is.EqualTo(fingerprint));
            Assert.That(GetStringArray(session, "GetRoomOrder"), Is.EqualTo(order));
            Assert.That(
                InvokeString(session, "GetProjectionCanonical", "route.start"),
                Is.Not.EqualTo(projectionBefore));

            AssertApplied(InvokeInstance(session, "EnterNextRoom"));
            string[] newActors = GetStringArray(session, "GetActiveEnemyIds");
            Assert.That(newActors.Intersect(staleActors), Is.Empty);
            Assert.That(newActors.All(actorId => actorId.Contains("-g2-")), Is.True);
            Assert.That(GetProperty<string>(session, "CurrentMarkerId"), Is.EqualTo("route.start"));
        }

        [Test]
        public void PackageBoundary_LeavesRouteSceneContractsRegistriesAndPersistenceReadOnly()
        {
            string compositionSource = ReadProjectFile(CompositionSourcePath);
            string sessionSource = ReadProjectFile(SessionSourcePath);
            string packageSource = compositionSource + "\n" + sessionSource;
            string[] forbiddenTokens =
            {
                "UnityEngine",
                "SceneManager",
                "LoadScene",
                "Stage1ShortRouteShell.unity",
                "MissionRunState",
                "PlayerPrefs",
                "Resources.Load",
                "Instantiate(",
                "Destroy(",
                "RegistryDocument",
                "File.Write",
                "Directory.CreateDirectory",
            };

            foreach (string token in forbiddenTokens)
            {
                Assert.That(packageSource, Does.Not.Contain(token), "Forbidden token: " + token);
            }

            Assert.That(packageSource, Does.Not.Contain("UnityEngine.Color"));
            Assert.That(packageSource, Does.Contain("RoomProjectionIdentity"));
            Assert.That(packageSource, Does.Contain("RoomProjectionKey"));
            Assert.That(packageSource, Does.Contain("EncounterLifecycle.Create"));
            Assert.That(packageSource, Does.Contain("BeginRetreat"));
            Assert.That(packageSource, Does.Contain("ApplyLockdown"));
            Assert.That(packageSource, Does.Contain("EncounterCompletionMessage"));
            Assert.That(ProjectFileExists(RouteScenePath), Is.True);
            Assert.That(ProjectFileExists(RouteMapPath), Is.True);
            Assert.That(ProjectFileExists(LoadoutSourcePath), Is.True);

            string loadoutSource = ReadProjectFile(LoadoutSourcePath);
            Assert.That(loadoutSource, Does.Contain("loadout.stage1-default-comparison"));
            Assert.That(loadoutSource, Does.Contain("loadout.stage1-ricochet-comparison"));

            TestContext.WriteLine(
                "EN-011 route map: start[pursuer,pursuer] -> "
                + "arena-entry[ram,mobile-blaster;chevron-sweep] -> "
                + "connector[pursuer,turret;double-bar-gate] -> "
                + "review-end[four-blaster-elite;lockdown] -> restart[projection-only].");
            TestContext.WriteLine(
                "Route-shell read-only audit: package source has no Unity/scene/persistence/registry authority; "
                + RouteScenePath + " is consumed read-only.");
        }

        private static object CreateSession(string runId)
        {
            Type type = FindRuntimeType(SessionTypeName);
            return InvokeStatic(type, "CreateApproved", runId);
        }

        private static void AssertHazard(string canonical, params string[] expectedTokens)
        {
            Assert.That(canonical, Is.Not.Empty);
            Assert.That(canonical, Does.Contain("maximum_hits_per_activation=1"));
            Assert.That(canonical, Does.Contain("active_ticks="));
            Assert.That(canonical, Does.Contain("cooldown_ticks="));
            Assert.That(canonical, Does.Contain("footprint_id="));
            foreach (string token in expectedTokens)
            {
                Assert.That(canonical, Does.Contain(token));
            }
        }

        private static void AssertRuntimeTokensCleared(object session)
        {
            Assert.That(GetProperty<int>(session, "ActiveEnemyCount"), Is.Zero);
            Assert.That(GetProperty<int>(session, "ActiveHazardCount"), Is.Zero);
            Assert.That(GetProperty<int>(session, "ActiveProjectileCount"), Is.Zero);
            Assert.That(GetStringArray(session, "GetActiveEnemyIds"), Is.Empty);
            Assert.That(GetStringArray(session, "GetActiveHazardIds"), Is.Empty);
            Assert.That(GetStringArray(session, "GetActiveProjectileIds"), Is.Empty);
        }

        private static void AssertApplied(object transition)
        {
            AssertDisposition(transition, "Applied");
        }

        private static void AssertDisposition(object transition, string expected)
        {
            Assert.That(
                GetProperty<object>(transition, "Disposition").ToString(),
                Is.EqualTo(expected),
                GetProperty<string>(transition, "Reason") ?? "none");
        }

        private static string[] GetStringArray(
            object instance,
            string methodName,
            params object[] arguments)
        {
            return (string[])InvokeInstance(instance, methodName, arguments);
        }

        private static string InvokeString(
            object instance,
            string methodName,
            params object[] arguments)
        {
            return (string)InvokeInstance(instance, methodName, arguments);
        }

        private static T GetProperty<T>(object instance, string propertyName)
        {
            PropertyInfo property = instance.GetType().GetProperty(
                propertyName,
                BindingFlags.Public | BindingFlags.Instance);
            Assert.That(property, Is.Not.Null, instance.GetType().FullName + "." + propertyName);
            return (T)property.GetValue(instance, null);
        }

        private static object InvokeStatic(Type type, string methodName, params object[] arguments)
        {
            MethodInfo method = RequireMethod(
                type,
                methodName,
                BindingFlags.Public | BindingFlags.Static,
                arguments.Length);
            return Invoke(method, null, arguments);
        }

        private static object InvokeInstance(
            object instance,
            string methodName,
            params object[] arguments)
        {
            MethodInfo method = RequireMethod(
                instance.GetType(),
                methodName,
                BindingFlags.Public | BindingFlags.Instance,
                arguments.Length);
            return Invoke(method, instance, arguments);
        }

        private static MethodInfo RequireMethod(
            Type type,
            string methodName,
            BindingFlags flags,
            int argumentCount)
        {
            MethodInfo[] matches = type.GetMethods(flags)
                .Where(method => string.Equals(method.Name, methodName, StringComparison.Ordinal))
                .Where(method => method.GetParameters().Length == argumentCount)
                .ToArray();
            Assert.That(
                matches,
                Has.Length.EqualTo(1),
                type.FullName + "." + methodName + " with " + argumentCount + " arguments");
            return matches[0];
        }

        private static object Invoke(
            MethodInfo method,
            object instance,
            object[] arguments)
        {
            try
            {
                return method.Invoke(instance, arguments);
            }
            catch (TargetInvocationException exception)
            {
                if (exception.InnerException != null)
                {
                    throw exception.InnerException;
                }

                throw;
            }
        }

        private static Type FindRuntimeType(string fullName)
        {
            Type found = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(fullName, false))
                .FirstOrDefault(type => type != null);
            Assert.That(found, Is.Not.Null, "Runtime type was not compiled: " + fullName);
            return found;
        }

        private static bool ProjectFileExists(string assetPath)
        {
            return File.Exists(ToProjectPath(assetPath));
        }

        private static string ReadProjectFile(string assetPath)
        {
            string path = ToProjectPath(assetPath);
            Assert.That(File.Exists(path), Is.True, assetPath);
            return File.ReadAllText(path);
        }

        private static string ToProjectPath(string assetPath)
        {
            return Path.GetFullPath(
                Path.Combine(
                    Directory.GetCurrentDirectory(),
                    assetPath.Replace('/', Path.DirectorySeparatorChar)));
        }
    }
}
#endif
