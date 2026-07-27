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
        private void DrawCanvas(Rect rect)
        {
            EditorGUI.DrawRect(rect, new Color(0.12f, 0.12f, 0.12f, 1f));
            DrawGrid(rect);

            if (activeRoot == null)
            {
                GUI.Label(
                    new Rect(rect.x + 24f, rect.y + 24f, rect.width - 48f, 64f),
                    "Select a LevelDesignSceneAuthoringRoot2D to begin.\n"
                        + "The window never creates a hidden global root.",
                    EditorStyles.helpBox);
                HandleCanvasInput(rect);
                return;
            }

            BuildVisualCache();
            DrawConnections();
            DrawRooms();
            DrawConnectionDrag();
            DrawNotification();
            HandleCanvasInput(rect);
        }

        private void DrawGrid(Rect rect)
        {
            float cellWidth = RoomCellWidth * zoom;
            float cellHeight = RoomCellHeight * zoom;
            if (cellWidth < 12f || cellHeight < 12f)
            {
                return;
            }

            Vector2 origin = rect.center + pan;
            Handles.BeginGUI();
            Color previous = Handles.color;
            Handles.color = new Color(1f, 1f, 1f, 0.075f);

            int firstX = Mathf.FloorToInt((rect.xMin - origin.x) / cellWidth) - 1;
            int lastX = Mathf.CeilToInt((rect.xMax - origin.x) / cellWidth) + 1;
            for (int x = firstX; x <= lastX; x++)
            {
                float screenX = origin.x + x * cellWidth;
                Handles.DrawLine(
                    new Vector3(screenX, rect.yMin),
                    new Vector3(screenX, rect.yMax));
            }

            int firstY = Mathf.FloorToInt((origin.y - rect.yMax) / cellHeight) - 1;
            int lastY = Mathf.CeilToInt((origin.y - rect.yMin) / cellHeight) + 1;
            for (int y = firstY; y <= lastY; y++)
            {
                float screenY = origin.y - y * cellHeight;
                Handles.DrawLine(
                    new Vector3(rect.xMin, screenY),
                    new Vector3(rect.xMax, screenY));
            }

            Handles.color = new Color(0.3f, 0.65f, 1f, 0.5f);
            Handles.DrawLine(
                new Vector3(origin.x, rect.yMin),
                new Vector3(origin.x, rect.yMax));
            Handles.DrawLine(
                new Vector3(rect.xMin, origin.y),
                new Vector3(rect.xMax, origin.y));
            Handles.color = previous;
            Handles.EndGUI();
        }

        private void BuildVisualCache()
        {
            roomRects.Clear();
            doorRects.Clear();
            linkLines.Clear();

            for (int index = 0; index < projection.Rooms.Count; index++)
            {
                LevelGridEditorRoomProjectionV2 roomProjection = projection.Rooms[index];
                LevelRoomAuthoring2D room = roomProjection.Room;
                Vector2Int coordinate = room == draggedRoom
                    ? dragPreviewCoordinate
                    : room.GridCoordinate;
                Rect roomRect = GetRoomRect(room, coordinate);
                roomRects[room] = roomRect;
                for (int doorIndex = 0; doorIndex < roomProjection.Doors.Count; doorIndex++)
                {
                    LevelDoorEndpointAuthoring2D door = roomProjection.Doors[doorIndex];
                    Vector2 center = ResolveDoorScreenPosition(door, roomRect);
                    doorRects[door] = new Rect(
                        center.x - DoorRadius,
                        center.y - DoorRadius,
                        DoorRadius * 2f,
                        DoorRadius * 2f);
                }
            }

            for (int index = 0; index < projection.Connections.Count; index++)
            {
                LevelDoorLinkAuthoring2D link = projection.Connections[index];
                Rect sourceRect;
                Rect destinationRect;
                if (link.SourceDoor == null
                    || link.DestinationDoor == null
                    || !doorRects.TryGetValue(link.SourceDoor, out sourceRect)
                    || !doorRects.TryGetValue(link.DestinationDoor, out destinationRect))
                {
                    continue;
                }
                linkLines[link] = new LineVisual(
                    sourceRect.center,
                    destinationRect.center);
            }
        }

        private void DrawConnections()
        {
            Handles.BeginGUI();
            Color previous = Handles.color;
            foreach (KeyValuePair<LevelDoorLinkAuthoring2D, LineVisual> pair in linkLines)
            {
                bool selected = selectedAuthoringObject == pair.Key;
                bool problem = HasProblem(pair.Key.ConnectionIdText);
                Handles.color = problem
                    ? new Color(1f, 0.28f, 0.22f, 1f)
                    : selected
                        ? new Color(0.35f, 0.8f, 1f, 1f)
                        : new Color(0.72f, 0.78f, 0.86f, 0.85f);
                Handles.DrawAAPolyLine(
                    selected ? 5f : 3f,
                    pair.Value.Start,
                    pair.Value.End);

                Vector2 midpoint = Vector2.Lerp(
                    pair.Value.Start,
                    pair.Value.End,
                    0.5f);
                GUI.Label(
                    new Rect(midpoint.x - 70f, midpoint.y - 10f, 140f, 18f),
                    new GUIContent(
                        ShortId(pair.Key.ConnectionIdText),
                        pair.Key.ConnectionIdText),
                    EditorStyles.centeredGreyMiniLabel);
            }
            Handles.color = previous;
            Handles.EndGUI();
        }

        private void DrawRooms()
        {
            for (int index = 0; index < projection.Rooms.Count; index++)
            {
                LevelGridEditorRoomProjectionV2 roomProjection = projection.Rooms[index];
                LevelRoomAuthoring2D room = roomProjection.Room;
                Rect rect = roomRects[room];
                bool selected = selectedAuthoringObject == room;
                Color cardColor = roomProjection.OverlapsAnotherRoom
                    ? new Color(0.55f, 0.16f, 0.16f, 0.98f)
                    : roomProjection.HasValidationProblem
                        ? new Color(0.42f, 0.31f, 0.09f, 0.98f)
                        : selected
                            ? new Color(0.18f, 0.36f, 0.5f, 0.98f)
                            : new Color(0.19f, 0.21f, 0.24f, 0.98f);
                EditorGUI.DrawRect(rect, cardColor);
                DrawRectOutline(
                    rect,
                    selected
                        ? new Color(0.35f, 0.82f, 1f, 1f)
                        : new Color(0.55f, 0.58f, 0.62f, 1f),
                    selected ? 3f : 1f);

                float inset = Mathf.Clamp(8f * zoom, 4f, 10f);
                Rect content = new Rect(
                    rect.x + inset,
                    rect.y + inset,
                    rect.width - inset * 2f,
                    rect.height - inset * 2f);
                GUI.Label(
                    new Rect(content.x, content.y, content.width, 20f),
                    room.EditorLabel,
                    EditorStyles.boldLabel);
                GUI.Label(
                    new Rect(content.x, content.y + 22f, content.width, 18f),
                    FormatCoordinate(room == draggedRoom
                        ? dragPreviewCoordinate
                        : room.GridCoordinate)
                        + " / " + room.FolderSlot.ToString("00"),
                    EditorStyles.miniLabel);
                GUI.Label(
                    new Rect(content.x, content.y + 40f, content.width, 18f),
                    new GUIContent(
                        ShortId(room.RoomIdText),
                        room.RoomIdText),
                    EditorStyles.miniLabel);
                GUI.Label(
                    new Rect(content.x, content.y + 58f, content.width, 18f),
                    "Footprint " + room.FootprintCells.x + "×"
                        + room.FootprintCells.y + "  |  Doors "
                        + roomProjection.Doors.Count,
                    EditorStyles.miniLabel);

                string state = roomProjection.OverlapsAnotherRoom
                    ? "OVERLAP"
                    : roomProjection.HasValidationProblem
                        ? "VALIDATION ISSUE"
                        : "OK";
                GUI.Label(
                    new Rect(content.x, rect.yMax - 24f, content.width, 18f),
                    state,
                    EditorStyles.centeredGreyMiniLabel);

                for (int doorIndex = 0; doorIndex < roomProjection.Doors.Count; doorIndex++)
                {
                    DrawDoor(roomProjection.Doors[doorIndex]);
                }
            }
        }

        private void DrawDoor(LevelDoorEndpointAuthoring2D door)
        {
            Rect rect;
            if (!doorRects.TryGetValue(door, out rect))
            {
                return;
            }

            bool connected = projection.IsConnected(door);
            bool unconnectedProblem = HasProblem(
                door.DoorIdText,
                LevelGridProblemCodeV2.UnconnectedTraversableDoor);
            bool otherProblem = HasProblemOtherThan(
                door.DoorIdText,
                LevelGridProblemCodeV2.UnconnectedTraversableDoor);
            bool production = activeRoot.LastGridValidation.Purpose
                == LevelGridValidationPurposeV2.ProductionPublish;
            Color color;
            if (otherProblem)
            {
                color = new Color(1f, 0.2f, 0.16f, 1f);
            }
            else if ((!connected && door.Traversable) || unconnectedProblem)
            {
                color = production
                    ? new Color(1f, 0.16f, 0.12f, 1f)
                    : new Color(1f, 0.48f, 0.05f, 1f);
            }
            else if (!connected)
            {
                color = new Color(0.52f, 0.52f, 0.52f, 1f);
            }
            else
            {
                color = new Color(0.25f, 0.85f, 0.58f, 1f);
            }

            EditorGUI.DrawRect(rect, color);
            DrawRectOutline(
                rect,
                selectedAuthoringObject == door
                    ? Color.white
                    : new Color(0.08f, 0.08f, 0.08f, 1f),
                selectedAuthoringObject == door ? 3f : 1f);

            string marker = door.PlacementMode == LevelDoorPlacementModeV2.Fixed
                ? "F"
                : door.AutoFaceConnection
                    ? string.Empty
                    : "!";
            if (!string.IsNullOrEmpty(marker))
            {
                GUI.Label(rect, marker, EditorStyles.centeredGreyMiniLabel);
            }
            GUI.Label(
                rect,
                new GUIContent(
                    string.Empty,
                    door.DoorIdText + "\n" + door.Side + " | "
                        + door.PlacementMode + "\n"
                        + (connected ? "Connected" : "Unconnected")
                        + (door.Traversable ? " | Traversable" : " | Non-traversable")
                        + (door.VisibleOnMap ? " | Map-visible" : " | Map-hidden")
                        + (door.AutoFaceConnection
                            ? " | Automatic facing"
                            : " | Automatic facing disabled")));
        }

        private void DrawConnectionDrag()
        {
            if (connectionDragSource == null)
            {
                return;
            }

            Rect sourceRect;
            if (!doorRects.TryGetValue(connectionDragSource, out sourceRect))
            {
                return;
            }

            Handles.BeginGUI();
            Color previous = Handles.color;
            Handles.color = new Color(0.35f, 0.82f, 1f, 1f);
            Handles.DrawAAPolyLine(4f, sourceRect.center, connectionDragMouse);
            Handles.color = previous;
            Handles.EndGUI();
        }

        private void DrawNotification()
        {
            if (string.IsNullOrEmpty(notificationMessage)
                || EditorApplication.timeSinceStartup > notificationUntil)
            {
                return;
            }

            Rect notificationRect = new Rect(
                notificationPosition.x + 12f,
                notificationPosition.y + 12f,
                Mathf.Min(390f, canvasRect.width - 36f),
                44f);
            GUI.Label(notificationRect, notificationMessage, EditorStyles.helpBox);
        }

        private void HandleCanvasInput(Rect rect)
        {
            Event current = Event.current;
            if (rect.Contains(current.mousePosition))
            {
                lastCanvasMouse = current.mousePosition;
            }

            if (current.type == EventType.ScrollWheel
                && rect.Contains(current.mousePosition))
            {
                ZoomAround(current.mousePosition, current.delta.y);
                current.Use();
                return;
            }

            if (current.type == EventType.MouseDown
                && rect.Contains(current.mousePosition))
            {
                if (current.button == 2 || (current.button == 0 && current.alt))
                {
                    panning = true;
                    panStartMouse = current.mousePosition;
                    panStartValue = pan;
                    current.Use();
                    return;
                }

                LevelDoorEndpointAuthoring2D hitDoor = HitDoor(current.mousePosition);
                LevelRoomAuthoring2D hitRoom = HitRoom(current.mousePosition);
                LevelDoorLinkAuthoring2D hitLink = HitLink(current.mousePosition);

                if (current.button == 1)
                {
                    ShowCanvasContextMenu(
                        current.mousePosition,
                        hitRoom,
                        hitDoor,
                        hitLink);
                    current.Use();
                    return;
                }

                if (current.button == 0)
                {
                    if (hitDoor != null)
                    {
                        SetSelectedAuthoringObject(hitDoor);
                        if (!projection.IsConnected(hitDoor))
                        {
                            connectionDragSource = hitDoor;
                            connectionDragMouse = current.mousePosition;
                        }
                        current.Use();
                        return;
                    }
                    if (hitRoom != null)
                    {
                        SetSelectedAuthoringObject(hitRoom);
                        draggedRoom = hitRoom;
                        dragPreviewCoordinate = hitRoom.GridCoordinate;
                        Vector2 continuous = ScreenToGridContinuous(current.mousePosition);
                        dragGridOffset = continuous - (Vector2)hitRoom.GridCoordinate;
                        current.Use();
                        return;
                    }
                    if (hitLink != null)
                    {
                        SetSelectedAuthoringObject(hitLink);
                        current.Use();
                        return;
                    }

                    selectedProblem = null;
                    SetSelectedAuthoringObject(activeRoot);
                    current.Use();
                }
            }

            if (current.type == EventType.MouseDrag)
            {
                if (panning)
                {
                    pan = panStartValue + current.mousePosition - panStartMouse;
                    Repaint();
                    current.Use();
                    return;
                }
                if (draggedRoom != null)
                {
                    Vector2 target = ScreenToGridContinuous(current.mousePosition)
                        - dragGridOffset;
                    dragPreviewCoordinate = new Vector2Int(
                        Mathf.RoundToInt(target.x),
                        Mathf.RoundToInt(target.y));
                    Repaint();
                    current.Use();
                    return;
                }
                if (connectionDragSource != null)
                {
                    connectionDragMouse = current.mousePosition;
                    Repaint();
                    current.Use();
                    return;
                }
            }

            if (current.type == EventType.MouseUp)
            {
                if (panning)
                {
                    panning = false;
                    SaveViewState();
                    current.Use();
                    return;
                }
                if (draggedRoom != null)
                {
                    LevelRoomAuthoring2D room = draggedRoom;
                    draggedRoom = null;
                    if (room.GridCoordinate != dragPreviewCoordinate)
                    {
                        LevelGridEditorOperationsV2.MoveRoom(
                            room,
                            dragPreviewCoordinate);
                        RequestRefresh(true);
                    }
                    current.Use();
                    return;
                }
                if (connectionDragSource != null)
                {
                    LevelDoorEndpointAuthoring2D source = connectionDragSource;
                    connectionDragSource = null;
                    LevelDoorEndpointAuthoring2D destination =
                        HitDoor(current.mousePosition);
                    LevelDoorLinkAuthoring2D created;
                    string rejection;
                    if (!LevelGridEditorOperationsV2.TryCreateConnection(
                        activeRoot,
                        source,
                        destination,
                        out created,
                        out rejection))
                    {
                        ShowNotification(rejection, current.mousePosition);
                    }
                    else
                    {
                        SetSelectedAuthoringObject(created);
                        RequestRefresh(true);
                    }
                    current.Use();
                    return;
                }
            }

            if (current.type == EventType.KeyDown
                && (current.keyCode == KeyCode.Delete
                    || current.keyCode == KeyCode.Backspace))
            {
                DeleteSelection();
                current.Use();
            }
        }

        private void ShowCanvasContextMenu(
            Vector2 mousePosition,
            LevelRoomAuthoring2D room,
            LevelDoorEndpointAuthoring2D door,
            LevelDoorLinkAuthoring2D link)
        {
            GenericMenu menu = new GenericMenu();
            if (activeRoot == null)
            {
                menu.AddDisabledItem(new GUIContent("Select a level root first"));
                menu.ShowAsContext();
                return;
            }
            if (door != null)
            {
                menu.AddItem(new GUIContent("Select Door"), false, delegate
                {
                    SetSelectedAuthoringObject(door);
                });
                menu.AddItem(new GUIContent("Reflow"), false, delegate
                {
                    LevelGridEditorOperationsV2.ReflowDoor(door);
                    RequestRefresh(true);
                });
                menu.AddItem(new GUIContent("Keep Placement"), false, delegate
                {
                    LevelGridEditorOperationsV2.KeepDoorPlacement(door);
                    RequestRefresh(true);
                });
                menu.AddSeparator(string.Empty);
                menu.AddItem(new GUIContent("Delete Door"), false, delegate
                {
                    LevelGridEditorOperationsV2.DeleteDoor(door);
                    RequestRefresh(true);
                });
            }
            else if (room != null)
            {
                Rect roomRect = roomRects[room];
                LevelDoorSideV2 side;
                float offset;
                ResolveNearestEdge(roomRect, mousePosition, out side, out offset);
                menu.AddItem(
                    new GUIContent("Add Door Here/" + side),
                    false,
                    delegate
                    {
                        LevelDoorEndpointAuthoring2D created =
                            LevelGridEditorOperationsV2.CreateDoor(
                                room,
                                side,
                                offset);
                        SetSelectedAuthoringObject(created);
                        RequestRefresh(true);
                    });
                AddDoorSideMenu(menu, room, LevelDoorSideV2.North);
                AddDoorSideMenu(menu, room, LevelDoorSideV2.East);
                AddDoorSideMenu(menu, room, LevelDoorSideV2.South);
                AddDoorSideMenu(menu, room, LevelDoorSideV2.West);
                menu.AddSeparator(string.Empty);
                menu.AddItem(new GUIContent("Delete Room"), false, delegate
                {
                    LevelGridEditorOperationsV2.DeleteRoom(room, true);
                    RequestRefresh(true);
                });
            }
            else if (link != null)
            {
                menu.AddItem(new GUIContent("Select Connection"), false, delegate
                {
                    SetSelectedAuthoringObject(link);
                });
                menu.AddItem(new GUIContent("Delete Connection"), false, delegate
                {
                    LevelGridEditorOperationsV2.DeleteConnection(link);
                    RequestRefresh(true);
                });
            }
            else
            {
                Vector2Int coordinate = ScreenToNearestGrid(mousePosition);
                menu.AddItem(
                    new GUIContent("Create Room Here"),
                    false,
                    delegate
                    {
                        LevelRoomAuthoring2D created =
                            LevelGridEditorOperationsV2.CreateRoom(
                                activeRoot,
                                coordinate);
                        SetSelectedAuthoringObject(created);
                        RequestRefresh(true);
                    });
            }
            menu.ShowAsContext();
        }

        private void AddDoorSideMenu(
            GenericMenu menu,
            LevelRoomAuthoring2D room,
            LevelDoorSideV2 side)
        {
            menu.AddItem(
                new GUIContent("Add Door/" + side),
                false,
                delegate
                {
                    LevelDoorEndpointAuthoring2D created =
                        LevelGridEditorOperationsV2.CreateDoor(room, side, 0.5f);
                    SetSelectedAuthoringObject(created);
                    RequestRefresh(true);
                });
        }
    }
}
#endif
