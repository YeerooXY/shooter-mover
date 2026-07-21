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
        [SerializeField] private RoomAccessReferenceKindV1 kind =
            RoomAccessReferenceKindV1.Holding;
        [SerializeField] private RoomAccessReferenceSourceV1 source =
            RoomAccessReferenceSourceV1.RunHolding;

        public RoomAccessReferenceRegistrationV1 Build()
        {
            return new RoomAccessReferenceRegistrationV1(
                StableId.Parse(referenceStableId),
                kind,
                source);
        }

        public void ConfigureForTests(
            string configuredReferenceStableId,
            RoomAccessReferenceKindV1 configuredKind,
            RoomAccessReferenceSourceV1 configuredSource)
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

        public RoomAccessImportResultV1 Import()
        {
            IRoomAccessReferenceRegistryV1 referenceRegistry;
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
                BuiltInRoomContentObjectCatalogV1.Create(),
                referenceRegistry);
        }

        public RoomAccessImportResultV1 Import(
            IRoomContentObjectCatalogV1 objectCatalog)
        {
            IRoomAccessReferenceRegistryV1 referenceRegistry;
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

        public RoomAccessImportResultV1 Import(
            IRoomContentObjectCatalogV1 objectCatalog,
            IRoomAccessReferenceRegistryV1 referenceRegistry)
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

        private RoomAccessReferenceCatalogV1 BuildReferenceRegistry()
        {
            RoomAccessReferenceAuthoring2D[] authored = references
                ?? Array.Empty<RoomAccessReferenceAuthoring2D>();
            var registrations =
                new RoomAccessReferenceRegistrationV1[authored.Length];
            for (int index = 0; index < authored.Length; index++)
            {
                if (authored[index] == null)
                {
                    throw new InvalidOperationException(
                        "Room access reference authoring cannot contain null entries.");
                }
                registrations[index] = authored[index].Build();
            }
            return new RoomAccessReferenceCatalogV1(registrations);
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
