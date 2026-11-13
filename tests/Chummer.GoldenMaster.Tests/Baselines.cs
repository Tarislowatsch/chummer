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

		private const int ShownContext = 60;
		private const int ShownLength = 160;

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

		internal static string FirstDifference(string expected, string actual)
		{
			string[] expectedLines = expected.Split('\n');
			string[] actualLines = actual.Split('\n');
			int line = 0;
			while (line < expectedLines.Length && line < actualLines.Length && expectedLines[line] == actualLines[line])
				line++;

			string expectedLine = line < expectedLines.Length ? expectedLines[line] : null;
			string actualLine = line < actualLines.Length ? actualLines[line] : null;
			int column = FirstDifferingColumn(expectedLine ?? "", actualLine ?? "");
			int start = Math.Max(0, column - ShownContext);
			return "First difference in line " + (line + 1) + ", column " + (column + 1) + ":"
				+ "\nexpected: " + Window(expectedLine, start)
				+ "\nactual:   " + Window(actualLine, start);
		}

		private static int FirstDifferingColumn(string expected, string actual)
		{
			int column = 0;
			while (column < expected.Length && column < actual.Length && expected[column] == actual[column])
				column++;
			return column;
		}

		// - a window around the column: mugshot lines run to 184 KB
		// - tabs shown as \t: an indentation change must stay visible
		private static string Window(string line, int start)
		{
			if (line == null)
				return "(end of print)";
			int length = Math.Min(ShownLength, Math.Max(0, line.Length - start));
			string window = start < line.Length ? line.Substring(start, length) : "";
			return (start > 0 ? "..." : "") + window.Replace("\t", "\\t") + (start + length < line.Length ? "..." : "");
		}
	}
}
