using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
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

		// - printed under German as well: decimals in Print follow the thread culture
		// - covers each such site that fixtures reach: Essence, cyberware ess, power points
		public static readonly string[] GermanSubset =
		{
			@"examples\Street Samurai.chum",
			@"The Way of the Samurai\Troll Samurai.chum",
			@"examples\Gunslinger Adept.chum",
			@"10 Mercs\Bravo Company\Mystic Adept Combat Medic.chum",
		};

		public static IEnumerable<object[]> GermanCharacters()
		{
			return GermanSubset.Select(path => new object[] { path });
		}

		// GetCultureInfo: a machine's own overrides of the culture must not reach the baseline.
		public static readonly CultureInfo Baseline = CultureInfo.GetCultureInfo("en-US");
		public static readonly CultureInfo German = CultureInfo.GetCultureInfo("de-DE");

		// Load runs under the culture too: the app loads and prints on the same thread.
		public static string Print(string relativePath, IMugshotStore mugshots = null, CultureInfo culture = null)
		{
			Thread thread = Thread.CurrentThread;
			CultureInfo ambient = thread.CurrentCulture;
			CultureInfo ambientUi = thread.CurrentUICulture;
			thread.CurrentCulture = culture ?? Baseline;
			thread.CurrentUICulture = culture ?? Baseline;
			try
			{
				return PrintUnderCurrentCulture(relativePath, mugshots ?? new RecordingMugshotStore());
			}
			finally
			{
				thread.CurrentCulture = ambient;
				thread.CurrentUICulture = ambientUi;
			}
		}

		// - same document shape as frmViewer builds around PrintToStream
		// - no BOM: frmViewer's preamble is not part of the print
		private static string PrintUnderCurrentCulture(string relativePath, IMugshotStore mugshots)
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
