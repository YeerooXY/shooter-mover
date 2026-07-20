using ShooterMover.Application.Missions.Rooms.Content;
using ShooterMover.Content.Definitions.Missions.Rooms;
using UnityEngine;

namespace ShooterMover.UnityAdapters.Authoring.LevelDesign
{
    [CreateAssetMenu(
        fileName = "JsonRoomAccessDefinition2D",
        menuName = "Shooter Mover/Level Design/JSON Room Access Definition 2D")]
    public sealed class JsonRoomAccessDefinition2D : ScriptableObject
    {
        [SerializeField] private JsonRoomContentDefinition2D roomContent;
        [SerializeField] private TextAsset accessDocument;

        public RoomAccessImportResultV1 Import()
        {
            return Import(BuiltInRoomContentObjectCatalogV1.Create());
        }

        public RoomAccessImportResultV1 Import(
            IRoomContentObjectCatalogV1 objectCatalog)
        {
            if (roomContent == null)
            {
                return Failure(
                    "room-access-content-asset-missing",
                    "$.room_content",
                    "A JSON room content definition asset is required.");
            }
            if (accessDocument == null)
            {
                return Failure(
                    "room-access-document-asset-missing",
                    "$.access_document",
                    "A room access TextAsset is required.");
            }

            RoomContentImportResultV1 content = roomContent.Import(objectCatalog);
            if (content == null || !content.IsValid)
            {
                string detail = content == null || content.Issues.Count == 0
                    ? "The room content import did not produce a valid bundle."
                    : content.Issues[0].Code
                        + ":"
                        + content.Issues[0].Path
                        + ":"
                        + content.Issues[0].Message;
                return Failure(
                    "room-access-content-import-invalid",
                    "$.room_content",
                    detail);
            }

            return RoomAccessJsonImporterV1.Import(
                accessDocument.text,
                content.Bundle.RuntimeDefinition);
        }

        public void ConfigureForTests(
            JsonRoomContentDefinition2D configuredRoomContent,
            TextAsset configuredAccessDocument)
        {
            roomContent = configuredRoomContent;
            accessDocument = configuredAccessDocument;
        }

        private static RoomAccessImportResultV1 Failure(
            string code,
            string path,
            string message)
        {
            return new RoomAccessImportResultV1(
                null,
                new[] { new RoomAccessImportIssueV1(code, path, message) });
        }
    }
}
