using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace Chummer.Tests
{
	// - Gear.Create reads <avail> unguarded (clsEquipment.cs:9552): a missing field closes the app
	// - nothing on the way out catches the throw (clsXmlManager.cs:145 rethrows unfiltered)
	// - the .xsd files were aligned with the data, never with what a Create dereferences
	public class RequiredFieldContractTests
	{
		[Theory]
		[MemberData(nameof(RequiredFields.Entities), MemberType = typeof(RequiredFields))]
		public void EveryEntryCarriesTheFieldsItsCreateReadsUnguarded(string entity)
		{
			AssertComplete(RequiredFields.ContractFor(entity));
		}

		// - a missing field crashes Create once a user copies the pack into data/
		[Theory]
		[MemberData(nameof(RequiredFields.PackEntities), MemberType = typeof(RequiredFields))]
		public void EveryPackEntryCarriesTheFieldsItsCreateReadsUnguarded(string packPath, string entity)
		{
			AssertComplete(RequiredFields.PackContractFor(packPath, entity));
		}

		private static void AssertComplete(RequiredFields.RequiredFieldContract contract)
		{
			string entity = contract.Rule.Entity;

			// - a missing field usually vanishes from a whole batch of entries at once
			// - grouping by field shows how many distinct fields broke
			var absent = contract.MissingFields
				.GroupBy(missing => missing.Field, StringComparer.Ordinal)
				.OrderBy(group => group.Key, StringComparer.Ordinal)
				.ToArray();

			if (absent.Length > 0)
			{
				Assert.Fail(FailureReport.Build(
					Path.GetFileName(contract.FilePath) + " has " + entity + " entries missing "
						+ absent.Length + " field(s) that " + entity + ".Create reads unguarded",
					absent,
					group => "<" + group.Key + "> absent from " + group.Count()
						+ " entries, e.g. '" + group.First().ItemName + "'"));
			}
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
			IReadOnlyDictionary<string, string> actual = RequiredFields.EntityCreateFingerprints();

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
				+ "them in " + nameof(RequiredFields) + "." + nameof(RequiredFields.Rules)
				+ " and the diverted values in " + nameof(NumericFields)
				+ " may no longer describe them. Re-read each one, fix "
				+ "its rule and diversions, then update the hash:\n  "
				+ string.Join("\n  ", drifted));
		}

		// - a pin deleted by hand leaves its class unwatched by the drift check
		// - the count guard in DataPathsTests cannot see that either: it counts methods, not pins
		[Fact]
		public void EveryCreateMethodHasAPinnedFingerprint()
		{
			string[] unpinned = RequiredFields.EntityCreateFingerprints().Keys
				.Where(className => !CreateMethodFingerprints.ContainsKey(className))
				.OrderBy(className => className, StringComparer.Ordinal)
				.ToArray();

			Assert.True(unpinned.Length == 0,
				"These Create methods have no pinned fingerprint, so the drift check does not "
				+ "watch them:\n  " + string.Join("\n  ", unpinned));
		}

		// - an overlap or a gap checks a gear entry against the wrong Create's contract or against none
		// - pins the partition over the real file, not the detection tests' hand-built one
		[Fact]
		public void TheGearRulesBetweenThemCoverEveryGearEntry()
		{
			int partitioned = new[] { "Gear", "Commlink", "OperatingSystem" }
				.Sum(entity => RequiredFields.ContractFor(entity).ItemCount);

			Assert.Equal(
				NameUniqueness.RuleCollectionFor(Path.Combine(DataPaths.ChummerDataDir, "gear.xml"), "gears")
					.ItemKeys.Count,
				partitioned);
		}

		// - a rule matching nothing checks nothing while its theory case stays green
		// - two rules are vacant on purpose: no vehicle's built-in weapon has accessories or mods
		// - fails when a populated rule falls to zero or when a vacant one starts to match
		[Fact]
		public void OnlyTheKnownUnpopulatedRulesMatchNothing()
		{
			string[] vacant = RequiredFields.Contracts
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

		// - summed over all packs: the case pin only notices an entity losing its last entry
		private static readonly IReadOnlyDictionary<string, int> PinnedPackEntryCounts =
			new Dictionary<string, int>(StringComparer.Ordinal)
			{
				{ "Armor", 1 },
				{ "ArmorMod", 1 },
				{ "CritterPower", 1 },
				{ "Cyberware", 1 },
				{ "Gear", 13 },
				{ "Metamagic", 1 },
				{ "Vehicle", 21 },
				{ "Vehicle built-in weapon reference", 1 },
				{ "VehicleMod", 4 },
				{ "Weapon", 3 },
			};

		[Fact]
		public void EveryPackEntityReachesItsMeasuredEntryCount()
		{
			Dictionary<string, int> actual = RequiredFields.PackEntities()
				.Select(testCase => RequiredFields.PackContractFor((string)testCase[0], (string)testCase[1]))
				.GroupBy(contract => contract.Rule.Entity, StringComparer.Ordinal)
				.ToDictionary(group => group.Key, group => group.Sum(contract => contract.ItemCount),
					StringComparer.Ordinal);

			PinReport.AssertEqual("Pack entries per entity", PinnedPackEntryCounts, actual);
		}
	}
}
