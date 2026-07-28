using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;

namespace Chummer.Tests
{
	// P1-02. The test assembly runs from bin\Debug\net48 under tests\Chummer.Tests,
	// not from the repo root, so every path here is found by walking up from the
	// running assembly until the repo's marker file turns up rather than assumed
	// relative to the current directory.
	public static class DataPaths
	{
		public static string RepoRoot { get; } = FindRepoRoot();

		public static string ChummerDataDir => Path.Combine(RepoRoot, "Chummer", "data");

		public static string ChummerLangDir => Path.Combine(RepoRoot, "Chummer", "lang");

		public static string ChummerSheetsDir => Path.Combine(RepoRoot, "Chummer", "sheets");

		// One MemberData entry per file so a bad file fails its own test case
		// instead of being absorbed into one pass/fail verdict for the whole
		// directory - see the P1-06 warning about collapsing per-file signal.
		public static IEnumerable<object[]> RuleXmlFiles()
		{
			return Directory.EnumerateFiles(ChummerDataDir, "*.xml", SearchOption.AllDirectories)
				.OrderBy(path => path, StringComparer.Ordinal)
				.Select(path => new object[] { path });
		}

		public static IEnumerable<object[]> LangXmlFiles()
		{
			return Directory.EnumerateFiles(ChummerLangDir, "*.xml", SearchOption.AllDirectories)
				.OrderBy(path => path, StringComparer.Ordinal)
				.Select(path => new object[] { path });
		}

		public static IEnumerable<object[]> SheetXslFiles()
		{
			// Not two EnumerateFiles calls with "*.xsl" and "*.xslt": .NET Framework's
			// file-system globbing still honours the legacy 8.3 short-name matching
			// rule, under which "*.xsl" also matches "*.xslt". That silently duplicated
			// every .xslt file into both result sets - xUnit only stayed correct
			// because it happens to drop theory cases with a colliding ID, which is a
			// lucky safety net, not something to depend on. Enumerating everything
			// once and comparing the actual extension sidesteps the quirk entirely.
			return Directory.EnumerateFiles(ChummerSheetsDir, "*", SearchOption.AllDirectories)
				.Where(path =>
				{
					string extension = Path.GetExtension(path);
					return string.Equals(extension, ".xsl", StringComparison.OrdinalIgnoreCase)
						|| string.Equals(extension, ".xslt", StringComparison.OrdinalIgnoreCase);
				})
				.OrderBy(path => path, StringComparer.Ordinal)
				.Select(path => new object[] { path });
		}

		// P1-05. Pairs each top-level Chummer/data/*.xml file with its same-named
		// .xsd. Built from the .xml side and filtered by File.Exists rather than a
		// hardcoded list of 26 names, so character.xsd (no matching character.xml)
		// and improvements.xml (no matching improvements.xsd) drop out on their own.
		// Deliberately SearchOption.TopDirectoryOnly: "custom content/<pack>/" uses
		// inconsistent name-to-schema pairing and validating it is P1-16's job -
		// "TopLevel" in the name says so without needing the reader to find this
		// comment. Pairing by matching filename is itself a soft spot: a rename
		// that touches only the .xml or only the .xsd side drops the pair from this
		// method silently rather than erroring - accepted because the exact-count
		// guard in DataPathsTests turns that silence into an immediate, loud
		// failure instead of trying to prevent the rename from being possible.
		public static IEnumerable<object[]> TopLevelRuleXmlWithSchemaFiles()
		{
			return Directory.EnumerateFiles(ChummerDataDir, "*.xml", SearchOption.TopDirectoryOnly)
				.OrderBy(path => path, StringComparer.Ordinal)
				.Select(path => new { XmlPath = path, XsdPath = Path.ChangeExtension(path, ".xsd") })
				.Where(pair => File.Exists(pair.XsdPath))
				.Select(pair => new object[] { pair.XmlPath, pair.XsdPath });
		}

		// Which collections carry a composite lookup key instead of <name> alone.
		// The running application decides this, not taste: every kit lookup in
		// frmSelectPACKSKit.cs (:111 when a kit is picked, :715 when a custom kit
		// is deleted) selects by name AND category, so two packs sharing a name
		// only collide when their categories match too. That is deliberate in the
		// data - a kit like "Brawler" is split into an Attribute Kit part and a
		// Gear Kit part carrying one display name. If those lookups ever change,
		// this entry has to follow them.
		// The map describes key composition and nothing else: no exclusions, no
		// expected counts, no other test knobs. Anything absent is keyed by <name>.
		private static readonly IReadOnlyDictionary<string, string[]> CompositeKeyFields =
			new Dictionary<string, string[]>(StringComparer.Ordinal)
			{
				{ "packs.xml/packs", new[] { "name", "category" } },
			};

		private static readonly string[] NameOnlyKeyFields = { "name" };

		// Separates the parts of a composite key. A control character rather than a
		// printable separator, so a value that legitimately contains the separator
		// cannot forge a collision with a different field split.
		public const string KeyFieldSeparator = "\u001F";

