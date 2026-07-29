using System.Linq;
using System.Xml;
using Xunit;

namespace Chummer.Tests
{
	// - the theories over the real files cannot pin these rules: the data has no near-miss cases
	// - the axis pair matters most: deep for <source>, direct children only for <category>
	// - each axis call is deliberate: a tidy-up making them consistent breaks one of them
	public class ReferenceIntegrityDetectionTests
	{
		private static readonly string[] DeclaredCodes = { "SR4", "SM", "AR" };

		[Fact]
		public void CodeNotInBooksXmlIsReported()
		{
			Assert.Equal(new[] { "XYZ" }, UndeclaredCodesIn(Catalogue(Entry("Ares Predator", "XYZ"))));
		}

		[Fact]
		public void DeclaredCodeIsAccepted()
		{
			Assert.Empty(UndeclaredCodesIn(Catalogue(Entry("Ares Predator", "SR4"))));
		}

		// Ordinal on purpose: Options.BookXPath() emits source = "SR4", XPath equality is exact.
		[Fact]
		public void CodeDifferingOnlyInCaseIsUndeclared()
		{
			Assert.Equal(new[] { "sr4" }, UndeclaredCodesIn(Catalogue(Entry("Ares Predator", "sr4"))));
		}

		[Fact]
		public void CodeWithSurroundingWhitespaceIsUndeclared()
		{
			Assert.Equal(new[] { "SR4 " }, UndeclaredCodesIn(Catalogue(Entry("Ares Predator", "SR4 "))));
		}

		// - metatypes.xml and critters.xml put <source> on metavariants, one level further down
		// - an item-anchored query would skip them all without failing
		[Fact]
		public void SourceBelowTheItemLevelIsStillFound()
		{
			XmlDocument document = Catalogue(WithMetavariant(Entry("Dwarf", "SR4"), "Gnome", "XYZ"));

			Assert.Equal(new[] { "XYZ" }, UndeclaredCodesIn(document));
		}

		// Naming the containing metatype would send a reader to the wrong element.
		[Fact]
		public void NestedSourceIsAttributedToTheNearestNamedElement()
		{
			XmlDocument document = Catalogue(WithMetavariant(Entry("Dwarf", "SR4"), "Gnome", "XYZ"));

			DataPaths.BookReference undeclared = DataPaths
				.ReferencesToUndeclaredBooks(DataPaths.BookReferencesIn(document), DeclaredCodes)
				.Single();

			Assert.Equal("Gnome", undeclared.ItemName);
		}

		[Fact]
		public void UndeclaredCategoryIsReported()
		{
			Assert.Equal(new[] { "Rotocraft" },
				UndeclaredCategoriesIn(new[] { "Bike", "Car" }, Entry("Banshee", category: "Rotocraft")));
		}

		[Fact]
		public void DeclaredCategoryIsAccepted()
		{
			Assert.Empty(UndeclaredCategoriesIn(new[] { "Bike", "Car" }, Entry("Dodge Scoot", category: "Bike")));
		}

		// Ordinal again, for the same reason as the book codes.
		[Fact]
		public void CategoryDifferingOnlyInCaseIsUndeclared()
		{
			Assert.Equal(new[] { "bike" },
				UndeclaredCategoriesIn(new[] { "Bike" }, Entry("Dodge Scoot", category: "bike")));
		}

		// Neither side inherits the other's guarantee: each builds its own ordinal set.
		[Fact]
		public void CategoryWithSurroundingWhitespaceIsUndeclared()
		{
			Assert.Equal(new[] { "Bike " },
				UndeclaredCategoriesIn(new[] { "Bike" }, Entry("Dodge Scoot", category: "Bike ")));
		}

		// - the shallow axis, the mirror of SourceBelowTheItemLevelIsStillFound
		// - checking nested <category> would report cyberware.xml's built-in options as undeclared
		[Fact]
		public void CategoryBelowTheItemLevelIsNotAUsage()
		{
			XmlElement collection = Collection(WithNestedEntry(
				Entry("Cybereye", category: "Bike"), "Thermographic Vision", "Rotocraft"));

			Assert.Equal(new[] { "Bike" },
				DataPaths.CategoryUsagesIn(collection).Select(usage => usage.Category).ToArray());
		}

		[Fact]
		public void ItemWithoutACategoryIsNotAUsage()
		{
			Assert.Empty(DataPaths.CategoryUsagesIn(Collection(Entry("Banshee"))));
		}

		private static string[] UndeclaredCodesIn(XmlDocument document)
		{
			return DataPaths.ReferencesToUndeclaredBooks(DataPaths.BookReferencesIn(document), DeclaredCodes)
				.Select(reference => reference.Code)
				.ToArray();
		}

		// Reimplementing the set difference here would let these facts drift from the real check.
		private static string[] UndeclaredCategoriesIn(string[] declared, params XmlElement[] items)
		{
			DataPaths.CategoryContract contract = new DataPaths.CategoryContract(
				"vehicles.xml", "vehicles", "categories", declared,
				DataPaths.CategoryUsagesIn(Collection(items)).ToArray());

			return contract.UndeclaredUsages().Select(usage => usage.Category).ToArray();
		}

		private static XmlDocument Catalogue(params XmlElement[] items)
		{
			XmlDocument document = new XmlDocument();
			XmlElement root = document.CreateElement("chummer");
			document.AppendChild(root);

			XmlElement collection = document.CreateElement("metatypes");
			root.AppendChild(collection);
			foreach (XmlElement item in items)
				collection.AppendChild(document.ImportNode(item, true));

			return document;
		}

		private static XmlElement Collection(params XmlElement[] items)
		{
			XmlDocument document = new XmlDocument();
			XmlElement collection = document.CreateElement("vehicles");
			foreach (XmlElement item in items)
				collection.AppendChild(document.ImportNode(item, true));

			return collection;
		}

		// The metavariant shape metatypes.xml and critters.xml actually use.
		private static XmlElement WithMetavariant(XmlElement item, string name, string source)
		{
			XmlDocument document = item.OwnerDocument;
			XmlElement wrapper = document.CreateElement("metavariants");
			wrapper.AppendChild(document.ImportNode(Entry(name, source), true));
			item.AppendChild(wrapper);

			return item;
		}

		// The way cyberware.xml lists the options built into a piece of cyberware.
		private static XmlElement WithNestedEntry(XmlElement item, string name, string category)
		{
			XmlDocument document = item.OwnerDocument;
			XmlElement wrapper = document.CreateElement("gears");
			wrapper.AppendChild(document.ImportNode(Entry(name, category: category), true));
			item.AppendChild(wrapper);

			return item;
		}

		private static XmlElement Entry(string name, string source = null, string category = null)
		{
			XmlDocument document = new XmlDocument();
			XmlElement item = document.CreateElement("metatype");
			document.AppendChild(item);

			AppendText(item, "name", name);
			if (source != null)
				AppendText(item, "source", source);
			if (category != null)
				AppendText(item, "category", category);

			return item;
		}

		private static void AppendText(XmlElement parent, string elementName, string value)
		{
			XmlElement element = parent.OwnerDocument.CreateElement(elementName);
			element.InnerText = value;
			parent.AppendChild(element);
		}
	}
}
