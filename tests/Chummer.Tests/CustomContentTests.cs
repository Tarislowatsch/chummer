using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using Xunit;

namespace Chummer.Tests
{
	// - the schema check for pack files sits in XsdSchemaValidationTests, as a second data source
	public class CustomContentTests
	{
		// - format: <pack file>/<collection>/<name>, copied verbatim from the failure output
		private static readonly HashSet<string> KnownDiscardedEntries = new HashSet<string>(StringComparer.Ordinal)
		{
			// - not a copy: no Obsolete mod, ARN p.19 instead of UCL p.13
			"custom_another_rainy_night_vehicles.xml/vehicles/Ford Americar (Sedan)",
		};

		// - same values as the base allowlist in CategoryDeclarationTests: the pack inherits the defect
		private static readonly HashSet<string> KnownUndeclaredCategories = new HashSet<string>(StringComparer.Ordinal)
		{
			"custom_datahaven1_armor.xml/mods/Full Body Armor",
		};

		// - an unpaired file is never loaded, a doubly paired one lands in two documents
		[Fact]
		public void EveryPackFilePairsWithExactlyOneBaseFile()
		{
			string[] unpaired = CustomContent.PackFiles
				.Where(file => file.Prefix == null || file.BaseFileNames.Count != 1)
				.Select(file => file.PackName + "/" + file.FileName + " -> "
					+ (file.Prefix == null
						? "starts with neither '" + CustomContent.CustomPrefix + "' nor '" + CustomContent.OverridePrefix + "'"
						: "[" + string.Join(", ", file.BaseFileNames) + "]"))
				.ToArray();

			Assert.True(unpaired.Length == 0,
				"These pack files do not pair with exactly one base data file:\n  " + string.Join("\n  ", unpaired));
		}

		[Theory]
		[MemberData(nameof(CustomContent.PackFilesCitingBooks), MemberType = typeof(CustomContent))]
		public void EverySourceCitesABookThePackCanSee(string packPath)
		{
			CustomContent.PackFile file = CustomContent.PackFileFor(packPath);

			var unknown = DataPaths
				.ReferencesToUndeclaredBooks(DataPaths.BookReferencesIn(CustomContent.Load(packPath)),
					CustomContent.BookCodesVisibleTo(file))
				.GroupBy(reference => reference.Code, StringComparer.Ordinal)
				.OrderBy(group => group.Key, StringComparer.Ordinal)
				.ToArray();

			if (unknown.Length > 0)
			{
				Assert.Fail(FailureReport.Build(
					file.PackName + "/" + file.FileName + " cites " + unknown.Length
						+ " book code(s) that neither books.xml nor the pack declares",
					unknown,
					group => "'" + group.Key + "' used by " + group.Count()
						+ " entries, e.g. '" + group.First().ItemName + "'"));
			}
		}

		[Theory]
		[MemberData(nameof(CustomContent.PairedPackFileCases), MemberType = typeof(CustomContent))]
		public void LoadKeepsEveryPackEntry(string packPath)
		{
			CustomContent.PackFile file = CustomContent.PackFileFor(packPath);

			string[] unexpected = DiscardedEntries(file)
				.Where(entry => !KnownDiscardedEntries.Contains(entry))
				.ToArray();

			if (unexpected.Length > 0)
			{
				Assert.Fail(FailureReport.Build(
					file.PackName + "/" + file.FileName + " has " + unexpected.Length + " entr(ies) Load would "
						+ (file.IsOverride ? "ignore: no base entry has the name" : "drop: a base entry has the name"),
					unexpected,
					entry => entry));
			}
		}

		[Fact]
		public void AllowlistedDiscardedEntriesAreAllStillDiscarded()
		{
			HashSet<string> actual = new HashSet<string>(
				CustomContent.PairedPackFiles().SelectMany(DiscardedEntries), StringComparer.Ordinal);

			AssertNoneStale(KnownDiscardedEntries, actual, nameof(KnownDiscardedEntries));
		}

		// - Load dedupes against the merged document, never within the file it is merging
		// - a twin within one file therefore survives twice
		// - a twin across packs survives or drops depending on load order
		[Fact]
		public void NoTwoCustomEntriesShareAName()
		{
			string[] duplicated = CustomContent.PairedPackFiles()
				.Where(file => !file.IsOverride)
				.SelectMany(file => CustomContent.NamedItemsIn(CustomContent.Load(file.FilePath))
					.Select(item => new { Key = file.BaseFileNames[0] + item.XPath, Source = file.PackName + "/" + file.FileName }))
				.GroupBy(entry => entry.Key, StringComparer.Ordinal)
				.Where(group => group.Count() > 1)
				.Select(group => group.Key + " in " + string.Join(", ", group.Select(entry => entry.Source)))
				.OrderBy(entry => entry, StringComparer.Ordinal)
				.ToArray();

			Assert.True(duplicated.Length == 0,
				"These custom entries are defined more than once:\n  " + string.Join("\n  ", duplicated));
		}

