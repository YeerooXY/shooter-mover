using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using ShooterMover.Application.Items.Generated;

namespace ShooterMover.Application.Items
{
    public sealed class ItemPackageCatalog
    {
        private static readonly ItemPackageCatalog current = Build();
        private readonly ReadOnlyDictionary<string, ItemPackageDocument> packages;

        private ItemPackageCatalog(IDictionary<string, ItemPackageDocument> values)
        {
            packages = new ReadOnlyDictionary<string, ItemPackageDocument>(
                new Dictionary<string, ItemPackageDocument>(values, StringComparer.Ordinal));
        }

        public static ItemPackageCatalog Current { get { return current; } }
        public IReadOnlyDictionary<string, ItemPackageDocument> Packages { get { return packages; } }
        public string SourceFingerprint { get { return ItemPackageSources.Fingerprint; } }

        public bool TryGet(string kind, string id, out ItemPackageDocument value)
        {
            return packages.TryGetValue((kind ?? string.Empty) + ":" + (id ?? string.Empty), out value);
        }

        private static ItemPackageCatalog Build()
        {
            var values = new Dictionary<string, ItemPackageDocument>(StringComparer.Ordinal);
            foreach (ItemPackageSource source in ItemPackageSources.All)
            {
                ItemPackageDocument document = ItemPackageJson.Import(source.Json);
                string key = document.Kind + ":" + document.Id;
                if (!string.Equals(source.Kind, document.Kind, StringComparison.Ordinal)
                    || !string.Equals(source.Id, document.Id, StringComparison.Ordinal)
                    || values.ContainsKey(key))
                {
                    throw new InvalidOperationException("Generated item package identity mismatch: " + source.SourcePath);
                }
                values.Add(key, document);
            }
            return new ItemPackageCatalog(values);
        }
    }

    public static class ItemPackageJson
    {
        private static readonly DataContractJsonSerializer serializer =
            new DataContractJsonSerializer(
                typeof(ItemPackageDocument),
                new DataContractJsonSerializerSettings { UseSimpleDictionaryFormat = true });

        public static ItemPackageDocument Import(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) throw new ArgumentException("Item package JSON is required.", nameof(json));
            ItemPackageDocument value;
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
            {
                value = serializer.ReadObject(stream) as ItemPackageDocument;
            }
            Validate(value);
            return value;
        }

