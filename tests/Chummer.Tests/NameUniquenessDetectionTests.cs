using System.Collections.Generic;
using System.Linq;
using System.Xml;
using Xunit;

namespace Chummer.Tests
{
	// - the theory over the real files cannot pin the rules: the data has no near-miss pairs
	// - hand-built XML here fails when the rules change
	// - case- and whitespace-sensitivity mirrors the application's XPath lookups, not a preference
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

		// - a printable separator lets shifted field splits collide into a phantom duplicate
		// - nothing else in the suite notices if U+001F is replaced by a printable character
		// - both entries must embed the separator inside a value or the case pins nothing
		[Fact]
		public void CompositeKeyPartsCannotBeForgedByEmbeddingTheSeparator()
		{
			Assert.Empty(DuplicateKeysIn(NameAndCategory,
				Item("Brawler", "Attribute Kits + Gear Kits"),
				Item("Brawler + Attribute Kits", "Gear Kits")));
		}

		// - the no-<name> rule keeps <categories>, <costs> and group wrappers out without a skip list
		// - accepting nameless elements would flood the theory with wrapper cases
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
			// The absent field must not make the two entries distinct.
			Assert.Single(DuplicateKeysIn(NameAndCategory,
				Item("Brawler"),
				Item("Brawler")));
		}

		// Reimplementing the grouping here would let these facts drift from the check they describe.
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
