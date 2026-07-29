using System;
using System.Globalization;
using ShooterMover.Application.Flow.Production;
using ShooterMover.Application.Inventory.LoadoutScreen;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.UI.InventoryLoadout;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShooterMover.UI.ProductionFlow
{
    /// <summary>
    /// Production-only presentation over the existing canonical Inventory controller.
    /// It owns no holdings, equipment, mount, or persistence state.
    /// </summary>
    [DefaultExecutionOrder(12000)]
    [DisallowMultipleComponent]
    public sealed class InventoryWeaponCards : MonoBehaviour
    {
        [SerializeField] private Texture2D weaponPreviewOverride;
        [SerializeField] private string weaponPreviewResourcePath =
            WeaponInventoryCardPresentation.TemporaryImageResourceKey;

        private InventoryLoadoutScreenController controller;
        private PlayerLoadoutLive runtime;
        private Texture2D preview;
        private bool previewResolved;
        private bool bound;
        private Vector2 mountScroll;
        private Vector2 weaponScroll;
        private GUIStyle titleStyle;
        private GUIStyle smallStyle;

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

            InventoryLoadoutScreenController target =
                UnityEngine.Object.FindFirstObjectByType<
                    InventoryLoadoutScreenController>(
                    FindObjectsInactive.Include);
            if (target == null) return;

            InventoryWeaponCards presenter =
                target.GetComponent<InventoryWeaponCards>();
            if (presenter == null)
            {
                presenter = target.gameObject.AddComponent<
                    InventoryWeaponCards>();
            }
            presenter.controller = target;
        }

        private void Awake()
        {
            controller = GetComponent<InventoryLoadoutScreenController>();
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
                controller = GetComponent<InventoryLoadoutScreenController>();
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
            InventoryLoadoutScreenController target,
            PlayerLoadoutLive currentRuntime)
        {
            return Bind(target, currentRuntime);
        }

        private bool Bind(
            InventoryLoadoutScreenController target,
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
            WeaponMountLoadoutRegistry.Register(
                runtime.WeaponHoldings,
                runtime.MountLoadoutAuthority);
            controller.ConnectCanonicalAuthorities(
                runtime.Holdings,
                runtime.CatalogBridge,
                runtime.WeaponHoldings,
                runtime.LoadoutAuthority,
                runtime.MountLayout,
                runtime.WeaponCatalog);
            controller.ConfigureWeaponPresentation(
                runtime.EquipmentCatalog,
                runtime.WeaponCatalog);
            controller.Present(HubRoute.Inventory, payload);
            bound = controller.CanonicalSnapshot != null;
            if (bound) controller.enabled = false;
            return bound;
        }

        private void OnGUI()
        {
            WeaponInventorySnapshot snapshot =
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
            DrawWeapons(snapshot);
            GUILayout.EndHorizontal();

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

        private void DrawMounts(WeaponInventorySnapshot snapshot)
        {
            GUILayout.BeginVertical(GUILayout.Width(280f));
            GUILayout.Label("WEAPON MOUNTS", titleStyle);
            mountScroll = GUILayout.BeginScrollView(mountScroll);
            for (int index = 0; index < snapshot.Mounts.Count; index++)
            {
                WeaponInventoryMount mount =
                    snapshot.Mounts[index];
                bool selected = mount.Position.LoadoutSlotStableId
                    == controller.ActiveSlotStableId;
                bool locked = mount.Position.Availability
                    != WeaponMountAvailability.Active;
                string equipped = mount.EquippedCard == null
                    ? "EMPTY"
                    : mount.EquippedCard.DisplayName;
                if (GUILayout.Button(
                    (selected ? "▶ " : string.Empty)
                    + mount.Position.DisplayName
                    + "\n"
                    + (locked
                        ? "LOCKED — SKILL REQUIRED"
                        : equipped),
                    GUILayout.MinHeight(72f)))
                {
                    controller.SelectSlot(
                        mount.Position.LoadoutSlotStableId);
                }
            }
            GUILayout.EndScrollView();
            GUILayout.EndVertical();
        }

        private void DrawWeapons(
            WeaponInventorySnapshot snapshot)
        {
            GUILayout.BeginVertical();
            GUILayout.Label("OWNED WEAPONS", titleStyle);
            weaponScroll = GUILayout.BeginScrollView(weaponScroll);
            for (int index = 0;
                 index < snapshot.OwnedWeapons.Count;
                 index++)
            {
                DrawCard(snapshot, snapshot.OwnedWeapons[index]);
                GUILayout.Space(6f);
            }
            GUILayout.EndScrollView();
            GUILayout.EndVertical();
        }

        private void DrawCard(
            WeaponInventorySnapshot snapshot,
            WeaponInventoryCard card)
        {
            WeaponInventoryCardPresentation info;
            string error;
            bool resolved = WeaponInventoryCardPresentation.TryCreate(
                runtime.WeaponCatalog,
                card.Instance.WeaponDefinitionId.Value,
                out info,
                out error);

            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.BeginHorizontal();
            DrawPreview();
            GUILayout.Space(10f);
            GUILayout.BeginVertical();
            GUILayout.Label(card.DisplayName, titleStyle);
            GUILayout.Label(
                card.Instance.WeaponDefinitionId.Value
                + (string.IsNullOrEmpty(card.Family)
                    ? string.Empty
                    : "  •  " + card.Family),
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
                GUILayout.Label(error, smallStyle);
            }
            GUILayout.Label(
                card.IsEquipped
                    ? "EQUIPPED — " + card.EquippedMountId
                    : "UNEQUIPPED",
                smallStyle);
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();

            WeaponInventoryMount active =
                snapshot.FindMount(controller.ActiveSlotStableId);
            GUI.enabled = active != null
                && active.Position.Availability
                    == WeaponMountAvailability.Active;
            if (GUILayout.Button(
                "EQUIP TO ACTIVE MOUNT",
                GUILayout.MinHeight(36f)))
            {
                controller.SelectInstance(card.Instance.InstanceId);
            }
            GUI.enabled = true;
            GUILayout.EndVertical();
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
            if (weaponPreviewOverride != null)
            {
                preview = weaponPreviewOverride;
                return true;
            }
            if (previewResolved) return preview != null;

            previewResolved = true;
            string path = string.IsNullOrWhiteSpace(
                    weaponPreviewResourcePath)
                ? WeaponInventoryCardPresentation
                    .TemporaryImageResourceKey
                : weaponPreviewResourcePath.Trim();
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
