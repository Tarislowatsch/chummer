using System.Collections.Generic;
using System.Linq;
using System.Xml;
using Chummer.Tests.Kernel;
using Xunit;

namespace Chummer.Tests.Rulebook
{
	// - hand-built input: the shipped packs are too clean to show each check can fail
	public class CustomContentDetectionTests
	{
		private static readonly string[] BaseFiles = { "gear.xml", "powers.xml", "critterpowers.xml", "improvements.xml" };

		[Theory]
		[InlineData("custom_mypack_gear.xml", "custom", "gear.xml")]
		[InlineData("override_mypack_gear.xml", "override", "gear.xml")]
		[InlineData("CUSTOM_MyPack_Gear.xml", "custom", "gear.xml")]
		[InlineData("custom_gear.xml", "custom", "gear.xml")]
		[InlineData("custom_rigger_critterpowers.xml", "custom", "critterpowers.xml")]
		public void ClassifyPairsAFileTheWayLoadsGlobDoes(string fileName, string prefix, string baseFile)
		{
			PackFiles.PackFile file = PackFiles.Classify(@"C:\pack\" + fileName, BaseFiles);

			Assert.Equal(prefix, file.Prefix);
			Assert.Equal(new[] { baseFile }, file.BaseFileNames);
		}

		[Fact]
		public void ClassifyLeavesAFileWithoutPrefixUnpaired()
		{
			Assert.Null(PackFiles.Classify(@"C:\pack\mypack_gear.xml", BaseFiles).Prefix);
		}

		[Fact]
		public void ClassifyNeverPairsWithImprovements()
		{
			Assert.Empty(PackFiles.Classify(@"C:\pack\custom_mypack_improvements.xml", BaseFiles).BaseFileNames);
		}

		[Fact]
		public void ClassifyReportsEveryBaseAnAmbiguousNameMatches()
		{
			PackFiles.PackFile file = PackFiles.Classify(
				@"C:\pack\custom_extra_gear.xml", new[] { "gear.xml", "extra_gear.xml" });

			Assert.Equal(new[] { "extra_gear.xml", "gear.xml" }, file.BaseFileNames);
		}

		private const string Base =
			"<chummer><gears><gear><name>Taken</name></gear></gears></chummer>";

		private const string Pack =
			"<chummer><categories><category>Made Up</category></categories>"
			+ "<gears><gear><name>Taken</name><category>Made Up</category></gear>"
			+ "<gear><name>Fresh</name><category>Made Up</category></gear></gears></chummer>";

		[Fact]
		public void ItemsLoadDiscardsFindsACustomEntryTheBaseAlreadyHas()
		{
			Assert.Equal(new[] { "Taken" }, DiscardedNames("custom"));
		}

		[Fact]
		public void ItemsLoadDiscardsFindsAnOverrideWithoutABaseEntry()
		{
			Assert.Equal(new[] { "Fresh" }, DiscardedNames("override"));
		}

		// - gear.xml declares no "Made Up": only the pack's own block can satisfy it
		[Fact]
		public void ACustomFileMayDeclareItsOwnCategories()
		{
			Assert.Empty(UndeclaredCategories("custom"));
		}

		[Fact]
		public void AnOverrideFileCannotDeclareCategories()
		{
			Assert.Equal(new[] { "Made Up", "Made Up" }, UndeclaredCategories("override"));
		}

		[Fact]
		public void ConflictingOverridesIgnoresTwinsWithEqualContent()
		{
			Assert.Empty(CustomContent.ConflictingOverrides(new[]
			{
				Override("override_a_gear.xml", "<name>Taken</name><cost>1</cost>"),
				Override("override_b_gear.xml", "<name>Taken</name><cost>1</cost>"),
			}));
		}

		[Fact]
		public void ConflictingOverridesFindsTwinsWithDifferentContent()
		{
			var conflict = CustomContent.ConflictingOverrides(new[]
			{
				Override("override_a_gear.xml", "<name>Taken</name><cost>1</cost>"),
				Override("override_b_gear.xml", "<name>Taken</name><cost>2</cost>"),
			}).Single();

			Assert.Equal("gear.xml/chummer/gears/gear[name = \"Taken\"]", conflict.Key);
			Assert.Equal(new[] { "override_a_gear.xml", "override_b_gear.xml" }, conflict.Value);
		}

		// - "Ammunition" is declared by the real gear.xml, "Made Up" is not
		[Fact]
		public void RedeclaredCategoriesFindsOnlyValuesTheBaseDeclares()
		{
			XmlDocument pack = Xml.Parse(
				"<chummer><categories><category>Ammunition</category><category>Made Up</category></categories></chummer>");

			Assert.Equal(new[] { "categories/Ammunition" },
				CustomContent.RedeclaredCategories(PackFile("custom"), pack));
			Assert.Empty(CustomContent.RedeclaredCategories(PackFile("override"), pack));
		}

		private static KeyValuePair<PackFiles.PackFile, XmlDocument> Override(
			string fileName, string item)
		{
			return new KeyValuePair<PackFiles.PackFile, XmlDocument>(
				new PackFiles.PackFile(@"C:\pack\" + fileName, "override", new[] { "gear.xml" }),
				Xml.Parse("<chummer><gears><gear>" + item + "</gear></gears></chummer>"));
		}

		private static string[] DiscardedNames(string prefix)
		{
			return CustomContent.ItemsLoadDiscards(PackFile(prefix), Xml.Parse(Pack), Xml.Parse(Base))
				.Select(item => item.Name)
				.ToArray();
		}

		private static string[] UndeclaredCategories(string prefix)
		{
			return CustomContent.UndeclaredCategoryUsages(PackFile(prefix), Xml.Parse(Pack))
				.Select(usage => usage.Category)
				.ToArray();
		}

		// - paired with the real gear.xml: the declarations come from the cached base parse
		private static PackFiles.PackFile PackFile(string prefix)
		{
			return new PackFiles.PackFile(@"C:\pack\" + prefix + "_test_gear.xml", prefix, new[] { "gear.xml" });
		}
	}
}