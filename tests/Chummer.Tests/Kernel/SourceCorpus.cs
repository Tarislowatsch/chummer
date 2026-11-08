using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Chummer.Tests.Kernel
{
	// - the code-side twin of RuleCorpus: every scan of Chummer/**/*.cs reads from here
	// - lexed only: a commented-out line must not count for any scan
	public static class SourceCorpus
	{
		public sealed class SourceFile
		{
			public SourceFile(string path, SourceLines source)
			{
				Path = path;
				Source = source;
			}

			public string Path { get; }

			public string Name => System.IO.Path.GetFileName(Path);

			public SourceLines Source { get; }
		}

		public static IReadOnlyList<SourceFile> Files => CachedFiles.Value;

		// - Single: a file name shared by two folders must not pick a consumer silently
		public static SourceFile File(string name)
		{
			SourceFile[] matches = Files.Where(file => file.Name == name).ToArray();
			if (matches.Length != 1)
				throw new InvalidOperationException("Expected one " + name + " under Chummer/, found " + matches.Length);

			return matches[0];
		}

		// - null when the path is not part of the corpus
		public static SourceFile At(string path)
		{
			return CachedFilesByPath.Value.TryGetValue(path, out SourceFile file) ? file : null;
		}

		// - lexed once: about twenty scans walk the whole tree
		private static readonly Lazy<IReadOnlyList<SourceFile>> CachedFiles =
			new Lazy<IReadOnlyList<SourceFile>>(() => ApplicationSourceFiles()
				.Select(path => new SourceFile(path, SourceLines.Of(System.IO.File.ReadAllLines(path))))
				.ToArray());

		// - private: a scan that lists the files itself would read and lex them a second time
		// - obj/ is generated and absent from a fresh clone: results would differ between here and CI
		private static IEnumerable<string> ApplicationSourceFiles()
		{
			string generated = Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar;

			return Directory
				.EnumerateFiles(Path.Combine(DataPaths.RepoRoot, "Chummer"), "*.cs", SearchOption.AllDirectories)
				.Where(path => path.IndexOf(generated, StringComparison.Ordinal) < 0)
				.OrderBy(path => path, StringComparer.Ordinal);
		}

		private static readonly Lazy<IReadOnlyDictionary<string, SourceFile>> CachedFilesByPath =
			new Lazy<IReadOnlyDictionary<string, SourceFile>>(
				() => CachedFiles.Value.ToDictionary(file => file.Path, StringComparer.Ordinal));
	}
}
