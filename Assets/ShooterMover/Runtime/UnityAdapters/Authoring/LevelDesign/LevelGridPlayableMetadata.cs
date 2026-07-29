using System;
using UnityEngine;

namespace ShooterMover.UnityAdapters.Authoring.LevelDesign
{
    /// <summary>
    /// Minimal playable-level metadata that is not implied by room coordinates or folder names.
    /// Stable room and door references remain authoritative when the graph moves in the editor.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LevelGridPlayableMetadata : MonoBehaviour
    {
        [SerializeField] private LevelRoom startRoom;
        [SerializeField] private Vector2 playerStartLocalPosition = new Vector2(-9f, 0f);
        [SerializeField] private float playerStartRotation;
        [SerializeField] private LevelRoom finalExitRoom;
        [SerializeField] private DoorEndpoint finalExitDoor;
        [SerializeField] private string runtimeDoorObjectId = "door.room-standard";

        public LevelRoom StartRoom { get { return startRoom; } }
        public Vector2 PlayerStartLocalPosition { get { return playerStartLocalPosition; } }
        public float PlayerStartRotation { get { return playerStartRotation; } }
        public LevelRoom FinalExitRoom { get { return finalExitRoom; } }
        public DoorEndpoint FinalExitDoor { get { return finalExitDoor; } }
        public string RuntimeDoorObjectId
        {
            get
            {
                return string.IsNullOrWhiteSpace(runtimeDoorObjectId)
                    ? string.Empty
                    : runtimeDoorObjectId.Trim();
            }
        }

        public void ValidateForPlayableExport(LevelDraft root)
        {
            if (root == null) throw new ArgumentNullException(nameof(root));
            if (startRoom == null || startRoom.GetComponentInParent<LevelDraft>() != root)
            {
                throw new InvalidOperationException(
                    "Playable Level metadata requires a start room owned by this level root.");
            }
            if (finalExitRoom == null
                || finalExitRoom.GetComponentInParent<LevelDraft>() != root)
            {
                throw new InvalidOperationException(
                    "Playable Level metadata requires a final-exit room owned by this level root.");
            }
            if (finalExitDoor == null
                || finalExitDoor.OwningRoom != finalExitRoom
                || finalExitDoor.GetComponentInParent<LevelDraft>() != root)
            {
                throw new InvalidOperationException(
                    "Playable Level final exit must reference an exact door owned by the final-exit room and this level root.");
            }
            if (!finalExitDoor.Traversable)
            {
                throw new InvalidOperationException(
                    "Playable Level final-exit door must be traversable.");
            }
            if (string.IsNullOrWhiteSpace(RuntimeDoorObjectId))
            {
                throw new InvalidOperationException(
                    "Playable Level metadata requires a runtime door object ID.");
            }
            if (!IsFinite(playerStartLocalPosition.x)
                || !IsFinite(playerStartLocalPosition.y)
                || !IsFinite(playerStartRotation))
            {
                throw new InvalidOperationException(
                    "Playable Level player arrival values must be finite.");
            }
        }

        public static Vector2 ResolveDoorLocalPosition(
            LevelRoom owningRoom,
            Transform doorTransform)
        {
            if (owningRoom == null) throw new ArgumentNullException(nameof(owningRoom));
            if (doorTransform == null) throw new ArgumentNullException(nameof(doorTransform));
            Vector3 local = owningRoom.transform.InverseTransformPoint(doorTransform.position);
            return new Vector2(local.x, local.y);
        }

        public void ConfigureForTests(
            LevelRoom configuredStartRoom,
            Vector2 configuredPlayerStartLocalPosition,
            float configuredPlayerStartRotation,
            LevelRoom configuredFinalExitRoom,
            DoorEndpoint configuredFinalExitDoor,
            string configuredRuntimeDoorObjectId = "door.room-standard")
        {
            startRoom = configuredStartRoom;
            playerStartLocalPosition = configuredPlayerStartLocalPosition;
            playerStartRotation = configuredPlayerStartRotation;
            finalExitRoom = configuredFinalExitRoom;
            finalExitDoor = configuredFinalExitDoor;
            runtimeDoorObjectId = configuredRuntimeDoorObjectId;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
