using System;
using System.Collections.Generic;
using UnityEngine;

namespace ShooterMover.UI.Common
{
    [DisallowMultipleComponent]
    public sealed class AugmentList : MonoBehaviour
    {
        [SerializeField] private RectTransform rowsRoot;
        [SerializeField] private AugmentRow rowPrefab;

        private readonly List<AugmentRow> rows = new List<AugmentRow>();

        public void Show(IReadOnlyList<AugmentLine> lines)
        {
            int count = lines == null ? 0 : lines.Count;
            EnsureRows(count);

            for (int index = 0; index < rows.Count; index++)
            {
                bool visible = index < count;
                rows[index].gameObject.SetActive(visible);
                if (visible)
                {
                    rows[index].Show(lines[index]);
                }
            }

            gameObject.SetActive(count > 0);
        }

        public void Clear()
        {
            Show(Array.Empty<AugmentLine>());
        }

        public void Configure(
            RectTransform root,
            AugmentRow prefab)
        {
            rowsRoot = root;
            rowPrefab = prefab;
        }

        private void EnsureRows(int count)
        {
            if (count <= rows.Count)
            {
                return;
            }
            if (rowsRoot == null || rowPrefab == null)
            {
                throw new InvalidOperationException(
                    "AugmentList requires a rows root and row prefab.");
            }

            while (rows.Count < count)
            {
                AugmentRow row = Instantiate(rowPrefab, rowsRoot);
                row.name = "AugmentRow_" + (rows.Count + 1);
                rows.Add(row);
            }
        }
    }
}
