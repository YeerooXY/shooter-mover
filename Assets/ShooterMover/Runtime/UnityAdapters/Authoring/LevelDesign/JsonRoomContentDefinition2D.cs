using System;
using System.Collections.Generic;
using ShooterMover.Application.Missions.Rooms.Content;
using ShooterMover.Content.Definitions.Missions.Rooms;
using UnityEngine;

namespace ShooterMover.UnityAdapters.Authoring.LevelDesign
{
    [Serializable]
    public sealed class RoomContentJsonDocumentAsset2D
    {
        [SerializeField] private string key = string.Empty;
        [SerializeField] private TextAsset document;

        public string Key { get { return key; } }
        public TextAsset Document { get { return document; } }

        public void ConfigureCompiledAsset(
            string configuredKey,
            TextAsset configuredDocument)
        {
            if (string.IsNullOrWhiteSpace(configuredKey))
            {
                throw new ArgumentException(
                    "A compiled room-content document key is required.",
                    nameof(configuredKey));
            }
            key = configuredKey.Trim();
            document = configuredDocument
                ?? throw new ArgumentNullException(nameof(configuredDocument));
        }

        public void ConfigureForTests(string configuredKey, TextAsset configuredDocument)
        {
            ConfigureCompiledAsset(configuredKey, configuredDocument);
        }
    }

    [CreateAssetMenu(
        fileName = "JsonRoomContentDefinition2D",
        menuName = "Shooter Mover/Level Design/JSON Room Content Definition 2D")]
    public sealed class JsonRoomContentDefinition2D : ScriptableObject
    {
        [SerializeField] private TextAsset manifest;
        [SerializeField] private RoomContentJsonDocumentAsset2D[] documents =
            Array.Empty<RoomContentJsonDocumentAsset2D>();

        public RoomContentImportResultV1 Import()
        {
            return Import(BuiltInRoomContentObjectCatalogV1.Create());
        }

        public RoomContentImportResultV1 Import(
            IRoomContentObjectCatalogV1 objectCatalog)
        {
            if (manifest == null)
            {
                return new RoomContentImportResultV1(
                    null,
                    new[]
                    {
                        new RoomContentImportIssueV1(
                            "room-content-manifest-asset-missing",
                            "$.manifest",
                            "A manifest TextAsset is required."),
                    });
            }

            var source = new Dictionary<string, string>(StringComparer.Ordinal);
            RoomContentJsonDocumentAsset2D[] authored = documents
                ?? Array.Empty<RoomContentJsonDocumentAsset2D>();
            for (int index = 0; index < authored.Length; index++)
            {
                RoomContentJsonDocumentAsset2D entry = authored[index];
                if (entry == null
                    || string.IsNullOrWhiteSpace(entry.Key)
                    || entry.Document == null)
                {
                    return new RoomContentImportResultV1(
                        null,
                        new[]
                        {
                            new RoomContentImportIssueV1(
                                "room-content-document-asset-invalid",
                                "$.documents[" + index + "]",
                                "Every JSON document asset requires a unique key and TextAsset."),
                        });
                }
                if (source.ContainsKey(entry.Key))
                {
                    return new RoomContentImportResultV1(
                        null,
                        new[]
                        {
                            new RoomContentImportIssueV1(
                                "room-content-document-asset-duplicate",
                                "$.documents[" + index + "]",
                                "Duplicate JSON document key: " + entry.Key),
                        });
                }
                source.Add(entry.Key, entry.Document.text);
            }

            return RoomContentJsonImporterV1.Import(
                new RoomContentJsonPackageV1(manifest.text, source),
                objectCatalog);
        }

        public void ConfigureCompiledAssets(
            TextAsset configuredManifest,
            params RoomContentJsonDocumentAsset2D[] configuredDocuments)
        {
            manifest = configuredManifest
                ?? throw new ArgumentNullException(nameof(configuredManifest));
            documents = configuredDocuments == null
                ? Array.Empty<RoomContentJsonDocumentAsset2D>()
                : (RoomContentJsonDocumentAsset2D[])configuredDocuments.Clone();
        }

        public void ConfigureForTests(
            TextAsset configuredManifest,
            params RoomContentJsonDocumentAsset2D[] configuredDocuments)
        {
            ConfigureCompiledAssets(configuredManifest, configuredDocuments);
        }
    }
}
