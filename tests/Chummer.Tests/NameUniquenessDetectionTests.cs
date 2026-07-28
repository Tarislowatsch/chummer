using System.Collections.Generic;
using System.Linq;
using System.Xml;
using Xunit;

namespace Chummer.Tests
{
	// The theory next door only ever asserts that today's files hold nothing
	// unexpected. Every one of its 42 cases would still pass if the detection
	// underneath it stopped detecting anything at all, or if the comparison rules
	// were quietly relaxed - the real data has no near-miss pairs to notice the
	// difference. These drive the same code with hand-built XML instead, so the
	// rules are pinned by something that fails when they change.
	//
	// The comparison rules are a deliberate mirror of the application's own
	// name-based XPath lookups, not a preference: entries differing only in case
	// or in surrounding whitespace really are two separately reachable entries
	// there. That is stated in a comment on BuildKey, and a comment cannot stop a
	// well-meant switch to OrdinalIgnoreCase or Trim(). The two "distinct" facts
	// below can.
	public class NameUniquenessDetectionTests
	{
		private static readonly string[] NameOnly = { "name" };
		private static readonly string[] NameAndCategory = { "name", "category" };

		[Fact]
		public void RepeatedNameIsADuplicate()
		{
			string[] duplicates = DuplicateKeysIn(NameOnly,
				Item("Bottle"),
				Item("Bottle"));

			Assert.Equal(new[] { "Bottle" }, duplicates);
		}

		[Fact]
		public void DistinctNamesAreNotDuplicates()
		{
			Assert.Empty(DuplicateKeysIn(NameOnly,
				Item("Bottle"),
				Item("Club")));
		}

		[Fact]
		public void NamesDifferingOnlyInCaseAreDistinct()
		{
			Assert.Empty(DuplicateKeysIn(NameOnly,
				Item("Bottle"),
				Item("bottle")));
		}

		[Fact]
		public void NamesDifferingOnlyInSurroundingWhitespaceAreDistinct()
		{
			Assert.Empty(DuplicateKeysIn(NameOnly,
				Item("Bottle"),
				Item("Bottle ")));
		}

		[Fact]
		public void CompositeKeyIgnoresANameRepeatedUnderADifferentCategory()
		{
			Assert.Empty(DuplicateKeysIn(NameAndCategory,
				Item("Brawler", "Attribute Kits"),
				Item("Brawler", "Gear Kits")));
		}

		[Fact]
		public void CompositeKeyCatchesANameRepeatedUnderTheSameCategory()
		{
			string[] duplicates = DuplicateKeysIn(NameAndCategory,
				Item("Brawler", "Attribute Kits"),
				Item("Brawler", "Attribute Kits"));

			Assert.Single(duplicates);
		}

		// A composite key must not be forgeable by shifting the split between its
		// parts. Both entries below join to the same text if the separator is
		// something a value can itself contain - "Brawler" + "Attribute Kits +
		// Gear Kits" and "Brawler + Attribute Kits" + "Gear Kits" both read
		// "Brawler + Attribute Kits + Gear Kits" - and would be reported as a
		// duplicate that does not exist. The U+001F separator is what rules that
		// out, and nothing else in the suite would notice if it were replaced by a
		// printable one.
		// Note both entries must carry the separator inside a value: with a fixed
		// two-field key, join always emits exactly one separator, so a construction
		// that leaves one field empty cannot collide either way and would pin
		// nothing.
		[Fact]
		public void CompositeKeyPartsCannotBeForgedByEmbeddingTheSeparator()
		{
			Assert.Empty(DuplicateKeysIn(NameAndCategory,
				Item("Brawler", "Attribute Kits + Gear Kits"),
				Item("Brawler + Attribute Kits", "Gear Kits")));
		}

		// The rule that decides which elements are catalogue entries at all. It is
		// what keeps <categories>, <costs> and the skills.xml group wrappers out of
		// the check without a hand-maintained skip list, so it is worth pinning:
		// were it to start accepting elements without a <name>, those wrappers
		// would flood the theory with meaningless cases.
		[Fact]
		public void ElementsWithoutANameAreNotEntries()
		{
			XmlElement collection = Collection(Item("Bottle"));
			XmlElement stray = collection.OwnerDocument.CreateElement("category");
			stray.InnerText = "Blades";
			collection.AppendChild(stray);

			Assert.Equal(new[] { "Bottle" }, DataPaths.ItemKeysIn(collection, NameOnly).ToArray());
		}

		[Fact]
		public void MissingKeyFieldCountsAsEmptyRatherThanThrowing()
		{
			// Two entries sharing a name, neither carrying the category the key
			// asks for, still collide - the absent field cannot make them distinct.
			Assert.Single(DuplicateKeysIn(NameAndCategory,
				Item("Brawler"),
				Item("Brawler")));
		}

		// Goes through the very same detection the theory over the real files uses.
		// Reimplementing the grouping here would let the two drift apart and leave
		// these facts passing while the check they describe had changed.
		private static string[] DuplicateKeysIn(string[] keyFields, params XmlElement[] items)
		{
			return DataPaths.DuplicateItemKeys(DataPaths.ItemKeysIn(Collection(items), keyFields))
				.Select(pair => pair.Key.Replace(DataPaths.KeyFieldSeparator, " + "))
				.ToArray();
		}

		private static XmlElement Collection(params XmlElement[] items)
		{
			XmlDocument document = new XmlDocument();
			XmlElement collection = document.CreateElement("things");
			foreach (XmlElement item in items)
				collection.AppendChild(document.ImportNode(item, true));

			return collection;
		}

		private static XmlElement Item(string name, string category = null)
		{
			XmlDocument document = new XmlDocument();
			XmlElement item = document.CreateElement("thing");

			XmlElement nameElement = document.CreateElement("name");
			nameElement.InnerText = name;
			item.AppendChild(nameElement);

			if (category != null)
			{
				XmlElement categoryElement = document.CreateElement("category");
				categoryElement.InnerText = category;
				item.AppendChild(categoryElement);
			}

			return item;
		}
	}
}
