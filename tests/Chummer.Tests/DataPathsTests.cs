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
