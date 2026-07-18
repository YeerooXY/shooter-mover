using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using ShooterMover.Domain.Progression.Skills;
using ShooterMover.UnityAdapters.Progression.Skills.Content;
using UnityEngine;

namespace ShooterMover.Application.Progression.Skills.Presentation
{
    public sealed class RankedSkillsImportedCatalogBundleV2
    {
        public RankedSkillsImportedCatalogBundleV2(RankedSkillJsonImportResultV1 import)
        {
            Import = import ?? throw new ArgumentNullException(nameof(import));
            if (!import.Success) throw new InvalidOperationException(Format(import));
            Catalog = import.Catalog;
            Text = new ImportedTextCatalog(import);
        }
        public RankedSkillJsonImportResultV1 Import { get; }
        public RankedSkillCatalogV2 Catalog { get; }
        public IRankedSkillTextCatalogV2 Text { get; }
        public string Fingerprint => Import.NormalizedFingerprint;
        private static string Format(RankedSkillJsonImportResultV1 value) => string.Join("\n", value.Diagnostics.Select(x => x.ErrorCode + " " + x.JsonPath + " " + x.Message));

        private sealed class ImportedTextCatalog : IRankedSkillTextCatalogV2
        {
            private readonly IReadOnlyDictionary<string, string> synergyNames;
            private readonly IReadOnlyDictionary<string, string> synergyDescriptions;
            public ImportedTextCatalog(RankedSkillJsonImportResultV1 import)
            {
                synergyNames = new ReadOnlyDictionary<string, string>(import.ImportedSynergies.ToDictionary(x => x.Definition.Id, x => x.DisplayName, StringComparer.Ordinal));
                synergyDescriptions = new ReadOnlyDictionary<string, string>(import.ImportedSynergies.ToDictionary(x => x.Definition.Id, x => x.Description, StringComparer.Ordinal));
            }
            public string DisplayName(string id) { string value; return synergyNames.TryGetValue(id, out value) && !string.IsNullOrWhiteSpace(value) ? value : Humanize(id); }
            public string Description(string id) { string value; return synergyDescriptions.TryGetValue(id, out value) ? value : string.Empty; }
            private static string Humanize(string id)
            {
                string tail = (id ?? string.Empty).Split('.').LastOrDefault() ?? string.Empty;
                return string.Join(" ", tail.Split('_').Where(x => x.Length > 0).Select(x => char.ToUpperInvariant(x[0]) + x.Substring(1)));
            }
        }
    }

    public static class RankedSkillsImportedCatalogAdapterV2
    {
        private const string RelativePath = "ShooterMover/Content/Definitions/Progression/Skills/ranked_skills_v01.json";
        public static RankedSkillsImportedCatalogBundleV2 Load()
        {
            string path = Path.Combine(Application.dataPath, RelativePath);
            if (!File.Exists(path)) throw new FileNotFoundException("Missing ranked skill catalog.", path);
            return new RankedSkillsImportedCatalogBundleV2(RankedSkillJsonImporterV1.Import(File.ReadAllText(path)));
        }
        public static RankedSkillsImportedCatalogBundleV2 Import(string json) => new RankedSkillsImportedCatalogBundleV2(RankedSkillJsonImporterV1.Import(json));
    }
}
