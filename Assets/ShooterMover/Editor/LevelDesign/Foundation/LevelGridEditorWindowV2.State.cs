#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using ShooterMover.UnityAdapters.Authoring.LevelDesign;
using UnityEditor;
using UnityEngine;

namespace ShooterMover.Editor.LevelDesign.Foundation
{
    public sealed partial class LevelGridEditorWindowV2 : EditorWindow
    {

        private void ShowRootMenu()
        {
            GenericMenu menu = new GenericMenu();
            LevelDesignSceneAuthoringRoot2D[] roots =
                Resources.FindObjectsOfTypeAll<LevelDesignSceneAuthoringRoot2D>();
            int added = 0;
            for (int index = 0; index < roots.Length; index++)
            {
                LevelDesignSceneAuthoringRoot2D root = roots[index];
                if (root == null
                    || !root.gameObject.scene.IsValid()
                    || EditorUtility.IsPersistent(root))
                {
                    continue;
                }

                added++;
                string label = root.LevelIdText + " — "
                    + LevelGridEditorProblemLocatorV2.BuildDiagnosticLocation(
                        root.transform);
                menu.AddItem(
                    new GUIContent(label),
                    root == activeRoot,
                    delegate(object selected)
                    {
                        SetActiveRoot(
                            (LevelDesignSceneAuthoringRoot2D)selected);
                    },
                    root);
            }

            if (added == 0)
            {
                menu.AddDisabledItem(new GUIContent("No scene level roots found"));
            }
            menu.ShowAsContext();
        }

        private void SetActiveRoot(LevelDesignSceneAuthoringRoot2D root)
        {
            if (activeRoot == root)
            {
                return;
            }

            SaveViewState();
            activeRoot = root;
            selectedProblem = null;
            selectedAuthoringObject = root;
            projectionDirty = true;
            if (root == null)
            {
                EditorPrefs.DeleteKey(RootPreferenceKey);
            }
            else
            {
                GlobalObjectId globalId = GlobalObjectId.GetGlobalObjectIdSlow(root);
                EditorPrefs.SetString(RootPreferenceKey, globalId.ToString());
            }
            LoadViewState();
            EnsureProjection();
            Repaint();
        }

        private void TryAdoptSelectionRoot()
        {
            GameObject selected = Selection.activeGameObject;
            if (selected == null)
            {
                return;
            }
            LevelDesignSceneAuthoringRoot2D root =
                selected.GetComponentInParent<LevelDesignSceneAuthoringRoot2D>();
            if (root != null)
            {
                SetActiveRoot(root);
            }
        }

        private void RestorePersistedRoot()
        {
            string persisted = EditorPrefs.GetString(RootPreferenceKey, string.Empty);
            GlobalObjectId globalId;
            if (string.IsNullOrEmpty(persisted)
                || !GlobalObjectId.TryParse(persisted, out globalId))
            {
                return;
            }

            activeRoot = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(globalId)
                as LevelDesignSceneAuthoringRoot2D;
        }

        private void OnUndoRedo()
        {
            RequestRefresh(true);
        }

        private void OnHierarchyChanged()
        {
            RequestRefresh(true);
        }

        private void OnObjectChangesPublished(ref ObjectChangeEventStream stream)
        {
            if (activeRoot != null)
            {
                RequestRefresh(true);
            }
        }

        private void OnUnitySelectionChanged()
        {
            if (suppressSelectionSync)
            {
                return;
            }

            UnityEngine.Object selected = ResolveAuthoringSelection(Selection.activeObject);
            Component selectedComponent = selected as Component;
            if (selectedComponent != null
                && LevelGridEditorOperationsV2.ResolveRoot(selectedComponent) == activeRoot)
            {
                selectedAuthoringObject = selected;
                selectedProblem = null;
                Repaint();
                return;
            }

            LevelDesignSceneAuthoringRoot2D selectedRoot =
                selected as LevelDesignSceneAuthoringRoot2D;
            if (selectedRoot != null)
            {
                SetActiveRoot(selectedRoot);
            }
        }

        private void OnEditorUpdate()
        {
            if (!string.IsNullOrEmpty(notificationMessage)
                && EditorApplication.timeSinceStartup <= notificationUntil)
            {
                Repaint();
            }
        }

