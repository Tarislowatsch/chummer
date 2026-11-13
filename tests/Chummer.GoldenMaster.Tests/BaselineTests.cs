using System;
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
			Assert.Equal(Fixtures.RelativePaths().Where(path => !PrintTests.KnownPrintFailures.ContainsKey(path)), Baselines.FixturesWithBaseline(Fixtures.Baseline));
		}

		[Fact]
		public void GermanBaselinesExistForExactlyTheGermanSubset()
		{
			Assert.Equal(Fixtures.GermanSubset.OrderBy(path => path, StringComparer.Ordinal), Baselines.FixturesWithBaseline(Fixtures.German));
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

		// Without PreserveWhitespace the indentation of the formatted print is dropped.
		private static string[] TextNodes(string print)
		{
			XmlDocument document = new XmlDocument();
			document.LoadXml(print);
			return document.SelectNodes("//text()").Cast<XmlNode>().Select(node => node.Value).ToArray();
		}
	}
}
