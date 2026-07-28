using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using Xunit;

namespace Chummer.Tests
{
	// <name> is the de-facto primary key of the rule data - there are no ids and
	// no guids anywhere in it. A duplicate name therefore makes the second entry
	// permanently unreachable through a name-based XPath lookup, or hands back
	// whichever entry happens to come first in document order, depending on the
	// call site.
	//
	// Anchoring matters: an unanchored //cyberware query reports 99 apparent
	// duplicates in cyberware.xml, because that file nests <cyberware> nodes as
	// references to built-in options ("Thermographic Vision" inside a cybereye)
	// rather than only as catalogue entries. Only the top-level items directly
	// under each collection wrapper are catalogue entries, which is what the
	// per-collection cases below iterate over.
	public class NameUniquenessTests
	{
		// Enough to print every duplicate this repo has in its worst collection
		// today without an unbounded message if a future file racks up hundreds.
		private const int MaxDuplicatesInMessage = 20;

		// Duplicates that already exist in the data. Resolving them is a separate
		// job: each one is either a real duplicate to delete or two different
		// things that happen to share a name and need renaming plus a sweep of
		// every cross-reference to them - and renaming is not free, because saved
		// characters store these very strings, so a rename silently breaks every
		// save that used the old name. That work waits for the regression net that
		// can prove what it changed; until then this list keeps the check green
		// while still failing on any *new* duplicate.
		//
		// Entry format: <file>/<collection>/<key>. Kept as raw text rather than
		// structured tuples so a resolved entry is deleted by removing one line.
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
				StringBuilder message = new StringBuilder();
				message.Append(Path.GetFileName(xmlPath)).Append(" has ").Append(unexpected.Length)
					.Append(" duplicate name(s) in <").Append(collectionName).Append(">")
					.Append(" (key: ").Append(string.Join(" + ", KeyFieldsFor(xmlPath, collectionName)))
					.Append("):");
				foreach (KeyValuePair<string, int> pair in unexpected.Take(MaxDuplicatesInMessage))
				{
					message.Append("\n  '").Append(Display(pair.Key)).Append("' appears ")
						.Append(pair.Value).Append(" times");
				}
				if (unexpected.Length > MaxDuplicatesInMessage)
				{
					message.Append("\n  ... and ").Append(unexpected.Length - MaxDuplicatesInMessage)
						.Append(" more");
				}
				Assert.Fail(message.ToString());
			}
		}

		// Without this the allowlist would rot in the one direction the theory
		// above cannot see: an entry that stops being a duplicate (because the
		// data was fixed, or an item renamed) would sit there forever, silently
		// covering a future duplicate of that same name. Failing here forces the
		// list to be pruned in the same change that fixed the data.
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
				+ nameof(KnownDuplicates) + ":\n  " + string.Join("\n  ", stale.Select(Display)));
		}

		private static IEnumerable<KeyValuePair<string, int>> DuplicateKeyCounts(
			string xmlPath, string collectionName)
		{
			string[] keyFields = KeyFieldsFor(xmlPath, collectionName);

			XmlDocument document = new XmlDocument();
			document.Load(xmlPath);

			// Matched by element name rather than an XPath step: collection names
			// come from the data, and skills.xml has wrappers like <Animal
			// Husbandry> whose names are not valid XPath at all.
			XmlNode collection = document.DocumentElement?.ChildNodes.Cast<XmlNode>()
				.FirstOrDefault(node => node.NodeType == XmlNodeType.Element
					&& string.Equals(node.Name, collectionName, StringComparison.Ordinal));
			if (collection == null)
				return Enumerable.Empty<KeyValuePair<string, int>>();

			return collection.ChildNodes.Cast<XmlNode>()
				.Where(item => item.NodeType == XmlNodeType.Element && item["name"] != null)
				.GroupBy(item => BuildKey(item, keyFields), StringComparer.Ordinal)
				.Where(group => group.Count() > 1)
				.Select(group => new KeyValuePair<string, int>(group.Key, group.Count()));
		}

		// Ordinal, untrimmed, case-sensitive - on purpose. This mirrors what the
		// application itself does: a lookup like
		// SelectSingleNode("/chummer/gears/gear[name = \"...\"]") compares the raw
		// string codepoint for codepoint, so two entries differing only in case or
		// in surrounding whitespace genuinely are two reachable entries, not a
		// collision. Relaxing this to OrdinalIgnoreCase or adding Trim() would
		// look like a tidy-up and would in fact make the test disagree with the
		// behaviour it exists to describe.
		private static string BuildKey(XmlNode item, string[] keyFields)
		{
			return string.Join(DataPaths.KeyFieldSeparator,
				keyFields.Select(field => item[field]?.InnerText ?? string.Empty));
		}

		private static string[] KeyFieldsFor(string xmlPath, string collectionName)
		{
			return DataPaths.KeyFieldsFor(Path.GetFileName(xmlPath), collectionName);
		}

		private static string Entry(string xmlPath, string collectionName, string key)
		{
			return Path.GetFileName(xmlPath) + "/" + collectionName + "/" + key;
		}

		private static string Display(string value)
		{
			return value.Replace(DataPaths.KeyFieldSeparator, " + ");
		}
	}
}
