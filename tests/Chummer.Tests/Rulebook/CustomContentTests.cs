using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using Chummer.Tests.Kernel;
using Xunit;

namespace Chummer.Tests.Rulebook
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
			string[] unpaired = PackFiles.All
				.Where(file => file.Prefix == null || file.BaseFileNames.Count != 1)
				.Select(file => file.PackName + "/" + file.FileName + " -> "
					+ (file.Prefix == null
						? "starts with neither '" + PackFiles.CustomPrefix + "' nor '" + PackFiles.OverridePrefix + "'"
						: "[" + string.Join(", ", file.BaseFileNames) + "]"))
				.ToArray();

			FailureReport.AssertNone("These pack files do not pair with exactly one base data file", unpaired);
		}

		[Theory]
		[MemberData(nameof(CustomContent.PackFilesCitingBooks), MemberType = typeof(CustomContent))]
		public void EverySourceCitesABookThePackCanSee(string packPath)
		{
			PackFiles.PackFile file = PackFiles.For(packPath);

			var unknown = BookReferences
				.Undeclared(BookReferences.In(Xml.Load(packPath)),
					CustomContent.BookCodesVisibleTo(file))
				.GroupBy(reference => reference.Code, StringComparer.Ordinal)
				.OrderBy(group => group.Key, StringComparer.Ordinal)
				.ToArray();

			FailureReport.AssertNone(
				file.PackName + "/" + file.FileName + " cites " + unknown.Length
					+ " book code(s) that neither books.xml nor the pack declares",
				unknown,
				group => "'" + group.Key + "' used by " + group.Count()
					+ " entries, e.g. '" + group.First().ItemName + "'");
		}

		[Theory]
		[MemberData(nameof(PackFiles.PairedCases), MemberType = typeof(PackFiles))]
		public void LoadKeepsEveryPackEntry(string packPath)
		{
			PackFiles.PackFile file = PackFiles.For(packPath);

			string[] unexpected = DiscardedEntries(file)
				.Where(entry => !KnownDiscardedEntries.Contains(entry))
				.ToArray();

			FailureReport.AssertNone(
				file.PackName + "/" + file.FileName + " has " + unexpected.Length + " entr(ies) Load would "
					+ (file.IsOverride ? "ignore: no base entry has the name" : "drop: a base entry has the name"),
				unexpected);
		}

		[Fact]
		public void AllowlistedDiscardedEntriesAreAllStillDiscarded()
		{
			HashSet<string> actual = new HashSet<string>(
				PackFiles.Paired().SelectMany(DiscardedEntries), StringComparer.Ordinal);

			AssertNoneStale(KnownDiscardedEntries, actual, nameof(KnownDiscardedEntries));
		}

		// - Load dedupes against the merged document, never within the file it is merging
		// - a twin within one file therefore survives twice
		// - a twin across packs survives or drops depending on load order
		[Fact]
		public void NoTwoCustomEntriesShareAName()
		{
			string[] duplicated = PackFiles.Paired()
				.Where(file => !file.IsOverride)
				.SelectMany(file => CustomContent.NamedItemsIn(Xml.Load(file.FilePath))
					.Select(item => new { Key = file.BaseFileNames[0] + item.XPath, Source = file.PackName + "/" + file.FileName }))
				.GroupBy(entry => entry.Key, StringComparer.Ordinal)
				.Where(group => group.Count() > 1)
				.Select(group => group.Key + " in " + string.Join(", ", group.Select(entry => entry.Source)))
				.OrderBy(entry => entry, StringComparer.Ordinal)
				.ToArray();

			FailureReport.AssertNone("These custom entries are defined more than once", duplicated);
		}

		// - the audio/vision pack ships alternatives that overlap on 3 entries, with equal content
		[Fact]
		public void OverridesOfOneBaseEntryAgreeOnItsContent()
		{
			string[] conflicting = CustomContent.ConflictingOverrides(PackFiles.Paired()
					.Where(file => file.IsOverride)
					.Select(file => new KeyValuePair<PackFiles.PackFile, XmlDocument>(
						file, Xml.Load(file.FilePath))))
				.Select(entry => entry.Key + " in " + string.Join(", ", entry.Value))
				.ToArray();

			FailureReport.AssertNone("These base entries get different content from different override files, "
				+ "so load order decides", conflicting);
		}

		[Fact]
		public void NoCustomFileRedeclaresABaseCategory()
		{
			string[] redeclared = PackFiles.Paired()
				.SelectMany(file => CustomContent.RedeclaredCategories(file, Xml.Load(file.FilePath))
					.Select(entry => file.PackName + "/" + file.FileName + ": " + entry))
				.ToArray();

			FailureReport.AssertNone("These pack categories are already declared by the base file and would be listed "
				+ "twice", redeclared);
		}

		[Theory]
		[MemberData(nameof(PackFiles.PairedCases), MemberType = typeof(PackFiles))]
		public void EveryCategoryUsedIsDeclared(string packPath)
		{
			PackFiles.PackFile file = PackFiles.For(packPath);

			var unexpected = UndeclaredCategories(file)
				.GroupBy(entry => entry.Key, StringComparer.Ordinal)
				.Where(group => !KnownUndeclaredCategories.Contains(group.Key))
				.OrderBy(group => group.Key, StringComparer.Ordinal)
				.ToArray();

			FailureReport.AssertNone(
				file.PackName + "/" + file.FileName + " uses " + unexpected.Length
					+ " category value(s) that neither " + file.BaseFileNames[0] + " nor the pack declares",
				unexpected,
				group => "'" + group.Key + "' on " + group.Count()
					+ " entries, e.g. '" + group.First().Value + "'");
		}

		[Fact]
		public void AllowlistedCategoriesAreAllStillUndeclared()
		{
			HashSet<string> actual = new HashSet<string>(
				PackFiles.Paired().SelectMany(UndeclaredCategories).Select(entry => entry.Key),
				StringComparer.Ordinal);

			AssertNoneStale(KnownUndeclaredCategories, actual, nameof(KnownUndeclaredCategories));
		}

		private static IEnumerable<string> DiscardedEntries(PackFiles.PackFile file)
		{
			XmlDocument baseDocument = Xml.Load(file.BaseFilePath);

			return CustomContent.ItemsLoadDiscards(file, Xml.Load(file.FilePath), baseDocument)
				.Select(item => file.FileName + "/" + item.Collection + "/" + item.Name);
		}

		// - key is <pack file>/<collection>/<category>, value the entry's name for the message
		private static IEnumerable<KeyValuePair<string, string>> UndeclaredCategories(PackFiles.PackFile file)
		{
			return CustomContent.UndeclaredCategoryUsages(file, Xml.Load(file.FilePath))
				.Select(usage => new KeyValuePair<string, string>(
					file.FileName + "/" + usage.CollectionName + "/" + usage.Category, usage.ItemName));
		}

		private static void AssertNoneStale(HashSet<string> allowlist, HashSet<string> actual, string listName)
		{
			string[] stale = allowlist.Where(entry => !actual.Contains(entry))
				.OrderBy(entry => entry, StringComparer.Ordinal)
				.ToArray();

			FailureReport.AssertNoneInFull("These entries are no longer defects and must be removed from " + listName, stale);
		}
	}
}