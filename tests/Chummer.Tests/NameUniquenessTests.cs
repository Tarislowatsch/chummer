using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace Chummer.Tests
{
	// - <name> is the de-facto primary key: the data has no ids or guids
	// - a duplicate makes the second entry unreachable through name-based XPath lookups
	// - only direct children of the wrapper are entries: nested <cyberware> nodes are references
	public class NameUniquenessTests
	{
		// - resolving these is deferred: a rename silently breaks every save storing the old name
		// - format: <file>/<collection>/<key>, one line per entry
		private static readonly HashSet<string> KnownDuplicates = new HashSet<string>(StringComparer.Ordinal)
		{
			"armor.xml/mods/Transparent Ruthenium Polymer Coating",
			"critterpowers.xml/powers/Binding",
			"critterpowers.xml/powers/Engulf",
			"critterpowers.xml/powers/Spirit Pact",
			"critters.xml/metatypes/Musk Ox",
			"critters.xml/metatypes/Ross Seal",
			"cyberware.xml/cyberwares/Radio (2050)",
			"cyberware.xml/cyberwares/Telephone (2050)",
			"gear.xml/gears/Crimson Orchid",
			"gear.xml/gears/Deepweed",
			"gear.xml/gears/Hardening",
			"gear.xml/gears/Immortal Flower",
			"gear.xml/gears/Microphone",
			"gear.xml/gears/Optimization",
			"gear.xml/gears/Orichalcum",
			"gear.xml/gears/Overdrive",
			"gear.xml/gears/Targeting",
			"vehicles.xml/vehicles/Dodge Scoot (Scooter)",
			"weapons.xml/weapons/Bottle",
		};

		[Theory]
		[MemberData(nameof(DataPaths.TopLevelRuleXmlCollections), MemberType = typeof(DataPaths))]
		public void NamesAreUniqueWithinCollection(string xmlPath, string collectionName)
		{
			KeyValuePair<string, int>[] unexpected = DuplicateKeyCounts(xmlPath, collectionName)
				.Where(pair => !KnownDuplicates.Contains(Entry(xmlPath, collectionName, pair.Key)))
				.OrderBy(pair => pair.Key, StringComparer.Ordinal)
				.ToArray();

			if (unexpected.Length > 0)
			{
				Assert.Fail(FailureReport.Build(
					Path.GetFileName(xmlPath) + " has " + unexpected.Length
						+ " duplicate name(s) in <" + collectionName + "> (key: "
						+ string.Join(" + ", DataPaths.RuleCollectionFor(xmlPath, collectionName).KeyFields)
						+ ")",
					unexpected,
					pair => "'" + Display(pair.Key) + "' appears " + pair.Value + " times"));
			}
		}

		// - a stale entry would silently cover a future duplicate of the same name
		// - failing forces the list to be pruned in the change that fixed the data
		[Fact]
		public void AllowlistedDuplicatesAllStillExist()
		{
			HashSet<string> actual = new HashSet<string>(StringComparer.Ordinal);
			foreach (object[] testCase in DataPaths.TopLevelRuleXmlCollections())
			{
				string xmlPath = (string)testCase[0];
				string collectionName = (string)testCase[1];
				foreach (KeyValuePair<string, int> pair in DuplicateKeyCounts(xmlPath, collectionName))
					actual.Add(Entry(xmlPath, collectionName, pair.Key));
			}

			string[] stale = KnownDuplicates.Where(entry => !actual.Contains(entry))
				.OrderBy(entry => entry, StringComparer.Ordinal)
				.ToArray();

			Assert.True(stale.Length == 0,
				"These entries are no longer duplicates in the data and must be removed from "
				+ nameof(KnownDuplicates) + ":\n  " + string.Join("\n  ", stale));
		}

		private static IEnumerable<KeyValuePair<string, int>> DuplicateKeyCounts(
			string xmlPath, string collectionName)
		{
			return DataPaths.DuplicateItemKeys(
				DataPaths.RuleCollectionFor(xmlPath, collectionName).ItemKeys);
		}

		// - printable form, not the raw key: a composite key carries a U+001F nobody can type
		// - what the failure message prints must be exactly what the allowlist takes
		private static string Entry(string xmlPath, string collectionName, string key)
		{
			return Path.GetFileName(xmlPath) + "/" + collectionName + "/" + Display(key);
		}

		private static string Display(string value)
		{
			return value.Replace(DataPaths.KeyFieldSeparator, " + ");
		}
	}
}