        private void RequestRefresh(bool queueValidation)
        {
            projectionDirty = true;
            if (queueValidation && activeRoot != null && !validationQueued)
            {
                validationQueued = true;
                EditorApplication.delayCall += RunQueuedDraftValidation;
            }
            Repaint();
        }

        private void RunQueuedDraftValidation()
        {
            validationQueued = false;
            if (activeRoot == null)
            {
                return;
            }
            activeRoot.ValidateHierarchy();
            activeRoot.ValidateGridAuthoring(LevelGridValidationPurposeV2.Draft);
            projectionDirty = true;
            Repaint();
        }

        private void EnsureProjection()
        {
            if (!projectionDirty)
            {
                return;
            }
            projection = LevelGridEditorProjectionV2.Build(activeRoot);
            projectionDirty = false;
        }

        private void SetSelectedAuthoringObject(UnityEngine.Object selected)
        {
            selectedAuthoringObject = selected;
            selectedProblem = null;
            suppressSelectionSync = true;
            Selection.activeObject = selected;
            suppressSelectionSync = false;
            Repaint();
        }

        private UnityEngine.Object ResolveAuthoringSelection(UnityEngine.Object selected)
        {
            if (selected is LevelDoorEndpointAuthoring2D
                || selected is LevelDoorLinkAuthoring2D
                || selected is LevelRoomAuthoring2D
                || selected is LevelDesignSceneAuthoringRoot2D)
            {
                return selected;
            }

            GameObject gameObject = selected as GameObject;
            if (gameObject == null)
            {
                Component component = selected as Component;
                gameObject = component == null ? null : component.gameObject;
            }
            if (gameObject == null)
            {
                return null;
            }

            LevelDoorEndpointAuthoring2D door =
                gameObject.GetComponent<LevelDoorEndpointAuthoring2D>();
            if (door != null)
            {
                return door;
            }
            LevelDoorLinkAuthoring2D connection =
                gameObject.GetComponent<LevelDoorLinkAuthoring2D>();
            if (connection != null)
            {
                return connection;
            }
            LevelRoomAuthoring2D room =
                gameObject.GetComponentInParent<LevelRoomAuthoring2D>();
            if (room != null)
            {
                return room;
            }
            return gameObject.GetComponentInParent<LevelDesignSceneAuthoringRoot2D>();
        }

        private Rect GetRoomRect(
            LevelRoomAuthoring2D room,
            Vector2Int coordinate)
        {
            Vector2Int footprint = room.FootprintCells;
            Vector2 topLeftGrid = new Vector2(
                coordinate.x,
                coordinate.y + Mathf.Max(1, footprint.y));
            Vector2 topLeft = GridToScreenContinuous(topLeftGrid);
            return new Rect(
                topLeft.x + RoomMargin * zoom,
                topLeft.y + RoomMargin * zoom,
                Mathf.Max(42f, footprint.x * RoomCellWidth * zoom
                    - RoomMargin * 2f * zoom),
                Mathf.Max(42f, footprint.y * RoomCellHeight * zoom
                    - RoomMargin * 2f * zoom));
        }

        private Vector2 ResolveDoorScreenPosition(
            LevelDoorEndpointAuthoring2D door,
            Rect roomRect)
        {
            if (door.PlacementMode == LevelDoorPlacementModeV2.Fixed
                && door.OwningRoom != null
                && door.OwningRoom.RoomBounds != null)
            {
                Bounds bounds = door.OwningRoom.RoomBounds.bounds;
                Vector3 world = door.transform.position;
                float x = Mathf.InverseLerp(bounds.min.x, bounds.max.x, world.x);
                float y = Mathf.InverseLerp(bounds.min.y, bounds.max.y, world.y);
                return new Vector2(
                    Mathf.Lerp(roomRect.xMin, roomRect.xMax, x),
                    Mathf.Lerp(roomRect.yMax, roomRect.yMin, y));
            }

            float offset = Mathf.Clamp01(door.EdgeOffset);
            switch (door.Side)
            {
                case LevelDoorSideV2.North:
                    return new Vector2(
                        Mathf.Lerp(roomRect.xMin, roomRect.xMax, offset),
                        roomRect.yMin);
                case LevelDoorSideV2.East:
                    return new Vector2(
                        roomRect.xMax,
                        Mathf.Lerp(roomRect.yMax, roomRect.yMin, offset));
                case LevelDoorSideV2.South:
                    return new Vector2(
                        Mathf.Lerp(roomRect.xMin, roomRect.xMax, offset),
                        roomRect.yMax);
                default:
                    return new Vector2(
                        roomRect.xMin,
                        Mathf.Lerp(roomRect.yMax, roomRect.yMin, offset));
            }
        }

