using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using ShooterMover.Domain.Common;
using UnityEngine;

namespace ShooterMover.UI.StrongboxOpening
{
    /// <summary>
    /// Bindable grouped-owned-box view. It owns only local exact selection state.
    /// Batch resolution fails closed when the selected group cannot satisfy the requested count.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class OwnedStrongboxGroupsView : MonoBehaviour
    {
        private IReadOnlyList<OwnedStrongboxGroupPresentation> groups =
            Array.Empty<OwnedStrongboxGroupPresentation>();
        private ExactStrongboxSelection selection;
        private Vector2 scroll;

        public IReadOnlyList<OwnedStrongboxGroupPresentation> Groups
        {
            get { return groups; }
        }

        public ExactStrongboxSelection Selection { get { return selection; } }

        public StableId SelectedInstanceStableId
        {
            get { return selection == null ? null : selection.SelectedInstanceStableId; }
        }

        public OwnedStrongboxGroupPresentation SelectedGroup
        {
            get
            {
                if (selection == null || selection.SelectedInstanceStableId == null)
                {
                    return null;
                }

                for (int groupIndex = 0; groupIndex < groups.Count; groupIndex++)
                {
                    OwnedStrongboxGroupPresentation group = groups[groupIndex];
                    for (int instanceIndex = 0;
                         instanceIndex < group.Instances.Count;
                         instanceIndex++)
                    {
                        if (group.Instances[instanceIndex].InstanceStableId
                            == selection.SelectedInstanceStableId)
                        {
                            return group;
                        }
                    }
                }
                return null;
            }
        }

        public void Bind(
            IEnumerable<OwnedStrongboxGroupPresentation> immutableGroups)
        {
            if (immutableGroups == null)
            {
                throw new ArgumentNullException(nameof(immutableGroups));
            }

            var copy = new List<OwnedStrongboxGroupPresentation>();
            foreach (OwnedStrongboxGroupPresentation group in immutableGroups)
            {
                if (group == null)
                {
                    throw new ArgumentException(
                        "Owned strongbox groups cannot contain null.",
                        nameof(immutableGroups));
                }
                copy.Add(group);
            }

            groups =
                new ReadOnlyCollection<OwnedStrongboxGroupPresentation>(copy);
            selection = new ExactStrongboxSelection(groups);
            scroll = Vector2.zero;
        }

        public bool TrySelectExact(
            StableId instanceStableId,
            out string diagnostic)
        {
            if (selection == null)
            {
                diagnostic = "loot-presentation-groups-view-unbound";
                return false;
            }
            return selection.TrySelectExact(instanceStableId, out diagnostic);
        }

        public bool TryResolveBatchExact(
            int requestedCount,
            out IReadOnlyList<StableId> batch,
            out string diagnostic)
        {
            batch = Array.Empty<StableId>();
            diagnostic = string.Empty;
            if (requestedCount < 1)
            {
                diagnostic = "loot-presentation-opening-count-invalid";
                return false;
            }
            if (selection == null)
            {
                diagnostic = "loot-presentation-groups-view-unbound";
                return false;
            }

            OwnedStrongboxGroupPresentation selected = SelectedGroup;
            if (selected == null)
            {
                diagnostic = "loot-presentation-opening-selection-missing";
                return false;
            }
            if (selected.Quantity < requestedCount)
            {
                diagnostic =
                    "loot-presentation-opening-insufficient-exact-instances:"
                    + selected.Quantity.ToString(CultureInfo.InvariantCulture)
                    + "/"
                    + requestedCount.ToString(CultureInfo.InvariantCulture);
                return false;
            }

            IReadOnlyList<StableId> resolved =
                selection.ResolveBatch(requestedCount);
            if (resolved.Count != requestedCount)
            {
                diagnostic =
                    "loot-presentation-opening-exact-batch-incomplete";
                return false;
            }

            batch = resolved;
            return true;
        }

        public void DrawImGui(GUIStyle headingStyle)
        {
            GUILayout.Label(
                "OWNED BOX GROUPS — exact identities remain selectable",
                headingStyle ?? GUI.skin.label);
            scroll = GUILayout.BeginScrollView(scroll);
            for (int groupIndex = 0; groupIndex < groups.Count; groupIndex++)
            {
                OwnedStrongboxGroupPresentation group = groups[groupIndex];
                GUILayout.BeginVertical(GUI.skin.box);
                GUILayout.Label(
                    "T" + group.TierNumber.ToString(CultureInfo.InvariantCulture)
                    + "  " + group.TierLabel
                    + " x " + group.Quantity.ToString(CultureInfo.InvariantCulture),
                    headingStyle ?? GUI.skin.label);
                for (int instanceIndex = 0;
                     instanceIndex < group.Instances.Count;
                     instanceIndex++)
                {
                    StableId instanceId =
                        group.Instances[instanceIndex].InstanceStableId;
                    bool selected = instanceId == SelectedInstanceStableId;
                    if (GUILayout.Button(
                        (selected ? "> " : "  ") + instanceId,
                        GUILayout.Height(24f)))
                    {
                        string ignored;
                        TrySelectExact(instanceId, out ignored);
                    }
                }
                GUILayout.EndVertical();
            }
            GUILayout.EndScrollView();
        }
    }
}
