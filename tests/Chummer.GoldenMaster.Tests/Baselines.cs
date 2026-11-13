using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using Xunit;

namespace Chummer.GoldenMaster.Tests
{
	public static class Baselines
	{
		// - set to 1 to write each print as its new baseline
		// - the test still fails: an accepting run is never green
		public const string AcceptVariable = "CHUMMER_ACCEPT_PRINTS";

		private const int MaxShownLineLength = 200;

		private static readonly UTF8Encoding NoBom = new UTF8Encoding(false);

		public static string Root => Path.Combine(Fixtures.RepoRoot, "tests", "fixtures", "prints");

		public static string PathFor(string relativePath, CultureInfo culture)
		{
			return Path.Combine(Root, culture.Name, Path.ChangeExtension(relativePath, ".xml"));
		}

		// Every file counts, not only .xml: a stray file must show up as an unknown fixture.
		public static IEnumerable<string> FixturesWithBaseline(CultureInfo culture)
		{
			string directory = Path.Combine(Root, culture.Name);
			return Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)
				.Select(path => Path.ChangeExtension(path.Substring(directory.Length + 1), ".chum"))
				.OrderBy(path => path, StringComparer.Ordinal);
		}

		// - indented: a changed value diffs as one line, not as the whole print
		// - CR as &#xD;: the file stays LF only and the text survives the round trip
		public static string Format(string print)
		{
			XmlDocument document = new XmlDocument { PreserveWhitespace = true };
			document.LoadXml(print);
			XmlWriterSettings settings = new XmlWriterSettings { Indent = true, IndentChars = "\t", NewLineChars = "\n", NewLineHandling = NewLineHandling.Entitize, Encoding = NoBom };
			using (MemoryStream stream = new MemoryStream())
			{
				using (XmlWriter writer = XmlWriter.Create(stream, settings))
					document.Save(writer);
				return NoBom.GetString(stream.ToArray());
			}
		}

		public static void AssertMatches(string relativePath, CultureInfo culture, string print)
		{
			string path = PathFor(relativePath, culture);
			string actual = Format(print);
			if (Environment.GetEnvironmentVariable(AcceptVariable) == "1")
			{
				Directory.CreateDirectory(Path.GetDirectoryName(path));
				File.WriteAllText(path, actual, NoBom);
				Assert.Fail(AcceptVariable + " is set, wrote " + path + ". Review it with git diff, then run without it.");
			}

			if (!File.Exists(path))
				Assert.Fail("No baseline at " + path + ". Run with " + AcceptVariable + "=1 to write it.");

			string expected = File.ReadAllText(path, NoBom);
			if (expected != actual)
				Assert.Fail(path + " differs from the print. " + FirstDifference(expected, actual)
					+ "\nIf the change is intended, run with " + AcceptVariable + "=1 and review it with git diff.");
		}

		private static string FirstDifference(string expected, string actual)
		{
			string[] expectedLines = expected.Split('\n');
			string[] actualLines = actual.Split('\n');
			int line = 0;
			while (line < expectedLines.Length && line < actualLines.Length && expectedLines[line] == actualLines[line])
				line++;

			return "First difference in line " + (line + 1) + ":"
				+ "\nexpected: " + Shown(expectedLines, line)
				+ "\nactual:   " + Shown(actualLines, line);
		}

		// Mugshot lines run to 200 KB.
		private static string Shown(string[] lines, int line)
		{
			if (line >= lines.Length)
				return "(end of print)";
			string text = lines[line].Trim();
			return text.Length <= MaxShownLineLength ? text : text.Substring(0, MaxShownLineLength) + " ...";
		}
	}
}
