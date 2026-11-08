using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Chummer.Tests.Kernel;
using Xunit;

namespace Chummer.Tests.Consumption.Bonus
{
	// - the .xsd files enumerate the tags the data uses, never the ones the code reads
	// - deliberately no allowlist: every tag in the data has a consumer today
	public class BonusVocabularyTests
	{
		[Theory]
		[MemberData(nameof(BonusVocabulary.TopLevelRuleXmlFilesWithBonuses), MemberType = typeof(BonusVocabulary))]
		public void EveryBonusTagHasACodePathThatReadsIt(string xmlPath)
		{
			AssertHandled(xmlPath, BonusVocabulary.BonusTagsFor(xmlPath));
		}

		[Theory]
		[MemberData(nameof(BonusVocabulary.PackFilesWithBonuses), MemberType = typeof(BonusVocabulary))]
		public void EveryPackBonusTagHasACodePathThatReadsIt(string packPath)
		{
			AssertHandled(packPath, BonusVocabulary.BonusTagsInPack(packPath));
		}

		private static void AssertHandled(string xmlPath, IReadOnlyList<BonusVocabulary.BonusTag> tags)
		{
			// Grouped by tag: one misspelling copied across entries is one problem.
			var unhandled = BonusVocabulary
				.UnhandledTags(tags, BonusVocabulary.VocabularyFor)
				.GroupBy(tag => tag.Consumer + " <" + tag.Tag + ">", StringComparer.Ordinal)
				.OrderBy(group => group.Key, StringComparer.Ordinal)
				.ToArray();

			if (unhandled.Length > 0)
			{
				Assert.Fail(FailureReport.Build(
					Path.GetFileName(xmlPath) + " uses " + unhandled.Length
						+ " bonus tag(s) that no code path reads, so they silently grant nothing",
					unhandled,
					group => group.Key + " on " + group.Count() + " entries, e.g. '"
						+ group.First().ItemName + "'"));
			}
		}

		// - a missing tag fails loudly, an over-reaching extractor would hide one
		// - bumping the number is only correct for a deliberately added handler
		[Fact]
		public void CreateImprovementsHandlesTheMeasuredTagCount()
		{
			Assert.Equal(85, BonusVocabulary.CreateImprovementsTags.Count);
		}

		// "initiation" has a case but no data entry: dead, not a defect.
		[Fact]
		public void EnableTabHandlesTheMeasuredTabs()
		{
			Assert.Equal(
				new[] { "adept", "critter", "initiation", "magician", "technomancer" },
				BonusVocabulary.EnabledTabs.OrderBy(tab => tab, StringComparer.Ordinal));
		}

		// - the vehicle list is hand-written: a removed read would leave it accepting a dead tag
		[Fact]
		public void EveryListedVehicleModTagIsStillRead()
		{
			Assert.Empty(BonusVocabulary.VehicleModTagsNoLongerRead());
		}
	}
}