        private static void Validate(ItemPackageDocument value)
        {
            if (value == null
                || (value.Schema != "shooter-mover.gun-family/1" && value.Schema != "shooter-mover.gear-set/1")
                || (value.Kind != "gun-family" && value.Kind != "gear-set")
                || string.IsNullOrWhiteSpace(value.Id)
                || string.IsNullOrWhiteSpace(value.Name))
            {
                throw new SerializationException("Item package identity is invalid.");
            }
            if (value.Marks == null || value.Marks.Count != 3)
            {
                throw new SerializationException("Item packages require exactly three marks.");
            }
            for (int index = 0; index < value.Marks.Count; index++)
            {
                if (value.Marks[index] == null || value.Marks[index].Mark != index + 1)
                {
                    throw new SerializationException("Item marks must be MK1, MK2, and MK3 in order.");
                }
            }
            if (value.Kind == "gun-family" && (value.Fire == null || value.Shot == null || value.Delivery == null))
            {
                throw new SerializationException("Gun packages require shared fire, shot, and delivery definitions.");
            }
        }
    }

    [DataContract]
    public sealed class ItemPackageDocument
    {
        [DataMember(Name = "$schema", IsRequired = true, Order = 0)] public string Schema { get; private set; }
        [DataMember(Name = "kind", IsRequired = true, Order = 1)] public string Kind { get; private set; }
        [DataMember(Name = "id", IsRequired = true, Order = 2)] public string Id { get; private set; }
        [DataMember(Name = "name", IsRequired = true, Order = 3)] public string Name { get; private set; }
        [DataMember(Name = "intendedUse", Order = 4)] public string IntendedUse { get; private set; }
        [DataMember(Name = "rarity", IsRequired = true, Order = 5)] public string Rarity { get; private set; }
        [DataMember(Name = "category", Order = 6)] public string Category { get; private set; }
        [DataMember(Name = "damageType", Order = 7)] public string DamageType { get; private set; }
        [DataMember(Name = "runtimeStatus", Order = 8)] public string RuntimeStatus { get; private set; }
        [DataMember(Name = "fire", Order = 9)] public GunFirePackage Fire { get; private set; }
        [DataMember(Name = "shot", Order = 10)] public GunShotPackage Shot { get; private set; }
        [DataMember(Name = "delivery", Order = 11)] public GunDeliveryPackage Delivery { get; private set; }
        [DataMember(Name = "marks", IsRequired = true, Order = 12)] public List<ItemMarkPackage> Marks { get; private set; }
    }

    [DataContract]
    public sealed class GunFirePackage
    {
        [DataMember(Name = "mode", IsRequired = true)] public string Mode { get; private set; }
        [DataMember(Name = "cyclesPerSecond", IsRequired = true)] public double CyclesPerSecond { get; private set; }
        [DataMember(Name = "shotsPerBurst")] public int ShotsPerBurst { get; private set; }
        [DataMember(Name = "secondsBetweenShots")] public double SecondsBetweenShots { get; private set; }
    }

    [DataContract]
    public sealed class GunShotPackage
    {
        [DataMember(Name = "kind", IsRequired = true)] public string Kind { get; private set; }
        [DataMember(Name = "projectiles", IsRequired = true)] public int Projectiles { get; private set; }
        [DataMember(Name = "spreadDegrees")] public double SpreadDegrees { get; private set; }
        [DataMember(Name = "randomnessDegrees")] public double RandomnessDegrees { get; private set; }
        [DataMember(Name = "pulses")] public int Pulses { get; private set; }
        [DataMember(Name = "secondsBetweenPulses")] public double SecondsBetweenPulses { get; private set; }
    }

    [DataContract]
    public sealed class GunDeliveryPackage
    {
        [DataMember(Name = "type", IsRequired = true)] public string Type { get; private set; }
        [DataMember(Name = "speed")] public double Speed { get; private set; }
        [DataMember(Name = "radius")] public double Radius { get; private set; }
        [DataMember(Name = "range")] public double Range { get; private set; }
        [DataMember(Name = "beamWidth")] public double BeamWidth { get; private set; }
    }

    [DataContract]
    public sealed class ItemMarkPackage
    {
        [DataMember(Name = "mark", IsRequired = true)] public int Mark { get; private set; }
        [DataMember(Name = "available", IsRequired = true)] public bool Available { get; private set; }
        [DataMember(Name = "peakDropLevel", IsRequired = true)] public int PeakDropLevel { get; private set; }
        [DataMember(Name = "craftLevel", IsRequired = true)] public int CraftLevel { get; private set; }
        [DataMember(Name = "dropWeight", IsRequired = true)] public double DropWeight { get; private set; }
        [DataMember(Name = "minimumBoxTier", IsRequired = true)] public int MinimumBoxTier { get; private set; }
        [DataMember(Name = "maxAugmentSlots", IsRequired = true)] public int MaxAugmentSlots { get; private set; }
        [DataMember(Name = "damage")] public GunMarkDamagePackage Damage { get; private set; }
        [DataMember(Name = "art")] public Dictionary<string, string> Art { get; private set; }
        [DataMember(Name = "pieces")] public Dictionary<string, GearPiecePackage> Pieces { get; private set; }
    }

    [DataContract]
    public sealed class GunMarkDamagePackage
    {
        [DataMember(Name = "direct")] public double Direct { get; private set; }
        [DataMember(Name = "area")] public double Area { get; private set; }
        [DataMember(Name = "dotPerSecond")] public double DotPerSecond { get; private set; }
    }

    [DataContract]
    public sealed class GearPiecePackage
    {
        [DataMember(Name = "name", IsRequired = true)] public string Name { get; private set; }
        [DataMember(Name = "maxAugmentSlots", IsRequired = true)] public int MaxAugmentSlots { get; private set; }
        [DataMember(Name = "art", IsRequired = true)] public string Art { get; private set; }
        [DataMember(Name = "modifiers")] public List<GearModifierPackage> Modifiers { get; private set; }
        [DataMember(Name = "pendingModules")] public List<string> PendingModules { get; private set; }
    }

    [DataContract]
    public sealed class GearModifierPackage
    {
        [DataMember(Name = "target", IsRequired = true)] public string Target { get; private set; }
        [DataMember(Name = "operation", IsRequired = true)] public string Operation { get; private set; }
        [DataMember(Name = "value", IsRequired = true)] public double Value { get; private set; }
    }
}
