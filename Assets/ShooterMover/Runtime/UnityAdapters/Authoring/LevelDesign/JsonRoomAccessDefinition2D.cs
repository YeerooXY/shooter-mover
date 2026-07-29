using System;
using ShooterMover.Application.Missions.Rooms.Content;
using ShooterMover.Content.Definitions.Missions.Rooms;
using ShooterMover.Contracts.Missions.Rooms;
using ShooterMover.Domain.Common;
using UnityEngine;

namespace ShooterMover.UnityAdapters.Authoring.LevelDesign
{
    [Serializable]
    public sealed class RoomAccessReferenceAuthoring2D
    {
        [SerializeField] private string referenceStableId = string.Empty;
        [SerializeField] private RoomAccessReferenceKind kind =
            RoomAccessReferenceKind.Holding;
        [SerializeField] private RoomAccessReferenceSource source =
            RoomAccessReferenceSource.RunHolding;

        public RoomAccessReferenceRegistration Build()
        {
            return new RoomAccessReferenceRegistration(
                StableId.Parse(referenceStableId),
                kind,
                source);
        }

        public void ConfigureForTests(
            string configuredReferenceStableId,
            RoomAccessReferenceKind configuredKind,
            RoomAccessReferenceSource configuredSource)
        {
            referenceStableId = configuredReferenceStableId;
            kind = configuredKind;
            source = configuredSource;
        }
    }

    [CreateAssetMenu(
        fileName = "JsonRoomAccessDefinition2D",
        menuName = "Shooter Mover/Level Design/JSON Room Access Definition 2D")]
    public sealed class JsonRoomAccessDefinition2D : ScriptableObject
    {
        [SerializeField] private JsonRoomContentDefinition2D roomContent;
        [SerializeField] private TextAsset accessDocument;
        [SerializeField] private RoomAccessReferenceAuthoring2D[] references =
            Array.Empty<RoomAccessReferenceAuthoring2D>();

        public RoomAccessImportResult Import()
        {
            IRoomAccessReferenceRegistry referenceRegistry;
            try
            {
                referenceRegistry = BuildReferenceRegistry();
            }
            catch (Exception exception)
            {
                return Failure(
                    "room-access-reference-authoring-invalid",
                    "$.references",
                    exception.Message);
            }

            return Import(
                BuiltInRoomContentObjectCatalog.Create(),
                referenceRegistry);
        }

        public RoomAccessImportResult Import(
            IRoomContentObjectCatalog objectCatalog)
        {
            IRoomAccessReferenceRegistry referenceRegistry;
            try
            {
                referenceRegistry = BuildReferenceRegistry();
            }
            catch (Exception exception)
            {
                return Failure(
                    "room-access-reference-authoring-invalid",
                    "$.references",
                    exception.Message);
            }

            return Import(objectCatalog, referenceRegistry);
        }

        public RoomAccessImportResult Import(
            IRoomContentObjectCatalog objectCatalog,
            IRoomAccessReferenceRegistry referenceRegistry)
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
            if (referenceRegistry == null)
            {
                return Failure(
                    "room-access-reference-registry-missing",
                    "$.references",
                    "An immutable room access reference registry is required.");
            }

            RoomContentImportResult content = roomContent.Import(objectCatalog);
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

            return RoomAccessJsonImporter.Import(
                accessDocument.text,
                content.Bundle.RuntimeDefinition,
                referenceRegistry);
        }

        public void ConfigureForTests(
            JsonRoomContentDefinition2D configuredRoomContent,
            TextAsset configuredAccessDocument,
            params RoomAccessReferenceAuthoring2D[] configuredReferences)
        {
            roomContent = configuredRoomContent;
            accessDocument = configuredAccessDocument;
            references = configuredReferences == null
                ? Array.Empty<RoomAccessReferenceAuthoring2D>()
                : (RoomAccessReferenceAuthoring2D[])configuredReferences.Clone();
        }

        private RoomAccessReferenceCatalog BuildReferenceRegistry()
        {
            RoomAccessReferenceAuthoring2D[] authored = references
                ?? Array.Empty<RoomAccessReferenceAuthoring2D>();
            var registrations =
                new RoomAccessReferenceRegistration[authored.Length];
            for (int index = 0; index < authored.Length; index++)
            {
                if (authored[index] == null)
                {
                    throw new InvalidOperationException(
                        "Room access reference authoring cannot contain null entries.");
                }
                registrations[index] = authored[index].Build();
            }
            return new RoomAccessReferenceCatalog(registrations);
        }

        private static RoomAccessImportResult Failure(
            string code,
            string path,
            string message)
        {
            return new RoomAccessImportResult(
                null,
                new[] { new RoomAccessImportIssue(code, path, message) });
        }
    }
}
