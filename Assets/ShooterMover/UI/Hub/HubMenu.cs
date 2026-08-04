using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using ShooterMover.Application.Flow.Game;
using ShooterMover.Application.Flow.Hub;
using ShooterMover.Application.Guns.Catalog;
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
        private GUIStyle sectionStyle;
        private GUIStyle leftHeadingStyle;
        private GUIStyle leftBodyStyle;
        private GUIStyle levelNumberStyle;
        private GUIStyle progressTextStyle;
        private GUIStyle weaponNameStyle;
        private GUIStyle weaponSlotStyle;
        private GUIStyle rightSecondaryStyle;
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
            GUILayout.Label("COMMAND HUB", secondaryStyle);
            GUILayout.Space(10f);

            HubStatus status = ResolveStatus();
            if (status == null)
            {
                GUILayout.BeginVertical(sectionStyle);
                GUILayout.Label("CHARACTER DATA UNAVAILABLE", headingStyle);
                GUILayout.Label(
                    "Return to Character Select and choose a character before entering the Hub.",
                    bodyStyle);
                GUILayout.EndVertical();
            }
            else
            {
                DrawCharacterCard(status);
                GUILayout.Space(12f);
                DrawLoadout(status);
            }

            GUILayout.Space(16f);
            DrawNavigation();

            if (IsTransitionPending)
            {
                GUILayout.Space(8f);
                GUILayout.Label("Loading… repeated input is locked.", secondaryStyle);
            }

            GUILayout.EndScrollView();
            GUILayout.EndArea();
            GUI.depth = priorDepth;
        }

        private void DrawCharacterCard(HubStatus status)
        {
            GUILayout.BeginVertical(sectionStyle);
            GUILayout.BeginHorizontal();

            GUILayout.BeginVertical();
            GUILayout.Label(
                "CHARACTER SLOT " + status.Slot.ToString(CultureInfo.InvariantCulture),
                secondaryStyle);
            GUILayout.Label(status.ClassName.ToUpperInvariant(), leftHeadingStyle);
            GUILayout.EndVertical();

            GUILayout.FlexibleSpace();
            GUILayout.BeginVertical(GUILayout.Width(160f));
            GUILayout.Label("LEVEL", secondaryStyle);
            GUILayout.Label(
                status.Level.ToString(CultureInfo.InvariantCulture),
                levelNumberStyle);
            GUILayout.EndVertical();

            GUILayout.EndHorizontal();
            GUILayout.Space(8f);
            DrawExperience(status);
            GUILayout.Space(12f);

            GUILayout.BeginHorizontal();
            GUILayout.Label(
                "MONEY  " + status.Money.ToString("N0", CultureInfo.InvariantCulture),
                leftBodyStyle);
            GUILayout.FlexibleSpace();
            GUILayout.Label(
                "SCRAP  " + status.Scrap.ToString("N0", CultureInfo.InvariantCulture),
                leftBodyStyle);
            GUILayout.EndHorizontal();
            GUILayout.Label(status.StrongboxSummary, secondaryStyle);
            GUILayout.EndVertical();
        }

        private void DrawExperience(HubStatus status)
        {
            Rect progressRect = GUILayoutUtility.GetRect(
                10f,
                24f,
                GUILayout.ExpandWidth(true));
            GUI.Box(progressRect, GUIContent.none);

            float progress = status.IsAtLevelCap
                ? 1f
                : status.ExperienceRequiredForNextLevel <= 0L
                    ? 0f
                    : Mathf.Clamp01(
                        (float)status.ExperienceIntoCurrentLevel
                        / status.ExperienceRequiredForNextLevel);
            if (progress > 0f)
            {
                Rect fillRect = new Rect(
                    progressRect.x + 2f,
                    progressRect.y + 2f,
                    Mathf.Max(0f, (progressRect.width - 4f) * progress),
                    Mathf.Max(0f, progressRect.height - 4f));
                GUI.Box(fillRect, GUIContent.none, GUI.skin.button);
            }

            string progressText = status.IsAtLevelCap
                ? "MAX LEVEL   •   TOTAL XP "
                    + status.CumulativeExperience.ToString(
                        "N0",
                        CultureInfo.InvariantCulture)
                : "XP  "
                    + status.ExperienceIntoCurrentLevel.ToString(
                        "N0",
                        CultureInfo.InvariantCulture)
                    + " / "
                    + status.ExperienceRequiredForNextLevel.ToString(
                        "N0",
                        CultureInfo.InvariantCulture);
            GUI.Label(progressRect, progressText, progressTextStyle);

            if (!status.IsAtLevelCap)
            {
                GUILayout.Label(
                    status.ExperienceToNextLevel.ToString(
                        "N0",
                        CultureInfo.InvariantCulture)
                    + " XP TO LEVEL "
                    + (status.Level + 1).ToString(CultureInfo.InvariantCulture),
                    secondaryStyle);
            }
        }

        private void DrawLoadout(HubStatus status)
        {
            GUILayout.BeginVertical(sectionStyle);
            GUILayout.BeginHorizontal();
            GUILayout.Label("GUN LOADOUT", leftHeadingStyle);
            GUILayout.FlexibleSpace();
            GUILayout.Label(
                status.EquippedGunCount.ToString(CultureInfo.InvariantCulture)
                + " / "
                + status.Guns.Count.ToString(CultureInfo.InvariantCulture)
                + " EQUIPPED",
                rightSecondaryStyle);
            GUILayout.EndHorizontal();
            GUILayout.Space(6f);

            if (status.Guns.Count == 0)
            {
                GUILayout.Label("NO GUN MOUNTS AVAILABLE", bodyStyle);
            }
            else
            {
                for (int index = 0; index < status.Guns.Count; index++)
                {
                    DrawGunCard(status.Guns[index]);
                    if (index < status.Guns.Count - 1)
                    {
                        GUILayout.Space(4f);
                    }
                }
            }

            GUILayout.EndVertical();
        }

        private void DrawGunCard(HubGunStatus gun)
        {
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.BeginHorizontal();

            GUILayout.BeginVertical(GUILayout.Width(115f));
            GUILayout.Label(
                "WEAPON " + gun.Number.ToString(CultureInfo.InvariantCulture),
                weaponSlotStyle);
            GUILayout.Label(gun.MountName.ToUpperInvariant(), secondaryStyle);
            GUILayout.EndVertical();

            GUILayout.BeginVertical();
            GUILayout.Label(
                gun.IsEquipped
                    ? gun.DisplayName.ToUpperInvariant()
                    : "EMPTY SLOT",
                weaponNameStyle);
            GUILayout.Label(
                gun.IsEquipped
                    ? "READY"
                    : "Open Inventory to equip a weapon.",
                gun.IsEquipped ? leftBodyStyle : secondaryStyle);
            GUILayout.EndVertical();

            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
        }

        private void DrawNavigation()
        {
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
            GUILayout.Space(12f);
            DrawButton("BACK", NavigateBack);
            DrawButton("MAIN MENU", GoToMainMenu);
            GUI.enabled = true;
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

            var experience = graph.ExperienceAuthority == null
                ? null
                : graph.ExperienceAuthority.CurrentState;
            int level = experience == null ? 1 : experience.Level;
            long experienceIntoLevel = experience == null
                ? 0L
                : experience.ExperienceIntoCurrentLevel;
            long experienceRequired = experience == null
                ? 0L
                : experience.ExperienceRequiredForNextLevel;
            long experienceToNextLevel = experience == null
                ? 0L
                : experience.ExperienceToNextLevel;
            long cumulativeExperience = experience == null
                ? 0L
                : experience.CumulativeExperience;
            bool isAtLevelCap = experience != null && experience.IsAtLevelCap;
            long money = graph.MoneyWallet == null ? 0L : graph.MoneyWallet.Balance;
            long scrap = graph.ScrapWallet == null ? 0L : graph.ScrapWallet.Balance;

            PlayerRouteProfilePayload route =
                graph.LoadoutRuntime.CurrentRoutePayload;
            GunSlots layout = graph.LoadoutRuntime.MountLayout;
            var guns = new List<HubGunStatus>();
            int equippedGunCount = 0;
            for (int index = 0;
                 index < layout.ConfigurablePositions.Count;
                 index++)
            {
                GunSlot position = layout.ConfigurablePositions[index];
                PlayerRouteGunSlot routeSlot = FindRouteSlot(
                    route,
                    position.LoadoutSlotStableId);
                GunItem item = routeSlot == null
                    || routeSlot.EquipmentInstanceStableId == null
                    ? null
                    : graph.LoadoutRuntime.GunInventory.Find(
                        routeSlot.EquipmentInstanceStableId);
                if (item != null)
                {
                    equippedGunCount++;
                }

                guns.Add(new HubGunStatus(
                    index + 1,
                    position.DisplayName,
                    item == null ? string.Empty : ResolveGunName(item),
                    item != null));
            }

            return new HubStatus(
                graph.Character.SlotIndex + 1,
                FriendlyClass(graph.Character.ClassDefinitionStableId),
                level,
                experienceIntoLevel,
                experienceRequired,
                experienceToNextLevel,
                cumulativeExperience,
                isAtLevelCap,
                money,
                scrap,
                BuildStrongboxSummary(graph.LoadoutRuntime.Holdings),
                guns,
                equippedGunCount);
        }

        private static PlayerRouteGunSlot FindRouteSlot(
            PlayerRouteProfilePayload route,
            StableId loadoutSlotId)
        {
            if (route == null || loadoutSlotId == null)
            {
                return null;
            }

            for (int index = 0; index < route.GunSlots.Count; index++)
            {
                PlayerRouteGunSlot slot = route.GunSlots[index];
                if (slot.GunSlotStableId == loadoutSlotId)
                {
                    return slot;
                }
            }

            return null;
        }

        private static string ResolveGunName(GunItem item)
        {
            if (item == null || item.GunDefinitionId == null)
            {
                return "Unknown Weapon";
            }

            GunMark mark;
            if (AuthoredGunCatalogue.Current.TryGetMark(
                    item.GunDefinitionId.Value,
                    out mark)
                && mark != null
                && mark.Blueprint != null
                && !string.IsNullOrWhiteSpace(mark.Blueprint.DisplayName))
            {
                return mark.Blueprint.DisplayName.Trim();
            }

            return FriendlyGunName(item.GunDefinitionId.Value);
        }

        private static string FriendlyGunName(string definitionId)
        {
            if (string.IsNullOrWhiteSpace(definitionId))
            {
                return "Unknown Weapon";
            }

            string[] sections = definitionId.Split('.');
            var words = new List<string>();
            for (int sectionIndex = 0;
                 sectionIndex < sections.Length;
                 sectionIndex++)
            {
                string[] sectionWords = sections[sectionIndex]
                    .Replace('-', ' ')
                    .Replace('_', ' ')
                    .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                for (int wordIndex = 0;
                     wordIndex < sectionWords.Length;
                     wordIndex++)
                {
                    words.Add(FriendlyGunWord(sectionWords[wordIndex]));
                }
            }

            return words.Count == 0
                ? "Unknown Weapon"
                : string.Join(" ", words.ToArray());
        }

        private static string FriendlyGunWord(string word)
        {
            if (string.Equals(word, "mk1", StringComparison.OrdinalIgnoreCase))
                return "MK I";
            if (string.Equals(word, "mk2", StringComparison.OrdinalIgnoreCase))
                return "MK II";
            if (string.Equals(word, "mk3", StringComparison.OrdinalIgnoreCase))
                return "MK III";

            return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(
                word.ToLowerInvariant());
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

            titleStyle = Style(30, FontStyle.Bold, TextAnchor.MiddleCenter);
            headingStyle = Style(19, FontStyle.Bold, TextAnchor.MiddleCenter);
            bodyStyle = Style(15, FontStyle.Normal, TextAnchor.MiddleCenter);
            secondaryStyle = Style(12, FontStyle.Normal, TextAnchor.MiddleCenter);
            leftHeadingStyle = Style(20, FontStyle.Bold, TextAnchor.MiddleLeft);
            leftBodyStyle = Style(14, FontStyle.Normal, TextAnchor.MiddleLeft);
            levelNumberStyle = Style(32, FontStyle.Bold, TextAnchor.MiddleCenter);
            progressTextStyle = Style(12, FontStyle.Bold, TextAnchor.MiddleCenter);
            weaponNameStyle = Style(17, FontStyle.Bold, TextAnchor.MiddleLeft);
            weaponSlotStyle = Style(13, FontStyle.Bold, TextAnchor.MiddleLeft);
            rightSecondaryStyle = Style(12, FontStyle.Normal, TextAnchor.MiddleRight);
            sectionStyle = new GUIStyle(GUI.skin.box)
            {
                padding = new RectOffset(18, 18, 14, 14),
                margin = new RectOffset(8, 8, 4, 4),
            };
        }

        private static GUIStyle Style(
            int size,
            FontStyle fontStyle,
            TextAnchor alignment)
        {
            return new GUIStyle(GUI.skin.label)
            {
                alignment = alignment,
                fontSize = size,
                fontStyle = fontStyle,
                wordWrap = true,
            };
        }

        private sealed class HubGunStatus
        {
            internal HubGunStatus(
                int number,
                string mountName,
                string displayName,
                bool isEquipped)
            {
                Number = number;
                MountName = mountName ?? string.Empty;
                DisplayName = displayName ?? string.Empty;
                IsEquipped = isEquipped;
            }

            internal int Number { get; }
            internal string MountName { get; }
            internal string DisplayName { get; }
            internal bool IsEquipped { get; }
        }

        private sealed class HubStatus
        {
            internal HubStatus(
                int slot,
                string className,
                int level,
                long experienceIntoCurrentLevel,
                long experienceRequiredForNextLevel,
                long experienceToNextLevel,
                long cumulativeExperience,
                bool isAtLevelCap,
                long money,
                long scrap,
                string strongboxSummary,
                IReadOnlyList<HubGunStatus> guns,
                int equippedGunCount)
            {
                Slot = slot;
                ClassName = className;
                Level = level;
                ExperienceIntoCurrentLevel = experienceIntoCurrentLevel;
                ExperienceRequiredForNextLevel = experienceRequiredForNextLevel;
                ExperienceToNextLevel = experienceToNextLevel;
                CumulativeExperience = cumulativeExperience;
                IsAtLevelCap = isAtLevelCap;
                Money = money;
                Scrap = scrap;
                StrongboxSummary = strongboxSummary;
                Guns = guns;
                EquippedGunCount = equippedGunCount;
            }

            internal int Slot { get; }
            internal string ClassName { get; }
            internal int Level { get; }
            internal long ExperienceIntoCurrentLevel { get; }
            internal long ExperienceRequiredForNextLevel { get; }
            internal long ExperienceToNextLevel { get; }
            internal long CumulativeExperience { get; }
            internal bool IsAtLevelCap { get; }
            internal long Money { get; }
            internal long Scrap { get; }
            internal string StrongboxSummary { get; }
            internal IReadOnlyList<HubGunStatus> Guns { get; }
            internal int EquippedGunCount { get; }
        }
    }
}