        private void ResolveNearestEdge(
            Rect roomRect,
            Vector2 mouse,
            out LevelDoorSideV2 side,
            out float offset)
        {
            float north = Mathf.Abs(mouse.y - roomRect.yMin);
            float east = Mathf.Abs(mouse.x - roomRect.xMax);
            float south = Mathf.Abs(mouse.y - roomRect.yMax);
            float west = Mathf.Abs(mouse.x - roomRect.xMin);
            float minimum = Mathf.Min(north, east, south, west);
            if (Mathf.Approximately(minimum, north))
            {
                side = LevelDoorSideV2.North;
                offset = Mathf.InverseLerp(roomRect.xMin, roomRect.xMax, mouse.x);
            }
            else if (Mathf.Approximately(minimum, east))
            {
                side = LevelDoorSideV2.East;
                offset = Mathf.InverseLerp(roomRect.yMax, roomRect.yMin, mouse.y);
            }
            else if (Mathf.Approximately(minimum, south))
            {
                side = LevelDoorSideV2.South;
                offset = Mathf.InverseLerp(roomRect.xMin, roomRect.xMax, mouse.x);
            }
            else
            {
                side = LevelDoorSideV2.West;
                offset = Mathf.InverseLerp(roomRect.yMax, roomRect.yMin, mouse.y);
            }
            offset = Mathf.Clamp01(offset);
        }

        private LevelDoorEndpointAuthoring2D HitDoor(Vector2 mouse)
        {
            foreach (KeyValuePair<LevelDoorEndpointAuthoring2D, Rect> pair in doorRects)
            {
                Rect expanded = pair.Value;
                expanded.xMin -= 4f;
                expanded.xMax += 4f;
                expanded.yMin -= 4f;
                expanded.yMax += 4f;
                if (expanded.Contains(mouse))
                {
                    return pair.Key;
                }
            }
            return null;
        }

        private LevelRoomAuthoring2D HitRoom(Vector2 mouse)
        {
            for (int index = projection.Rooms.Count - 1; index >= 0; index--)
            {
                LevelRoomAuthoring2D room = projection.Rooms[index].Room;
                Rect rect;
                if (roomRects.TryGetValue(room, out rect) && rect.Contains(mouse))
                {
                    return room;
                }
            }
            return null;
        }

        private LevelDoorLinkAuthoring2D HitLink(Vector2 mouse)
        {
            float bestDistance = 8f;
            LevelDoorLinkAuthoring2D best = null;
            foreach (KeyValuePair<LevelDoorLinkAuthoring2D, LineVisual> pair in linkLines)
            {
                float distance = DistanceToSegment(
                    mouse,
                    pair.Value.Start,
                    pair.Value.End);
                if (distance <= bestDistance)
                {
                    bestDistance = distance;
                    best = pair.Key;
                }
            }
            return best;
        }

