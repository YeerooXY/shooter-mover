using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using ShooterMover.Content.Definitions.Objects;
using ShooterMover.ContentPackages.Environment.VoidHazards;
using ShooterMover.Domain.Authoring;
using ShooterMover.UnityAdapters.Authoring;
using UnityEngine;
using UnityEngine.TestTools;

namespace ShooterMover.Tests.PlayMode.Environment.VoidHazards
{
    public sealed partial class VoidHazardAuthoring2DTests
    {
        private readonly List<Object> _created = new List<Object>();
        private int _identityOrdinal;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            for (int index = _created.Count - 1; index >= 0; index--)
            {
                Object value = _created[index];
                if (value != null)
                {
                    Object.Destroy(value);
                }
            }

            _created.Clear();
            _identityOrdinal = 0;
            yield return null;
        }

        private ObjectFamilyDefinitionAsset CreateFamily()
        {
            ObjectCapabilityDefinitionAsset capability = Track(
                ObjectCapabilityDefinitionAsset.CreateRuntime(
                    "capability.void-hazard"));
            return Track(ObjectFamilyDefinitionAsset.CreateRuntime(
                "family.void-hazard",
                "Void hazard",
                "variant.default",
                new[] { capability },
                new ObjectVariantAuthoring(
                    "variant.default",
                    null,
                    ObjectCapabilitySelectionAuthoring.Inherit(
                        "capability.void-hazard"))));
        }

        private GameplaySceneScope2D CreateScope(string name)
        {
            GameObject root = Track(new GameObject(name));
            GameplaySceneScope2D scope = root.AddComponent<GameplaySceneScope2D>();
            scope.ConfigureForTests(
                NextId("scope"),
                "scope.gameplay",
                NextId("projection"),
                "run.void-tests",
                0L);
            return scope;
        }

        private VoidHazardTestPorts CreatePorts(string name)
        {
            GameObject root = Track(new GameObject(name));
            return root.AddComponent<VoidHazardTestPorts>();
        }

        private VoidHazardAuthoring2D CreateHazard(
            string name,
            Transform parent,
            ObjectFamilyDefinitionAsset family,
            VoidPlayerResponseKind playerResponse,
            double playerDamageAmount,
            string checkpointId,
            VoidEnemyResponseKind enemyResponse,
            VoidProjectileResponseKind projectileResponse,
            VoidPropResponseKind propResponse,
            MonoBehaviour checkpointPort,
            MonoBehaviour presentationPort)
        {
            VoidHazardAuthoring2D hazard = CreateUnactivatedHazard(
                name,
                parent,
                family,
                playerResponse,
                playerDamageAmount,
                checkpointId,
                checkpointPort,
                presentationPort,
                enemyResponse,
                projectileResponse,
                propResponse);
            Assert.That(hazard.TryActivate(), Is.True, hazard.LastValidationResult.Diagnostic);
            return hazard;
        }

        private VoidHazardAuthoring2D CreateUnactivatedHazard(
            string name,
            Transform parent,
            ObjectFamilyDefinitionAsset family,
            VoidPlayerResponseKind playerResponse,
            double playerDamageAmount,
            string checkpointId,
            MonoBehaviour checkpointPort,
            MonoBehaviour presentationPort,
            VoidEnemyResponseKind enemyResponse = VoidEnemyResponseKind.Ignore,
            VoidProjectileResponseKind projectileResponse =
                VoidProjectileResponseKind.Ignore,
            VoidPropResponseKind propResponse = VoidPropResponseKind.Ignore)
        {
            GameObject root = Track(new GameObject(name));
            root.SetActive(false);
            root.transform.SetParent(parent);
            BoxCollider2D collider = root.AddComponent<BoxCollider2D>();
            collider.isTrigger = true;
            PlacedObjectAuthoring2D placed = root.AddComponent<PlacedObjectAuthoring2D>();
            placed.ConfigureForTests(
                NextId("placed"),
                family,
                "variant.default",
                null,
                "scope.gameplay",
                System.Array.Empty<CapabilityOverrideAuthoring>());
            VoidHazardAuthoring2D hazard = root.AddComponent<VoidHazardAuthoring2D>();
            hazard.ConfigureForTests(
                placed,
                collider,
                true,
                playerResponse,
                playerDamageAmount,
                checkpointId,
                enemyResponse,
                projectileResponse,
                propResponse,
                checkpointPort,
                presentationPort);
            root.SetActive(true);
            return hazard;
        }

        private VoidHazardTarget2D CreateTarget(
            string name,
            VoidHazardTargetCategory category,
            bool supportedProp,
            VoidHazardTestPorts ports)
        {
            GameObject root = Track(new GameObject(name));
            root.AddComponent<BoxCollider2D>();
            VoidHazardTarget2D target = root.AddComponent<VoidHazardTarget2D>();
            target.ConfigureForTests(
                NextId("target"),
                category,
                supportedProp,
                ports,
                ports,
                ports,
                ports,
                ports);
            return target;
        }

        private string NextId(string idNamespace)
        {
            _identityOrdinal++;
            return idNamespace + ".void-" + _identityOrdinal;
        }

        private T Track<T>(T value) where T : Object
        {
            _created.Add(value);
            return value;
        }
    }
}
