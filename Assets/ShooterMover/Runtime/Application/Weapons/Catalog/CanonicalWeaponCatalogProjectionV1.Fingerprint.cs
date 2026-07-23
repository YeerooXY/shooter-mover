
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Weapons.Catalog;

namespace ShooterMover.Application.Weapons.Catalog
{
    public sealed partial class CanonicalWeaponCatalogProjectionV1
    {
        private static string CalculateFingerprint(
            string sourceId,
            string sourceFingerprint,
            string normalizationPolicyFingerprint,
            WeaponCatalog weaponCatalog,
            EquipmentCatalog equipmentCatalog,
            IReadOnlyList<CanonicalWeaponCatalogEntryV1> values)
        {
            var builder = new StringBuilder();
            builder.Append(sourceId).Append('\n');
            builder.Append(sourceFingerprint).Append('\n');
            builder.Append(normalizationPolicyFingerprint).Append('\n');
            builder.Append(weaponCatalog.Fingerprint).Append('\n');
            builder.Append(equipmentCatalog.Fingerprint).Append('\n');
            for (int index = 0; index < values.Count; index++)
            {
                CanonicalWeaponCatalogEntryV1 entry = values[index];
                builder.Append(entry.WeaponDefinition.DefinitionId)
                    .Append('|')
                    .Append(entry.SourceRarity)
                    .Append('|')
                    .Append(entry.NormalizedRarity)
                    .Append('|')
                    .Append(entry.QualityId)
                    .Append('|')
                    .Append(entry.ArtReferenceId)
                    .Append('\n');
            }
            return Sha256(builder.ToString());
        }

        private static string Sha256(string value)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] digest = sha.ComputeHash(
                    Encoding.UTF8.GetBytes(value ?? string.Empty));
                var hex = new StringBuilder(digest.Length * 2);
                for (int index = 0; index < digest.Length; index++)
                {
                    hex.Append(digest[index].ToString(
                        "x2",
                        CultureInfo.InvariantCulture));
                }
                return "sha256:" + hex;
            }
        }
    }
}
