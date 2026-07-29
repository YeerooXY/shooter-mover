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
    public sealed partial class VoidHazardTests
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
                "VoidArea hazard",
                "variant.default",
                new[] { capability },
                new ObjectVariantAuthoring(
                    "variant.default",
                    null,
                    ObjectCapabilitySelectionAuthoring.Inherit(
                        "capability.void-hazard"))));
        }

        private GameplayScene CreateScope(string name)
        {
            GameObject root = Track(new GameObject(name));
            GameplayScene scope = root.AddComponent<GameplayScene>();
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

        private VoidHazard CreateHazard(
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
            VoidHazard hazard = CreateUnactivatedHazard(
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

        private VoidHazard CreateUnactivatedHazard(
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
            PlacedObject placed = root.AddComponent<PlacedObject>();
            placed.ConfigureForTests(
                NextId("placed"),
                family,
                "variant.default",
                null,
                "scope.gameplay",
                System.Array.Empty<CapabilityOverrideAuthoring>());
            VoidHazard hazard = root.AddComponent<VoidHazard>();
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

        private VoidTarget CreateTarget(
            string name,
            VoidHazardTargetCategory category,
            bool supportedProp,
            VoidHazardTestPorts ports)
        {
            GameObject root = Track(new GameObject(name));
            root.AddComponent<BoxCollider2D>();
            VoidTarget target = root.AddComponent<VoidTarget>();
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
