using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using Chummer.Tests.Kernel;

namespace Chummer.Tests.Rulebook
{
	// - every <source> has to name a book code books.xml declares
	public static class BookReferences
	{
		// One <source> element: a reference to a book code declared in books.xml.
		public sealed class BookReference
		{
			public BookReference(string code, string itemName)
			{
				Code = code;
				ItemName = itemName;
			}

			public string Code { get; }

			// - nearest enclosing element with a <name>, so a failure names the entry, not just the file
			public string ItemName { get; }
		}

		// - the book codes books.xml declares, the set every <source> has to land in
		// - parsed on its own: /chummer/books/book/code is a shape no other file shares
		public static IReadOnlyCollection<string> BookCodes => CachedBookCodes.Value;

		private static readonly Lazy<IReadOnlyCollection<string>> CachedBookCodes =
			new Lazy<IReadOnlyCollection<string>>(LoadBookCodes);

		private static IReadOnlyCollection<string> LoadBookCodes()
		{
			XmlDocument document = new XmlDocument { XmlResolver = null };
			document.Load(Path.Combine(DataPaths.ChummerDataDir, "books.xml"));

			return new HashSet<string>(
				document.SelectNodes("/chummer/books/book/code").Cast<XmlNode>()
					.Select(node => node.InnerText),
				StringComparer.Ordinal);
		}

		// - only files that cite a book: the rest would contribute empty, always-green cases
		// - the exact-count guard in DataPathsTests keeps that omission from quietly growing
		public static IEnumerable<object[]> TopLevelRuleXmlFilesCitingBooks()
		{
			return RuleCorpus.Files
				.Where(file => file.BookReferences.Count > 0)
				.Select(file => new object[] { file.FilePath });
		}

		public static IReadOnlyList<BookReference> For(string xmlPath)
		{
			return RuleCorpus.FileAt(xmlPath).BookReferences;
		}

		// - every <source>, at any depth, the opposite axis from the anchored uniqueness check
		// - <source> has no definition/reference duality to conflate, unlike <name>
		// - anchoring to collection items would silently drop the metavariant references
		public static IReadOnlyList<BookReference> In(XmlDocument document)
		{
			return document.SelectNodes("//source").Cast<XmlNode>()
				.Select(node => new BookReference(node.InnerText, RuleCorpus.NearestItemName(node)))
				.ToArray();
		}

		// - one implementation for the real files and the hand-built cases that prove it works
		// - ordinal and verbatim: Options.BookXPath() (clsOptions.cs:2086-2090) compares exactly
		// - the set is rebuilt here so a caller's comparer cannot loosen the comparison
		public static IEnumerable<BookReference> Undeclared(
			IEnumerable<BookReference> references, IEnumerable<string> declaredCodes)
		{
			HashSet<string> declared = new HashSet<string>(declaredCodes, StringComparer.Ordinal);

			return references.Where(reference => !declared.Contains(reference.Code));
		}
	}
}
