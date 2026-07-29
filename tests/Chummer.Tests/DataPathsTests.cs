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

		// - the declaration side of the book-code check, guarded like the rest
		// - the theory over the data would fail loudly if this came back empty, but it cannot notice books.xml quietly losing a code that no entry happens to cite - and the next entry to cite it would then look like the defect
		[Fact]
		public void BookCodesFindsEveryDeclaredBook()
		{
			Assert.Equal(42, DataPaths.BookCodes.Count);
		}
	}
}
