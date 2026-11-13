using System.Collections.Generic;
using System.IO;
using System.Linq;
using Chummer.Tests.Consumption.Bonus;
using Chummer.Tests.Consumption.Expressions;
using Chummer.Tests.Consumption.Fields;
using Chummer.Tests.Localization;
using Chummer.Tests.Rulebook;
using Xunit;

namespace Chummer.Tests.Kernel
{
	// - a [Theory] with an empty MemberData source reports green on zero cases
	// - broken path resolution would silently disable the well-formedness theories
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

		// - derivable: 16 sheets (8 top-level, 7 de, 1 fr) plus the one export template
		// - the four .xslt are left out on purpose: they compile through their includes
		[Fact]
		public void LoadableStylesheetsFindsEverySheetAndExport()
		{
			Assert.Equal(17, DataPaths.LoadableStylesheets().Count());
		}

		// - the orphan guard is green on zero partials: the selector needs its own pin
		// - Shadowrun 4 Base.xslt and ConditionMonitor.xslt, once at the top level and once in de
		[Fact]
		public void PartialStylesheetsFindsEveryPartial()
		{
			Assert.Equal(4, DataPaths.PartialStylesheets().Count);
		}

		// - a broken extraction finds no calls: an empty set has no missing keys
		// - a floor, not exact: this counts code, which every later phase edits (2798 today)
		[Fact]
		public void UiStringCallsFindsTheLiteralCalls()
		{
			int literal = UiStringKeys.AllCalls.Count(call => UiStringKeys.LiteralKey(call.Argument) != null);

			Assert.True(literal >= 2500, "Only " + literal + " literal GetString calls found.");
		}

		// - 28 calls in the 8 forms of UiStringKeys.ComposedForms
		[Fact]
		public void UiStringCallsFindsEveryComposedCall()
		{
			Assert.Equal(28, UiStringKeys.AllCalls.Count(call => UiStringKeys.LiteralKey(call.Argument) == null
				&& !UiStringKeys.IsTagLookup(call.Argument)));
		}

		[Fact]
		public void UiStringCallsFindsEveryTagLookup()
		{
			Assert.Equal(25, UiStringKeys.AllCalls.Count(call => UiStringKeys.IsTagLookup(call.Argument)));
		}

		[Fact]
		public void EnglishKeysFindsEveryKey()
		{
			Assert.Equal(1613, UiStringKeys.EnglishKeys.Count);
		}

		// - exact count, not ">N": this file set is small and enumerable
		// - a dropped pair must fail immediately, not slip under a threshold
		// - bumping the number is only correct for a deliberate new data file plus schema
		[Fact]
		public void TopLevelRuleXmlWithSchemaFilesFindsAllPairs()
		{
			Assert.Equal(26, DataPaths.TopLevelRuleXmlWithSchemaFiles().Count());
		}

		// - NameUniquenessTests' allowlist guard catches a dropped file in only 8 of 42 collections
		// - a discovery gap would leave spells.xml or qualities.xml unchecked while staying green
		// - bumping the number is only correct for a deliberate new collection wrapper
		[Fact]
		public void TopLevelRuleXmlCollectionsFindsEveryNamedCollection()
		{
			Assert.Equal(42, NameUniqueness.TopLevelRuleXmlCollections().Count());
		}

		// - derivable: 27 top-level files minus the four carrying no <source>
		// - a file dropped from the scan would leave its book references unchecked
		[Fact]
		public void TopLevelRuleXmlFilesCitingBooksFindsEveryCitingFile()
		{
			Assert.Equal(23, BookReferences.TopLevelRuleXmlFilesCitingBooks().Count());
		}

		// - derivable: 18 files carry a <bonus>, plus improvements.xml through <internal>
		// - a file dropped from the scan would leave its bonus tags unchecked
		[Fact]
		public void TopLevelRuleXmlFilesWithBonusesFindsEveryBonusFile()
		{
			Assert.Equal(19, BonusVocabulary.TopLevelRuleXmlFilesWithBonuses().Count());
		}

		// - derivable: 24 collections carry <category>, 2 are exempt, 2 sit in files with no block
		// - any of the three rules quietly changing scope makes the number stop adding up
		[Fact]
		public void CategoryKeyedCollectionsCoversEveryGovernedCollection()
		{
			Assert.Equal(20, CategoryDeclarations.CategoryKeyedCollections().Count());
		}

