using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;

namespace Chummer.Tests
{
	// - the shipped packs under data/custom content/, checked as XmlManager.Load would merge them
	// - Load reads data/ itself only: a pack takes effect once its files are copied there
	public static class CustomContent
	{
		public const string CustomPrefix = "custom";

		public const string OverridePrefix = "override";

		public static string CustomContentDir => Path.Combine(DataPaths.ChummerDataDir, "custom content");

		// - clsXmlManager.cs:155 and :337 skip both merges for this one file
		private const string FileWithoutPackMerge = "improvements.xml";

		public sealed class PackFile
		{
			public PackFile(string filePath, string prefix, IReadOnlyList<string> baseFileNames)
			{
				FilePath = filePath;
				Prefix = prefix;
				BaseFileNames = baseFileNames;
			}

			public string FilePath { get; }

			public string FileName => Path.GetFileName(FilePath);

			public string PackName => Path.GetFileName(Path.GetDirectoryName(FilePath));

			// - null when the name starts with neither prefix: Load would never pick the file up
			public string Prefix { get; }

			public bool IsOverride => string.Equals(Prefix, OverridePrefix, StringComparison.Ordinal);

			// - more than one would merge the file into several documents
			public IReadOnlyList<string> BaseFileNames { get; }

			public string BaseFilePath => Path.Combine(DataPaths.ChummerDataDir, BaseFileNames.Single());
		}

		public static IReadOnlyList<PackFile> PackFiles => CachedPackFiles.Value;

		private static readonly Lazy<IReadOnlyList<PackFile>> CachedPackFiles =
			new Lazy<IReadOnlyList<PackFile>>(DiscoverPackFiles);

		private static IReadOnlyList<PackFile> DiscoverPackFiles()
		{
			string[] baseFileNames = Directory
				.EnumerateFiles(DataPaths.ChummerDataDir, "*.xml", SearchOption.TopDirectoryOnly)
				.Select(Path.GetFileName)
				.ToArray();

			return Directory.EnumerateFiles(CustomContentDir, "*.xml", SearchOption.AllDirectories)
				.OrderBy(path => path, StringComparer.Ordinal)
				.Select(path => Classify(path, baseFileNames))
				.ToArray();
		}

		// - public so the detection tests can drive it with made-up names
		// - mirrors Load's globs "custom*_<base>" and "override*_<base>" (clsXmlManager.cs:158, :340)
		// - case-insensitive like Directory.GetFiles on Windows
		public static PackFile Classify(string path, IEnumerable<string> baseFileNames)
		{
			string fileName = Path.GetFileName(path);
			string prefix = new[] { CustomPrefix, OverridePrefix }
				.FirstOrDefault(candidate => fileName.StartsWith(candidate, StringComparison.OrdinalIgnoreCase));

			string[] bases = prefix == null
				? new string[0]
				: baseFileNames
					.Where(baseName => !string.Equals(baseName, FileWithoutPackMerge, StringComparison.OrdinalIgnoreCase))
					.Where(baseName => fileName.Length >= prefix.Length + 1 + baseName.Length
						&& fileName.EndsWith("_" + baseName, StringComparison.OrdinalIgnoreCase))
					.OrderBy(baseName => baseName, StringComparer.Ordinal)
					.ToArray();

			return new PackFile(path, prefix, bases);
		}

		// - every check below needs a base file: an unpaired file fails the pairing guard instead
		public static IEnumerable<PackFile> PairedPackFiles()
		{
			return PackFiles.Where(file => file.Prefix != null && file.BaseFileNames.Count == 1);
		}

		public static IEnumerable<object[]> PairedPackFileCases()
		{
			return PairedPackFiles().Select(file => new object[] { file.FilePath });
		}

		public static IEnumerable<object[]> PackXmlWithSchemaFiles()
		{
			return PairedPackFiles()
				.Select(file => new { file.FilePath, XsdPath = Path.ChangeExtension(file.BaseFilePath, ".xsd") })
				.Where(pair => File.Exists(pair.XsdPath))
				.Select(pair => new object[] { pair.FilePath, pair.XsdPath });
		}

		public static IEnumerable<object[]> PackFilesCitingBooks()
		{
			return PairedPackFiles()
				.Where(file => BookReferences.In(Load(file.FilePath)).Count > 0)
				.Select(file => new object[] { file.FilePath });
		}

		public static PackFile PackFileFor(string path)
		{
			return PackFiles.Single(file => string.Equals(file.FilePath, path, StringComparison.Ordinal));
		}

		// - per pack, not across packs: a user copies in only the packs they want
		public static IReadOnlyCollection<string> BookCodesVisibleTo(PackFile file)
		{
			IEnumerable<string> packCodes = PairedPackFiles()
				.Where(other => string.Equals(other.PackName, file.PackName, StringComparison.Ordinal)
					&& !other.IsOverride
					&& string.Equals(other.BaseFileNames[0], "books.xml", StringComparison.Ordinal))
				.SelectMany(books => Load(books.FilePath).SelectNodes("/chummer/books/book/code").Cast<XmlNode>())
				.Select(code => code.InnerText);

			return new HashSet<string>(BookReferences.BookCodes.Concat(packCodes), StringComparer.Ordinal);
		}

		// One named entry in a pack file, keyed the way Load looks it up.
		public sealed class PackItem
		{
			public PackItem(string collection, string element, string name, string content)
			{
				Collection = collection;
				Element = element;
				Content = content;
				Name = name;
			}

			public string Collection { get; }

			public string Element { get; }

			public string Name { get; }

			// - the InnerXml an override writes over the base entry (clsXmlManager.cs:170)
			public string Content { get; }

