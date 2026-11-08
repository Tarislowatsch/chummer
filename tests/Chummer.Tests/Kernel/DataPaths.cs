using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;

namespace Chummer.Tests.Kernel
{
	// - the assembly runs from bin\Debug\net48, not the repo root
	// - every path is found by walking up from the assembly to the repo's marker file
	public static class DataPaths
	{
		public static string RepoRoot { get; } = FindRepoRoot();

		public static string ChummerDataDir => Path.Combine(RepoRoot, "Chummer", "data");

		public static string ChummerLangDir => Path.Combine(RepoRoot, "Chummer", "lang");

		public static string EnglishLanguageFile => Path.Combine(ChummerLangDir, "en-us.xml");

		public static string ChummerSheetsDir => Path.Combine(RepoRoot, "Chummer", "sheets");

		// - one MemberData entry per file: a bad file fails its own case, not the whole directory
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
			// - not two globs: .NET Framework's legacy 8.3 rule makes "*.xsl" also match "*.xslt"
			// - xUnit masked the duplicates only by dropping colliding theory ids
			// - filtering on the real extension sidesteps the quirk
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

		public static string ChummerExportDir => Path.Combine(RepoRoot, "Chummer", "export");

		// - the pickers' filter, frmViewer.cs:36 and frmExport.cs:27: case-sensitive ".xsl"
		// - a .xslt is only ever reached through xsl:include
		public static IEnumerable<object[]> LoadableStylesheets()
		{
			return StylesheetFiles()
				.Where(path => path.EndsWith(".xsl", StringComparison.Ordinal))
				.Select(path => new object[] { path });
		}

		public static IReadOnlyList<string> PartialStylesheets()
		{
			return StylesheetFiles().Where(path => path.EndsWith(".xslt", StringComparison.Ordinal)).ToArray();
		}

		// - both picker folders: an include may cross from export/ into sheets/
		public static IEnumerable<string> StylesheetFiles()
		{
			return new[] { ChummerSheetsDir, ChummerExportDir }
				.SelectMany(dir => Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
				.Where(path => path.EndsWith(".xsl", StringComparison.Ordinal)
					|| path.EndsWith(".xslt", StringComparison.Ordinal))
				.OrderBy(path => path, StringComparer.Ordinal);
		}

		private const string XsltNamespace = "http://www.w3.org/1999/XSL/Transform";

		// - parsed, not grepped: a commented-out include must not count as one
		// - xsl:import too: the compiler pulls both in
		// - public so the detection tests can drive it with hand-built stylesheets
		public static IEnumerable<string> IncludedStylesheetPaths(string stylesheetPath, XmlDocument stylesheet)
		{
			XmlNamespaceManager namespaces = new XmlNamespaceManager(stylesheet.NameTable);
			namespaces.AddNamespace("xsl", XsltNamespace);

			return stylesheet.SelectNodes("/xsl:stylesheet/xsl:include/@href | /xsl:stylesheet/xsl:import/@href"
					+ " | /xsl:transform/xsl:include/@href | /xsl:transform/xsl:import/@href", namespaces)
				.Cast<XmlNode>()
				.Select(href => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(stylesheetPath), href.Value)));
		}

		// - data/*.xml without the packs, the files XmlManager loads by name
		public static IEnumerable<string> TopLevelRuleXmlFiles()
		{
			return Directory.EnumerateFiles(ChummerDataDir, "*.xml", SearchOption.TopDirectoryOnly)
				.OrderBy(path => path, StringComparer.Ordinal);
		}

		// One data file as Load merges it: top-level, or a pack file merged into its base.
		public sealed class DataFile
		{
			public DataFile(string path, string baseFileName)
			{
				FilePath = path;
				BaseFileName = baseFileName;
			}

			public string FilePath { get; }

			public string BaseFileName { get; }

			public string Label => FilePath.Substring(ChummerDataDir.Length + 1).Replace('\\', '/');
		}

		public static IReadOnlyList<DataFile> DataFilesWithPacks => CachedDataFilesWithPacks.Value;

		private static readonly Lazy<IReadOnlyList<DataFile>> CachedDataFilesWithPacks =
			new Lazy<IReadOnlyList<DataFile>>(() => TopLevelRuleXmlFiles()
				.Select(path => new DataFile(path, Path.GetFileName(path)))
				.Concat(PackFiles.Paired()
					.Select(pack => new DataFile(pack.FilePath, pack.BaseFileName)))
				.ToArray());

		// - File.Exists pairing lets character.xsd and improvements.xml drop out on their own
		// - top-level only: pack files pair with their base file's schema, see CustomContent
		// - the exact-count guard in DataPathsTests makes a silently dropped pair fail loudly
		public static IEnumerable<object[]> TopLevelRuleXmlWithSchemaFiles()
		{
			return TopLevelRuleXmlFiles()
				.Select(path => new { XmlPath = path, XsdPath = Path.ChangeExtension(path, ".xsd") })
				.Where(pair => File.Exists(pair.XsdPath))
				.Select(pair => new object[] { pair.XmlPath, pair.XsdPath });
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
