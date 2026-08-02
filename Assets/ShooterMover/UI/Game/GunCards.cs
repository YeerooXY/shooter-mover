using System;
using System.Globalization;
using ShooterMover.Application.Flow.Game;
using ShooterMover.Application.Inventory.LoadoutScreen;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Domain.Common;
using ShooterMover.UI.InventoryLoadout;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShooterMover.UI.Game
{
    /// <summary>
    /// Production-only presentation over the existing canonical Inventory controller.
    /// It owns no holdings, equipment, mount, or persistence state.
    /// </summary>
    [DefaultExecutionOrder(12000)]
    [DisallowMultipleComponent]
    public sealed class GunCards : MonoBehaviour
    {
        [SerializeField] private Texture2D gunPreviewOverride;
        [SerializeField] private string gunPreviewResourcePath =
            GunInventoryCardPresentation.TemporaryImageResourceKey;

        private InventoryMenu controller;
        private PlayerLoadoutLive runtime;
        private Texture2D preview;
        private bool previewResolved;
        private bool bound;
        private Vector2 mountScroll;
        private Vector2 gunScroll;
        private GUIStyle titleStyle;
        private GUIStyle smallStyle;
        private GUIStyle warningStyle;

        public bool IsBound { get { return bound; } }

        [RuntimeInitializeOnLoadMethod(
            RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
        }

        [RuntimeInitializeOnLoadMethod(
            RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            EnsureInstalled();
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            EnsureInstalled();
        }

        private static void EnsureInstalled()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid()
                || scene.path != FlowScenePaths.Inventory)
            {
                return;
            }

            InventoryMenu target =
                UnityEngine.Object.FindFirstObjectByType<
                    InventoryMenu>(
                    FindObjectsInactive.Include);
            if (target == null) return;

            GunCards presenter =
                target.GetComponent<GunCards>();
            if (presenter == null)
            {
                presenter = target.gameObject.AddComponent<
                    GunCards>();
            }
            presenter.controller = target;
        }

        private void Awake()
        {
            controller = GetComponent<InventoryMenu>();
        }

        private void Update()
        {
            if (bound
                && (controller == null
                    || controller.CanonicalSnapshot == null))
            {
                bound = false;
                runtime = null;
            }
            if (!bound) TryBindProduction();
        }

        private bool TryBindProduction()
        {
            if (controller == null)
            {
                controller = GetComponent<InventoryMenu>();
            }
            if (controller == null || controller.IncomingPayload == null)
            {
                return false;
            }

            PlayerLoadoutLive currentRuntime;
            FlowProfileRecord profile;
            return InventoryLoadoutFlow.TryResolveCurrent(
                    out currentRuntime,
                    out profile)
                && currentRuntime != null
                && profile != null
                && Bind(controller, currentRuntime);
        }

        public bool BindForTests(
            InventoryMenu target,
            PlayerLoadoutLive currentRuntime)
        {
            return Bind(target, currentRuntime);
        }

        private bool Bind(
            InventoryMenu target,
            PlayerLoadoutLive currentRuntime)
        {
            if (target == null || currentRuntime == null) return false;
            PlayerRouteProfilePayload payload =
                currentRuntime.CurrentRoutePayload;
            if (payload == null || !payload.HasValidFingerprint())
            {
                return false;
            }

            controller = target;
            runtime = currentRuntime;
            controller.enabled = true;
            LoadoutRegistry.Register(
                runtime.GunInventory,
                runtime.MountLoadoutAuthority);
            controller.ConnectCanonicalAuthorities(
                runtime.GunInventory,
                runtime.MountLayout,
                runtime.GunCatalog);
            controller.ConfigureGunPresentation(
                runtime.EquipmentCatalog,
                runtime.GunCatalog);
            controller.Present(HubRoute.Inventory, payload);
            bound = controller.CanonicalSnapshot != null;
            if (bound) controller.enabled = false;
            return bound;
        }

        private void OnGUI()
        {
            InventoryMenuState snapshot =
                bound && controller != null
                    ? controller.CanonicalSnapshot
                    : null;
            if (snapshot == null) return;

            EnsureStyles();
            int priorDepth = GUI.depth;
            GUI.depth = -900;
            GUI.Box(
                new Rect(0f, 0f, Screen.width, Screen.height),
                GUIContent.none);

            float width = Mathf.Min(1320f, Screen.width - 24f);
            float height = Mathf.Min(860f, Screen.height - 24f);
            Rect panel = new Rect(
                (Screen.width - width) * 0.5f,
                (Screen.height - height) * 0.5f,
                width,
                height);
            GUILayout.BeginArea(panel, GUI.skin.window);
            GUILayout.Label("INVENTORY / LOADOUT", titleStyle);
            GUILayout.Label(
                runtime.CurrentRoutePayload.SelectedCharacterStableId
                + " / "
                + runtime.CurrentRoutePayload.LoadoutProfileStableId,
                smallStyle);
            GUILayout.Space(8f);

            GUILayout.BeginHorizontal();
            DrawMounts(snapshot);
            GUILayout.Space(12f);
            DrawGuns(snapshot);
            GUILayout.EndHorizontal();

            DrawLastDiagnostic();
            GUILayout.BeginHorizontal();
            GUI.enabled = snapshot.CanConfirm;
            if (GUILayout.Button("CONFIRM", GUILayout.MinHeight(42f)))
            {
                controller.Confirm();
            }
            GUI.enabled = true;
            if (GUILayout.Button("BACK", GUILayout.MinHeight(42f)))
            {
                controller.Back();
            }
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
            GUI.depth = priorDepth;
        }

        private void DrawMounts(InventoryMenuState snapshot)
        {
            GUILayout.BeginVertical(GUILayout.Width(300f));
            GUILayout.Label("GUN SLOTS", titleStyle);
            mountScroll = GUILayout.BeginScrollView(mountScroll);
            for (int index = 0; index < snapshot.Mounts.Count; index++)
            {
                GunInventoryMount mount = snapshot.Mounts[index];
                bool selected = mount.Position.LoadoutSlotStableId
                    == controller.ActiveSlotStableId;
                bool locked = mount.Position.Availability
                    != GunMountAvailability.Active;
                string equipped = mount.EquippedCard == null
                    ? "EMPTY"
                    : mount.EquippedCard.DisplayName
                        + "\n"
                        + "Instance "
                        + ShortIdentity(mount.EquippedInstanceId);
                string state = locked
                    ? "LOCKED\nUnlock through class skill"
                    : equipped;

                GUI.enabled = !locked;
                if (GUILayout.Button(
                    (selected && !locked ? "▶ " : string.Empty)
                    + mount.Position.DisplayName
                    + "\n"
                    + state,
                    GUILayout.MinHeight(88f)))
                {
                    controller.SelectSlot(
                        mount.Position.LoadoutSlotStableId);
                }
                GUI.enabled = true;
            }

            GunInventoryMount active =
                snapshot.FindMount(controller.ActiveSlotStableId);
            bool canUnequip = active != null
                && active.Position.Availability
                    == GunMountAvailability.Active
                && active.EquippedInstanceId != null;
            GUI.enabled = canUnequip;
            if (GUILayout.Button(
                "UNEQUIP ACTIVE MOUNT",
                GUILayout.MinHeight(38f)))
            {
                controller.UnequipActiveSlot();
            }
            GUI.enabled = true;
            GUILayout.EndScrollView();
            GUILayout.EndVertical();
        }

        private void DrawGuns(
            InventoryMenuState snapshot)
        {
            GUILayout.BeginVertical();
            GUILayout.Label("OWNED GUNS", titleStyle);
            gunScroll = GUILayout.BeginScrollView(gunScroll);
            for (int index = 0;
                 index < snapshot.OwnedGuns.Count;
                 index++)
            {
                DrawCard(snapshot, snapshot.OwnedGuns[index]);
                GUILayout.Space(6f);
            }
            GUILayout.EndScrollView();
            GUILayout.EndVertical();
        }

        private void DrawCard(
            InventoryMenuState snapshot,
            GunInventoryCard card)
        {
            GunInventoryCardPresentation info;
            string error;
            bool resolved = GunInventoryCardPresentation.TryCreate(
                runtime.GunCatalog,
                card.Instance.GunDefinitionId.Value,
                out info,
                out error);

            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.BeginHorizontal();
            DrawPreview();
            GUILayout.Space(10f);
            GUILayout.BeginVertical();
            GUILayout.Label(card.DisplayName, titleStyle);
            GUILayout.Label(
                card.Instance.GunDefinitionId.Value
                + (string.IsNullOrEmpty(card.Family)
                    ? string.Empty
                    : "  •  " + card.Family),
                smallStyle);
            GUILayout.Label(
                "Exact instance: "
                + ShortIdentity(card.Instance.InstanceId),
                smallStyle);
            if (resolved)
            {
                GUILayout.BeginHorizontal();
                DrawStat("DAMAGE / SHOT", Format(info.DamagePerShot));
                DrawStat(
                    "PELLETS / SHOT",
                    info.ProjectilesPerShot.ToString(
                        CultureInfo.InvariantCulture));
                DrawStat(
                    "RATE OF FIRE",
                    Format(info.RateOfFire) + " /s");
                GUILayout.EndHorizontal();
            }
            else
            {
                GUILayout.Label(error, warningStyle);
            }

            GunInventoryMount equippedMount =
                FindMountByPhysicalId(snapshot, card.EquippedMountId);
            GUILayout.Label(
                card.IsEquipped
                    ? "EQUIPPED — "
                        + (equippedMount == null
                            ? card.EquippedMountId.ToString()
                            : equippedMount.Position.DisplayName)
                    : "UNEQUIPPED",
                smallStyle);
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();

            GunInventoryMount active =
                snapshot.FindMount(controller.ActiveSlotStableId);
            GUI.enabled = active != null
                && active.Position.Availability
                    == GunMountAvailability.Active;
            if (GUILayout.Button(
                "EQUIP / REPLACE ACTIVE MOUNT",
                GUILayout.MinHeight(36f)))
            {
                controller.SelectInstance(card.Instance.InstanceId);
            }
            GUI.enabled = true;
            GUILayout.EndVertical();
        }

        private void DrawLastDiagnostic()
        {
            if (controller.LastResult == null
                || string.IsNullOrEmpty(
                    controller.LastResult.RejectionCode))
            {
                return;
            }
            GUILayout.Label(
                controller.LastResult.RejectionCode,
                warningStyle);
        }

        private void DrawStat(string label, string value)
        {
            GUILayout.BeginVertical(
                GUI.skin.box,
                GUILayout.Width(132f),
                GUILayout.MinHeight(64f));
            GUILayout.Label(label, smallStyle);
            GUILayout.Label(value, titleStyle);
            GUILayout.EndVertical();
        }

        private void DrawPreview()
        {
            Rect frame = GUILayoutUtility.GetRect(
                210f,
                118f,
                GUILayout.Width(210f),
                GUILayout.Height(118f));
            GUI.Box(frame, GUIContent.none);
            Rect content = new Rect(
                frame.x + 7f,
                frame.y + 7f,
                frame.width - 14f,
                frame.height - 14f);
            if (ResolvePreview())
            {
                GUI.DrawTexture(
                    content,
                    preview,
                    ScaleMode.ScaleToFit,
                    true);
            }
            else
            {
                GUI.Label(
                    content,
                    "BLASTER_SP\nPREVIEW MISSING\n"
                    + "Use a Resources asset or assign the override.",
                    smallStyle);
            }
        }

        private bool ResolvePreview()
        {
            if (gunPreviewOverride != null)
            {
                preview = gunPreviewOverride;
                return true;
            }
            if (previewResolved) return preview != null;

            previewResolved = true;
            string path = string.IsNullOrWhiteSpace(
                    gunPreviewResourcePath)
                ? GunInventoryCardPresentation
                    .TemporaryImageResourceKey
                : gunPreviewResourcePath.Trim();
            Sprite sprite = Resources.Load<Sprite>(path);
            preview = sprite == null
                ? Resources.Load<Texture2D>(path)
                : sprite.texture;
            return preview != null;
        }

        private void EnsureStyles()
        {
            if (titleStyle != null) return;
            titleStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 20,
                fontStyle = FontStyle.Bold,
                wordWrap = true,
            };
            smallStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 12,
                wordWrap = true,
            };
            warningStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 12,
                fontStyle = FontStyle.Italic,
                wordWrap = true,
            };
        }

        private static GunInventoryMount FindMountByPhysicalId(
            InventoryMenuState snapshot,
            StableId mountId)
        {
            if (snapshot == null || mountId == null)
            {
                return null;
            }
            for (int index = 0; index < snapshot.Mounts.Count; index++)
            {
                GunInventoryMount mount = snapshot.Mounts[index];
                if (mount.Position.MountStableId == mountId)
                {
                    return mount;
                }
            }
            return null;
        }

        private static string ShortIdentity(StableId stableId)
        {
            string text = stableId == null
                ? string.Empty
                : stableId.ToString();
            return text.Length <= 28
                ? text
                : "…" + text.Substring(text.Length - 27);
        }

        private static string Format(double value)
        {
            return value.ToString(
                Math.Abs(value - Math.Round(value)) < 0.000001d
                    ? "0"
                    : "0.##",
                CultureInfo.InvariantCulture);
        }
    }
}
