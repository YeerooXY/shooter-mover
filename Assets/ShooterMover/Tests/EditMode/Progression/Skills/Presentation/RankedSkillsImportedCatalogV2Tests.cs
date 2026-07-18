using System.IO;
using System.Linq;
using NUnit.Framework;
using ShooterMover.Application.Progression.Skills.Presentation;
using ShooterMover.Domain.Progression.Skills;
using UnityEngine;

namespace ShooterMover.Tests.EditMode.Progression.Skills.Presentation
{
    public sealed class RankedSkillsImportedCatalogV2Tests
    {
        private static RankedSkillsImportedCatalogBundleV2 Load()
        {
            string path=Path.Combine(Application.dataPath,"ShooterMover/Content/Definitions/Progression/Skills/ranked_skills_v01.json");
            return RankedSkillsImportedCatalogAdapterV2.Import(File.ReadAllText(path));
        }

        [Test]
        public void ScreenUsesImportedCatalogAndFingerprint()
        {
            var bundle=Load();
            Assert.That(bundle.Catalog.Skills.Count,Is.EqualTo(20));
            Assert.That(bundle.Fingerprint,Has.Length.EqualTo(64));
            Assert.That(bundle.Catalog.Synergies.Select(x=>x.Id),Does.Contain("synergy.farming.legendary_prospecting"));
        }

        [Test]
        public void CombinedRankSynergyUsesDomainSatisfaction()
        {
            var bundle=Load();
            var synergy=bundle.Catalog.Synergies.Single(x=>x.Id=="synergy.farming.legendary_prospecting");
            var inactive=new RankedSkillAllocationSnapshotV2("profile","class.striker",1,bundle.Catalog.SchemaVersion,bundle.Catalog.ContentVersion,new System.Collections.Generic.Dictionary<string,int>{{"generic.farming.credit_gain",4},{"generic.farming.strongbox_finder",4},{"generic.farming.strongbox_quality",4}});
            var active=new RankedSkillAllocationSnapshotV2("profile","class.striker",2,bundle.Catalog.SchemaVersion,bundle.Catalog.ContentVersion,new System.Collections.Generic.Dictionary<string,int>{{"generic.farming.credit_gain",8},{"generic.farming.strongbox_finder",4},{"generic.farming.strongbox_quality",4}});
            Assert.That(synergy.IsSatisfied(inactive),Is.False);
            Assert.That(synergy.IsSatisfied(active),Is.True);
            Assert.That(synergy.CombinedRankRequirements.Single().MinimumCombinedRank,Is.EqualTo(16));
        }
    }
}