        private bool HasProblem(string authoredId)
        {
            if (activeRoot == null || string.IsNullOrEmpty(authoredId))
            {
                return false;
            }

            IReadOnlyList<LevelGridProblemV2> problems =
                activeRoot.LastGridValidation.Problems;
            for (int index = 0; index < problems.Count; index++)
            {
                if (string.Equals(
                    problems[index].AuthoredId,
                    authoredId,
                    StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return false;
        }

        private bool HasProblem(
            string authoredId,
            LevelGridProblemCodeV2 code)
        {
            if (activeRoot == null || string.IsNullOrEmpty(authoredId))
            {
                return false;
            }

            IReadOnlyList<LevelGridProblemV2> problems =
                activeRoot.LastGridValidation.Problems;
            for (int index = 0; index < problems.Count; index++)
            {
                if (problems[index].Code == code
                    && string.Equals(
                        problems[index].AuthoredId,
                        authoredId,
                        StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return false;
        }

        private bool HasProblemOtherThan(
            string authoredId,
            LevelGridProblemCodeV2 excludedCode)
        {
            if (activeRoot == null || string.IsNullOrEmpty(authoredId))
            {
                return false;
            }

            IReadOnlyList<LevelGridProblemV2> problems =
                activeRoot.LastGridValidation.Problems;
            for (int index = 0; index < problems.Count; index++)
            {
                if (problems[index].Code != excludedCode
                    && string.Equals(
                        problems[index].AuthoredId,
                        authoredId,
                        StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return false;
        }

        private Vector2 GridToScreenContinuous(Vector2 grid)
        {
            Vector2 origin = canvasRect.center + pan;
            return origin + new Vector2(
                grid.x * RoomCellWidth * zoom,
                -grid.y * RoomCellHeight * zoom);
        }

        private Vector2 ScreenToGridContinuous(Vector2 screen)
        {
            Vector2 origin = canvasRect.center + pan;
            Vector2 delta = screen - origin;
            return new Vector2(
                delta.x / (RoomCellWidth * zoom),
                -delta.y / (RoomCellHeight * zoom));
        }

        private Vector2Int ScreenToNearestGrid(Vector2 screen)
        {
            Vector2 continuous = ScreenToGridContinuous(screen);
            return new Vector2Int(
                Mathf.RoundToInt(continuous.x),
                Mathf.RoundToInt(continuous.y));
        }

        private void ZoomAround(Vector2 mousePosition, float scrollDelta)
        {
            Vector2 before = ScreenToGridContinuous(mousePosition);
            float multiplier = Mathf.Pow(1.1f, -scrollDelta);
            zoom = Mathf.Clamp(zoom * multiplier, MinZoom, MaxZoom);
            Vector2 afterScreen = GridToScreenContinuous(before);
            pan += mousePosition - afterScreen;
            SaveViewState();
            Repaint();
        }

        private void FrameAll()
        {
            EnsureProjection();
            if (projection.Rooms.Count == 0)
            {
                pan = Vector2.zero;
                zoom = 1f;
                SaveViewState();
                Repaint();
                return;
            }

            int minX = int.MaxValue;
            int minY = int.MaxValue;
            int maxX = int.MinValue;
            int maxY = int.MinValue;
            for (int index = 0; index < projection.Rooms.Count; index++)
            {
                LevelRoomAuthoring2D room = projection.Rooms[index].Room;
                minX = Mathf.Min(minX, room.GridCoordinate.x);
                minY = Mathf.Min(minY, room.GridCoordinate.y);
                maxX = Mathf.Max(
                    maxX,
                    room.GridCoordinate.x + room.FootprintCells.x);
                maxY = Mathf.Max(
                    maxY,
                    room.GridCoordinate.y + room.FootprintCells.y);
            }
            FrameGridBounds(minX, minY, maxX, maxY);
        }

        private void FrameSelection()
        {
            LevelRoomAuthoring2D room = selectedAuthoringObject as LevelRoomAuthoring2D;
            LevelDoorEndpointAuthoring2D door =
                selectedAuthoringObject as LevelDoorEndpointAuthoring2D;
            if (door != null)
            {
                room = door.OwningRoom;
            }
            LevelDoorLinkAuthoring2D link =
                selectedAuthoringObject as LevelDoorLinkAuthoring2D;
            if (link != null)
            {
                LevelRoomAuthoring2D source = link.SourceRoom;
                LevelRoomAuthoring2D destination = link.DestinationRoom;
                if (source != null && destination != null)
                {
                    int minX = Mathf.Min(
                        source.GridCoordinate.x,
                        destination.GridCoordinate.x);
                    int minY = Mathf.Min(
                        source.GridCoordinate.y,
                        destination.GridCoordinate.y);
                    int maxX = Mathf.Max(
                        source.GridCoordinate.x + source.FootprintCells.x,
                        destination.GridCoordinate.x + destination.FootprintCells.x);
                    int maxY = Mathf.Max(
                        source.GridCoordinate.y + source.FootprintCells.y,
                        destination.GridCoordinate.y + destination.FootprintCells.y);
                    FrameGridBounds(minX, minY, maxX, maxY);
                    return;
                }
            }
            if (room == null)
            {
                FrameAll();
                return;
            }
            FrameGridBounds(
                room.GridCoordinate.x,
                room.GridCoordinate.y,
                room.GridCoordinate.x + room.FootprintCells.x,
                room.GridCoordinate.y + room.FootprintCells.y);
        }

        private void FrameGridBounds(int minX, int minY, int maxX, int maxY)
        {
            float widthCells = Mathf.Max(1f, maxX - minX);
            float heightCells = Mathf.Max(1f, maxY - minY);
            float availableWidth = Mathf.Max(100f, canvasRect.width - 120f);
            float availableHeight = Mathf.Max(100f, canvasRect.height - 100f);
            zoom = Mathf.Clamp(
                Mathf.Min(
                    availableWidth / (widthCells * RoomCellWidth),
                    availableHeight / (heightCells * RoomCellHeight)),
                MinZoom,
                MaxZoom);
            Vector2 center = new Vector2(
                (minX + maxX) * 0.5f,
                (minY + maxY) * 0.5f);
            pan = new Vector2(
                -center.x * RoomCellWidth * zoom,
                center.y * RoomCellHeight * zoom);
            SaveViewState();
            Repaint();
        }

        private void ShowNotification(string message, Vector2 positionOnCanvas)
        {
            notificationMessage = string.IsNullOrEmpty(message)
                ? "The operation was rejected."
                : message;
            notificationPosition = positionOnCanvas;
            notificationUntil = EditorApplication.timeSinceStartup + 2.8d;
            Repaint();
        }

        private void SaveViewState()
        {
            string key = ViewStateKey();
            EditorPrefs.SetFloat(key + ".PanX", pan.x);
            EditorPrefs.SetFloat(key + ".PanY", pan.y);
            EditorPrefs.SetFloat(key + ".Zoom", zoom);
        }

        private void LoadViewState()
        {
            string key = ViewStateKey();
            pan = new Vector2(
                EditorPrefs.GetFloat(key + ".PanX", 0f),
                EditorPrefs.GetFloat(key + ".PanY", 0f));
            zoom = Mathf.Clamp(
                EditorPrefs.GetFloat(key + ".Zoom", 1f),
                MinZoom,
                MaxZoom);
        }

        private string ViewStateKey()
        {
            if (activeRoot == null)
            {
                return "ShooterMover.LevelGridEditorV2.NoRoot";
            }
            GlobalObjectId globalId = GlobalObjectId.GetGlobalObjectIdSlow(activeRoot);
            return "ShooterMover.LevelGridEditorV2." + globalId;
        }

        private static void DrawRectOutline(Rect rect, Color color, float width)
        {
            EditorGUI.DrawRect(new Rect(rect.xMin, rect.yMin, rect.width, width), color);
            EditorGUI.DrawRect(
                new Rect(rect.xMin, rect.yMax - width, rect.width, width),
                color);
            EditorGUI.DrawRect(new Rect(rect.xMin, rect.yMin, width, rect.height), color);
            EditorGUI.DrawRect(
                new Rect(rect.xMax - width, rect.yMin, width, rect.height),
                color);
        }

        private static float DistanceToSegment(Vector2 point, Vector2 start, Vector2 end)
        {
            Vector2 segment = end - start;
            float lengthSquared = segment.sqrMagnitude;
            if (lengthSquared <= Mathf.Epsilon)
            {
                return Vector2.Distance(point, start);
            }
            float t = Mathf.Clamp01(Vector2.Dot(point - start, segment) / lengthSquared);
            return Vector2.Distance(point, start + segment * t);
        }

        private static string FormatCoordinate(Vector2Int coordinate)
        {
            return "(" + coordinate.x + "," + coordinate.y + ")";
        }

        private static string ShortId(string id)
        {
            if (string.IsNullOrEmpty(id) || id.Length <= 24)
            {
                return id ?? string.Empty;
            }
            return id.Substring(0, 10) + "…" + id.Substring(id.Length - 8);
        }

        private struct LineVisual
        {
            public LineVisual(Vector2 start, Vector2 end)
            {
                Start = start;
                End = end;
            }

            public Vector2 Start { get; }

            public Vector2 End { get; }
        }
    }
}
#endif
