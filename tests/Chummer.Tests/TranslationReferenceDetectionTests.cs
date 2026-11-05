using System.Collections.Generic;
using System.Linq;
using System.Xml;
using Xunit;

namespace Chummer.Tests
{
	// - each case mirrors one lookup in the translation pass of XmlManager.Load
	public class TranslationReferenceDetectionTests
	{
		private const string Gear =
			"<chummer><categories><category>Commlinks</category></categories>"
			+ "<gears><gear><name>Meta Link</name></gear><gear><name>Ammo &amp; Grenades</name></gear></gears>"
			+ "<mods><mod><name>Hardening</name></mod></mods></chummer>";

		[Fact]
		public void TranslationOfAnExistingItemApplies()
		{
			Assert.Equal(new[] { "Applied" }, Outcomes("gear.xml", Gear,
				"<gears><gear><name>Meta Link</name><translate>Meta-Link</translate></gear></gears>"));
		}

		[Fact]
		public void TranslationOfARenamedItemIsDropped()
		{
			Assert.Equal(new[] { "Dropped" }, Outcomes("gear.xml", Gear,
				"<gears><gear><name>Metalink</name><translate>Meta-Link</translate></gear></gears>"));
		}

		// - Load anchors the lookup on the translation's own collection and element
		[Fact]
		public void SameNameInAnotherCollectionDoesNotCount()
		{
			Assert.Equal(new[] { "Dropped" }, Outcomes("gear.xml", Gear,
				"<gears><gear><name>Hardening</name><translate>Härte</translate></gear></gears>"));
		}

		[Fact]
		public void PageAloneIsLookedUpLikeATranslation()
		{
			Assert.Equal(new[] { "Dropped" }, Outcomes("gear.xml", Gear,
				"<gears><gear><name>Metalink</name><page>12</page></gear></gears>"));
		}

		[Fact]
		public void EscapedAmpersandInTheNameMatchesTheEnglishText()
		{
			Assert.Equal(new[] { "Applied" }, Outcomes("gear.xml", Gear,
				"<gears><gear><name>Ammo &amp; Grenades</name><translate>Munition</translate></gear></gears>"));
		}

		[Fact]
		public void QuoteInTheKeyCrashesTheLookup()
		{
			Assert.Equal(new[] { "Crashes" }, Outcomes("gear.xml", Gear,
				"<gears><gear><name>12\" Display</name><translate>Anzeige</translate></gear></gears>"));
		}

		[Fact]
		public void TranslationWithoutANameCrashesTheLookup()
		{
			Assert.Equal(new[] { "Crashes" }, Outcomes("gear.xml", Gear,
				"<gears><gear><translate>Meta-Link</translate></gear></gears>"));
		}

		[Fact]
		public void CategoryTranslationIsMatchedByItsText()
		{
			Assert.Equal(new[] { "Applied", "Dropped" }, Outcomes("gear.xml", Gear,
				"<categories><category translate=\"Kommlinks\">Commlinks</category>"
				+ "<category translate=\"Chemie\">Utility Chemicals</category></categories>"));
		}

		// - a spec is looked up under its skill: the same spec elsewhere does not count
		[Fact]
		public void SpecIsMatchedUnderItsSkill()
		{
			string skills = "<chummer><skills><skill><name>Longarms</name><specs><spec>Shotguns</spec></specs></skill>"
				+ "<skill><name>Pistols</name><specs><spec>Revolvers</spec></specs></skill></skills></chummer>";

			Assert.Equal(new[] { "Applied", "Dropped" }, Outcomes("skills.xml", skills,
				"<skills><skill><name>Longarms</name><specs><spec translate=\"Schrotflinten\">Shotguns</spec>"
				+ "<spec translate=\"Revolver\">Revolvers</spec></specs></skill></skills>"));
		}

		// - the shape of the first allowlist section
		[Fact]
		public void AdvantageWithABonusCannotBeMatched()
		{
			string arts = "<chummer><martialarts><martialart><name>Kali</name><advantages>"
				+ "<advantage><name>Disarm</name></advantage>"
				+ "<advantage><name>+1 DV</name><bonus><weaponcategorydv><name>Blades</name></weaponcategorydv></bonus>"
				+ "</advantage></advantages></martialart></martialarts></chummer>";

			Assert.Equal(new[] { "Applied", "Dropped" }, Outcomes("martialarts.xml", arts,
				"<martialarts><martialart><name>Kali</name><advantages>"
				+ "<advantage translate=\"Entwaffnen\">Disarm</advantage>"
				+ "<advantage translate=\"+1 SW\">+1 DV</advantage></advantages></martialart></martialarts>"));
		}

		// - the shape of the metavariant lookup
		[Fact]
		public void MetavariantPageWithoutTranslateIsNotRead()
		{
			string metatypes = "<chummer><metatypes><metatype><name>Elf</name><metavariants>"
				+ "<metavariant><name>Night One</name></metavariant></metavariants></metatype></metatypes></chummer>";

			Assert.Empty(Entries("metatypes.xml", metatypes,
				"<metatypes><metatype><name>Elf</name><metavariants><metavariant><name>Nite One</name><page>9</page>"
				+ "</metavariant></metavariants></metatype></metatypes>")
				.Where(entry => entry.Kind == TranslationReferences.Kind.Metavariant));
		}

		[Fact]
		public void BlockForAFileTheDataLacksIsDropped()
		{
			TranslationReferences.Entry block = TranslationReferences.EntriesIn("de",
				Document("<chummer><chummer file=\"gone.xml\"><gears /></chummer></chummer>"), fileName => null)
				.Single();

			Assert.Equal(TranslationReferences.Outcome.Dropped, block.Outcome);
		}

		private static string[] Outcomes(string fileName, string englishXml, string blockXml)
		{
			return Entries(fileName, englishXml, blockXml)
				.Where(entry => entry.Kind != TranslationReferences.Kind.File)
				.Select(entry => entry.Outcome.ToString())
				.ToArray();
		}

		private static IReadOnlyList<TranslationReferences.Entry> Entries(string fileName, string englishXml,
			string blockXml)
		{
			XmlDocument english = Document(englishXml);
			XmlDocument language = Document("<chummer><chummer file=\"" + fileName + "\">" + blockXml + "</chummer></chummer>");

			return TranslationReferences.EntriesIn("de", language, name => name == fileName ? english : null);
		}

		private static XmlDocument Document(string xml)
		{
			XmlDocument document = new XmlDocument();
			document.LoadXml(xml);
			return document;
		}
	}
}
