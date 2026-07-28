using System.Linq;
using Xunit;

namespace Chummer.Tests
{
	// A [Theory] with an empty MemberData source runs zero cases and reports
	// green - so if path resolution ever broke (wrong directory, a rename), the
	// well-formedness theories below would silently stop checking anything
	// while CI stayed green. These guard against that by asserting each source
	// actually found a plausible number of files.
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

		// Exact rather than the ">N" style above: the top-level Chummer/data/*.xml
		// set is a small, deliberately-enumerable universe (27 top-level XML
		// files, 26 with a matching .xsd - only improvements.xml has none),
		// unlike the recursively-grown data/lang/sheet corpora the guards above
		// watch. An exact count turns a silently-dropped pair (see the
		// file-rename risk noted on TopLevelRuleXmlWithSchemaFiles) into an
		// immediate failure instead of a threshold that would tolerate losing
		// one or two pairs unnoticed. The 26 is expected to stay put; a
		// deliberate new top-level data file + schema is the one case where
		// bumping this number on purpose is the correct fix, not a workaround.
		[Fact]
		public void TopLevelRuleXmlWithSchemaFilesFindsAllPairs()
		{
			Assert.Equal(26, DataPaths.TopLevelRuleXmlWithSchemaFiles().Count());
		}

		// Exact for the same reason as the pair count above. The allowlist guard
		// in NameUniquenessTests only half-covers this: it notices a dropped file
		// that happened to hold an allowlisted duplicate, but only 8 of these 42
		// collections do. Were discovery to quietly stop finding spells.xml or
		// qualities.xml, that guard would stay green while those collections
		// simply stopped being checked. Adding a collection wrapper to the data
		// is the one case where bumping this number deliberately is the right
		// fix rather than a workaround.
		[Fact]
		public void TopLevelRuleXmlCollectionsFindsEveryNamedCollection()
		{
			Assert.Equal(42, DataPaths.TopLevelRuleXmlCollections().Count());
		}
	}
}
