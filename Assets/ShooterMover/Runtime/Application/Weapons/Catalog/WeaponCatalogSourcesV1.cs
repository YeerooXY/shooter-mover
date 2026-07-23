
using System;
#if UNITY_EDITOR
using System.IO;
#endif

namespace ShooterMover.Application.Weapons.Catalog
{
    public interface IWeaponCatalogSourceV1
    {
        string SourceId { get; }
        string ReadJson();
    }

    public sealed class StringWeaponCatalogSourceV1 : IWeaponCatalogSourceV1
    {
        private readonly string json;

        public StringWeaponCatalogSourceV1(string sourceId, string jsonText)
        {
            if (string.IsNullOrWhiteSpace(sourceId))
            {
                throw new ArgumentException("A catalog source identity is required.", nameof(sourceId));
            }
            SourceId = sourceId.Trim();
            json = jsonText ?? throw new ArgumentNullException(nameof(jsonText));
        }

        public string SourceId { get; }

        public string ReadJson()
        {
            return json;
        }
    }

#if UNITY_EDITOR
    /// <summary>
    /// Repository-path source for Editor tooling and tests only. Player builds do not
    /// compile this adapter; production content is supplied by a Unity TextAsset source.
    /// </summary>
    public sealed class FileWeaponCatalogSourceV1 : IWeaponCatalogSourceV1
    {
        public FileWeaponCatalogSourceV1(string sourceId, string path)
        {
            if (string.IsNullOrWhiteSpace(sourceId))
            {
                throw new ArgumentException("A catalog source identity is required.", nameof(sourceId));
            }
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("A catalog path is required.", nameof(path));
            }
            SourceId = sourceId.Trim();
            Path = path.Trim();
        }

        public string SourceId { get; }
        public string Path { get; }

        public string ReadJson()
        {
            return File.ReadAllText(Path);
        }
    }
#endif
}