		private static string[] KeyFieldsFor(string xmlFileName, string collectionName)
		{
			string[] fields;
			return CompositeKeyFields.TryGetValue(xmlFileName + "/" + collectionName, out fields)
				? fields
				: NameOnlyKeyFields;
		}

		// One collection of catalogue entries, reduced to the lookup keys of its
		// items - which is all the uniqueness check needs.
		public sealed class RuleCollection
		{
			public RuleCollection(string filePath, string name, string[] keyFields,
				IReadOnlyList<string> itemKeys)
			{
				FilePath = filePath;
				Name = name;
				KeyFields = keyFields;
				ItemKeys = itemKeys;
			}

			public string FilePath { get; }

			public string Name { get; }

			public string[] KeyFields { get; }

			public IReadOnlyList<string> ItemKeys { get; }
		}

		// Parsed once, then reused. Without this the same documents get reparsed
		// roughly five times over - once to discover the collections, once per
		// theory case, and once more per collection for the allowlist guard.
		// Measured before caching: one pass over the 3.2 MB corpus costs ~60 ms,
		// and the uniqueness tests accounted for ~70% of the whole suite's runtime
		// re-doing it.
		// Deliberately caching immutable string projections rather than the
		// XmlDocument instances themselves: xUnit runs test classes in parallel and
		// XmlDocument promises thread safety only for static members, so sharing
		// live documents would be a trap waiting for the next test class that uses
		// this helper. Strings cannot have that problem.
		private static readonly Lazy<IReadOnlyList<RuleCollection>> CachedRuleCollections =
			new Lazy<IReadOnlyList<RuleCollection>>(LoadTopLevelRuleCollections);

		// Every (file, collection) pair whose entries are identified by <name>.
		// Which collections those are is read off the data instead of being listed
		// here: a collection qualifies when its items have a direct <name> child.
		// That rule on its own excludes <version> (no element children),
		// <categories>, <costs>/<safehousecosts>, <limits> and <modcategories>
		// (their items are <category>/<cost>/<limit> elements holding text, with no
		// <name> inside), and the per-skill-group wrappers in skills.xml, where the
		// items *are* <name> elements rather than elements *having* one.
		// Deliberately top-level-only, in step with the schema-validation pairing
		// above: "custom content/<pack>/" is a separate concern.
		public static IEnumerable<object[]> TopLevelRuleXmlCollections()
		{
			return CachedRuleCollections.Value
				.Select(collection => new object[] { collection.FilePath, collection.Name });
		}

		public static RuleCollection RuleCollectionFor(string xmlPath, string collectionName)
		{
			return CachedRuleCollections.Value.Single(collection =>
				string.Equals(collection.FilePath, xmlPath, StringComparison.Ordinal)
				&& string.Equals(collection.Name, collectionName, StringComparison.Ordinal));
		}

		private static IReadOnlyList<RuleCollection> LoadTopLevelRuleCollections()
		{
			List<RuleCollection> collections = new List<RuleCollection>();

			foreach (string xmlPath in Directory
				.EnumerateFiles(ChummerDataDir, "*.xml", SearchOption.TopDirectoryOnly)
				.OrderBy(path => path, StringComparer.Ordinal))
			{
				XmlDocument document = new XmlDocument();
				document.Load(xmlPath);

				XmlElement root = document.DocumentElement;
				if (root == null)
					continue;

				string fileName = Path.GetFileName(xmlPath);

				foreach (XmlNode collection in root.ChildNodes)
				{
					if (collection.NodeType != XmlNodeType.Element)
						continue;

					XmlNode[] items = collection.ChildNodes.Cast<XmlNode>()
						.Where(item => item.NodeType == XmlNodeType.Element && item["name"] != null)
						.ToArray();

					if (items.Length == 0)
						continue;

					string[] keyFields = KeyFieldsFor(fileName, collection.Name);
					collections.Add(new RuleCollection(xmlPath, collection.Name, keyFields,
						items.Select(item => BuildKey(item, keyFields)).ToArray()));
				}
			}

			return collections;
		}

		// Ordinal and verbatim - no trimming, no case folding - on purpose. This
		// mirrors what the application does: a lookup like
		// SelectSingleNode("/chummer/gears/gear[name = \"...\"]") compares the raw
		// string codepoint for codepoint, so two entries differing only in case or
		// in surrounding whitespace genuinely are two separately reachable entries,
		// not a collision. Normalising here would look like a tidy-up and would in
		// fact make the tests disagree with the behaviour they exist to describe.
		private static string BuildKey(XmlNode item, string[] keyFields)
		{
			return string.Join(KeyFieldSeparator,
				keyFields.Select(field => item[field]?.InnerText ?? string.Empty));
		}

		private static string FindRepoRoot()
		{
			DirectoryInfo dir = new DirectoryInfo(AppContext.BaseDirectory);
			while (dir != null)
			{
				if (File.Exists(Path.Combine(dir.FullName, "ChummerCS.sln")))
					return dir.FullName;
				dir = dir.Parent;
			}

			throw new DirectoryNotFoundException(
				"Could not locate the repo root (ChummerCS.sln) above " + AppContext.BaseDirectory);
		}
	}
}
