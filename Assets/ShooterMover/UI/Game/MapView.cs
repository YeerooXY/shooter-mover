using System;
using System.Collections.Generic;
using ShooterMover.Domain.Common;
using UnityEngine;

namespace ShooterMover.UI.Game
{
    [DisallowMultipleComponent]
    public sealed class MapView : MonoBehaviour
    {
        private readonly List<RoomLink> links = new List<RoomLink>();
        private readonly HashSet<string> linkKeys =
            new HashSet<string>(StringComparer.Ordinal);

        private MapLayout layout;
        private StableId currentRoomStableId;
        private bool isVisible;
        private GUIStyle roomNameStyle;
        private GUIStyle roomNoteStyle;
        private GUIStyle playerStyle;
        private GUIStyle titleStyle;
        private GUIStyle hintStyle;

        public bool IsVisible { get { return isVisible; } }

        public void Build(MapLayout configuredLayout)
        {
            layout = configuredLayout
                ?? throw new ArgumentNullException(nameof(configuredLayout));
            links.Clear();
            linkKeys.Clear();
        }

        public void AddConnection(
            StableId fromRoomStableId,
            StableId toRoomStableId)
        {
            EnsureBuilt();
            if (fromRoomStableId == null)
                throw new ArgumentNullException(nameof(fromRoomStableId));
            if (toRoomStableId == null)
                throw new ArgumentNullException(nameof(toRoomStableId));
            if (fromRoomStableId == toRoomStableId)
                return;

            MapLayout.Room from;
            MapLayout.Room to;
            if (!layout.TryGetRoom(fromRoomStableId, out from)
                || !layout.TryGetRoom(toRoomStableId, out to))
            {
                throw new InvalidOperationException(
                    "map-view-connection-room-missing");
            }

            string key = ConnectionKey(fromRoomStableId, toRoomStableId);
            if (!linkKeys.Add(key))
                return;
            links.Add(new RoomLink(fromRoomStableId, toRoomStableId));
        }

        public void Show(StableId roomStableId)
        {
            EnsureBuilt();
            SetCurrentRoom(roomStableId);
            isVisible = true;
        }

        public void Hide()
        {
            isVisible = false;
        }

        public void SetCurrentRoom(StableId roomStableId)
        {
            EnsureBuilt();
            MapLayout.Room room;
            if (roomStableId == null
                || !layout.TryGetRoom(roomStableId, out room))
            {
                throw new InvalidOperationException(
                    "map-view-current-room-missing");
            }
            currentRoomStableId = roomStableId;
        }

        private void OnGUI()
        {
            if (!isVisible
                || layout == null
                || Event.current.type != EventType.Repaint)
            {
                return;
            }

            EnsureStyles();
            int previousDepth = GUI.depth;
            Color previousColor = GUI.color;
            Matrix4x4 previousMatrix = GUI.matrix;
            GUI.depth = -1000;

            DrawBackground();
            DrawConnections();
            DrawRooms();
            DrawHeader();

            GUI.matrix = previousMatrix;
            GUI.color = previousColor;
            GUI.depth = previousDepth;
        }

        private void DrawBackground()
        {
            GUI.color = new Color(0.025f, 0.04f, 0.06f, 0.97f);
            GUI.DrawTexture(
                new Rect(0f, 0f, Screen.width, Screen.height),
                Texture2D.whiteTexture);

            GUI.color = new Color(0.25f, 0.4f, 0.56f, 0.18f);
            const float step = 24f;
            float centreX = Screen.width * 0.5f;
            float centreY = Screen.height * 0.5f;
            for (float x = centreX % step; x < Screen.width; x += step)
            {
                GUI.DrawTexture(
                    new Rect(Mathf.Round(x), 0f, 1f, Screen.height),
                    Texture2D.whiteTexture);
            }
            for (float y = centreY % step; y < Screen.height; y += step)
            {
                GUI.DrawTexture(
                    new Rect(0f, Mathf.Round(y), Screen.width, 1f),
                    Texture2D.whiteTexture);
            }

            GUI.color = new Color(0.43f, 0.68f, 0.9f, 0.38f);
            GUI.DrawTexture(
                new Rect(Mathf.Round(centreX), 0f, 2f, Screen.height),
                Texture2D.whiteTexture);
            GUI.DrawTexture(
                new Rect(0f, Mathf.Round(centreY), Screen.width, 2f),
                Texture2D.whiteTexture);
        }

        private void DrawConnections()
        {
            GUI.color = new Color(0.35f, 0.7f, 0.94f, 0.78f);
            float width = Mathf.Max(1.5f, 2.5f * layout.Scale);
            for (int index = 0; index < links.Count; index++)
            {
                RoomLink link = links[index];
                MapLayout.Room from;
                MapLayout.Room to;
                if (!layout.TryGetRoom(link.FromRoomStableId, out from)
                    || !layout.TryGetRoom(link.ToRoomStableId, out to))
                {
                    continue;
                }

                Vector2 direction = to.Rect.center - from.Rect.center;
                Vector2 start = RoomEdge(from.Rect, direction);
                Vector2 end = RoomEdge(to.Rect, -direction);
                DrawLine(ToScreen(start), ToScreen(end), width);
            }
        }

