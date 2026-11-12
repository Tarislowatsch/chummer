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

		// - the test project's own source, for the context graph
		public static IReadOnlyList<SourceFile> TestProjectFiles => CachedTestProjectFiles.Value;

		// - Single: a file name shared by two folders must not pick a consumer silently
		public static SourceFile File(string name)
		{
			SourceFile[] matches = CachedFilesByName.Value[name].ToArray();
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
			new Lazy<IReadOnlyList<SourceFile>>(() => Lex("Chummer"));

		private static readonly Lazy<IReadOnlyList<SourceFile>> CachedTestProjectFiles =
			new Lazy<IReadOnlyList<SourceFile>>(() => Lex(Path.Combine("tests", "Chummer.Tests")));

		// - private: a scan that lists the files itself would read and lex them a second time
		// - obj/ is generated and absent from a fresh clone: results would differ between here and CI
		private static IReadOnlyList<SourceFile> Lex(string projectDir)
		{
			string generated = Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar;

			return Directory
				.EnumerateFiles(Path.Combine(DataPaths.RepoRoot, projectDir), "*.cs", SearchOption.AllDirectories)
				.Where(path => path.IndexOf(generated, StringComparison.Ordinal) < 0)
				.OrderBy(path => path, StringComparer.Ordinal)
				.Select(path => new SourceFile(path, SourceLines.Of(System.IO.File.ReadAllLines(path))))
				.ToArray();
		}

		private static readonly Lazy<IReadOnlyDictionary<string, SourceFile>> CachedFilesByPath =
			new Lazy<IReadOnlyDictionary<string, SourceFile>>(
				() => CachedFiles.Value.ToDictionary(file => file.Path, StringComparer.Ordinal));

		private static readonly Lazy<ILookup<string, SourceFile>> CachedFilesByName =
			new Lazy<ILookup<string, SourceFile>>(() => CachedFiles.Value.ToLookup(file => file.Name, StringComparer.Ordinal));
	}
}
