using System.Linq;
using System.Xml;
using Xunit;

namespace Chummer.Tests
{
	// The two theories next door only ever assert that today's files hold nothing
	// unexpected - and the book-code one finds nothing at all, so all 23 of its
	// cases would pass unchanged if the detection underneath stopped detecting.
	// The real data has no near-miss to notice the difference: no code differing
	// only in case, no category differing only in padding. These drive the same
	// code with hand-built XML instead, so the rules are pinned by something that
	// fails when they change.
	//
	// The pair of axis decisions is the main thing being held down here. Book
	// references are gathered with the deep axis because <source> means the same
	// thing at any depth; category usages are gathered from direct children only
	// because a nested <category> belongs to a reference no dropdown can select.
	// Two opposite calls, made in the same change, each correct for its own
	// reason - exactly the pair that a later "let's make these consistent" tidy-up
	// would break, and that no assertion over the real files would catch.
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

		// Pins the ordinal comparison. Options.BookXPath() emits source = "SR4",
		// and XPath equality is exact, so a lower-cased code really does fail to
		// match there - reporting it as fine here would describe behaviour the
		// application does not have.
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

		// The deep axis, pinned. metatypes.xml and critters.xml put 127 of their
		// <source> elements one level further down, on metavariants, and an
		// item-anchored query would skip every one of them without failing.
		[Fact]
		public void SourceBelowTheItemLevelIsStillFound()
		{
			XmlDocument document = Catalogue(WithMetavariant(Entry("Dwarf", "SR4"), "Gnome", "XYZ"));

			Assert.Equal(new[] { "XYZ" }, UndeclaredCodesIn(document));
		}

		// The failure message has to name the metavariant, not the metatype that
		// contains it: "Dwarf cites XYZ" would send a reader to the wrong element.
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

		// Ordinal again, and for the same reason: the picker queries
		// category = "Bike", so "bike" selects nothing.
		[Fact]
		public void CategoryDifferingOnlyInCaseIsUndeclared()
		{
			Assert.Equal(new[] { "bike" },
				UndeclaredCategoriesIn(new[] { "Bike" }, Entry("Dodge Scoot", category: "bike")));
		}

		// Symmetry with CodeWithSurroundingWhitespaceIsUndeclared. Both sides build
		// their own ordinal set, so neither inherits the other's guarantee, even
		// though within one side case and padding do share a lookup.
		[Fact]
		public void CategoryWithSurroundingWhitespaceIsUndeclared()
		{
			Assert.Equal(new[] { "Bike " },
				UndeclaredCategoriesIn(new[] { "Bike" }, Entry("Dodge Scoot", category: "Bike ")));
		}

		// The shallow axis, pinned - the mirror image of
		// SourceBelowTheItemLevelIsStillFound above. A <category> on a nested
		// reference is not a catalogue entry's category and must not be checked
		// against the declaration block, or files like cyberware.xml would report
		// the categories of their built-in options as undeclared.
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

		// Goes through the very same contract type the theory over the real files
		// uses, rather than reimplementing the set difference here.
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

		// item + <metavariants><metavariant><name/><source/></metavariant></...>,
		// the shape metatypes.xml and critters.xml actually use.
		private static XmlElement WithMetavariant(XmlElement item, string name, string source)
		{
			XmlDocument document = item.OwnerDocument;
			XmlElement wrapper = document.CreateElement("metavariants");
			wrapper.AppendChild(document.ImportNode(Entry(name, source), true));
			item.AppendChild(wrapper);

			return item;
		}

		// item + a nested catalogue-shaped child, the way cyberware.xml lists the
		// options built into a piece of cyberware.
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
