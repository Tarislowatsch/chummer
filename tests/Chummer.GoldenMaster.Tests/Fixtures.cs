using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;

namespace Chummer.GoldenMaster.Tests
{
	public static class Fixtures
	{
		private static readonly UTF8Encoding NoBom = new UTF8Encoding(false);

		public static string RepoRoot { get; } = FindRepoRoot();

		public static string CharactersDir => Path.Combine(RepoRoot, "tests", "fixtures", "characters");

		// Relative paths keep test case names stable across machines.
		public static IEnumerable<string> RelativePaths()
		{
			return Directory.EnumerateFiles(CharactersDir, "*.chum", SearchOption.AllDirectories)
				.Select(path => path.Substring(CharactersDir.Length + 1))
				.OrderBy(path => path, StringComparer.Ordinal);
		}

		public static IEnumerable<object[]> Characters()
		{
			return RelativePaths().Select(path => new object[] { path });
		}

		public static string Print(string relativePath)
		{
			return Print(relativePath, new RecordingMugshotStore());
		}

		// - same document shape as frmViewer builds around PrintToStream
		// - no BOM: frmViewer's preamble is not part of the print
		public static string Print(string relativePath, IMugshotStore mugshots)
		{
			Character character = new Character();
			character.MugshotStore = mugshots;
			character.FileName = Path.Combine(CharactersDir, relativePath);
			if (!character.Load())
				throw new InvalidOperationException("Character.Load returned false for " + relativePath + ".");

			using (MemoryStream stream = new MemoryStream())
			{
				XmlTextWriter writer = new XmlTextWriter(stream, NoBom);
				writer.WriteStartDocument();
				writer.WriteStartElement("characters");
				character.PrintToStream(stream, writer);
				writer.WriteEndElement();
				writer.WriteEndDocument();
				writer.Flush();
				return NoBom.GetString(stream.ToArray());
			}
		}

		// - read from the save, not the loaded character: the oracle must not depend on Load
		// - streamed: a DOM would parse every save a second time
		public static bool HasMugshot(string relativePath)
		{
			using (XmlReader save = XmlReader.Create(Path.Combine(CharactersDir, relativePath)))
				return save.ReadToFollowing("mugshot") && save.ReadElementContentAsString() != "";
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
