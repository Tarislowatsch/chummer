using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace Chummer.Tests
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

		// - exact count, not ">N": this file set is small and enumerable
		// - a dropped pair must fail immediately, not slip under a threshold
		// - bumping the number is only correct for a deliberate new data file plus schema
		[Fact]
		public void TopLevelRuleXmlWithSchemaFilesFindsAllPairs()
		{
			Assert.Equal(26, DataPaths.TopLevelRuleXmlWithSchemaFiles().Count());
		}

		// - the allowlist guard next door notices a dropped file for only 8 of the 42 collections
		// - a discovery gap would leave spells.xml or qualities.xml unchecked while staying green
		// - bumping the number is only correct for a deliberate new collection wrapper
		[Fact]
		public void TopLevelRuleXmlCollectionsFindsEveryNamedCollection()
		{
			Assert.Equal(42, DataPaths.TopLevelRuleXmlCollections().Count());
		}

		// - derivable: 27 top-level files minus the four carrying no <source>
		// - a file dropped from the scan would leave its book references unchecked
		[Fact]
		public void TopLevelRuleXmlFilesCitingBooksFindsEveryCitingFile()
		{
			Assert.Equal(23, DataPaths.TopLevelRuleXmlFilesCitingBooks().Count());
		}

		// - derivable: 24 collections carry <category>, 2 are exempt, 2 sit in files with no block
		// - any of the three rules quietly changing scope makes the number stop adding up
		[Fact]
		public void CategoryKeyedCollectionsCoversEveryGovernedCollection()
		{
			Assert.Equal(20, DataPaths.CategoryKeyedCollections().Count());
		}

		// - a renamed collection leaves its hand-written exemption applying to nothing
		// - the count above cannot tell that silence from a legitimate change
		// - both structures share the check: a key belongs to exactly one of them
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

		// - a rule dropped from the hand-written table takes one entity type's whole contract with it
		// - 29 = 21 Create methods + 2 second-file rules + 5 nested references + 1 built-in <mount>
		// - bumping the number is only correct for a deliberately added rule
		[Fact]
		public void RequiredFieldRulesCoverEveryEntityType()
		{
			Assert.Equal(29, DataPaths.RequiredFieldRules.Count);
		}

		// - the only guard that looks at the production code rather than at the table or the data
		// - blind spot: a new unguarded read inside a Create it already knows about
		// - bumping the number is only correct together with a rule for the new entity type
		[Fact]
		public void NoEntityCreateMethodHasAppearedWithoutARule()
		{
			IReadOnlyList<string> sites = DataPaths.EntityCreateMethodSites();

			Assert.True(sites.Count == 21,
				"The rule table was read off 21 Create(XmlNode ...) methods, but the sources now "
				+ "hold " + sites.Count + ". Every one of them needs an entry in "
				+ nameof(DataPaths.RequiredFieldRules) + ":\n  " + string.Join("\n  ", sites));
		}

		// - a Create changing underneath its rule is drift no check over the table or the data can see
		// - blind spot: a field deleted from the rule table by hand while the method stays put
		// - updating a hash is only correct after re-reading the method and fixing its rule
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

		// - a pin deleted by hand leaves its class unwatched by the drift check
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

		// - an overlap or a gap checks a gear entry against the wrong Create's contract or against none
		// - this pins the partition over the real file, not the hand-built one next door
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

		// - a rule matching nothing checks nothing while its theory case stays green
		// - two rules are vacant on purpose: no vehicle's built-in weapon has accessories or mods
		// - fails when a populated rule falls to zero or when a vacant one starts to match
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

		// - the data-side theory cannot notice books.xml quietly losing a code no entry cites
		// - the next entry to cite the lost code would then look like the defect
		[Fact]
		public void BookCodesFindsEveryDeclaredBook()
		{
			Assert.Equal(42, DataPaths.BookCodes.Count);
		}
	}
}
