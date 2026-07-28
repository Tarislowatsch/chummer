using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

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