		// - the audio/vision pack ships alternatives that overlap on 3 entries, with equal content
		[Fact]
		public void OverridesOfOneBaseEntryAgreeOnItsContent()
		{
			string[] conflicting = CustomContent.ConflictingOverrides(CustomContent.PairedPackFiles()
					.Where(file => file.IsOverride)
					.Select(file => new KeyValuePair<CustomContent.PackFile, XmlDocument>(
						file, CustomContent.Load(file.FilePath))))
				.Select(entry => entry.Key + " in " + string.Join(", ", entry.Value))
				.ToArray();

			Assert.True(conflicting.Length == 0,
				"These base entries get different content from different override files, "
				+ "so load order decides:\n  " + string.Join("\n  ", conflicting));
		}

		[Fact]
		public void NoCustomFileRedeclaresABaseCategory()
		{
			string[] redeclared = CustomContent.PairedPackFiles()
				.SelectMany(file => CustomContent.RedeclaredCategories(file, CustomContent.Load(file.FilePath))
					.Select(entry => file.PackName + "/" + file.FileName + ": " + entry))
				.ToArray();

			Assert.True(redeclared.Length == 0,
				"These pack categories are already declared by the base file and would be listed "
				+ "twice:\n  " + string.Join("\n  ", redeclared));
		}

		[Theory]
		[MemberData(nameof(CustomContent.PairedPackFileCases), MemberType = typeof(CustomContent))]
		public void EveryCategoryUsedIsDeclared(string packPath)
		{
			CustomContent.PackFile file = CustomContent.PackFileFor(packPath);

			var unexpected = UndeclaredCategories(file)
				.GroupBy(entry => entry.Key, StringComparer.Ordinal)
				.Where(group => !KnownUndeclaredCategories.Contains(group.Key))
				.OrderBy(group => group.Key, StringComparer.Ordinal)
				.ToArray();

			if (unexpected.Length > 0)
			{
				Assert.Fail(FailureReport.Build(
					file.PackName + "/" + file.FileName + " uses " + unexpected.Length
						+ " category value(s) that neither " + file.BaseFileNames[0] + " nor the pack declares",
					unexpected,
					group => "'" + group.Key + "' on " + group.Count()
						+ " entries, e.g. '" + group.First().Value + "'"));
			}
		}

		[Fact]
		public void AllowlistedCategoriesAreAllStillUndeclared()
		{
			HashSet<string> actual = new HashSet<string>(
				CustomContent.PairedPackFiles().SelectMany(UndeclaredCategories).Select(entry => entry.Key),
				StringComparer.Ordinal);

			AssertNoneStale(KnownUndeclaredCategories, actual, nameof(KnownUndeclaredCategories));
		}

		private static IEnumerable<string> DiscardedEntries(CustomContent.PackFile file)
		{
			XmlDocument baseDocument = CustomContent.Load(file.BaseFilePath);

			return CustomContent.ItemsLoadDiscards(file, CustomContent.Load(file.FilePath), baseDocument)
				.Select(item => file.FileName + "/" + item.Collection + "/" + item.Name);
		}

		// - key is <pack file>/<collection>/<category>, value the entry's name for the message
		private static IEnumerable<KeyValuePair<string, string>> UndeclaredCategories(CustomContent.PackFile file)
		{
			return CustomContent.UndeclaredCategoryUsages(file, CustomContent.Load(file.FilePath))
				.Select(usage => new KeyValuePair<string, string>(
					file.FileName + "/" + usage.CollectionName + "/" + usage.Category, usage.ItemName));
		}

		private static void AssertNoneStale(HashSet<string> allowlist, HashSet<string> actual, string listName)
		{
			string[] stale = allowlist.Where(entry => !actual.Contains(entry))
				.OrderBy(entry => entry, StringComparer.Ordinal)
				.ToArray();

			Assert.True(stale.Length == 0,
				"These entries are no longer defects and must be removed from " + listName + ":\n  "
				+ string.Join("\n  ", stale));
		}
	}
}