			// - the exact query Load builds to find this entry's twin (clsXmlManager.cs:168, :353)
			public string XPath => "/chummer/" + Collection + "/" + Element + "[name = \"" + Name + "\"]";
		}

		// - only entries with a <name>, the ones Load matches against the base
		public static IEnumerable<PackItem> NamedItemsIn(XmlDocument pack)
		{
			return pack.DocumentElement.ChildNodes.Cast<XmlNode>()
				.Where(collection => collection.NodeType == XmlNodeType.Element)
				.SelectMany(collection => collection.ChildNodes.Cast<XmlNode>()
					.Where(item => item.NodeType == XmlNodeType.Element && item["name"] != null)
					.Select(item => new PackItem(collection.Name, item.Name, item["name"].InnerText, item.InnerXml)));
		}

		// - base file + lookup -> the distinct contents the given overrides would write there
		// - one content is harmless however many files carry it, two make load order decide
		public static IEnumerable<KeyValuePair<string, string[]>> ConflictingOverrides(
			IEnumerable<KeyValuePair<PackFile, XmlDocument>> overrides)
		{
			return overrides
				.SelectMany(pair => NamedItemsIn(pair.Value)
					.Select(item => new { Key = pair.Key.BaseFileNames[0] + item.XPath, item.Content, pair.Key.FileName }))
				.GroupBy(entry => entry.Key, StringComparer.Ordinal)
				.Where(group => group.Select(entry => entry.Content).Distinct(StringComparer.Ordinal).Count() > 1)
				.Select(group => new KeyValuePair<string, string[]>(group.Key,
					group.Select(entry => entry.FileName).OrderBy(name => name, StringComparer.Ordinal).ToArray()))
				.OrderBy(entry => entry.Key, StringComparer.Ordinal);
		}

		// - frmSelectWeapon.cs:47-62 lists every <category> node without deduplicating
		// - the pack's copy lacks the translation: Load applies it before merging packs
		// - override files are exempt: their blocks are never merged
		public static IEnumerable<string> RedeclaredCategories(PackFile file, XmlDocument pack)
		{
			if (file.IsOverride)
				return Enumerable.Empty<string>();

			IReadOnlyDictionary<string, IReadOnlyCollection<string>> baseBlocks =
				CategoryDeclarations.For(file.BaseFilePath);

			return CategoryDeclarations.DeclarationBlocksIn(pack.DocumentElement, file.FileName)
				.SelectMany(block => block.Value
					.Where(value => Values(baseBlocks, block.Key).Contains(value, StringComparer.Ordinal))
					.Select(value => block.Key + "/" + value));
		}

		// - custom entries the base already names: Load drops them (clsXmlManager.cs:354-357)
		// - override entries the base does not name: Load ignores them (clsXmlManager.cs:169)
		// - one function for both: the same lookup, read with opposite expectations
		public static IEnumerable<PackItem> ItemsLoadDiscards(PackFile file, XmlDocument pack, XmlDocument baseDocument)
		{
			return NamedItemsIn(pack)
				.Where(item => (baseDocument.SelectSingleNode(item.XPath) != null) != file.IsOverride);
		}

		// - Load appends a custom file's <categories> next to the base block, pickers read both
		// - an override file's blocks are never merged (clsXmlManager.cs:164-171 needs a <name>)
		public static IReadOnlyDictionary<string, IReadOnlyCollection<string>> CategoryDeclarationsVisibleTo(
			PackFile file, XmlDocument pack)
		{
			IReadOnlyDictionary<string, IReadOnlyCollection<string>> baseBlocks =
				CategoryDeclarations.For(file.BaseFilePath);
			if (file.IsOverride)
				return baseBlocks;

			IReadOnlyDictionary<string, IReadOnlyCollection<string>> packBlocks =
				CategoryDeclarations.DeclarationBlocksIn(pack.DocumentElement, file.FileName);

			return baseBlocks.Keys.Union(packBlocks.Keys, StringComparer.Ordinal)
				.ToDictionary(
					block => block,
					block => (IReadOnlyCollection<string>)Values(baseBlocks, block).Concat(Values(packBlocks, block)).ToArray(),
					StringComparer.Ordinal);
		}

		// - scope resolved against the base file name: overrides and exemptions apply unchanged
		public static IEnumerable<CategoryDeclarations.CategoryUsage> UndeclaredCategoryUsages(PackFile file,
			XmlDocument pack)
		{
			IReadOnlyDictionary<string, IReadOnlyCollection<string>> declarations =
				CategoryDeclarationsVisibleTo(file, pack);

			foreach (XmlNode collection in RuleCorpus.CatalogueCollectionsIn(pack.DocumentElement))
			{
				string block;
				IReadOnlyCollection<string> declared;
				if (!CategoryDeclarations.TryResolveDeclarationBlock(file.BaseFileNames[0], collection.Name,
						declarations, out block, out declared))
				{
					continue;
				}

				HashSet<string> set = new HashSet<string>(declared, StringComparer.Ordinal);
				foreach (CategoryDeclarations.CategoryUsage usage in CategoryDeclarations.UsagesIn(collection))
				{
					if (!set.Contains(usage.Category))
						yield return usage;
				}
			}
		}

		private static IEnumerable<string> Values(
			IReadOnlyDictionary<string, IReadOnlyCollection<string>> blocks, string block)
		{
			IReadOnlyCollection<string> values;
			return blocks.TryGetValue(block, out values) ? values : Enumerable.Empty<string>();
		}

		// - reparsed per check, not cached: pack files are tiny
		public static XmlDocument Load(string path)
		{
			XmlDocument document = new XmlDocument { XmlResolver = null };
			document.Load(path);
			return document;
		}
	}
}