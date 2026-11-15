using System.Collections.Generic;
using System.Linq;
using System.Xml;
using Chummer.Tests.Kernel;
using Xunit;

namespace Chummer.Tests.Localization
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

		// The whole-text key is the old lookup: it never matched an advantage with a bonus.
		[Fact]
		public void AdvantageIsMatchedByItsName()
		{
			string arts = "<chummer><martialarts><martialart><name>Kali</name><advantages>"
				+ "<advantage><name>Disarm</name></advantage>"
				+ "<advantage><name>+1 DV</name><bonus><weaponcategorydv><name>Blades</name></weaponcategorydv></bonus>"
				+ "</advantage></advantages></martialart></martialarts></chummer>";

			Assert.Equal(new[] { "Applied", "Applied", "Dropped" }, Outcomes("martialarts.xml", arts,
				"<martialarts><martialart><name>Kali</name><advantages>"
				+ "<advantage translate=\"Entwaffnen\">Disarm</advantage>"
				+ "<advantage translate=\"+1 SW\">+1 DV</advantage>"
				+ "<advantage translate=\"+1 SW\">+1 DVBlades</advantage></advantages></martialart></martialarts>"));
		}

		[Fact]
		public void MetavariantPageWithoutTranslateIsLookedUp()
		{
			Assert.Equal(new[] { "Applied", "Dropped" }, Outcomes("metatypes.xml", Metatypes,
				"<metatypes><metatype><name>Elf</name><metavariants>"
				+ "<metavariant><name>Night One</name><page>9</page></metavariant>"
				+ "<metavariant><name>Nite One</name><page>9</page></metavariant>"
				+ "</metavariants></metatype></metatypes>"));
		}

		// - one dropped block, not one finding per translation inside it
		[Fact]
		public void BlockForAFileTheDataLacksIsDroppedOnce()
		{
			TranslationReferences.Entry block = TranslationReferences.EntriesIn("de",
				Xml.Parse("<chummer><chummer file=\"gone.xml\"><gears><gear><name>X</name><translate>Y</translate>"
					+ "</gear></gears></chummer></chummer>"), fileName => null)
				.Single();

			Assert.Equal(TranslationReferences.Kind.File, block.Kind);
			Assert.Equal(TranslationReferences.Outcome.Dropped, block.Outcome);
		}

		[Fact]
		public void FileNameMustMatchInCase()
		{
			Assert.True(TranslationReferences.IsDataFile("gear.xml"));
			Assert.False(TranslationReferences.IsDataFile("Gear.xml"));
		}

		[Theory]
		[InlineData("code")]
		[InlineData("advantage")]
		[InlineData("disadvantage")]
		public void EachItemFieldAloneTriggersTheLookup(string field)
		{
			Assert.Equal(new[] { "Dropped" }, Outcomes("gear.xml", Gear,
				"<gears><gear><name>Metalink</name><" + field + ">x</" + field + "></gear></gears>"));
		}

		[Fact]
		public void MetavariantIsMatchedUnderItsMetatype()
		{
			Assert.Equal(new[] { "Applied", "Dropped" }, Outcomes("metatypes.xml", Metatypes,
				"<metatypes><metatype><name>Elf</name><metavariants>"
				+ "<metavariant><name>Night One</name><translate>Nachtelf</translate></metavariant>"
				+ "<metavariant><name>Gnome</name><translate>Gnom</translate></metavariant>"
				+ "</metavariants></metatype></metatypes>"));
		}

		[Fact]
		public void MetavariantAltpageIsNotRead()
		{
			Assert.Equal(new[] { "Applied" }, Outcomes("metatypes.xml", Metatypes,
				"<metatypes><metatype><name>Elf</name><metavariants>"
				+ "<metavariant><name>Night One</name><altpage>9</altpage></metavariant>"
				+ "</metavariants></metatype><metatype><name>Dwarf</name><metavariants>"
				+ "<metavariant><name>Gnome</name><altpage>9</altpage><page>9</page></metavariant>"
				+ "</metavariants></metatype></metatypes>"));
		}

		[Fact]
		public void NestedEntryWithoutAnOwnerNameCrashes()
		{
			Assert.Equal(new[] { "Crashes" }, Outcomes("martialarts.xml", "<chummer><martialarts /></chummer>",
				"<martialarts><martialart><advantages><advantage translate=\"x\">Disarm</advantage></advantages>"
				+ "</martialart></martialarts>"));
		}

		[Fact]
		public void NestedLookupDoesNotDecodeTheAmpersand()
		{
			string skills = "<chummer><skills><skill><name>Pilot</name><specs><spec>Cars &amp; Bikes</spec></specs>"
				+ "</skill></skills></chummer>";

			Assert.Equal(new[] { "Dropped" }, Outcomes("skills.xml", skills,
				"<skills><skill><name>Pilot</name><specs><spec translate=\"x\">Cars &amp; Bikes</spec></specs>"
				+ "</skill></skills>"));
		}

		// - the shape of the misplaced <specs> block
		[Fact]
		public void TranslationOutsideEveryLookupIsUnreached()
		{
			string skills = "<chummer><skills><skill><name>Pistols</name><specs><spec>Holdouts</spec></specs>"
				+ "</skill></skills></chummer>";

			TranslationReferences.Entry entry = Entries("skills.xml", skills,
				"<skills><skill><name>Pistols</name><translate>Pistolen</translate></skill>"
				+ "<specs><spec translate=\"Holdouts\">Hold-Outs</spec></specs></skills>")
				.Single(candidate => candidate.Kind == TranslationReferences.Kind.Unreached);

			Assert.Equal("de skills.xml unreached: skills/specs/spec: Hold-Outs", entry.Display);
		}

		[Fact]
		public void TranslatedMentorChoiceIsUnreached()
		{
			Assert.Contains(Entries("mentors.xml", "<chummer><mentors /></chummer>",
					"<mentors><mentor><name>Bear</name><choices><choice><name>A</name><translate>B</translate></choice>"
					+ "</choices></mentor></mentors>"),
				entry => entry.Kind == TranslationReferences.Kind.Unreached);
		}

		[Fact]
		public void TwoTranslationsOfOneItemRepeat()
		{
			Assert.Equal(new[] { "de gear.xml item: gears/gear: Meta Link = 2 entries, 2 translations" },
				Repeats("gear.xml", Gear, "<gears><gear><name>Meta Link</name><translate>A</translate></gear>"
					+ "<gear><name>Meta Link</name><translate>B</translate></gear></gears>"));
		}

		[Fact]
		public void ExactCopyCountsAsOneTranslation()
		{
			Assert.Equal(new[] { "de gear.xml item: gears/gear: Meta Link = 2 entries, 1 translation" },
				Repeats("gear.xml", Gear, "<gears><gear><name>Meta Link</name><translate>A</translate></gear>"
					+ "<gear><name>Meta Link</name><translate>A</translate></gear></gears>"));
		}

		[Fact]
		public void TwoTranslationsOfOneCategoryRepeat()
		{
			Assert.Equal(new[] { "de gear.xml text: categories/category: Commlinks = 2 entries, 2 translations" },
				Repeats("gear.xml", Gear, "<categories><category translate=\"A\">Commlinks</category>"
					+ "<category translate=\"B\">Commlinks</category></categories>"));
		}

		// - the key alone would see a repeat here: the lookup is anchored on the collection
		[Fact]
		public void SameNameInTwoCollectionsDoesNotRepeat()
		{
			Assert.Empty(Repeats("gear.xml",
				"<chummer><gears><gear><name>Hardening</name></gear></gears><mods><mod><name>Hardening</name></mod></mods></chummer>",
				"<gears><gear><name>Hardening</name><translate>A</translate></gear></gears>"
					+ "<mods><mod><name>Hardening</name><translate>B</translate></mod></mods>"));
		}

		// - the key alone would see no repeat here: both translations meant different English items
		[Fact]
		public void EnglishDuplicatePullsBothTranslationsOntoTheFirstItem()
		{
			Assert.Equal(new[] { "de gear.xml item: gears/gear: Microphone = 2 entries, 2 translations" },
				Repeats("gear.xml",
					"<chummer><gears><gear><name>Microphone</name><category>Audio</category></gear>"
						+ "<gear><name>Microphone</name><category>Sensor</category></gear></gears></chummer>",
					"<gears><gear><name>Microphone</name><page>1</page></gear>"
						+ "<gear><name>Microphone</name><page>2</page></gear></gears>"));
		}

		[Fact]
		public void DroppedTranslationsDoNotRepeat()
		{
			Assert.Empty(Repeats("gear.xml", Gear, "<gears><gear><name>Metalink</name><translate>A</translate></gear>"
				+ "<gear><name>Metalink</name><translate>B</translate></gear></gears>"));
		}

		private static string[] Repeats(string fileName, string englishXml, string blockXml)
		{
			return TranslationReferences.RepeatedTargets(Entries(fileName, englishXml, blockXml))
				.Select(repeat => repeat.Key + " = " + repeat.Description)
				.ToArray();
		}

		private const string Metatypes ="<chummer><metatypes><metatype><name>Elf</name><metavariants>"
			+ "<metavariant><name>Night One</name></metavariant></metavariants></metatype>"
			+ "<metatype><name>Dwarf</name><metavariants><metavariant><name>Gnome</name></metavariant>"
			+ "</metavariants></metatype></metatypes></chummer>";

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
			XmlDocument english = Xml.Parse(englishXml);
			XmlDocument language = Xml.Parse("<chummer><chummer file=\"" + fileName + "\">" + blockXml + "</chummer></chummer>");

			return TranslationReferences.EntriesIn("de", language, name => name == fileName ? english : null);
		}
	}
}
