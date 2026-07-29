using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace Chummer.Tests
{
	// - a [Theory] with an empty MemberData source runs zero cases and reports green, so if path resolution ever broke (wrong directory, a rename), the well-formedness theories below would silently stop checking anything while CI stayed green
	// - these guard against that by asserting each source actually found a plausible number of files
	public class DataPathsTests
	{
		[Fact]
		public void RuleXmlFilesFindsData()
		{
			Assert.True(DataPaths.RuleXmlFiles().Count() > 10);
		}

		[Fact]
		public void LangXmlFilesFindsTranslations()
		{
			Assert.True(DataPaths.LangXmlFiles().Count() > 1);
		}

		[Fact]
		public void SheetXslFilesFindsCharacterSheets()
		{
			Assert.True(DataPaths.SheetXslFiles().Count() > 1);
		}

		// - exact rather than the ">N" style above: the top-level Chummer/data/*.xml set is a small, deliberately-enumerable universe (27 top-level XML files, 26 with a matching .xsd - only improvements.xml has none), unlike the recursively-grown data/lang/sheet corpora the guards above watch
		// - an exact count turns a silently-dropped pair (see the file-rename risk noted on TopLevelRuleXmlWithSchemaFiles) into an immediate failure instead of a threshold tolerating one or two lost pairs
		// - the 26 is expected to stay put; a deliberate new top-level data file + schema is the one case where bumping this number on purpose is the correct fix, not a workaround
		[Fact]
		public void TopLevelRuleXmlWithSchemaFilesFindsAllPairs()
		{
			Assert.Equal(26, DataPaths.TopLevelRuleXmlWithSchemaFiles().Count());
		}

		// - exact for the same reason as the pair count above; the allowlist guard in NameUniquenessTests only half-covers this, since it notices a dropped file that happened to hold an allowlisted duplicate, but only 8 of these 42 collections do
		// - were discovery to quietly stop finding spells.xml or qualities.xml, that guard would stay green while those collections simply stopped being checked
		// - adding a collection wrapper to the data is the one case where bumping this number deliberately is the right fix rather than a workaround
		[Fact]
		public void TopLevelRuleXmlCollectionsFindsEveryNamedCollection()
		{
			Assert.Equal(42, DataPaths.TopLevelRuleXmlCollections().Count());
		}

		// - exact for the same reason again, and here the number is derivable rather than merely observed: 27 top-level data files, of which four carry no <source> at all - books.xml (it is the declaration side), improvements.xml, packs.xml and ranges.xml
		// - if a file ever stopped being scanned, its book references would go unchecked with nothing else to notice
		[Fact]
		public void TopLevelRuleXmlFilesCitingBooksFindsEveryCitingFile()
		{
			Assert.Equal(23, DataPaths.TopLevelRuleXmlFilesCitingBooks().Count());
		}

		// - likewise derivable, worth spelling out because three separate rules combine to produce it: 24 collections have items carrying <category>
		// - two of them (weapons.xml/mods, programs.xml/options) are deliberately exempt because no code resolves their categories against any block
		// - two more (lifestyles.xml/qualities, ranges.xml/ranges) sit in files that declare no block at all, so there is no local contract to check for them
		// - 24 - 2 - 2 = 20; any of those three rules quietly changing scope shows up here as a number that no longer adds up
		[Fact]
		public void CategoryKeyedCollectionsCoversEveryGovernedCollection()
		{
			Assert.Equal(20, DataPaths.CategoryKeyedCollections().Count());
		}

		// - the scope exceptions are a hand-written list, and a stale entry fails quietly in a way the count above cannot separate from a legitimate change
		// - rename a collection and its exemption stops applying to anything, so the collection silently starts being checked (or, for a redirect, throws somewhere unrelated)
		// - both structures are covered together because a key belongs to exactly one of them, and the failure mode is identical either way
		[Fact]
		public void CategoryScopeExceptionsAllNameARealCollection()
		{
			string[] known = DataPaths.CollectionsUsingCategories().ToArray();

			string[] stale = DataPaths.CategoryScopeExceptionKeys()
				.Where(key => !known.Contains(key))
				.ToArray();

			Assert.True(stale.Length == 0,
				"These category scope exceptions name a (file, collection) that carries no "
				+ "<category> any more:\n  " + string.Join("\n  ", stale));
		}

		// - exact for the same reason as the counts above: the required-field rules are a hand-written table
		// - a rule that stops producing a theory case takes one entity type's whole contract with it
		// - 21 Create(XmlNode ...) methods, of which two are reached with a second file as well (Metamagic with echoes.xml, Cyberware with bioware.xml) and one - Gear - is split three ways by category, giving 23 entity rules
		// - plus 5 for the nested reference nodes those methods walk, and 1 for the <mount> a weapon's built-in accessory needs
		// - adding a rule because somebody found another unguarded read is the one case where bumping this deliberately is the right fix
		[Fact]
		public void RequiredFieldRulesCoverEveryEntityType()
		{
			Assert.Equal(29, DataPaths.RequiredFieldRules.Count);
		}

		// - the only guard that looks at the production code rather than at the data or at the table itself
		// - every other check here compares the table against something downstream of it, so all of them stay green when a 22nd Create method turns up without a rule
		// - that failure drops one entity type out of the suite with nothing to show for it, which is the worst shape a gap can have
		// - a count, not a mapping: matching a signature to a rule would need the same C# parsing the table deliberately avoids, while the count catches the case that matters
		// - what it cannot see is a new unguarded read inside a Create it already knows about - that one is left to review, and said so above the table
		// - a deliberate new entity type is the one case where bumping this number is the right fix, together with the rule that goes with it
		[Fact]
		public void NoEntityCreateMethodHasAppearedWithoutARule()
		{
			IReadOnlyList<string> sites = DataPaths.EntityCreateMethodSites();

			Assert.True(sites.Count == 21,
				"The rule table was read off 21 Create(XmlNode ...) methods, but the sources now "
				+ "hold " + sites.Count + ". Every one of them needs an entry in "
				+ nameof(DataPaths.RequiredFieldRules) + ":\n  " + string.Join("\n  ", sites));
		}

		// - the reads each required-field rule was derived from, pinned per entity class
		//
		// - the count guard above notices a Create method arriving or leaving; this notices one changing underneath its rule
		// - that is the drift the rule table cannot see for itself: a field dropped from a Create stays green forever, because the rule still demands something the data still has
		// - a hash rather than the lines themselves, because the point is that somebody re-derives the rule, not that this file keeps a second copy of the method
		//
		// - what it covers, measured on the real methods rather than assumed: deleting a required read fires, wrapping one in try/catch fires, an unrelated comment or a reindentation does not
		// - what it does not cover: a field deleted from the rule table by hand while the method stays put - that one no test can see, and only moving the contract onto the entity removes it
		//
		// - updating a hash is correct exactly once: after re-reading the changed method and bringing its rule back in line
		private static readonly IReadOnlyDictionary<string, string> CreateMethodFingerprints =
			new Dictionary<string, string>(StringComparer.Ordinal)
			{
				{ "Armor", "D96CF8F104A09DD6" },
				{ "ArmorMod", "CAEF435D72900464" },
				{ "Commlink", "A1968D0ED944DAA3" },
				{ "CritterPower", "444EA2340702DA75" },
				{ "Cyberware", "30EDD3E06D401001" },
				{ "Gear", "77775E141D4BED8A" },
				{ "Lifestyle", "90F0A62C8144D083" },
				{ "MartialArt", "E7ED6B22668C57DB" },
				{ "MartialArtAdvantage", "F038A04C8F8A8744" },
				{ "MartialArtManeuver", "0D12F627CC8677C6" },
				{ "Metamagic", "154D9A4FF2846718" },
				{ "OperatingSystem", "440470AB030489A4" },
				{ "Quality", "E3D95D0FA0790BF6" },
				{ "Spell", "13587537E7E1BBB5" },
				{ "TechProgram", "64569AC7FA73760D" },
				{ "TechProgramOption", "193418AE5B625714" },
				{ "Vehicle", "6B30C63B55C25B15" },
				{ "VehicleMod", "35B6C11AE894E486" },
				{ "Weapon", "58E362E9ED32246B" },
				{ "WeaponAccessory", "89F9C6C81F3E5E81" },
				{ "WeaponMod", "5EF93E211D92C838" },
			};

		[Fact]
		public void NoCreateMethodChangedSinceItsRuleWasRead()
		{
			IReadOnlyDictionary<string, string> actual = DataPaths.EntityCreateFingerprints();

			string[] drifted = CreateMethodFingerprints
				.Where(pinned => !actual.ContainsKey(pinned.Key)
					|| !string.Equals(actual[pinned.Key], pinned.Value, StringComparison.Ordinal))
				.Select(pinned => actual.ContainsKey(pinned.Key)
					? pinned.Key + ".Create now reads " + actual[pinned.Key]
						+ ", pinned as " + pinned.Value
					: pinned.Key + ".Create is gone")
				.OrderBy(entry => entry, StringComparer.Ordinal)
				.ToArray();

			Assert.True(drifted.Length == 0,
				"These Create methods changed the elements they read, so the rules derived from "
				+ "them in " + nameof(DataPaths.RequiredFieldRules) + " may no longer describe "
				+ "them. Re-read each one, fix its rule, then update the hash:\n  "
				+ string.Join("\n  ", drifted));
		}

		// - the drift check above walks the pinned table, so a pin deleted by hand leaves its class unwatched
		// - the count guard cannot see that either: it counts Create methods, not pins
		[Fact]
		public void EveryCreateMethodHasAPinnedFingerprint()
		{
			string[] unpinned = DataPaths.EntityCreateFingerprints().Keys
				.Where(className => !CreateMethodFingerprints.ContainsKey(className))
				.OrderBy(className => className, StringComparer.Ordinal)
				.ToArray();

			Assert.True(unpinned.Length == 0,
				"These Create methods have no pinned fingerprint, so the drift check does not "
				+ "watch them:\n  " + string.Join("\n  ", unpinned));
		}

		// - the three gear.xml rules have to cover the collection exactly once each, or an entry is checked against the wrong Create's contract or against none
		// - the partition is written as a negation so a category nobody anticipated lands in Gear rather than nowhere
		// - this proves it holds over the real file, where the detection test proves it over a hand-built one
		[Fact]
		public void TheGearRulesBetweenThemCoverEveryGearEntry()
		{
			int partitioned = new[] { "Gear", "Commlink", "OperatingSystem" }
				.Sum(entity => DataPaths.RequiredFieldContractFor(entity).ItemCount);

			Assert.Equal(
				DataPaths.RuleCollectionFor(Path.Combine(DataPaths.ChummerDataDir, "gear.xml"), "gears")
					.ItemKeys.Count,
				partitioned);
		}

		// - a rule matching nothing checks nothing
		// - it does so while its theory case reports green
		// - two rules are in that state on purpose: no vehicle carries a built-in weapon with accessories or mods, though Vehicle.Create reads both
		// - pinning the vacancy fails here when a populated rule falls to zero, e.g. on an XPath broken by a data reshuffle
		// - it fails just as well when one of these two starts to match, which is when somebody has to confirm the new data is checked rather than merely counted
		[Fact]
		public void OnlyTheKnownUnpopulatedRulesMatchNothing()
		{
			string[] vacant = DataPaths.RequiredFieldContracts
				.Where(contract => contract.ItemCount == 0)
				.Select(contract => contract.Rule.Entity)
				.OrderBy(entity => entity, StringComparer.Ordinal)
				.ToArray();

			Assert.Equal(
				new[]
				{
					"Vehicle built-in weapon accessory reference",
					"Vehicle built-in weapon mod reference",
				},
				vacant);
		}

		// - the declaration side of the book-code check, guarded like the rest
		// - the theory over the data would fail loudly if this came back empty, but it cannot notice books.xml quietly losing a code that no entry happens to cite - and the next entry to cite it would then look like the defect
		[Fact]
		public void BookCodesFindsEveryDeclaredBook()
		{
			Assert.Equal(42, DataPaths.BookCodes.Count);
		}
	}
}
