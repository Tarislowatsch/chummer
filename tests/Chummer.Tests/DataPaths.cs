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

		public static string[] KeyFieldsFor(string xmlFileName, string collectionName)
		{
			string[] fields;
			return CompositeKeyFields.TryGetValue(xmlFileName + "/" + collectionName, out fields)
				? fields
				: NameOnlyKeyFields;
		}

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
			foreach (string xmlPath in Directory
				.EnumerateFiles(ChummerDataDir, "*.xml", SearchOption.TopDirectoryOnly)
				.OrderBy(path => path, StringComparer.Ordinal))
			{
				XmlDocument document = new XmlDocument();
				document.Load(xmlPath);

				XmlElement root = document.DocumentElement;
				if (root == null)
					continue;

				foreach (XmlNode collection in root.ChildNodes)
				{
					if (collection.NodeType != XmlNodeType.Element)
						continue;

					bool hasNamedItems = collection.ChildNodes.Cast<XmlNode>().Any(item =>
						item.NodeType == XmlNodeType.Element && item["name"] != null);

					if (hasNamedItems)
						yield return new object[] { xmlPath, collection.Name };
				}
			}
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
