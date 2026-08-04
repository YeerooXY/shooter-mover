using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using ShooterMover.Application.Flow.Game;
using ShooterMover.Application.Flow.Hub;
using ShooterMover.Application.Persistence.Composition;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Guns;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ShooterMover.UI.Hub
{
    public sealed class HubRoutePlaceholderBridge : IHubRouteDestinationBridge
    {
        public HubRoute LastRoute { get; private set; }
        public PlayerRouteProfilePayload LastPayload { get; private set; }
        public int PresentCount { get; private set; }

        public void Present(HubRoute route, PlayerRouteProfilePayload payload)
        {
            LastRoute = route;
            LastPayload = payload ?? throw new ArgumentNullException(nameof(payload));
            PresentCount++;
        }
    }

    [DefaultExecutionOrder(10000)]
    [DisallowMultipleComponent]
    public sealed class HubMenu : MonoBehaviour
    {
        private const float MaximumPanelWidth = 1040f;
        private const float MaximumPanelHeight = 760f;

        private HubNavigationActions navigation;
        private IHubRouteDestinationBridge destinationAdapter;
        private IHubRouteTransactionPort transactionPort;
        private Func<long?> moneyBalanceProvider;
        private HubNavigationResult lastNavigationResult;
        private GUIStyle titleStyle;
        private GUIStyle headingStyle;
        private GUIStyle bodyStyle;
        private GUIStyle secondaryStyle;
        private Vector2 scrollPosition;

        public HubRoute CurrentRoute
        {
            get { EnsureInitialized(); return navigation.CurrentRoute; }
        }

        public PlayerRouteProfilePayload Payload
        {
            get { EnsureInitialized(); return navigation.Payload; }
        }

        public long? DisplayedMoneyBalance
        {
            get
            {
                HubStatus status = ResolveStatus();
                if (status != null) return status.Money;
                return moneyBalanceProvider == null ? null : moneyBalanceProvider();
            }
        }

        public HubNavigationResult LastNavigationResult { get { return lastNavigationResult; } }

        public bool IsTransitionPending
        {
            get { return transactionPort != null && transactionPort.IsTransitionPending; }
        }

        public HubNavigationSnapshot ExportSnapshot()
        {
            EnsureInitialized();
            return navigation.ExportSnapshot();
        }

        private void Awake()
        {
            EnsureInitialized();
        }

        private void Update()
        {
            bool keyboardBack = Keyboard.current != null
                && (Keyboard.current.escapeKey.wasPressedThisFrame
                    || Keyboard.current.backspaceKey.wasPressedThisFrame);
            bool gamepadBack = Gamepad.current != null
                && Gamepad.current.buttonEast.wasPressedThisFrame;
            if (keyboardBack || gamepadBack) NavigateBack();
        }

        private void OnGUI()
        {
            EnsureInitialized();
            EnsureStyles();

            int priorDepth = GUI.depth;
            GUI.depth = -1000;
            GUI.Box(new Rect(0f, 0f, Screen.width, Screen.height), GUIContent.none);

            float width = Mathf.Min(MaximumPanelWidth, Mathf.Max(360f, Screen.width - 24f));
            float height = Mathf.Min(MaximumPanelHeight, Mathf.Max(300f, Screen.height - 24f));
            Rect panel = new Rect(
                (Screen.width - width) * 0.5f,
                (Screen.height - height) * 0.5f,
                width,
                height);

            GUILayout.BeginArea(panel, GUI.skin.window);
            scrollPosition = GUILayout.BeginScrollView(scrollPosition);
            GUILayout.Label("SHOOTER MOVER", titleStyle);
            GUILayout.Label("HUB", headingStyle);

            HubStatus status = ResolveStatus();
            if (status == null)
            {
                GUILayout.Label("CHARACTER DATA UNAVAILABLE", bodyStyle);
            }
            else
            {
                GUILayout.Label(
                    "CHARACTER SLOT " + status.Slot.ToString(CultureInfo.InvariantCulture)
                    + "   •   " + status.ClassName.ToUpperInvariant()
                    + "   •   LEVEL " + status.Level.ToString(CultureInfo.InvariantCulture),
                    headingStyle);
                GUILayout.Label(
                    "MONEY  " + status.Money.ToString("N0", CultureInfo.InvariantCulture)
                    + "      SCRAP  " + status.Scrap.ToString("N0", CultureInfo.InvariantCulture),
                    bodyStyle);
                GUILayout.Label(status.StrongboxSummary, secondaryStyle);
            }

            GUILayout.Space(16f);
            GUILayout.Label("GUN LOADOUT", headingStyle);
            if (status != null && status.Guns.Count > 0)
            {
                for (int index = 0; index < status.Guns.Count; index++)
                {
                    GUILayout.Label(status.Guns[index], bodyStyle);
                }
            }
            else
            {
                GUILayout.Label("NO GUNS EQUIPPED", bodyStyle);
            }

            GUILayout.Space(20f);
            GUI.enabled = !IsTransitionPending;
            GUILayout.BeginHorizontal();
            DrawButton("INVENTORY", delegate { return OpenDestination(HubRoute.Inventory); });
            DrawButton("SKILLS", delegate { return OpenDestination(HubRoute.Skills); });
            DrawButton("SHOP", delegate { return OpenDestination(HubRoute.Shop); });
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            DrawButton("CRAFTING", delegate { return OpenDestination(HubRoute.Crafting); });
            DrawButton("PLAY", delegate { return OpenDestination(HubRoute.Play); });
            GUILayout.EndHorizontal();
            GUILayout.Space(16f);
            DrawButton("BACK", NavigateBack);
            DrawButton("MAIN MENU", GoToMainMenu);
            GUI.enabled = true;

            if (IsTransitionPending)
            {
                GUILayout.Label("Loading… repeated input is locked.", secondaryStyle);
            }

            GUILayout.EndScrollView();
            GUILayout.EndArea();
            GUI.depth = priorDepth;
        }

        public void ConfigureForTests(
            PlayerRouteProfilePayload payload,
            IHubRouteDestinationBridge adapter)
        {
            navigation = new HubNavigationActions(
                payload ?? throw new ArgumentNullException(nameof(payload)));
            destinationAdapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
            transactionPort = null;
            lastNavigationResult = null;
            destinationAdapter.Present(navigation.CurrentRoute, navigation.Payload);
        }

        public void ConfigureProduction(IHubRouteTransactionPort port)
        {
            transactionPort = port ?? throw new ArgumentNullException(nameof(port));
            navigation = port.Navigation
                ?? throw new ArgumentException(
                    "The production transaction port requires the existing navigation service.",
                    nameof(port));
            destinationAdapter = null;
            lastNavigationResult = null;
        }

        public void ConfigureMoneyPresentation(Func<long?> balanceProvider)
        {
            moneyBalanceProvider = balanceProvider;
        }

        public bool OpenCharacterSelect() { return NavigateTo(HubRoute.CharacterSelect); }
        public bool ContinueToHub() { return NavigateTo(HubRoute.InventoryLoadoutHub); }

        public bool OpenDestination(HubRoute route)
        {
            if (!HubNavigationActions.IsHubDestination(route))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(route),
                    "Only Inventory, Skills, Shop, Crafting, or Play are Hub destinations.");
            }
            return NavigateTo(route);
        }

        public bool ReturnToHub()
        {
            EnsureInitialized();
            return HubNavigationActions.IsHubDestination(navigation.CurrentRoute)
                && NavigateBack();
        }

        public bool GoToMainMenu() { return NavigateTo(HubRoute.MainMenu); }

        public bool NavigateBack()
        {
            EnsureInitialized();
            if (transactionPort != null) return transactionPort.TryNavigateBack();
            lastNavigationResult = navigation.NavigateBack();
            PresentWhenChanged();
            return lastNavigationResult.Changed;
        }

        private bool NavigateTo(HubRoute route)
        {
            EnsureInitialized();
            if (transactionPort != null) return transactionPort.TryNavigateTo(route);
            lastNavigationResult = navigation.TryNavigateTo(route);
            PresentWhenChanged();
            return lastNavigationResult.Changed;
        }

        private void PresentWhenChanged()
        {
            if (lastNavigationResult != null
                && lastNavigationResult.Changed
                && destinationAdapter != null)
            {
                destinationAdapter.Present(navigation.CurrentRoute, navigation.Payload);
            }
        }

        private HubStatus ResolveStatus()
        {
            CharacterLiveGraph graph;
            FlowProfileRecord profile;
            if (!CharacterSave.TryResolveCurrent(out graph, out profile)
                || graph == null
                || graph.IsDisposed
                || graph.Character == null
                || graph.LoadoutRuntime == null)
            {
                return null;
            }

            int level = graph.ExperienceAuthority == null
                || graph.ExperienceAuthority.CurrentState == null
                ? 1
                : graph.ExperienceAuthority.CurrentState.Level;
            long money = graph.MoneyWallet == null ? 0L : graph.MoneyWallet.Balance;
            long scrap = graph.ScrapWallet == null ? 0L : graph.ScrapWallet.Balance;
            var guns = new List<string>();
            PlayerRouteProfilePayload route = graph.LoadoutRuntime.CurrentRoutePayload;
            for (int index = 0; index < route.GunSlots.Count; index++)
            {
                PlayerRouteGunSlot slot = route.GunSlots[index];
                GunItem item = slot.EquipmentInstanceStableId == null
                    ? null
                    : graph.LoadoutRuntime.GunInventory.Find(slot.EquipmentInstanceStableId);
                string name = item == null
                    ? "EMPTY"
                    : FriendlyName(item.GunDefinitionId.Value);
                guns.Add("SLOT " + (index + 1).ToString(CultureInfo.InvariantCulture)
                    + "   •   " + name.ToUpperInvariant());
            }

            return new HubStatus(
                graph.Character.SlotIndex + 1,
                FriendlyClass(graph.Character.ClassDefinitionStableId),
                level,
                money,
                scrap,
                BuildStrongboxSummary(graph.LoadoutRuntime.Holdings),
                guns);
        }

        private static string BuildStrongboxSummary(object holdings)
        {
            var counts = new SortedDictionary<int, int>();
            object snapshot = InvokeNoArguments(holdings, "ExportSnapshot");
            if (snapshot != null)
            {
                PropertyInfo[] properties = snapshot.GetType().GetProperties(
                    BindingFlags.Instance | BindingFlags.Public);
                for (int index = 0; index < properties.Length; index++)
                {
                    PropertyInfo property = properties[index];
                    if (property.Name.IndexOf("strongbox", StringComparison.OrdinalIgnoreCase) < 0)
                        continue;
                    IEnumerable values = SafeGet(property, snapshot) as IEnumerable;
                    if (values == null || values is string) continue;
                    foreach (object value in values)
                    {
                        int tier = ReadTier(value);
                        if (tier < 1) continue;
                        int count;
                        counts.TryGetValue(tier, out count);
                        counts[tier] = count + 1;
                    }
                }
            }

            if (counts.Count == 0) return "STRONGBOXES   NONE";
            var parts = new List<string>();
            foreach (KeyValuePair<int, int> pair in counts)
            {
                parts.Add("T" + pair.Key.ToString(CultureInfo.InvariantCulture)
                    + " ×" + pair.Value.ToString(CultureInfo.InvariantCulture));
            }
            return "STRONGBOXES   " + string.Join("     ", parts.ToArray());
        }

        private static int ReadTier(object value)
        {
            if (value == null) return 0;
            string[] names = { "TierNumber", "Tier", "TierStableId", "DefinitionStableId" };
            for (int index = 0; index < names.Length; index++)
            {
                PropertyInfo property = value.GetType().GetProperty(names[index]);
                object raw = property == null ? null : SafeGet(property, value);
                if (raw is int) return (int)raw;
                int parsed = ParseTrailingNumber(raw == null ? null : raw.ToString());
                if (parsed > 0) return parsed;
            }
            return 0;
        }

        private static int ParseTrailingNumber(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return 0;
            int end = value.Length - 1;
            while (end >= 0 && !char.IsDigit(value[end])) end--;
            if (end < 0) return 0;
            int start = end;
            while (start > 0 && char.IsDigit(value[start - 1])) start--;
            int parsed;
            return int.TryParse(
                value.Substring(start, end - start + 1),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out parsed)
                ? parsed
                : 0;
        }

        private static object InvokeNoArguments(object target, string methodName)
        {
            if (target == null) return null;
            MethodInfo method = target.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.Public,
                null,
                Type.EmptyTypes,
                null);
            if (method == null) return null;
            try { return method.Invoke(target, null); }
            catch (TargetInvocationException) { return null; }
        }

        private static object SafeGet(PropertyInfo property, object target)
        {
            try { return property.GetValue(target, null); }
            catch (TargetInvocationException) { return null; }
        }

        private static string FriendlyClass(StableId classId)
        {
            string value = classId == null ? string.Empty : classId.ToString();
            if (Contains(value, "medic") || Contains(value, "healer")) return "Combat Medic";
            if (Contains(value, "juggernaut") || Contains(value, "tank")
                || Contains(value, "defensive")) return "Juggernaut";
            if (Contains(value, "striker") || Contains(value, "aggressive")) return "Striker";
            return FriendlyName(value);
        }

        private static bool Contains(string value, string token)
        {
            return value.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string FriendlyName(string stableName)
        {
            if (string.IsNullOrWhiteSpace(stableName)) return "Unknown";
            string[] sections = stableName.Split('.');
            string value = sections[sections.Length - 1].Replace('-', ' ').Replace('_', ' ');
            TextInfo text = CultureInfo.InvariantCulture.TextInfo;
            return text.ToTitleCase(value.ToLowerInvariant());
        }

        private void EnsureInitialized()
        {
            if (navigation != null) return;
            PlayerRouteProfilePayload payload = PlayerRouteProfilePayload.Create(
                StableId.Parse("character.default-pilot"),
                StableId.Parse("loadout-profile.hub-session-default"),
                new List<StableId>
                {
                    StableId.Parse("equipment-instance.hub-slot-1"),
                    StableId.Parse("equipment-instance.hub-slot-2"),
                    StableId.Parse("equipment-instance.hub-slot-3"),
                    StableId.Parse("equipment-instance.hub-slot-4"),
                });
            destinationAdapter = new HubRoutePlaceholderBridge();
            navigation = new HubNavigationActions(payload);
            destinationAdapter.Present(navigation.CurrentRoute, navigation.Payload);
        }

        private static void DrawButton(string label, Func<bool> action)
        {
            if (GUILayout.Button(label, GUILayout.MinHeight(46f))) action();
        }

        private void EnsureStyles()
        {
            if (titleStyle != null) return;
            titleStyle = Style(30, FontStyle.Bold);
            headingStyle = Style(19, FontStyle.Bold);
            bodyStyle = Style(15, FontStyle.Normal);
            secondaryStyle = Style(12, FontStyle.Normal);
        }

        private static GUIStyle Style(int size, FontStyle fontStyle)
        {
            return new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = size,
                fontStyle = fontStyle,
                wordWrap = true,
            };
        }

        private sealed class HubStatus
        {
            internal HubStatus(
                int slot,
                string className,
                int level,
                long money,
                long scrap,
                string strongboxSummary,
                IReadOnlyList<string> guns)
            {
                Slot = slot;
                ClassName = className;
                Level = level;
                Money = money;
                Scrap = scrap;
                StrongboxSummary = strongboxSummary;
                Guns = guns;
            }

            internal int Slot { get; }
            internal string ClassName { get; }
            internal int Level { get; }
            internal long Money { get; }
            internal long Scrap { get; }
            internal string StrongboxSummary { get; }
            internal IReadOnlyList<string> Guns { get; }
        }
    }
}