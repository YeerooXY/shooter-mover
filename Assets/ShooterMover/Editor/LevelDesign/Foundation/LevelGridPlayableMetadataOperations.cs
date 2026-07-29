#if UNITY_EDITOR
using System;
using ShooterMover.UnityAdapters.Authoring.LevelDesign;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ShooterMover.Editor.LevelDesign.Foundation
{
    /// <summary>
    /// The single editor mutation route for playable metadata. The component remains the serialized
    /// authority; this class only supplies grouped Undo, ownership checks and live validation.
    /// </summary>
    public static class LevelGridPlayableMetadataOperations
    {
        public static LevelGridPlayableMetadata Add(
            LevelDraft root)
        {
            if (root == null) throw new ArgumentNullException(nameof(root));
            LevelGridPlayableMetadata existing =
                root.GetComponent<LevelGridPlayableMetadata>();
            if (existing != null) return existing;

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Add Playable Level Metadata");
            LevelGridPlayableMetadata metadata =
                Undo.AddComponent<LevelGridPlayableMetadata>(root.gameObject);
            Undo.CollapseUndoOperations(group);
            MarkChanged(root, metadata);
            return metadata;
        }

        public static void SetStartRoom(
            LevelDraft root,
            LevelGridPlayableMetadata metadata,
            LevelRoom room)
        {
            RequireMetadata(root, metadata);
            RequireOwnedRoomOrNull(root, room, nameof(room));
            Apply(
                root,
                metadata,
                "Set Playable Start Room",
                serialized => serialized.FindProperty("startRoom").objectReferenceValue = room);
        }

        public static void SetPlayerStart(
            LevelDraft root,
            LevelGridPlayableMetadata metadata,
            Vector2 localPosition,
            float rotation)
        {
            RequireMetadata(root, metadata);
            if (!IsFinite(localPosition.x)
                || !IsFinite(localPosition.y)
                || !IsFinite(rotation))
            {
                throw new ArgumentException("Player start values must be finite.");
            }
            Apply(
                root,
                metadata,
                "Edit Playable Player Start",
                serialized =>
                {
                    serialized.FindProperty("playerStartLocalPosition").vector2Value =
                        localPosition;
                    serialized.FindProperty("playerStartRotation").floatValue = rotation;
                });
        }

        public static void SetFinalRoom(
            LevelDraft root,
            LevelGridPlayableMetadata metadata,
            LevelRoom room)
        {
            RequireMetadata(root, metadata);
            RequireOwnedRoomOrNull(root, room, nameof(room));
            Apply(
                root,
                metadata,
                "Set Playable Final Room",
                serialized =>
                {
                    SerializedProperty finalRoom = serialized.FindProperty("finalExitRoom");
                    SerializedProperty finalDoor = serialized.FindProperty("finalExitDoor");
                    finalRoom.objectReferenceValue = room;
                    DoorEndpoint selectedDoor =
                        finalDoor.objectReferenceValue as DoorEndpoint;
                    if (selectedDoor != null && selectedDoor.OwningRoom != room)
                    {
                        finalDoor.objectReferenceValue = null;
                    }
                });
        }

        public static void SetFinalDoor(
            LevelDraft root,
            LevelGridPlayableMetadata metadata,
            DoorEndpoint door)
        {
            RequireMetadata(root, metadata);
            if (door != null)
            {
                RequireOwnedDoor(root, door, nameof(door));
                if (metadata.FinalExitRoom == null)
                {
                    throw new InvalidOperationException(
                        "Select the exact final-exit room before assigning its door.");
                }
                if (!door.Traversable)
                {
                    throw new InvalidOperationException(
                        "The final-exit door must be traversable.");
                }
                if (door.OwningRoom != metadata.FinalExitRoom)
                {
                    throw new InvalidOperationException(
                        "The final-exit door must belong to the selected final room.");
                }
                RequireUnconnectedDoor(root, door);
            }
            Apply(
                root,
                metadata,
                "Set Playable Final Exit",
                serialized => serialized.FindProperty("finalExitDoor").objectReferenceValue = door);
        }

        public static void UseDoorAsFinalExit(
            LevelDraft root,
            LevelGridPlayableMetadata metadata,
            DoorEndpoint door)
        {
            RequireMetadata(root, metadata);
            RequireOwnedDoor(root, door, nameof(door));
            if (!door.Traversable)
            {
                throw new InvalidOperationException(
                    "The selected final-exit door must be traversable.");
            }
            RequireUnconnectedDoor(root, door);
            Apply(
                root,
                metadata,
                "Use Selected Door As Final Exit",
                serialized =>
                {
                    serialized.FindProperty("finalExitRoom").objectReferenceValue =
                        door.OwningRoom;
                    serialized.FindProperty("finalExitDoor").objectReferenceValue = door;
                });
        }

        public static void SetRuntimeDoorObjectId(
            LevelDraft root,
            LevelGridPlayableMetadata metadata,
            string runtimeDoorObjectId)
        {
            RequireMetadata(root, metadata);
            string normalized = string.IsNullOrWhiteSpace(runtimeDoorObjectId)
                ? string.Empty
                : runtimeDoorObjectId.Trim();
            Apply(
                root,
                metadata,
                "Set Runtime Door Object ID",
                serialized => serialized.FindProperty("runtimeDoorObjectId").stringValue =
                    normalized);
        }

        private static void Apply(
            LevelDraft root,
            LevelGridPlayableMetadata metadata,
            string undoName,
            Action<SerializedObject> mutation)
        {
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(undoName);
            Undo.RecordObject(metadata, undoName);
            var serialized = new SerializedObject(metadata);
            mutation(serialized);
            serialized.ApplyModifiedProperties();
            Undo.CollapseUndoOperations(group);
            MarkChanged(root, metadata);
        }

        private static void MarkChanged(
            LevelDraft root,
            UnityEngine.Object changed)
        {
            if (changed != null) EditorUtility.SetDirty(changed);
            if (root != null && root.gameObject.scene.IsValid())
            {
                EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
                LevelGridValidationPurpose purpose =
                    root.LastGridValidation.Purpose
                        == LevelGridValidationPurpose.ProductionPublish
                    ? LevelGridValidationPurpose.ProductionPublish
                    : LevelGridValidationPurpose.Draft;
                LevelGridAuthoringLiveValidation.ValidateNow(
                    root,
                    purpose,
                    false,
                    true);
            }
        }

        private static void RequireMetadata(
            LevelDraft root,
            LevelGridPlayableMetadata metadata)
        {
            if (root == null) throw new ArgumentNullException(nameof(root));
            if (metadata == null) throw new ArgumentNullException(nameof(metadata));
            if (metadata.gameObject != root.gameObject)
            {
                throw new InvalidOperationException(
                    "Playable metadata must be attached to the active level root.");
            }
        }

        private static void RequireOwnedRoomOrNull(
            LevelDraft root,
            LevelRoom room,
            string parameterName)
        {
            if (room == null) return;
            if (room.GetComponentInParent<LevelDraft>() != root)
            {
                throw new ArgumentException(
                    "The selected room is not owned by the active level root.",
                    parameterName);
            }
        }

        private static void RequireOwnedDoor(
            LevelDraft root,
            DoorEndpoint door,
            string parameterName)
        {
            if (door == null) throw new ArgumentNullException(parameterName);
            if (door.OwningRoom == null
                || door.GetComponentInParent<LevelDraft>() != root
                || door.OwningRoom.GetComponentInParent<LevelDraft>()
                    != root)
            {
                throw new ArgumentException(
                    "The selected door is not owned by the active level root.",
                    parameterName);
            }
        }

        private static void RequireUnconnectedDoor(
            LevelDraft root,
            DoorEndpoint door)
        {
            if (LevelGridEditorOperations.IsConnected(root, door))
            {
                throw new InvalidOperationException(
                    "A connected room endpoint cannot be assigned as the final exit. "
                    + "Delete its room connection first.");
            }
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
#endif
