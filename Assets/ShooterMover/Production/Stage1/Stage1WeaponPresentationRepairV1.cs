using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using ShooterMover.Application.Flow.Production;
using ShooterMover.Application.Holdings;
using ShooterMover.Application.Weapons.Execution;
using ShooterMover.Contracts.Equipment;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Contracts.Holdings;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Holdings;
using ShooterMover.Domain.Weapons.Catalog;
using ShooterMover.Domain.Weapons.Execution;
using ShooterMover.TestSupport.VisibleSlice;
using ShooterMover.UnityAdapters.Weapons.Live;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShooterMover.UnityAdapters.Production.Stage1
{
    /// <summary>
    /// Transitional repair for the production Level 1 cutover.
    ///
    /// The cutover currently owns four concrete route slots, while the approved starter
    /// showcase contains five weapons. Keys 4 and 5 therefore select two deterministic
    /// alternatives for the fourth route slot without mutating the persisted route payload.
    /// Every shot still executes through InventoryBackedWeaponExecutionAdapter/WPN-CORE-002.
    /// </summary>
    [DefaultExecutionOrder(19900)]
    [DisallowMultipleComponent]
    public sealed class Stage1WeaponPresentationRepairV1 : MonoBehaviour
    {
        private const int SimulationTicksPerSecond = 60;
        private const string ArcDefinitionId = "weapon.arc-gun";
        private const string RicochetDefinitionId = "weapon.ricochet-gun";

        private static readonly BindingFlags InstanceFlags =
            BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly FieldInfo InitializedField =
            typeof(Stage1PlayableLoopCompositionV1).GetField("initialized", InstanceFlags);
        private static readonly FieldInfo ControllerField =
            typeof(Stage1PlayableLoopCompositionV1).GetField("controller", InstanceFlags);
        private static readonly FieldInfo ProfileField =
            typeof(Stage1PlayableLoopCompositionV1).GetField("profile", InstanceFlags);
        private static readonly FieldInfo EffectEmitterField =
            typeof(Stage1PlayableLoopCompositionV1).GetField("effectEmitter", InstanceFlags);
        private static readonly FieldInfo HoldingsField =
            typeof(Stage1PlayableLoopCompositionV1).GetField("holdings", InstanceFlags);
        private static readonly FieldInfo EquipmentCatalogField =
            typeof(Stage1PlayableLoopCompositionV1).GetField("equipmentCatalog", InstanceFlags);
        private static readonly FieldInfo WeaponCatalogField =
            typeof(Stage1PlayableLoopCompositionV1).GetField("weaponCatalog", InstanceFlags);
        private static readonly FieldInfo WeaponsField =
            typeof(Stage1PlayableLoopCompositionV1).GetField("weapons", InstanceFlags);
        private static readonly FieldInfo DiagnosticField =
            typeof(Stage1PlayableLoopCompositionV1).GetField("diagnostic", InstanceFlags);
        private static readonly FieldInfo EnemyByColliderField =
            typeof(Stage1PlayableLoopCompositionV1).GetField("enemyByCollider", InstanceFlags);
        private static readonly MethodInfo ApplyEnemyDamageMethod =
            typeof(Stage1PlayableLoopCompositionV1).GetMethod(
                "ApplyEnemyDamage",
                InstanceFlags);
        private static readonly FieldInfo WeaponDisplayNamesField =
            typeof(Stage1PlayableLoopCompositionV1).GetField(
                "WeaponDisplayNames",
                BindingFlags.Static | BindingFlags.NonPublic);

        private readonly HashSet<InventoryWeaponEffectInstance2D> preparedEffects =
            new HashSet<InventoryWeaponEffectInstance2D>();
        private readonly List<UnityEngine.Object> runtimeAssets =
            new List<UnityEngine.Object>();

        private Stage1PlayableLoopCompositionV1 composition;
        private Stage1VisibleSliceController controller;
        private ProductionFlowProfileRecordV1 profile;
        private InventoryWeaponEffectEmitter2D effectEmitter;
        private bool installed;
        private bool ricochetSelected;
        private GUIStyle overlayStyle;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSceneHook()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallSceneHook()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            InstallForScene(SceneManager.GetActiveScene());
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            InstallForScene(scene);
        }

        private static void InstallForScene(Scene scene)
        {
            if (!string.Equals(
                    scene.path,
                    Stage1VisibleSliceController.ScenePath,
                    StringComparison.Ordinal))
            {
                return;
            }

            GameObject[] roots = scene.GetRootGameObjects();
            for (int index = 0; index < roots.Length; index++)
            {
                Stage1VisibleSliceController visibleSlice =
                    roots[index].GetComponentInChildren<Stage1VisibleSliceController>(true);
                if (visibleSlice == null)
                {
                    continue;
                }

                if (visibleSlice.GetComponent<Stage1WeaponPresentationRepairV1>() == null)
                {
                    visibleSlice.gameObject.AddComponent<Stage1WeaponPresentationRepairV1>();
                }
                return;
            }
        }

        private void Update()
        {
            if (!installed)
            {
                TryInstall();
            }
        }

        private void LateUpdate()
        {
            if (!installed || effectEmitter == null)
            {
                return;
            }

            IReadOnlyList<InventoryWeaponEffectInstance2D> emitted =
                effectEmitter.EmittedEffects;
            for (int index = 0; index < emitted.Count; index++)
            {
                InventoryWeaponEffectInstance2D effect = emitted[index];
                if (effect == null || !preparedEffects.Add(effect))
                {
                    continue;
                }

                ChainArcEffect chain = effect.Description as ChainArcEffect;
                if (chain != null)
                {
                    ExecuteArc(chain, effect.gameObject);
                    continue;
                }

                RepairProjectile(effect);
            }
        }

        private void TryInstall()
        {
            composition = GetComponent<Stage1PlayableLoopCompositionV1>();
            if (composition == null
                || InitializedField == null
                || !(bool)InitializedField.GetValue(composition))
            {
                return;
            }

            controller = ControllerField == null
                ? null
                : ControllerField.GetValue(composition) as Stage1VisibleSliceController;
            profile = ProfileField == null
                ? null
                : ProfileField.GetValue(composition) as ProductionFlowProfileRecordV1;
            effectEmitter = EffectEmitterField == null
                ? null
                : EffectEmitterField.GetValue(composition) as InventoryWeaponEffectEmitter2D;
            if (controller == null || profile == null || effectEmitter == null)
            {
                SetDiagnostic("Five-weapon repair could not resolve the live cutover fields.");
                enabled = false;
                return;
            }

            if (controller.LoadoutSelector != null && controller.LoadoutSelector.Visible)
            {
                return;
            }

            int selected = 0;
            InventoryWeaponRuntimeComposition existing = ReadWeapons();
            if (existing != null)
            {
                selected = existing.SelectedSlotIndex;
            }

            RebuildFourthSlot(ArcDefinitionId, "Arc Gun", false, selected);
            installed = true;
        }

        private void RebuildFourthSlot(
            string fourthWeaponDefinitionId,
            string displayName,
            bool selectsRicochet,
            int selectedSlot)
        {
            EquipmentCatalog equipmentCatalog = BuildEquipmentCatalog();
            PlayerHoldingsService holdings = BuildHoldings(
                equipmentCatalog,
                fourthWeaponDefinitionId);
            WeaponCatalog weaponCatalog = BuildWeaponCatalog();
            var actorState = new LiveActorStateSource(controller);
            var activeWeapon = new RouteProfileActiveWeaponSource(
                profile.Payload,
                Mathf.Clamp(selectedSlot, 0, PlayerRouteProfilePayloadV1.WeaponSlotCount - 1));
            var adapter = new InventoryBackedWeaponExecutionAdapter(
                holdings,
                equipmentCatalog,
                weaponCatalog,
                new LiveOwnershipResolver(controller),
                effectEmitter,
                SimulationTicksPerSecond);
            var runtime = new InventoryWeaponRuntimeComposition(
                actorState,
                activeWeapon,
                adapter);

            HoldingsField.SetValue(composition, holdings);
            EquipmentCatalogField.SetValue(composition, equipmentCatalog);
            WeaponCatalogField.SetValue(composition, weaponCatalog);
            WeaponsField.SetValue(composition, runtime);

            ricochetSelected = selectsRicochet;
            SetFourthDisplayName(displayName);
            if (selectedSlot == 3)
            {
                runtime.SelectSlot(3);
            }

            SetDiagnostic(
                selectsRicochet
                    ? "Slot 5: Ricochet Gun — two wall bounces maximum."
                    : string.Empty);
        }

        private PlayerHoldingsService BuildHoldings(
            EquipmentCatalog equipmentCatalog,
            string fourthWeaponDefinitionId)
        {
            var holdings = new PlayerHoldingsService(
                StableId.Parse("authority.demo-cutover-player-holdings"),
                999L,
                new CatalogEquipmentValidatorV1(equipmentCatalog));
            string[] definitions =
            {
                "equipment.demo-cutover-blaster",
                "equipment.demo-cutover-shotgun",
                "equipment.demo-cutover-rocket-launcher",
                string.Equals(
                    fourthWeaponDefinitionId,
                    RicochetDefinitionId,
                    StringComparison.Ordinal)
                    ? "equipment.demo-cutover-ricochet-gun"
                    : "equipment.demo-cutover-arc-gun",
            };
            StableId common = StableId.Parse("equipment-quality.common");
            for (int index = 0; index < PlayerRouteProfilePayloadV1.WeaponSlotCount; index++)
            {
                StableId instanceId =
                    profile.Payload.WeaponSlots[index].EquipmentInstanceStableId;
                EquipmentInstance instance = EquipmentInstance.Create(
                    instanceId,
                    StableId.Parse(definitions[index]),
                    1,
                    common,
                    Array.Empty<AugmentInstance>());
                string token = "weapon-visual-live-slot-" + (index + 1);
                PlayerHoldingsMutationResultV1 result = holdings.Apply(
                    PlayerHoldingsCommandV1.AddEquipment(
                        StableId.Parse("transaction." + token),
                        StableId.Parse("operation." + token),
                        holdings.AuthorityStableId,
                        instance,
                        HoldingProvenanceV1.Create(
                            StableId.Parse("grant." + token),
                            StableId.Parse("source.weapon-visual-live-repair")),
                        holdings.Sequence));
                if (result.Status != PlayerHoldingsMutationStatusV1.Applied
                    && result.Status
                        != PlayerHoldingsMutationStatusV1.ExactDuplicateNoChange)
                {
                    throw new InvalidOperationException(
                        "Unable to rebuild live weapon slot "
                        + (index + 1).ToString(CultureInfo.InvariantCulture)
                        + ": "
                        + result.RejectionCode);
                }
            }
            return holdings;
        }

        private static EquipmentCatalog BuildEquipmentCatalog()
        {
            EquipmentQualityTier common = EquipmentQualityTier.Create(
                StableId.Parse("equipment-quality.common"),
                "Common",
                1);
            EquipmentCatalogBuildResult result = EquipmentCatalog.Build(
                new[]
                {
                    WeaponEquipment(
                        "equipment.demo-cutover-blaster",
                        "family.blaster",
                        "Blaster",
                        "weapon.blaster-machine-gun",
                        common),
                    WeaponEquipment(
                        "equipment.demo-cutover-shotgun",
                        "family.shotgun",
                        "Shotgun",
                        "weapon.shotgun",
                        common),
                    WeaponEquipment(
                        "equipment.demo-cutover-rocket-launcher",
                        "family.rocket-launcher",
                        "Rocket Launcher",
                        "weapon.rocket-launcher",
                        common),
                    WeaponEquipment(
                        "equipment.demo-cutover-arc-gun",
                        "family.arc-gun",
                        "Arc Gun",
                        ArcDefinitionId,
                        common),
                    WeaponEquipment(
                        "equipment.demo-cutover-ricochet-gun",
                        "family.ricochet-gun",
                        "Ricochet Gun",
                        RicochetDefinitionId,
                        common),
                },
                Array.Empty<AugmentDefinition>());
            if (!result.IsValid || result.Catalog == null)
            {
                throw new InvalidOperationException(
                    "The repaired five-weapon equipment catalog is invalid.");
            }
            return result.Catalog;
        }

        private static EquipmentDefinition WeaponEquipment(
            string definition,
            string family,
            string displayName,
            string runtime,
            EquipmentQualityTier quality)
        {
            return EquipmentDefinition.Create(
                StableId.Parse(definition),
                EquipmentCategoryIds.Weapon,
                StableId.Parse(family),
                displayName,
                StableId.Parse(runtime),
                InclusiveIntRange.Create(1, 100),
                0,
                new[] { quality },
                Array.Empty<StableId>());
        }

        private static WeaponCatalog BuildWeaponCatalog()
        {
            var rules = new WeaponCatalogRules(
                true,
                false,
                "20-25",
                new[] { 75, 105, 135 },
                new[] { "Kinetic", "Energized" },
                10,
                true,
                true,
                true);
            var inputs = new WeaponCatalogInputs(
                12d,
                0.05d,
                0.055d,
                0.06d,
                new Dictionary<string, WeaponRarityInput>(StringComparer.Ordinal)
                {
                    { "Common", new WeaponRarityInput("Common", 1000d, 0, 4d, 13d) },
                });
            var archetype = new WeaponArchetypeDefinition(
                "DemoCutover",
                "Demo Cutover",
                1d,
                1d,
                1,
                1,
                0d,
                10d,
                10d,
                1d,
                0d,
                0d,
                0d,
                0d,
                0d,
                0d,
                0,
                0,
                0d,
                0d,
                1d);

            WeaponFamilyDefinition[] families =
            {
                Family("demo-cutover-blaster", "Blaster", "Kinetic"),
                Family("demo-cutover-shotgun", "Shotgun", "Kinetic"),
                Family("demo-cutover-rocket", "Rocket Launcher", "Kinetic"),
                Family("demo-cutover-arc", "Arc Gun", "Energized"),
                Family("demo-cutover-ricochet", "Ricochet Gun", "Kinetic"),
            };
            return new WeaponCatalog(
                "1.0",
                "weapon-visual-live-repair",
                rules,
                inputs,
                new Dictionary<string, WeaponArchetypeDefinition>(StringComparer.Ordinal)
                {
                    { "DemoCutover", archetype },
                },
                families,
                new[]
                {
                    WeaponDefinition(
                        "weapon.blaster-machine-gun",
                        "Blaster",
                        "demo-cutover-blaster",
                        "Kinetic",
                        10d,
                        1,
                        0d,
                        40d,
                        30d,
                        5d,
                        1),
                    WeaponDefinition(
                        "weapon.shotgun",
                        "Shotgun",
                        "demo-cutover-shotgun",
                        "Kinetic",
                        2d,
                        7,
                        24d,
                        30d,
                        15d,
                        3d,
                        0),
                    WeaponDefinition(
                        "weapon.rocket-launcher",
                        "Rocket Launcher",
                        "demo-cutover-rocket",
                        "Kinetic",
                        1d,
                        1,
                        0d,
                        12d,
                        35d,
                        4d,
                        0,
                        20d,
                        3d),
                    WeaponDefinition(
                        ArcDefinitionId,
                        "Arc Gun",
                        "demo-cutover-arc",
                        "Energized",
                        1.5d,
                        1,
                        0d,
                        12d,
                        12d,
                        12d,
                        0,
                        0d,
                        0d,
                        3,
                        6d),
                    WeaponDefinition(
                        RicochetDefinitionId,
                        "Ricochet Gun",
                        "demo-cutover-ricochet",
                        "Kinetic",
                        2.5d,
                        1,
                        0d,
                        24d,
                        30d,
                        8d,
                        0),
                });
        }

        private static WeaponFamilyDefinition Family(
            string id,
            string displayName,
            string damageType)
        {
            return new WeaponFamilyDefinition(
                id,
                displayName,
                "DemoCutover",
                damageType,
                "Universal",
                1,
                20,
                20,
                3,
                "Common",
                "Common",
                "Common",
                1d,
                "Standard",
                "Production vertical slice",
                "Production vertical slice",
                WeaponCatalogAvailability.Live,
                Array.Empty<string>());
        }

        private static WeaponDefinitionData WeaponDefinition(
            string id,
            string displayName,
            string family,
            string damageType,
            double fireRate,
            int projectiles,
            double spread,
            double speed,
            double range,
            double damage,
            int pierce,
            double areaDamage = 0d,
            double explosionRadius = 0d,
            int chainTargets = 0,
            double chainRange = 0d)
        {
            bool explosive = areaDamage > 0d;
            return new WeaponDefinitionData(
                id,
                displayName,
                family,
                1,
                damageType,
                "DemoCutover",
                "Universal",
                1,
                1,
                1,
                "Common",
                1000d,
                1d,
                1000d,
                4d,
                13d,
                "Standard",
                false,
                "Standard",
                1d,
                100d,
                10d,
                explosive ? 0.2d : 1d,
                explosive ? 0.8d : 0d,
                0d,
                fireRate,
                projectiles,
                1,
                damage,
                spread,
                speed,
                range,
                pierce,
                explosionRadius,
                areaDamage,
                0d,
                0d,
                0d,
                0d,
                chainTargets,
                chainRange,
                0.5d,
                1d,
                0d,
                "Production vertical slice",
                "Production vertical slice",
                WeaponCatalogAvailability.Live,
                Array.Empty<string>());
        }

        private void RepairProjectile(InventoryWeaponEffectInstance2D effect)
        {
            string definitionId = effect.Description.Identity.WeaponDefinitionId.Value;
            WeaponVisualProfile profile = WeaponVisualProfile.For(definitionId);

            SpriteRenderer[] existing =
                effect.GetComponents<SpriteRenderer>();
            for (int index = 0; index < existing.Length; index++)
            {
                existing[index].enabled = false;
            }

            effect.transform.localScale = Vector3.one;
            Rigidbody2D body = effect.GetComponent<Rigidbody2D>();
            if (body != null)
            {
                body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
                body.interpolation = RigidbodyInterpolation2D.Interpolate;
                if (body.linearVelocity.sqrMagnitude > 0.0001f)
                {
                    effect.transform.right = body.linearVelocity.normalized;
                }
            }

            CircleCollider2D collider = effect.GetComponent<CircleCollider2D>();
            if (collider != null)
            {
                collider.radius = profile.ColliderRadius;
            }

            GameObject visual = new GameObject("WeaponVisual");
            visual.transform.SetParent(effect.transform, false);
            visual.transform.localScale = profile.VisualScale;
            SpriteRenderer renderer = visual.AddComponent<SpriteRenderer>();
            renderer.sprite = CreateProjectileSprite(definitionId, profile);
            renderer.sortingOrder = 60;

            AddTrail(effect.gameObject, profile);

            if (string.Equals(definitionId, RicochetDefinitionId, StringComparison.Ordinal))
            {
                Stage1RicochetBounceV1 bounce =
                    effect.gameObject.AddComponent<Stage1RicochetBounceV1>();
                bounce.Configure(this);
            }
            if (effect.Description is ExplosiveProjectileEffect)
            {
                Stage1RocketExplosionVisualV1 explosion =
                    effect.gameObject.AddComponent<Stage1RocketExplosionVisualV1>();
                explosion.Configure(this);
            }
        }

        private Sprite CreateProjectileSprite(
            string key,
            WeaponVisualProfile profile)
        {
            const int width = 24;
            const int height = 12;
            Texture2D texture = new Texture2D(
                width,
                height,
                TextureFormat.RGBA32,
                false);
            texture.name = "Weapon Visual " + key;
            texture.filterMode = FilterMode.Point;
            Color clear = new Color(0f, 0f, 0f, 0f);
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float nx = ((x + 0.5f) / width) * 2f - 1f;
                    float ny = ((y + 0.5f) / height) * 2f - 1f;
                    bool filled = profile.Diamond
                        ? Mathf.Abs(nx) + Mathf.Abs(ny) <= 1f
                        : (nx * nx) + (ny * ny * 2.2f) <= 1f;
                    texture.SetPixel(
                        x,
                        y,
                        filled
                            ? Color.Lerp(profile.EdgeColor, profile.CoreColor, 1f - Mathf.Abs(ny))
                            : clear);
                }
            }
            texture.Apply(false, true);
            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, width, height),
                new Vector2(0.5f, 0.5f),
                width);
            runtimeAssets.Add(texture);
            runtimeAssets.Add(sprite);
            return sprite;
        }

        private void AddTrail(GameObject projectile, WeaponVisualProfile profile)
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                return;
            }

            Material material = new Material(shader);
            material.name = "Weapon Trail " + profile.Name;
            material.color = profile.EdgeColor;
            runtimeAssets.Add(material);
            TrailRenderer trail = projectile.AddComponent<TrailRenderer>();
            trail.material = material;
            trail.time = profile.TrailTime;
            trail.startWidth = profile.TrailWidth;
            trail.endWidth = 0f;
            trail.numCornerVertices = 2;
            trail.numCapVertices = 2;
            trail.sortingOrder = 59;
        }

        private void ExecuteArc(ChainArcEffect chain, GameObject effectObject)
        {
            List<ArcCandidate> candidates = GatherArcCandidates(chain);
            int maximumHits = Mathf.Clamp(chain.MaximumTargets + 1, 1, 4);
            List<Vector3> points = new List<Vector3>
            {
                new Vector3((float)chain.Origin.X, (float)chain.Origin.Y, 0f),
            };

            int applied = 0;
            for (int index = 0; index < candidates.Count && applied < maximumHits; index++)
            {
                ArcCandidate candidate = candidates[index];
                object[] arguments =
                {
                    candidate.Binding,
                    chain.Identity,
                    chain.Damage,
                    "arc-" + applied.ToString(CultureInfo.InvariantCulture),
                };
                object result = ApplyEnemyDamageMethod.Invoke(composition, arguments);
                if (result is bool && (bool)result)
                {
                    points.Add(candidate.Position);
                    applied++;
                }
            }

            if (points.Count == 1)
            {
                points.Add(
                    points[0]
                    + new Vector3(
                        (float)chain.Direction.X,
                        (float)chain.Direction.Y,
                        0f)
                    * Mathf.Min((float)chain.MaximumRange, 4f));
            }
            SpawnArcLine(points);
            Destroy(effectObject);
        }

        private List<ArcCandidate> GatherArcCandidates(ChainArcEffect chain)
        {
            List<ArcCandidate> candidates = new List<ArcCandidate>();
            IDictionary map = EnemyByColliderField == null
                ? null
                : EnemyByColliderField.GetValue(composition) as IDictionary;
            if (map == null)
            {
                return candidates;
            }

            Vector2 origin = new Vector2((float)chain.Origin.X, (float)chain.Origin.Y);
            Vector2 direction = new Vector2(
                (float)chain.Direction.X,
                (float)chain.Direction.Y).normalized;
            float maximumRange = (float)chain.MaximumRange;
            HashSet<object> uniqueBindings = new HashSet<object>();
            foreach (DictionaryEntry entry in map)
            {
                Collider2D collider = entry.Key as Collider2D;
                object binding = entry.Value;
                if (collider == null
                    || binding == null
                    || !collider.enabled
                    || !collider.gameObject.activeInHierarchy
                    || !uniqueBindings.Add(binding))
                {
                    continue;
                }

                Vector2 delta = (Vector2)collider.bounds.center - origin;
                float distance = delta.magnitude;
                if (distance > maximumRange || distance <= 0.001f)
                {
                    continue;
                }
                if (Vector2.Dot(direction, delta / distance) < 0.25f)
                {
                    continue;
                }

                candidates.Add(
                    new ArcCandidate(
                        binding,
                        collider.bounds.center,
                        distance,
                        BindingStableText(binding)));
            }
            candidates.Sort(ArcCandidate.Compare);
            return candidates;
        }

        private static string BindingStableText(object binding)
        {
            PropertyInfo property = binding.GetType().GetProperty(
                "RoomInstanceStableId",
                BindingFlags.Instance | BindingFlags.Public);
            object value = property == null ? null : property.GetValue(binding);
            return value == null ? string.Empty : value.ToString();
        }

        private void SpawnArcLine(IReadOnlyList<Vector3> points)
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                return;
            }

            GameObject visual = new GameObject("ArcGunLiveLine");
            Material material = new Material(shader);
            material.color = new Color(0.35f, 0.95f, 1f, 1f);
            runtimeAssets.Add(material);
            LineRenderer line = visual.AddComponent<LineRenderer>();
            line.material = material;
            line.positionCount = points.Count;
            line.startWidth = 0.16f;
            line.endWidth = 0.06f;
            line.numCornerVertices = 3;
            line.numCapVertices = 3;
            line.sortingOrder = 70;
            for (int index = 0; index < points.Count; index++)
            {
                line.SetPosition(index, points[index]);
            }
            Stage1TimedDestroyV1 timed = visual.AddComponent<Stage1TimedDestroyV1>();
            timed.Configure(0.14f);
        }

        internal bool IsEnemyCollider(Collider2D collider)
        {
            IDictionary map = EnemyByColliderField == null
                ? null
                : EnemyByColliderField.GetValue(composition) as IDictionary;
            if (map == null || collider == null)
            {
                return false;
            }
            if (map.Contains(collider))
            {
                return true;
            }

            Transform current = collider.transform.parent;
            while (current != null)
            {
                Collider2D[] colliders = current.GetComponents<Collider2D>();
                for (int index = 0; index < colliders.Length; index++)
                {
                    if (map.Contains(colliders[index]))
                    {
                        return true;
                    }
                }
                current = current.parent;
            }
            return false;
        }

        internal bool IsPlayerCollider(Collider2D collider)
        {
            return controller != null
                && collider != null
                && (collider == controller.PlayerCollider
                    || collider.transform.IsChildOf(controller.PlayerTransform));
        }

        internal void SpawnRocketExplosion(Vector3 position)
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null || !Application.isPlaying)
            {
                return;
            }

            GameObject visual = new GameObject("RocketExplosionVisual");
            visual.transform.position = position;
            Material material = new Material(shader);
            material.color = new Color(1f, 0.35f, 0.05f, 0.9f);
            runtimeAssets.Add(material);
            LineRenderer ring = visual.AddComponent<LineRenderer>();
            ring.material = material;
            ring.loop = true;
            ring.positionCount = 24;
            ring.startWidth = 0.14f;
            ring.endWidth = 0.14f;
            ring.sortingOrder = 69;
            for (int index = 0; index < ring.positionCount; index++)
            {
                float angle = index * Mathf.PI * 2f / ring.positionCount;
                ring.SetPosition(index, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * 0.7f);
            }
            Stage1TimedDestroyV1 timed = visual.AddComponent<Stage1TimedDestroyV1>();
            timed.Configure(0.18f);
        }

        private InventoryWeaponRuntimeComposition ReadWeapons()
        {
            return WeaponsField == null
                ? null
                : WeaponsField.GetValue(composition) as InventoryWeaponRuntimeComposition;
        }

        private void SetFourthDisplayName(string displayName)
        {
            string[] names = WeaponDisplayNamesField == null
                ? null
                : WeaponDisplayNamesField.GetValue(null) as string[];
            if (names != null && names.Length > 3)
            {
                names[3] = displayName;
            }
        }

        private void SetDiagnostic(string value)
        {
            if (DiagnosticField != null && composition != null)
            {
                DiagnosticField.SetValue(composition, value ?? string.Empty);
            }
        }

        private void OnGUI()
        {
            if (!installed)
            {
                return;
            }
            if (overlayStyle == null)
            {
                overlayStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 12,
                    fontStyle = FontStyle.Bold,
                };
                overlayStyle.normal.textColor = new Color(0.8f, 0.95f, 1f, 1f);
            }

            GUI.Label(
                new Rect(22f, 202f, 520f, 24f),
                "4 Arc Gun   5 Ricochet Gun   (slot 4 alternates; profile identity stays unchanged)",
                overlayStyle);
        }

        private void OnDestroy()
        {
            for (int index = 0; index < runtimeAssets.Count; index++)
            {
                if (runtimeAssets[index] != null)
                {
                    Destroy(runtimeAssets[index]);
                }
            }
            runtimeAssets.Clear();
        }

        private sealed class LiveActorStateSource : IInventoryWeaponActorStateSource
        {
            private readonly Stage1VisibleSliceController source;

            public LiveActorStateSource(Stage1VisibleSliceController controller)
            {
                source = controller;
            }

            public bool TryResolveActorState(
                out WeaponActorInstanceId actorId,
                out LifecycleGeneration lifecycleGeneration)
            {
                actorId = source == null
                    ? null
                    : new WeaponActorInstanceId(StableId.Parse("actor.vs007-player"));
                lifecycleGeneration = source == null
                    ? null
                    : new LifecycleGeneration(source.RestartGeneration);
                return actorId != null && lifecycleGeneration != null;
            }
        }

        private sealed class LiveOwnershipResolver : IWeaponActorOwnershipResolver
        {
            private readonly Stage1VisibleSliceController source;

            public LiveOwnershipResolver(Stage1VisibleSliceController controller)
            {
                source = controller;
            }

            public bool TryResolveParticipant(
                WeaponActorInstanceId actorId,
                LifecycleGeneration lifecycleGeneration,
                out RunParticipantId participantId)
            {
                participantId = source == null
                    || actorId == null
                    || lifecycleGeneration == null
                    ? null
                    : new RunParticipantId(source.PlayerRunParticipantId);
                return participantId != null;
            }
        }

        private sealed class ArcCandidate
        {
            public ArcCandidate(
                object binding,
                Vector3 position,
                float distance,
                string stableText)
            {
                Binding = binding;
                Position = position;
                Distance = distance;
                StableText = stableText ?? string.Empty;
            }

            public object Binding { get; }
            public Vector3 Position { get; }
            public float Distance { get; }
            public string StableText { get; }

            public static int Compare(ArcCandidate left, ArcCandidate right)
            {
                int distance = left.Distance.CompareTo(right.Distance);
                return distance != 0
                    ? distance
                    : string.CompareOrdinal(left.StableText, right.StableText);
            }
        }

        private sealed class WeaponVisualProfile
        {
            private WeaponVisualProfile(
                string name,
                Color core,
                Color edge,
                Vector3 visualScale,
                float colliderRadius,
                float trailTime,
                float trailWidth,
                bool diamond)
            {
                Name = name;
                CoreColor = core;
                EdgeColor = edge;
                VisualScale = visualScale;
                ColliderRadius = colliderRadius;
                TrailTime = trailTime;
                TrailWidth = trailWidth;
                Diamond = diamond;
            }

            public string Name { get; }
            public Color CoreColor { get; }
            public Color EdgeColor { get; }
            public Vector3 VisualScale { get; }
            public float ColliderRadius { get; }
            public float TrailTime { get; }
            public float TrailWidth { get; }
            public bool Diamond { get; }

            public static WeaponVisualProfile For(string definitionId)
            {
                if (string.Equals(
                        definitionId,
                        "weapon.shotgun",
                        StringComparison.Ordinal))
                {
                    return new WeaponVisualProfile(
                        "Shotgun",
                        new Color(1f, 0.95f, 0.45f, 1f),
                        new Color(1f, 0.55f, 0.08f, 1f),
                        new Vector3(0.42f, 0.32f, 1f),
                        0.1f,
                        0.08f,
                        0.06f,
                        false);
                }
                if (string.Equals(
                        definitionId,
                        "weapon.rocket-launcher",
                        StringComparison.Ordinal))
                {
                    return new WeaponVisualProfile(
                        "Rocket",
                        new Color(1f, 0.85f, 0.25f, 1f),
                        new Color(1f, 0.12f, 0.02f, 1f),
                        new Vector3(1.15f, 0.7f, 1f),
                        0.16f,
                        0.22f,
                        0.15f,
                        true);
                }
                if (string.Equals(
                        definitionId,
                        RicochetDefinitionId,
                        StringComparison.Ordinal))
                {
                    return new WeaponVisualProfile(
                        "Ricochet",
                        new Color(0.95f, 1f, 1f, 1f),
                        new Color(0.2f, 0.75f, 1f, 1f),
                        new Vector3(0.8f, 0.5f, 1f),
                        0.13f,
                        0.16f,
                        0.1f,
                        true);
                }
                return new WeaponVisualProfile(
                    "Blaster",
                    new Color(0.75f, 1f, 1f, 1f),
                    new Color(0.05f, 0.55f, 1f, 1f),
                    new Vector3(0.72f, 0.42f, 1f),
                    0.12f,
                    0.12f,
                    0.09f,
                    false);
            }
        }
    }

    [DisallowMultipleComponent]
    internal sealed class Stage1RicochetBounceV1 : MonoBehaviour
    {
        private const int MaximumWallBounces = 2;

        private Stage1WeaponPresentationRepairV1 repair;
        private Rigidbody2D body;
        private Collider2D lastCollider;
        private float lastBounceTime;
        private int bounceCount;

        public void Configure(Stage1WeaponPresentationRepairV1 configuredRepair)
        {
            repair = configuredRepair;
            body = GetComponent<Rigidbody2D>();
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (repair == null
                || body == null
                || other == null
                || other.isTrigger
                || repair.IsEnemyCollider(other)
                || repair.IsPlayerCollider(other))
            {
                return;
            }

            if (other == lastCollider && Time.time - lastBounceTime < 0.08f)
            {
                return;
            }
            lastCollider = other;
            lastBounceTime = Time.time;

            if (bounceCount >= MaximumWallBounces)
            {
                Destroy(gameObject);
                return;
            }

            Vector2 velocity = body.linearVelocity;
            if (velocity.sqrMagnitude < 0.0001f)
            {
                Destroy(gameObject);
                return;
            }

            Vector2 closest = other.ClosestPoint(transform.position);
            Vector2 normal = (Vector2)transform.position - closest;
            if (normal.sqrMagnitude < 0.0001f)
            {
                normal = -velocity.normalized;
            }
            normal.Normalize();
            body.linearVelocity = Vector2.Reflect(velocity, normal);
            transform.right = body.linearVelocity.normalized;
            bounceCount++;
        }
    }

    [DisallowMultipleComponent]
    internal sealed class Stage1RocketExplosionVisualV1 : MonoBehaviour
    {
        private Stage1WeaponPresentationRepairV1 repair;
        private bool shuttingDown;

        public void Configure(Stage1WeaponPresentationRepairV1 configuredRepair)
        {
            repair = configuredRepair;
        }

        private void OnApplicationQuit()
        {
            shuttingDown = true;
        }

        private void OnDestroy()
        {
            if (!shuttingDown && repair != null && repair.isActiveAndEnabled)
            {
                repair.SpawnRocketExplosion(transform.position);
            }
        }
    }

    [DisallowMultipleComponent]
    internal sealed class Stage1TimedDestroyV1 : MonoBehaviour
    {
        private float destroyAt;

        public void Configure(float lifetime)
        {
            destroyAt = Time.time + Mathf.Max(0.01f, lifetime);
        }

        private void Update()
        {
            if (Time.time >= destroyAt)
            {
                Destroy(gameObject);
            }
        }
    }
}
