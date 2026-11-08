using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Chummer.Tests.Kernel
{
	// - the shipped packs under data/custom content/, paired as XmlManager.Load would pair them
	// - Load reads data/ itself only: a pack takes effect once its files are copied there
	public static class PackFiles
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

			// - the contracts' rule tables are keyed by it: a pack shares its base file's rules
			public string BaseFileName => BaseFileNames.Single();

			public string BaseFilePath => Path.Combine(DataPaths.ChummerDataDir, BaseFileName);
		}

		public static IReadOnlyList<PackFile> All => CachedAll.Value;

		private static readonly Lazy<IReadOnlyList<PackFile>> CachedAll =
			new Lazy<IReadOnlyList<PackFile>>(Discover);

		private static IReadOnlyList<PackFile> Discover()
		{
			string[] baseFileNames = DataPaths.TopLevelRuleXmlFiles().Select(Path.GetFileName).ToArray();

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

		// - every check needs a base file: an unpaired file fails the pairing guard instead
		public static IEnumerable<PackFile> Paired()
		{
			return All.Where(file => file.Prefix != null && file.BaseFileNames.Count == 1);
		}

		public static IEnumerable<object[]> PairedCases()
		{
			return Paired().Select(file => new object[] { file.FilePath });
		}

		public static PackFile For(string path)
		{
			return All.Single(file => string.Equals(file.FilePath, path, StringComparison.Ordinal));
		}
	}
}
