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
        private const string CompositionSourcePath = PackageRoot + "Stage1ShortRouteComposition.cs";
        private const string SessionSourcePath = PackageRoot + "Stage1ShortRouteSession.cs";
        private const string RouteMapPath = PackageRoot + "STAGE1_SHORT_ROUTE_COMPOSITION.md";
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

            Assert.That(StringArray(first, "GetRoomOrder"), Is.EqualTo(ExpectedOrder));
            Assert.That(StringArray(second, "GetRoomOrder"), Is.EqualTo(ExpectedOrder));
            Assert.That(Property<string>(first, "CompositionFingerprint"),
                Is.EqualTo(Property<string>(second, "CompositionFingerprint")));

            foreach (string markerId in ExpectedOrder)
            {
                string left = StringResult(first, "GetProjectionCanonical", markerId);
                string right = StringResult(second, "GetProjectionCanonical", markerId);
                Assert.That(left, Is.EqualTo(right), markerId);
                Assert.That(left, Does.Contain("marker_id=" + markerId));
                Assert.That(left, Does.Contain("generation=1"));
                Assert.That(left, Does.Contain("mission_sequence=0"));
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
            var roles = new HashSet<string>(StringComparer.Ordinal);
            foreach (string markerId in ExpectedOrder.Take(3))
            {
                foreach (string roleId in StringArray(session, "GetRoomParticipantRoleIds", markerId))
                {
                    roles.Add(roleId);
                }
            }

            Assert.That(roles.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                Is.EqualTo(new[]
                {
                    "enemy.blaster-turret",
                    "enemy.mobile-blaster-droid",
                    "enemy.pursuer-drone",
                    "enemy.ram-droid",
                }));
            Assert.That(StringArray(session, "GetRoomParticipantRoleIds", "route.review-end"),
                Is.EqualTo(new[] { "enemy.four-blaster-elite" }));
            Assert.That(StringArray(session, "GetRoomParticipantRoleIds", "route.restart"), Is.Empty);
        }

        [Test]
        public void Retreat_IsAllowedForOrdinaryPressureAndRejectedByEliteLockdown()
        {
            object session = CreateSession("run.en011-retreat");
            Applied(Call(session, "EnterNextRoom"));
            Applied(Call(session, "CompleteCurrentEncounter"));
            Applied(Call(session, "EnterNextRoom"));
            Applied(Call(session, "RegisterProjectile", "projectile.en011-retreat-live"));
            string[] firstActors = StringArray(session, "GetActiveEnemyIds");

            Disposition(Call(session, "RetreatCurrentRoom"), "Applied");
            Assert.That(Property<string>(session, "CurrentMarkerId"), Is.EqualTo("route.start"));
            Assert.That(Property<int>(session, "CompletionEventCount"), Is.EqualTo(1));
            TokensCleared(session);

            Applied(Call(session, "EnterNextRoom"));
            string[] secondActors = StringArray(session, "GetActiveEnemyIds");
            Assert.That(secondActors.Intersect(firstActors), Is.Empty);
            Applied(Call(session, "CompleteCurrentEncounter"));
            Applied(Call(session, "EnterNextRoom"));
            Applied(Call(session, "CompleteCurrentEncounter"));
            Applied(Call(session, "EnterNextRoom"));

            Assert.That(Property<string>(session, "CurrentMarkerId"), Is.EqualTo("route.review-end"));
            Assert.That(Property<string>(session, "CurrentLockdownState"), Is.EqualTo("Engaged"));
            object rejected = Call(session, "RetreatCurrentRoom");
            Disposition(rejected, "Rejected");
            Assert.That(Property<string>(rejected, "Reason"), Is.EqualTo("lockdown-active"));
            Assert.That(Property<int>(session, "ActiveEnemyCount"), Is.EqualTo(1));
        }

        [Test]
        public void EliteCompletion_ReleasesLockdownAndRecordsCompletionOnce()
        {
            object session = CreateSession("run.en011-elite");
            for (int index = 0; index < 3; index++)
            {
                Applied(Call(session, "EnterNextRoom"));
                Applied(Call(session, "CompleteCurrentEncounter"));
            }

            Applied(Call(session, "EnterNextRoom"));
            Assert.That(Property<string>(session, "CurrentRoomKind"), Is.EqualTo("EliteEndpoint"));
            Assert.That(Property<string>(session, "CurrentLockdownState"), Is.EqualTo("Engaged"));
            Disposition(Call(session, "CompleteCurrentEncounter"), "Applied");
            Assert.That(Property<string>(session, "CurrentLockdownState"), Is.EqualTo("Released"));
            Assert.That(Property<string>(session, "CurrentEncounterPhase"), Is.EqualTo("Completed"));
            Assert.That(Property<int>(session, "CompletionEventCount"), Is.EqualTo(4));
            TokensCleared(session);

            object repeated = Call(session, "CompleteCurrentEncounter");
            Disposition(repeated, "NoChange");
            Assert.That(Property<string>(repeated, "Reason"),
                Is.EqualTo("completion-already-recorded"));
            Assert.That(Property<int>(session, "CompletionEventCount"), Is.EqualTo(4));

            Applied(Call(session, "EnterNextRoom"));
            Assert.That(Property<string>(session, "CurrentMarkerId"), Is.EqualTo("route.restart"));
            Assert.That(Property<string>(session, "CurrentRoomKind"), Is.EqualTo("ProjectionOnly"));
            Disposition(Call(session, "CompleteCurrentEncounter"), "Rejected");
        }

        [Test]
        public void Hazards_AreBoundedGeometryAndTextWarnings()
        {
            object session = CreateSession("run.en011-hazards");
            Assert.That(StringResult(session, "GetRoomHazardCanonical", "route.start"), Is.Empty);
            Assert.That(StringResult(session, "GetRoomHazardCanonical", "route.review-end"), Is.Empty);

            AssertHazard(StringResult(session, "GetRoomHazardCanonical", "route.arena-entry"),
                "warning_glyph=ChevronSweep", "warning_text=CHEVRON SWEEP",
                "telegraph_ticks=45", "damage_per_hit=4");
            AssertHazard(StringResult(session, "GetRoomHazardCanonical", "route.connector"),
                "warning_glyph=DoubleBarGate", "warning_text=DOUBLE BAR GATE",
                "telegraph_ticks=60", "damage_per_hit=6");

            Applied(Call(session, "EnterNextRoom"));
            Assert.That(Property<int>(session, "ActiveHazardCount"), Is.Zero);
            Applied(Call(session, "CompleteCurrentEncounter"));
            Applied(Call(session, "EnterNextRoom"));
            Assert.That(Property<int>(session, "ActiveHazardCount"), Is.EqualTo(1));
            Assert.That(StringArray(session, "GetActiveHazardIds"),
                Is.EqualTo(new[] { "hazard.stage1-short-route-chevron-sweep" }));
            Applied(Call(session, "CompleteCurrentEncounter"));
            Assert.That(Property<int>(session, "ActiveHazardCount"), Is.Zero);
        }

        [Test]
        public void RapidRestart_RestoresFrozenRouteWithoutStaleRuntimeTokens()
        {
            object session = CreateSession("run.en011-restart");
            string fingerprint = Property<string>(session, "CompositionFingerprint");
            string[] order = StringArray(session, "GetRoomOrder");
            string projectionBefore = StringResult(session, "GetProjectionCanonical", "route.start");

            Applied(Call(session, "EnterNextRoom"));
            Applied(Call(session, "CompleteCurrentEncounter"));
            Applied(Call(session, "EnterNextRoom"));
            string[] staleActors = StringArray(session, "GetActiveEnemyIds");
            Applied(Call(session, "RegisterProjectile", "projectile.en011-stale-a"));
            Applied(Call(session, "RegisterProjectile", "projectile.en011-stale-b"));
            Assert.That(Property<int>(session, "ActiveEnemyCount"), Is.EqualTo(2));
            Assert.That(Property<int>(session, "ActiveHazardCount"), Is.EqualTo(1));
            Assert.That(Property<int>(session, "ActiveProjectileCount"), Is.EqualTo(2));

            Disposition(Call(session, "Restart"), "Applied");
            Assert.That(Property<int>(session, "Generation"), Is.EqualTo(2));
            Assert.That(Property<string>(session, "CurrentMarkerId"), Is.Empty);
            Assert.That(Property<int>(session, "CompletionEventCount"), Is.Zero);
            TokensCleared(session);
            Assert.That(Property<string>(session, "CompositionFingerprint"), Is.EqualTo(fingerprint));
            Assert.That(StringArray(session, "GetRoomOrder"), Is.EqualTo(order));
            Assert.That(StringResult(session, "GetProjectionCanonical", "route.start"),
                Is.Not.EqualTo(projectionBefore));

            Applied(Call(session, "EnterNextRoom"));
            string[] newActors = StringArray(session, "GetActiveEnemyIds");
            Assert.That(newActors.Intersect(staleActors), Is.Empty);
            Assert.That(newActors.All(actorId => actorId.Contains("-g2-")), Is.True);
            Assert.That(Property<string>(session, "CurrentMarkerId"), Is.EqualTo("route.start"));
        }

        [Test]
        public void PackageBoundary_LeavesSceneContractsRegistriesAndPersistenceReadOnly()
        {
            string packageSource = ReadProjectFile(CompositionSourcePath)
                + "\n" + ReadProjectFile(SessionSourcePath);
            string[] forbiddenTokens =
            {
                "UnityEngine", "SceneManager", "LoadScene", "Stage1ShortRouteShell.unity",
                "MissionRunState", "PlayerPrefs", "Resources.Load", "Instantiate(",
                "Destroy(", "RegistryDocument", "File.Write", "Directory.CreateDirectory",
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
                "EN-011 route: start[pursuer,pursuer] -> arena[ram,mobile;chevron] -> "
                + "connector[pursuer,turret;double-bar] -> elite[lockdown] -> restart.");
            TestContext.WriteLine("EH-005 route shell consumed read-only: " + RouteScenePath);
        }

        private static object CreateSession(string runId)
        {
            return StaticCall(FindType(SessionTypeName), "CreateApproved", runId);
        }

        private static void AssertHazard(string canonical, params string[] tokens)
        {
            Assert.That(canonical, Does.Contain("maximum_hits_per_activation=1"));
            Assert.That(canonical, Does.Contain("active_ticks="));
            Assert.That(canonical, Does.Contain("cooldown_ticks="));
            Assert.That(canonical, Does.Contain("footprint_id="));
            foreach (string token in tokens)
            {
                Assert.That(canonical, Does.Contain(token));
            }
        }

        private static void TokensCleared(object session)
        {
            Assert.That(Property<int>(session, "ActiveEnemyCount"), Is.Zero);
            Assert.That(Property<int>(session, "ActiveHazardCount"), Is.Zero);
            Assert.That(Property<int>(session, "ActiveProjectileCount"), Is.Zero);
            Assert.That(StringArray(session, "GetActiveEnemyIds"), Is.Empty);
            Assert.That(StringArray(session, "GetActiveHazardIds"), Is.Empty);
            Assert.That(StringArray(session, "GetActiveProjectileIds"), Is.Empty);
        }

        private static void Applied(object transition)
        {
            Disposition(transition, "Applied");
        }

        private static void Disposition(object transition, string expected)
        {
            Assert.That(Property<object>(transition, "Disposition").ToString(), Is.EqualTo(expected),
                Property<string>(transition, "Reason") ?? "none");
        }

        private static string[] StringArray(object instance, string method, params object[] arguments)
        {
            return (string[])Call(instance, method, arguments);
        }

        private static string StringResult(object instance, string method, params object[] arguments)
        {
            return (string)Call(instance, method, arguments);
        }

        private static T Property<T>(object instance, string name)
        {
            PropertyInfo property = instance.GetType().GetProperty(name,
                BindingFlags.Public | BindingFlags.Instance);
            Assert.That(property, Is.Not.Null, instance.GetType().FullName + "." + name);
            return (T)property.GetValue(instance, null);
        }

        private static object StaticCall(Type type, string name, params object[] arguments)
        {
            return Invoke(RequireMethod(type, name, BindingFlags.Public | BindingFlags.Static,
                arguments.Length), null, arguments);
        }

        private static object Call(object instance, string name, params object[] arguments)
        {
            return Invoke(RequireMethod(instance.GetType(), name,
                BindingFlags.Public | BindingFlags.Instance, arguments.Length), instance, arguments);
        }

        private static MethodInfo RequireMethod(Type type, string name, BindingFlags flags, int count)
        {
            MethodInfo[] matches = type.GetMethods(flags)
                .Where(method => method.Name == name && method.GetParameters().Length == count)
                .ToArray();
            Assert.That(matches, Has.Length.EqualTo(1), type.FullName + "." + name);
            return matches[0];
        }

        private static object Invoke(MethodInfo method, object instance, object[] arguments)
        {
            try
            {
                return method.Invoke(instance, arguments);
            }
            catch (TargetInvocationException exception)
            {
                throw exception.InnerException ?? exception;
            }
        }

        private static Type FindType(string fullName)
        {
            Type result = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(fullName, false))
                .FirstOrDefault(type => type != null);
            Assert.That(result, Is.Not.Null, "Runtime type was not compiled: " + fullName);
            return result;
        }

        private static bool ProjectFileExists(string assetPath)
        {
            return File.Exists(ProjectPath(assetPath));
        }

        private static string ReadProjectFile(string assetPath)
        {
            string path = ProjectPath(assetPath);
            Assert.That(File.Exists(path), Is.True, assetPath);
            return File.ReadAllText(path);
        }

        private static string ProjectPath(string assetPath)
        {
            return Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(),
                assetPath.Replace('/', Path.DirectorySeparatorChar)));
        }
    }
}
#endif