		// - a rule dropped from the hand-written table takes one entity type's whole contract with it
		// - 29 = 21 Create methods + 2 second-file rules + 5 nested references + 1 built-in <mount>
		// - bumping the number is only correct for a deliberately added rule
		[Fact]
		public void RequiredFieldRulesCoverEveryEntityType()
		{
			Assert.Equal(29, RequiredFields.Rules.Count);
		}

		// - the only guard that looks at the production code rather than at the table or the data
		// - blind spot: a new unguarded read inside a Create it already knows about
		// - bumping the number is only correct together with a rule for the new entity type
		[Fact]
		public void NoEntityCreateMethodHasAppearedWithoutARule()
		{
			IReadOnlyList<string> sites = RequiredFields.EntityCreateMethodSites();

			Assert.True(sites.Count == 21,
				"The rule table was read off 21 Create(XmlNode ...) methods, but the sources now "
				+ "hold " + sites.Count + ". Every one of them needs an entry in "
				+ nameof(RequiredFields) + "." + nameof(RequiredFields.Rules) + ":\n  " + string.Join("\n  ", sites));
		}

		// - the data-side theory cannot notice books.xml quietly losing a code no entry cites
		// - the next entry to cite the lost code would then look like the defect
		[Fact]
		public void BookCodesFindsEveryDeclaredBook()
		{
			Assert.Equal(42, BookReferences.BookCodes.Count);
		}

		// - 23 files in 6 packs: a discovery gap would drop a whole pack from every pack check
		// - also pins PackFiles.PairedCases: the pairing guard makes every file a paired one
		[Fact]
		public void PackFilesFindsEveryShippedPackFile()
		{
			Assert.Equal(23, PackFiles.All.Count);
		}

		// - top-level files plus every pack file Load would merge
		// - bumping the number is only correct for a deliberately added or removed data file
		[Fact]
		public void DataFilesWithPacksFindsEveryDataFile()
		{
			Assert.Equal(50, DataPaths.DataFilesWithPacks.Count);
		}

		[Fact]
		public void FilesWithPacksRejectsAMisspelledBaseFile()
		{
			Assert.Throws<FileNotFoundException>(() => DataPaths.FilesWithPacks("metatypes.xml", "Critters.xml"));
		}

		// - derivable: every pack file, since each base file it pairs with has a schema
		[Fact]
		public void PackXmlWithSchemaFilesFindsEveryPair()
		{
			Assert.Equal(23, CustomContent.PackXmlWithSchemaFiles().Count());
		}

		// - derivable: 23 pack files minus the five books files, which cite nothing
		[Fact]
		public void PackFilesCitingBooksFindsEveryCitingFile()
		{
			Assert.Equal(18, CustomContent.PackFilesCitingBooks().Count());
		}

		// - a pack scoped by the wrong file name reaches no rule: its pack theories run zero cases
		// - one case per pack file and rule that reaches an entry there
		[Fact]
		public void PackEntitiesFindsEveryRuleAPackReaches()
		{
			Assert.Equal(18, RequiredFields.PackEntities().Count());
		}

		[Fact]
		public void PackNumericEntitiesFindsEveryRuleAPackReaches()
		{
			Assert.Equal(15, NumericFields.PackEntities().Count());
		}

		// - per field: a total would hide one field losing what another gains
		[Fact]
		public void PackExpressionRuleIdsFindsEveryRuleAPackReaches()
		{
			Assert.Equal(new[] { 15, 13, 1, 6, 7 }, new[] { "avail", "cost", "ess", "capacity", "armorcapacity" }
				.Select(field => ExpressionChecks.PackRuleIds(field).Count()));
		}

		// - aprilfools metatypes, datahaven1 critters, rigger4 cyberware
		[Fact]
		public void PackFilesWithBonusesFindsEveryBonusFile()
		{
			Assert.Equal(3, BonusVocabulary.PackFilesWithBonuses().Count());
		}

		[Fact]
		public void PackReferenceRuleIdsFindsEveryRuleAPackUses()
		{
			Assert.Equal(16, CrossFileReferences.PackRuleIds().Count());
		}
	}
}