        private void DrawRooms()
        {
            for (int index = 0; index < layout.Rooms.Count; index++)
            {
                MapLayout.Room room = layout.Rooms[index];
                Rect rect = ToScreen(room.Rect);
                bool current = room.RoomStableId == currentRoomStableId;

                GUI.color = current
                    ? new Color(0.35f, 0.78f, 1f, 1f)
                    : new Color(0.42f, 0.49f, 0.59f, 1f);
                GUI.DrawTexture(rect, Texture2D.whiteTexture);

                Rect body = new Rect(
                    rect.x + 2f,
                    rect.y + 2f,
                    Mathf.Max(1f, rect.width - 4f),
                    Mathf.Max(1f, rect.height - 4f));
                GUI.color = current
                    ? new Color(0.13f, 0.24f, 0.34f, 1f)
                    : new Color(0.08f, 0.12f, 0.17f, 1f);
                GUI.DrawTexture(body, Texture2D.whiteTexture);

                GUI.color = Color.white;
                float titleHeight = Mathf.Max(14f, 18f * layout.Scale);
                GUI.Label(
                    new Rect(
                        body.x + 5f,
                        body.y + 4f,
                        body.width - 10f,
                        titleHeight),
                    room.DisplayName,
                    roomNameStyle);

                string note = room.IsStart
                    ? "START"
                    : room.IsExit
                        ? "TARGET"
                        : string.Empty;
                if (!string.IsNullOrEmpty(note))
                {
                    GUI.Label(
                        new Rect(
                            body.x + 5f,
                            body.yMax - Mathf.Max(15f, 17f * layout.Scale),
                            body.width - 10f,
                            Mathf.Max(13f, 16f * layout.Scale)),
                        note,
                        roomNoteStyle);
                }

                if (current)
                {
                    GUI.Label(
                        new Rect(
                            body.x + 4f,
                            body.center.y - 9f,
                            28f,
                            24f),
                        "☺",
                        playerStyle);
                }
            }
        }

        private void DrawHeader()
        {
            GUI.color = Color.white;
            GUI.Label(
                new Rect(0f, 18f, Screen.width, 38f),
                "MAP",
                titleStyle);
            GUI.Label(
                new Rect(0f, Screen.height - 42f, Screen.width, 24f),
                "M  CLOSE",
                hintStyle);
        }

        private static void DrawLine(Vector2 from, Vector2 to, float width)
        {
            Vector2 difference = to - from;
            float length = difference.magnitude;
            if (length <= 0.01f)
                return;

            Matrix4x4 previous = GUI.matrix;
            float angle = Mathf.Atan2(difference.y, difference.x)
                * Mathf.Rad2Deg;
            GUIUtility.RotateAroundPivot(angle, from);
            GUI.DrawTexture(
                new Rect(from.x, from.y - width * 0.5f, length, width),
                Texture2D.whiteTexture);
            GUI.matrix = previous;
        }

        private static Vector2 RoomEdge(Rect room, Vector2 direction)
        {
            if (direction.sqrMagnitude <= 0.0001f)
                return room.center;
            Vector2 unit = direction.normalized;
            float x = Mathf.Abs(unit.x) <= 0.0001f
                ? float.PositiveInfinity
                : room.width * 0.5f / Mathf.Abs(unit.x);
            float y = Mathf.Abs(unit.y) <= 0.0001f
                ? float.PositiveInfinity
                : room.height * 0.5f / Mathf.Abs(unit.y);
            return room.center + unit * Mathf.Min(x, y);
        }

        private static Vector2 ToScreen(Vector2 point)
        {
            return new Vector2(
                Screen.width * 0.5f + point.x,
                Screen.height * 0.5f - point.y);
        }

        private static Rect ToScreen(Rect rect)
        {
            return new Rect(
                Screen.width * 0.5f + rect.xMin,
                Screen.height * 0.5f - rect.yMax,
                rect.width,
                rect.height);
        }

        private void EnsureStyles()
        {
            if (roomNameStyle != null)
                return;

            int roomFontSize = Mathf.Max(
                8,
                Mathf.RoundToInt(12f * layout.Scale));
            roomNameStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.UpperCenter,
                clipping = TextClipping.Clip,
                fontSize = roomFontSize,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.91f, 0.95f, 1f, 1f) },
            };
            roomNoteStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.LowerCenter,
                fontSize = Mathf.Max(7, roomFontSize - 2),
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.45f, 0.8f, 1f, 1f) },
            };
            playerStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = Mathf.Max(15, Mathf.RoundToInt(21f * layout.Scale)),
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.35f, 1f, 0.62f, 1f) },
            };
            titleStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 25,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.88f, 0.95f, 1f, 1f) },
            };
            hintStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.55f, 0.68f, 0.8f, 1f) },
            };
        }

        private static string ConnectionKey(StableId left, StableId right)
        {
            string leftText = left.ToString();
            string rightText = right.ToString();
            return string.CompareOrdinal(leftText, rightText) <= 0
                ? leftText + "|" + rightText
                : rightText + "|" + leftText;
        }

        private void EnsureBuilt()
        {
            if (layout == null)
                throw new InvalidOperationException("map-view-not-built");
        }

        private sealed class RoomLink
        {
            public RoomLink(
                StableId fromRoomStableId,
                StableId toRoomStableId)
            {
                FromRoomStableId = fromRoomStableId;
                ToRoomStableId = toRoomStableId;
            }

            public StableId FromRoomStableId { get; }
            public StableId ToRoomStableId { get; }
        }
    }
}
