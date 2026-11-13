using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using Xunit;

namespace Chummer.GoldenMaster.Tests
{
	[Collection(AppHost.Collection)]
	public class BaselineTests
	{
		private const string MultiLineFixture = @"Montreal 2074\Fallen.chum";

		// The theories only see fixtures: a baseline without one would never be compared.
		[Fact]
		public void BaselinesExistForExactlyThePrintingFixtures()
		{
			AssertSameFixtures(Fixtures.RelativePaths().Where(path => !PrintTests.KnownPrintFailures.ContainsKey(path)), Baselines.FixturesWithBaseline(Fixtures.Baseline));
		}

		[Fact]
		public void GermanBaselinesExistForExactlyTheGermanSubset()
		{
			AssertSameFixtures(Fixtures.GermanSubset, Baselines.FixturesWithBaseline(Fixtures.German));
		}

		[Fact]
		public void BaselinesExistForNoOtherCulture()
		{
			Assert.Equal(new[] { Fixtures.German.Name, Fixtures.Baseline.Name }, Directory.GetFileSystemEntries(Baselines.Root).Select(Path.GetFileName).OrderBy(name => name, StringComparer.Ordinal));
		}

		[Fact]
		public void FormatKeepsEveryTextNode()
		{
			string print = Fixtures.Print(MultiLineFixture);
			Assert.True(print.Contains("\r\n"), MultiLineFixture + " lost its multi-line text: pick another fixture.");
			Assert.Equal(TextNodes(print), TextNodes(Baselines.Format(print)));
		}

		[Fact]
		public void DifferenceShowsTheColumnInALongLine()
		{
			string line = new string('A', 1000);
			string message = Baselines.FirstDifference("<a>\n" + line + "B\n", "<a>\n" + line + "C\n");
			Assert.Contains("line 2, column 1001", message);
			Assert.Contains("AB", message);
			Assert.Contains("AC", message);
		}

		[Fact]
		public void DifferenceShowsAnIndentationChange()
		{
			string message = Baselines.FirstDifference("\t<b />", "\t\t<b />");
			Assert.Contains("expected: \\t<b />", message);
			Assert.Contains("actual:   \\t\\t<b />", message);
		}

		// One line per missing and per orphaned baseline, not only the first.
		private static void AssertSameFixtures(IEnumerable<string> expected, IEnumerable<string> actual)
		{
			string[] missing = expected.Except(actual, StringComparer.Ordinal).ToArray();
			string[] orphaned = actual.Except(expected, StringComparer.Ordinal).ToArray();
			Assert.True(missing.Length == 0 && orphaned.Length == 0,
				"Missing baselines:" + Lines(missing) + "\nBaselines without a printing fixture:" + Lines(orphaned));
		}

		private static string Lines(string[] paths)
		{
			return paths.Length == 0 ? " none" : string.Concat(paths.Select(path => "\n  " + path));
		}

		// Without PreserveWhitespace the indentation of the formatted print is dropped.
		private static string[] TextNodes(string print)
		{
			XmlDocument document = new XmlDocument();
			document.LoadXml(print);
			return document.SelectNodes("//text()").Cast<XmlNode>().Select(node => node.Value).ToArray();
		}
	}
}
