using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reflection;
using ShooterMover.Domain.Combat;
using ShooterMover.Domain.Common;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ShooterMover.Presentation.LoadoutPreview
{
    public enum TemporaryLoadoutMenuPhase
    {
        Browsing = 1,
        Confirmed = 2,
        Cancelled = 3,
    }

    public enum TemporaryLoadoutMenuCommand
    {
        None = 0,
        Previous = 1,
        Next = 2,
        Confirm = 3,
        Cancel = 4,
    }

    public sealed class TemporaryLoadoutChoice
    {
        private readonly StableId[] weaponIds;

        public TemporaryLoadoutChoice(
            string choiceId,
            string displayName,
            string description,
            params string[] weaponIds)
        {
            ChoiceId = StableId.Parse(choiceId);
            DisplayName = Require(displayName, nameof(displayName));
            Description = Require(description, nameof(description));

            if (weaponIds == null)
            {
                throw new ArgumentNullException(nameof(weaponIds));
            }

            if (weaponIds.Length != FourMountStatusSnapshot.SlotCount)
            {
                throw new ArgumentException(
                    "A temporary comparison loadout must contain exactly four stable slots.",
                    nameof(weaponIds));
            }

            this.weaponIds = new StableId[FourMountStatusSnapshot.SlotCount];
            for (int index = 0; index < weaponIds.Length; index++)
            {
                this.weaponIds[index] = StableId.Parse(weaponIds[index]);
            }
        }

        public StableId ChoiceId { get; }

        public string DisplayName { get; }

        public string Description { get; }

        public int SlotCount => weaponIds.Length;

        public StableId GetWeaponByStableIndex(int stableIndex)
        {
            if (stableIndex < 0 || stableIndex >= weaponIds.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(stableIndex));
            }

            return weaponIds[stableIndex];
        }

        public StableId[] CopyWeaponIds()
        {
            StableId[] copy = new StableId[weaponIds.Length];
            Array.Copy(weaponIds, copy, weaponIds.Length);
            return copy;
        }

        public string ToTraceString()
        {
            string[] slots = new string[weaponIds.Length];
            for (int index = 0; index < slots.Length; index++)
            {
                slots[index] = "S" + (index + 1) + "=" + weaponIds[index];
            }

            return ChoiceId + "[" + string.Join(";", slots) + "]";
        }

        private static string Require(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("Value is required.", parameterName);
            }

            return value;
        }
    }

    public static class TemporaryLoadoutCatalog
    {
        private static readonly TemporaryLoadoutChoice[] Values =
        {
            new TemporaryLoadoutChoice(
                "loadout.comparison-a",
                "COMPARISON A",
                "Fixed four-weapon reference set for the visible-slice room.",
                "weapon.blaster-machine-gun",
                "weapon.shotgun",
                "weapon.rocket-launcher",
                "weapon.arc-gun"),
            new TemporaryLoadoutChoice(
                "loadout.comparison-b",
                "COMPARISON B",
                "Fixed alternate ordering with ricochet coverage for comparison.",
                "weapon.blaster-machine-gun",
                "weapon.ricochet-gun",
                "weapon.arc-gun",
                "weapon.shotgun"),
            new TemporaryLoadoutChoice(
                "loadout.comparison-c",
                "COMPARISON C",
                "Fixed impact-first ordering for visible-slice comparison only.",
                "weapon.rocket-launcher",
                "weapon.shotgun",
                "weapon.ricochet-gun",
                "weapon.blaster-machine-gun"),
        };

        private static readonly ReadOnlyCollection<TemporaryLoadoutChoice> ReadOnlyValues =
            new ReadOnlyCollection<TemporaryLoadoutChoice>(Values);

        public const int DefaultIndex = 0;

        public static IReadOnlyList<TemporaryLoadoutChoice> Choices => ReadOnlyValues;
    }

    public sealed class TemporaryLoadoutMenuPresenter
    {
        private readonly IReadOnlyList<TemporaryLoadoutChoice> choices;

        public TemporaryLoadoutMenuPresenter()
            : this(TemporaryLoadoutCatalog.Choices)
        {
        }

        public TemporaryLoadoutMenuPresenter(IReadOnlyList<TemporaryLoadoutChoice> choices)
        {
            if (choices == null)
            {
                throw new ArgumentNullException(nameof(choices));
            }

            if (choices.Count < 1)
            {
                throw new ArgumentException("At least one fixed loadout choice is required.", nameof(choices));
            }

            for (int index = 0; index < choices.Count; index++)
            {
                if (choices[index] == null)
                {
                    throw new ArgumentException("Loadout choices cannot contain null.", nameof(choices));
                }
            }

            this.choices = choices;
            ResetForRestart();
        }

        public int ChoiceCount => choices.Count;

        public int SelectedIndex { get; private set; }

        public TemporaryLoadoutChoice CurrentChoice => choices[SelectedIndex];

        public TemporaryLoadoutChoice ConfirmedChoice { get; private set; }

        public TemporaryLoadoutMenuPhase Phase { get; private set; }

        public bool IsBrowsing => Phase == TemporaryLoadoutMenuPhase.Browsing;

        public bool MovePrevious()
        {
            return Move(-1);
        }

        public bool MoveNext()
        {
            return Move(1);
        }

        public bool Apply(TemporaryLoadoutMenuCommand command)
        {
            switch (command)
            {
                case TemporaryLoadoutMenuCommand.Previous:
                    return MovePrevious();
                case TemporaryLoadoutMenuCommand.Next:
                    return MoveNext();
                case TemporaryLoadoutMenuCommand.Confirm:
                    return Confirm();
                case TemporaryLoadoutMenuCommand.Cancel:
                    return Cancel();
                case TemporaryLoadoutMenuCommand.None:
                    return false;
                default:
                    throw new ArgumentOutOfRangeException(nameof(command), command, "Unknown menu command.");
            }
        }

        public bool Confirm()
        {
            if (!IsBrowsing)
            {
                return false;
            }

            ConfirmedChoice = CurrentChoice;
            Phase = TemporaryLoadoutMenuPhase.Confirmed;
            return true;
        }

        public bool Cancel()
        {
            if (!IsBrowsing)
            {
                return false;
            }

            ConfirmedChoice = null;
            Phase = TemporaryLoadoutMenuPhase.Cancelled;
            return true;
        }

        public void ResetForRestart()
        {
            SelectedIndex = TemporaryLoadoutCatalog.DefaultIndex;
            ConfirmedChoice = null;
            Phase = TemporaryLoadoutMenuPhase.Browsing;
        }

        private bool Move(int delta)
        {
            if (!IsBrowsing)
            {
                return false;
            }

            int next = (SelectedIndex + delta) % choices.Count;
            if (next < 0)
            {
                next += choices.Count;
            }

            SelectedIndex = next;
            return true;
        }
    }

    public sealed class TemporaryWeaponIdentityCue
    {
        internal TemporaryWeaponIdentityCue(
            StableId weaponId,
            string label,
            string glyph,
            string pattern,
            Color accent)
        {
            WeaponId = weaponId ?? throw new ArgumentNullException(nameof(weaponId));
            Label = label ?? throw new ArgumentNullException(nameof(label));
            Glyph = glyph ?? throw new ArgumentNullException(nameof(glyph));
            Pattern = pattern ?? throw new ArgumentNullException(nameof(pattern));
            Accent = accent;
        }

        public StableId WeaponId { get; }

        public string Label { get; }

        public string Glyph { get; }

        public string Pattern { get; }

        public Color Accent { get; }
    }

    public static class TemporaryWeaponIdentityResolver
    {
        private const string CatalogTypeName =
            "ShooterMover.ContentPackages.Weapons.Stage1Presentation.Stage1WeaponPresentationCatalog";

        private static readonly IReadOnlyDictionary<string, TemporaryWeaponIdentityCue> ByWeapon = Build();

        public static bool TryResolve(StableId weaponId, out TemporaryWeaponIdentityCue cue)
        {
            cue = null;
            return weaponId != null && ByWeapon.TryGetValue(weaponId.ToString(), out cue);
        }

        public static TemporaryWeaponIdentityCue ResolveOrUnknown(StableId weaponId)
        {
            TemporaryWeaponIdentityCue cue;
            if (TryResolve(weaponId, out cue))
            {
                return cue;
            }

            StableId safeId = weaponId ?? StableId.Parse("weapon.unknown");
            return new TemporaryWeaponIdentityCue(safeId, "UNKNOWN", "?", "MISSING", Color.gray);
        }

        private static IReadOnlyDictionary<string, TemporaryWeaponIdentityCue> Build()
        {
            Dictionary<string, TemporaryWeaponIdentityCue> result = BuildFallback();
            Type catalogType = FindType(CatalogTypeName);
            if (catalogType == null)
            {
                return new ReadOnlyDictionary<string, TemporaryWeaponIdentityCue>(result);
            }

            PropertyInfo entriesProperty = catalogType.GetProperty(
                "Entries",
                BindingFlags.Public | BindingFlags.Static);
            IEnumerable entries = entriesProperty == null ? null : entriesProperty.GetValue(null, null) as IEnumerable;
            if (entries == null)
            {
                return new ReadOnlyDictionary<string, TemporaryWeaponIdentityCue>(result);
            }

            foreach (object entry in entries)
            {
                TemporaryWeaponIdentityCue cue = ReadCue(entry);
                if (cue != null)
                {
                    result[cue.WeaponId.ToString()] = cue;
                }
            }

            return new ReadOnlyDictionary<string, TemporaryWeaponIdentityCue>(result);
        }

        private static Dictionary<string, TemporaryWeaponIdentityCue> BuildFallback()
        {
            Dictionary<string, TemporaryWeaponIdentityCue> result =
                new Dictionary<string, TemporaryWeaponIdentityCue>(StringComparer.Ordinal);
            Add(result, "weapon.blaster-machine-gun", "BLASTER", "|||", "RAPID", new Color(0.28f, 0.78f, 1f));
            Add(result, "weapon.shotgun", "SHOTGUN", "###", "SPREAD", new Color(1f, 0.72f, 0.24f));
            Add(result, "weapon.rocket-launcher", "ROCKET", "O>", "BLAST", new Color(1f, 0.34f, 0.22f));
            Add(result, "weapon.arc-gun", "ARC", "Z", "CHAIN", new Color(0.66f, 0.48f, 1f));
            Add(result, "weapon.ricochet-gun", "RICOCHET", "<>", "BOUNCE", new Color(0.42f, 1f, 0.58f));
            return result;
        }

        private static void Add(
            IDictionary<string, TemporaryWeaponIdentityCue> result,
            string weaponId,
            string label,
            string glyph,
            string pattern,
            Color accent)
        {
            StableId id = StableId.Parse(weaponId);
            result.Add(id.ToString(), new TemporaryWeaponIdentityCue(id, label, glyph, pattern, accent));
        }

        private static Type FindType(string fullName)
        {
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int index = 0; index < assemblies.Length; index++)
            {
                Type type = assemblies[index].GetType(fullName, false);
                if (type != null)
                {
                    return type;
                }
            }

            return null;
        }

        private static TemporaryWeaponIdentityCue ReadCue(object entry)
        {
            if (entry == null)
            {
                return null;
            }

            Type type = entry.GetType();
            string weaponId = Read<string>(type, entry, "WeaponId");
            string label = Read<string>(type, entry, "Label");
            string glyph = Read<string>(type, entry, "Glyph");
            string pattern = Read<string>(type, entry, "Pattern");
            Color accent = Read<Color>(type, entry, "Accent");

            StableId parsed;
            if (!StableId.TryParse(weaponId, out parsed)
                || string.IsNullOrWhiteSpace(label)
                || string.IsNullOrWhiteSpace(glyph)
                || string.IsNullOrWhiteSpace(pattern))
            {
                return null;
            }

            return new TemporaryWeaponIdentityCue(parsed, label, glyph, pattern, accent);
        }

        private static T Read<T>(Type type, object instance, string propertyName)
        {
            PropertyInfo property = type.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
            if (property == null)
            {
                return default(T);
            }

            object value = property.GetValue(instance, null);
            return value is T ? (T)value : default(T);
        }
    }

    public static class TemporaryLoadoutInputSource
    {
        public static TemporaryLoadoutMenuCommand ReadCurrent()
        {
            return Read(Keyboard.current, Gamepad.current);
        }

        public static TemporaryLoadoutMenuCommand Read(Keyboard keyboard, Gamepad gamepad)
        {
            if ((keyboard != null && (keyboard.escapeKey.wasPressedThisFrame || keyboard.backspaceKey.wasPressedThisFrame))
                || (gamepad != null && gamepad.buttonEast.wasPressedThisFrame))
            {
                return TemporaryLoadoutMenuCommand.Cancel;
            }

            if ((keyboard != null && (keyboard.enterKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame))
                || (gamepad != null && gamepad.buttonSouth.wasPressedThisFrame))
            {
                return TemporaryLoadoutMenuCommand.Confirm;
            }

            if ((keyboard != null && (keyboard.leftArrowKey.wasPressedThisFrame || keyboard.upArrowKey.wasPressedThisFrame))
                || (gamepad != null && (gamepad.dpad.left.wasPressedThisFrame
                    || gamepad.dpad.up.wasPressedThisFrame
                    || gamepad.leftShoulder.wasPressedThisFrame)))
            {
                return TemporaryLoadoutMenuCommand.Previous;
            }

            if ((keyboard != null && (keyboard.rightArrowKey.wasPressedThisFrame || keyboard.downArrowKey.wasPressedThisFrame))
                || (gamepad != null && (gamepad.dpad.right.wasPressedThisFrame
                    || gamepad.dpad.down.wasPressedThisFrame
                    || gamepad.rightShoulder.wasPressedThisFrame)))
            {
                return TemporaryLoadoutMenuCommand.Next;
            }

            return TemporaryLoadoutMenuCommand.None;
        }
    }

    [DisallowMultipleComponent]
    public sealed class TemporaryLoadoutMenuView : MonoBehaviour
    {
        [SerializeField] private bool visibleOnEnable = true;
        [SerializeField] private float panelWidth = 660f;
        [SerializeField] private float panelHeight = 390f;

        private TemporaryLoadoutMenuPresenter presenter;
        private bool visible;

        public event Action<TemporaryLoadoutChoice> Confirmed;

        public event Action Cancelled;

        public TemporaryLoadoutMenuPresenter Presenter
        {
            get
            {
                EnsurePresenter();
                return presenter;
            }
        }

        public bool Visible => visible;

        private void Awake()
        {
            EnsurePresenter();
        }

        private void OnEnable()
        {
            EnsurePresenter();
            if (visibleOnEnable)
            {
                ResetForRestart();
            }
        }

        private void Update()
        {
            if (!visible || !Presenter.IsBrowsing)
            {
                return;
            }

            ApplyCommand(TemporaryLoadoutInputSource.ReadCurrent());
        }

        public void ResetForRestart()
        {
            EnsurePresenter();
            presenter.ResetForRestart();
            visible = true;
        }

        public void Hide()
        {
            visible = false;
        }

        public bool ApplyCommand(TemporaryLoadoutMenuCommand command)
        {
            if (!visible || command == TemporaryLoadoutMenuCommand.None)
            {
                return false;
            }

            bool changed = Presenter.Apply(command);
            if (!changed)
            {
                return false;
            }

            if (Presenter.Phase == TemporaryLoadoutMenuPhase.Confirmed)
            {
                visible = false;
                Action<TemporaryLoadoutChoice> handler = Confirmed;
                if (handler != null)
                {
                    handler(Presenter.ConfirmedChoice);
                }
            }
            else if (Presenter.Phase == TemporaryLoadoutMenuPhase.Cancelled)
            {
                visible = false;
                Action handler = Cancelled;
                if (handler != null)
                {
                    handler();
                }
            }

            return true;
        }

        private void OnGUI()
        {
            if (!visible || !Presenter.IsBrowsing)
            {
                return;
            }

            float width = Mathf.Min(panelWidth, Screen.width - 32f);
            float height = Mathf.Min(panelHeight, Screen.height - 32f);
            Rect area = new Rect(
                (Screen.width - width) * 0.5f,
                (Screen.height - height) * 0.5f,
                width,
                height);

            GUILayout.BeginArea(area, GUI.skin.window);
            GUILayout.Label("TEMPORARY LOADOUT PREVIEW");
            GUILayout.Label("Choose one complete fixed four-slot comparison set before room entry.");
            GUILayout.Space(10f);

            TemporaryLoadoutChoice choice = Presenter.CurrentChoice;
            GUILayout.Label(
                (Presenter.SelectedIndex + 1) + " / " + Presenter.ChoiceCount + "   " + choice.DisplayName);
            GUILayout.Label(choice.Description);
            GUILayout.Space(10f);

            for (int stableIndex = 0; stableIndex < choice.SlotCount; stableIndex++)
            {
                StableId weaponId = choice.GetWeaponByStableIndex(stableIndex);
                TemporaryWeaponIdentityCue cue = TemporaryWeaponIdentityResolver.ResolveOrUnknown(weaponId);
                GUILayout.BeginHorizontal(GUI.skin.box);
                GUILayout.Label("S" + (stableIndex + 1), GUILayout.Width(36f));
                GUILayout.Label(cue.Glyph, GUILayout.Width(48f));
                GUILayout.Label(cue.Label, GUILayout.Width(150f));
                GUILayout.Label(cue.Pattern, GUILayout.Width(90f));
                GUILayout.Label(weaponId.ToString());
                GUILayout.EndHorizontal();
            }

            GUILayout.FlexibleSpace();
            GUILayout.Label("Keyboard: arrows, Enter/Space, Esc/Backspace");
            GUILayout.Label("Controller: D-pad/shoulders, South confirm, East cancel");
            GUILayout.EndArea();
        }

        private void EnsurePresenter()
        {
            if (presenter == null)
            {
                presenter = new TemporaryLoadoutMenuPresenter();
            }
        }
    }
}
