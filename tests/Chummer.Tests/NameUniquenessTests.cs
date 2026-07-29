using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace Chummer.Tests
{
	// - <name> is the de-facto primary key of the rule data - there are no ids and no guids anywhere in it
	// - a duplicate name therefore makes the second entry permanently unreachable through a name-based XPath lookup, or hands back whichever entry happens to come first in document order, depending on the call site
	//
	// - anchoring matters: an unanchored //cyberware query reports 99 apparent duplicates in cyberware.xml, because that file nests <cyberware> nodes as references to built-in options ("Thermographic Vision" inside a cybereye) rather than only as catalogue entries
	// - only the top-level items directly under each collection wrapper are catalogue entries, which is what the per-collection cases below iterate over
	public class NameUniquenessTests
	{
		// - duplicates that already exist in the data - resolving them is a separate job: each one is either a real duplicate to delete, or two different things that happen to share a name and need renaming plus a sweep of every cross-reference to them
		// - renaming is not free, because saved characters store these very strings, so a rename silently breaks every save that used the old name
		// - that work waits for the regression net that can prove what it changed
		// - until then this list keeps the check green while still failing on any *new* duplicate
		//
		// - entry format: <file>/<collection>/<key>, kept as raw text rather than structured tuples so a resolved entry is deleted by removing one line
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

		// - without this the allowlist would rot in the one direction the theory above cannot see: an entry that stops being a duplicate (data fixed, or an item renamed) would sit there forever, silently covering a future duplicate of that same name
		// - failing here forces the list to be pruned in the same change that fixed the data
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

		// - built from the printable form of the key, not the raw one: for a name-only collection the two are identical, but a composite key carries a U+001F separator that nobody can type
		// - an entry would have to read "packs.xml/packs/BrawlerAttribute Kit" with an invisible character in the middle, while every failure message prints "Brawler + Attribute Kit"
		// - someone deferring such a duplicate would copy what they were shown into the list, get no match, and see the test stay red with no hint why
		// - keeping both directions in the printable form means what the message shows is exactly what the allowlist takes
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
