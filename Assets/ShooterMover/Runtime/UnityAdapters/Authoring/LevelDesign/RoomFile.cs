using System;
using System.Collections.Generic;
using ShooterMover.Application.Missions.Rooms.Content;
using ShooterMover.Content.Definitions.Missions.Rooms;
using UnityEngine;

namespace ShooterMover.UnityAdapters.Authoring.LevelDesign
{
    [Serializable]
    public sealed class RoomDocument
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
        fileName = "RoomFile",
        menuName = "Shooter Mover/Level Design/JSON Room Content Definition 2D")]
    public sealed class RoomFile : ScriptableObject
    {
        [SerializeField] private TextAsset manifest;
        [SerializeField] private RoomDocument[] documents =
            Array.Empty<RoomDocument>();

        public TextAsset Manifest { get { return manifest; } }

        public IReadOnlyList<RoomDocument> Documents
        {
            get { return documents ?? Array.Empty<RoomDocument>(); }
        }

        public RoomContentImportResult Import()
        {
            return Import(BuiltInRoomContentObjectCatalog.Create());
        }

        public RoomContentImportResult Import(
            IRoomContentObjectCatalog objectCatalog)
        {
            if (manifest == null)
            {
                return new RoomContentImportResult(
                    null,
                    new[]
                    {
                        new RoomContentImportIssue(
                            "room-content-manifest-asset-missing",
                            "$.manifest",
                            "A manifest TextAsset is required."),
                    });
            }

            var source = new Dictionary<string, string>(StringComparer.Ordinal);
            RoomDocument[] authored = documents
                ?? Array.Empty<RoomDocument>();
            for (int index = 0; index < authored.Length; index++)
            {
                RoomDocument entry = authored[index];
                if (entry == null
                    || string.IsNullOrWhiteSpace(entry.Key)
                    || entry.Document == null)
                {
                    return new RoomContentImportResult(
                        null,
                        new[]
                        {
                            new RoomContentImportIssue(
                                "room-content-document-asset-invalid",
                                "$.documents[" + index + "]",
                                "Every JSON document asset requires a unique key and TextAsset."),
                        });
                }
                if (source.ContainsKey(entry.Key))
                {
                    return new RoomContentImportResult(
                        null,
                        new[]
                        {
                            new RoomContentImportIssue(
                                "room-content-document-asset-duplicate",
                                "$.documents[" + index + "]",
                                "Duplicate JSON document key: " + entry.Key),
                        });
                }
                source.Add(entry.Key, entry.Document.text);
            }

            return RoomContentJsonImporter.Import(
                new RoomContentJsonPackage(manifest.text, source),
                objectCatalog);
        }

        public void ConfigureCompiledAssets(
            TextAsset configuredManifest,
            params RoomDocument[] configuredDocuments)
        {
            manifest = configuredManifest
                ?? throw new ArgumentNullException(nameof(configuredManifest));
            documents = configuredDocuments == null
                ? Array.Empty<RoomDocument>()
                : (RoomDocument[])configuredDocuments.Clone();
        }

        public void ConfigureForTests(
            TextAsset configuredManifest,
            params RoomDocument[] configuredDocuments)
        {
            ConfigureCompiledAssets(configuredManifest, configuredDocuments);
        }
    }
}
