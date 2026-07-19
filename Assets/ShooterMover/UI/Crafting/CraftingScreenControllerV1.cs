using System;
using ShooterMover.Application.Crafting.Presentation;
using ShooterMover.Application.Flow.Hub;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Progression.Context;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ShooterMover.UI.Crafting
{
    [DisallowMultipleComponent]
    public sealed class CraftingScreenControllerV1 :
        MonoBehaviour,
        IHubRouteDestinationAdapterV1
    {
        [SerializeField] private TextAsset craftingBackplateAsset;

        private ICraftingPresentationAuthorityPortV1 authority;
        private ProgressionContext progression;
        private ulong rootSeed;
        private StableId sessionId;
        private StableId runId;
        private StableId claimantId;
        private Action<PlayerRouteProfilePayloadV1> returnToHub;
        private CraftingScreenServiceV1 service;
        private PlayerRouteProfilePayloadV1 incomingPayload;
        private CraftingScreenResultV1 lastResult;
        private Texture2D backplate;
        private Vector2 recipeScroll;
        private Vector2 detailScroll;
        private bool returnDispatched;

        public CraftingScreenSnapshotV1 Snapshot
        {
            get { return service == null ? null : service.Snapshot; }
        }

        public CraftingScreenResultV1 LastResult { get { return lastResult; } }

        public PlayerRouteProfilePayloadV1 IncomingPayload
        {
            get { return incomingPayload; }
        }

        public PlayerRouteProfilePayloadV1 LastReturnedPayload
        {
            get;
            private set;
        }

        public int ReturnCount { get; private set; }

        public bool IsConfigured
        {
            get
            {
                return authority != null
                    && progression != null
                    && sessionId != null
                    && runId != null
                    && claimantId != null;
            }
        }

        public bool HasBackplateAsset
        {
            get { return craftingBackplateAsset != null; }
        }

        public void Configure(
            ICraftingPresentationAuthorityPortV1 authority,
            ProgressionContext progression,
            ulong rootSeed,
            StableId sessionId,
            StableId runId,
            StableId claimantId,
            Action<PlayerRouteProfilePayloadV1> returnToHub)
        {
            this.authority = authority
                ?? throw new ArgumentNullException(nameof(authority));
            this.progression = progression
                ?? throw new ArgumentNullException(nameof(progression));
            this.rootSeed = rootSeed;
            this.sessionId = sessionId
                ?? throw new ArgumentNullException(nameof(sessionId));
            this.runId = runId ?? throw new ArgumentNullException(nameof(runId));
            this.claimantId = claimantId
                ?? throw new ArgumentNullException(nameof(claimantId));
            this.returnToHub = returnToHub;
            if (incomingPayload != null) BuildService();
        }

        public void ConfigureDisconnected(
            Action<PlayerRouteProfilePayloadV1> returnToHub)
        {
            authority = null;
            progression = null;
            sessionId = null;
            runId = null;
            claimantId = null;
            service = null;
            this.returnToHub = returnToHub
                ?? throw new ArgumentNullException(nameof(returnToHub));
        }

        public void ConfigureForTests(
            ICraftingPresentationAuthorityPortV1 authority,
            ProgressionContext progression,
            ulong rootSeed,
            StableId sessionId,
            StableId runId,
            StableId claimantId,
            Action<PlayerRouteProfilePayloadV1> returnToHub)
        {
            Configure(
                authority,
                progression,
                rootSeed,
                sessionId,
                runId,
                claimantId,
                returnToHub);
        }

        public void ConfigureBackplateForTests(TextAsset asset)
        {
            craftingBackplateAsset = asset;
            DestroyBackplate();
        }

        public void Present(
            HubRouteV1 route,
            PlayerRouteProfilePayloadV1 payload)
        {
            if (route != HubRouteV1.Crafting)
            {
                throw new ArgumentOutOfRangeException(nameof(route));
            }

            incomingPayload = payload
                ?? throw new ArgumentNullException(nameof(payload));
            if (!payload.HasValidFingerprint())
            {
                throw new ArgumentException(
                    "Invalid route payload.",
                    nameof(payload));
            }

            returnDispatched = false;
            ReturnCount = 0;
            LastReturnedPayload = null;
            lastResult = null;
            service = IsConfigured ? BuildService() : null;
        }

        public CraftingScreenResultV1 SelectRecipe(StableId recipeId)
        {
            return service == null ? null : Set(service.SelectRecipe(recipeId));
        }

        public CraftingScreenResultV1 Craft()
        {
            return service == null ? null : Set(service.CraftSelected());
        }

        public CraftingScreenResultV1 Retry()
        {
            return service == null ? null : Set(service.RetrySelected());
        }

        public CraftingScreenResultV1 CraftAnother()
        {
            return service == null ? null : Set(service.BeginNextAttempt());
        }

        public CraftingScreenResultV1 Refresh()
        {
            return service == null ? null : Set(service.Refresh());
        }

        public CraftingScreenResultV1 Back()
        {
            if (service == null)
            {
                DispatchReturn(incomingPayload);
                return null;
            }

            CraftingScreenResultV1 result = Set(service.Back());
            if (result.Status == CraftingScreenStatusV1.Cancelled)
            {
                DispatchReturn(result.RoutePayload);
            }
            return result;
        }

        private void Update()
        {
            bool back = Keyboard.current != null
                && (Keyboard.current.escapeKey.wasPressedThisFrame
                    || Keyboard.current.backspaceKey.wasPressedThisFrame);
            back |= Gamepad.current != null
                && Gamepad.current.buttonEast.wasPressedThisFrame;
            if (back)
            {
                Back();
                return;
            }

            bool craft = Keyboard.current != null
                && Keyboard.current.enterKey.wasPressedThisFrame;
            craft |= Gamepad.current != null
                && Gamepad.current.buttonSouth.wasPressedThisFrame;
            if (craft) Craft();
        }

        private void OnGUI()
        {
            EnsureBackplate();
            if (backplate != null)
            {
                GUI.DrawTexture(
                    new Rect(0f, 0f, Screen.width, Screen.height),
                    backplate,
                    ScaleMode.ScaleAndCrop);
            }
            else
            {
                GUI.Box(
                    new Rect(0f, 0f, Screen.width, Screen.height),
                    GUIContent.none);
            }

            float width = Mathf.Min(
                1280f,
                Mathf.Max(560f, Screen.width - 28f));
            float height = Mathf.Min(
                820f,
                Mathf.Max(420f, Screen.height - 28f));
            GUILayout.BeginArea(
                new Rect(
                    (Screen.width - width) * 0.5f,
                    (Screen.height - height) * 0.5f,
                    width,
                    height),
                GUI.skin.window);
            GUILayout.Label("CRAFTING", Heading(28));
            if (service == null) DrawDisconnected();
            else DrawConnected();
            GUILayout.EndArea();
        }

        private void DrawDisconnected()
        {
            GUILayout.FlexibleSpace();
            GUILayout.Label(
                "AWAITING AUTHORITY COMPOSITION",
                Heading(18));
            GUILayout.Label(
                "The real Crafting screen and artwork are active. No fallback "
                + "scrap, inventory, generation or reward authority was created.",
                Centered());
            if (GUILayout.Button(
                "BACK TO HUB",
                GUILayout.MinHeight(44f)))
            {
                Back();
            }
            GUILayout.FlexibleSpace();
        }

        private void DrawConnected()
        {
            CraftingScreenSnapshotV1 snapshot = service.Snapshot;
            GUILayout.Label(
                "SCRAP " + snapshot.ScrapBalance
                + "  •  SCR seq " + snapshot.ScrapSequence
                + "  •  INV seq " + snapshot.HoldingsSequence,
                Centered());
            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical(
                GUILayout.Width(
                    Mathf.Min(390f, Screen.width * 0.34f)));
            GUILayout.Label("RECIPES", Heading(17));
            recipeScroll = GUILayout.BeginScrollView(recipeScroll);
            for (int index = 0; index < snapshot.Recipes.Count; index++)
            {
                DrawRecipe(snapshot.Recipes[index]);
            }
            GUILayout.EndScrollView();
            GUILayout.EndVertical();
            GUILayout.Space(10f);
            detailScroll = GUILayout.BeginScrollView(detailScroll);
            DrawDetails(
                snapshot.SelectedRecipe,
                snapshot.LastAuthorityResult);
            GUILayout.EndScrollView();
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(
                "REFRESH",
                GUILayout.MinHeight(40f)))
            {
                Refresh();
            }

            CraftingRecipeProjectionV1 selected =
                snapshot.SelectedRecipe;
            if (selected != null && selected.IsRetryPending)
            {
                if (GUILayout.Button(
                    "RETRY SAME OPERATION",
                    GUILayout.MinHeight(40f)))
                {
                    Retry();
                }
            }
            else if (selected != null && selected.IsAttemptResolved)
            {
                if (GUILayout.Button(
                    "CRAFT ANOTHER",
                    GUILayout.MinHeight(40f)))
                {
                    CraftAnother();
                }
            }
            else
            {
                GUI.enabled = selected != null && selected.CanCraft;
                if (GUILayout.Button(
                    "CRAFT",
                    GUILayout.MinHeight(40f)))
                {
                    Craft();
                }
                GUI.enabled = true;
            }

            if (GUILayout.Button(
                "BACK",
                GUILayout.MinHeight(40f)))
            {
                Back();
            }
            GUILayout.EndHorizontal();
            if (lastResult != null
                && !string.IsNullOrEmpty(lastResult.RejectionCode))
            {
                GUILayout.Label(
                    lastResult.RejectionCode,
                    Centered());
            }
        }

        private void DrawRecipe(CraftingRecipeProjectionV1 recipe)
        {
            string marker =
                service.Snapshot.SelectedRecipeStableId
                    == recipe.RecipeStableId
                ? "▶ "
                : string.Empty;
            string label = marker + recipe.TargetDisplayName
                + "\n" + recipe.Availability
                + " • " + recipe.ScrapCost + " SCRAP"
                + "\nNatural L" + recipe.NaturalDiscoveryLevel
                + " • Craft L" + recipe.CraftingUnlockLevel;
            if (GUILayout.Button(
                label,
                GUILayout.MinHeight(68f)))
            {
                SelectRecipe(recipe.RecipeStableId);
            }
        }

        private static void DrawDetails(
            CraftingRecipeProjectionV1 recipe,
            CraftingPresentationAuthorityResultV1 result)
        {
            if (recipe == null)
            {
                GUILayout.Label(
                    "NO RECIPE SELECTED",
                    Heading(18));
                return;
            }

            GUILayout.Label(recipe.TargetDisplayName, Heading(22));
            GUILayout.Label(
                "Recipe " + recipe.RecipeStableId
                + "\nTarget "
                + recipe.TargetEquipmentDefinitionStableId
                + "\nPlayer L" + recipe.CharacterLevel
                + " • Natural L" + recipe.NaturalDiscoveryLevel
                + " • Craft L" + recipe.CraftingUnlockLevel
                + "\nCost " + recipe.ScrapCost
                + " • Balance " + recipe.ScrapBalance
                + " • " + recipe.Availability,
                Centered());
            DrawEquipment(
                "DETERMINISTIC PREVIEW",
                recipe.PreviewEquipment);
            if (recipe.Command != null)
            {
                GUILayout.Label(
                    "Operation "
                    + recipe.Command.CraftTransactionStableId
                    + "\nCommand "
                    + recipe.Command.Fingerprint,
                    Small());
            }

            if (result != null)
            {
                GUILayout.Space(10f);
                GUILayout.Label(
                    "EXACT AUTHORITY RESULT: " + result.Status,
                    Heading(17));
                DrawEquipment("GENERATED", result.Equipment);
                if (!string.IsNullOrEmpty(result.RejectionCode))
                {
                    GUILayout.Label(
                        result.RejectionCode,
                        Centered());
                }
            }
        }

        private static void DrawEquipment(
            string title,
            EquipmentInstance equipment)
        {
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label(title, Heading(16));
            if (equipment == null)
            {
                GUILayout.Label("Unavailable", Centered());
            }
            else
            {
                GUILayout.Label(
                    "Definition " + equipment.DefinitionId
                    + "\nInstance " + equipment.InstanceId
                    + "\nLevel " + equipment.ItemLevel
                    + " • Quality " + equipment.QualityId
                    + " • Augments " + equipment.Augments.Count
                    + "\nFingerprint " + equipment.Fingerprint,
                    Small());
            }
            GUILayout.EndVertical();
        }

        private CraftingScreenServiceV1 BuildService()
        {
            service = new CraftingScreenServiceV1(
                incomingPayload,
                progression,
                rootSeed,
                sessionId,
                runId,
                claimantId,
                authority);
            return service;
        }

        private CraftingScreenResultV1 Set(CraftingScreenResultV1 result)
        {
            lastResult = result;
            return result;
        }

        private void DispatchReturn(PlayerRouteProfilePayloadV1 payload)
        {
            if (returnDispatched || payload == null) return;
            returnDispatched = true;
            LastReturnedPayload = payload;
            ReturnCount++;
            if (returnToHub != null) returnToHub(payload);
        }

        private void EnsureBackplate()
        {
            if (backplate != null
                || craftingBackplateAsset == null
                || craftingBackplateAsset.bytes.Length == 0)
            {
                return;
            }

            Texture2D loaded = new Texture2D(
                2,
                2,
                TextureFormat.RGBA32,
                false);
            if (ImageConversion.LoadImage(
                loaded,
                craftingBackplateAsset.bytes,
                false))
            {
                backplate = loaded;
            }
            else
            {
                Destroy(loaded);
            }
        }

        private void DestroyBackplate()
        {
            if (backplate == null) return;
            Destroy(backplate);
            backplate = null;
        }

        private void OnDestroy()
        {
            DestroyBackplate();
        }

        private static GUIStyle Heading(int size)
        {
            return new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = size,
                fontStyle = FontStyle.Bold,
                wordWrap = true,
            };
        }

        private static GUIStyle Centered()
        {
            return new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 13,
                wordWrap = true,
            };
        }

        private static GUIStyle Small()
        {
            return new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 10,
                wordWrap = true,
            };
        }
    }
}
