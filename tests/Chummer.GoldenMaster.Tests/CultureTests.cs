using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Xml;
using Xunit;

namespace Chummer.GoldenMaster.Tests
{
	[Collection(AppHost.Collection)]
	public class CultureTests
	{
		// - every element whose print the German subset changes, not the whole corpus
		// - exact: a new culture-dependent element must be added here deliberately
		private static readonly string[] CultureDependentElements =
		{
			"attribute/base",
			"cyberware/ess",
			"power/pointsperlevel",
			"power/totalpoints",
		};

		[Fact]
		public void EveryGermanCaseIsAFixture()
		{
			Assert.DoesNotContain(Fixtures.GermanSubset, path => !Fixtures.RelativePaths().Contains(path));
		}

		// A German case that adds no differing element watches nothing.
		[Fact]
		public void GermanSubsetCoversEveryCultureDependentElement()
		{
			Dictionary<string, string[]> differing = Fixtures.GermanSubset.ToDictionary(path => path, DifferingElements);
			Assert.DoesNotContain(differing, entry => entry.Value.Length == 0);
			Assert.Equal(CultureDependentElements, differing.Values.SelectMany(names => names).Distinct().OrderBy(name => name, StringComparer.Ordinal));
		}

		[Fact]
		public void PrintIgnoresTheAmbientCulture()
		{
			string path = Fixtures.GermanSubset[0];
			// A fresh en-US, not Fixtures.Baseline: a missing restore must leave a different object behind.
			Assert.Equal(PrintUnderAmbient(path, new CultureInfo("en-US")), PrintUnderAmbient(path, Fixtures.German));
		}

		// Also checks that Print hands the thread back with its culture unchanged.
		private static string PrintUnderAmbient(string relativePath, CultureInfo ambient)
		{
			Thread thread = Thread.CurrentThread;
			CultureInfo before = thread.CurrentCulture;
			thread.CurrentCulture = ambient;
			try
			{
				string print = Fixtures.Print(relativePath);
				Assert.Same(ambient, thread.CurrentCulture);
				return print;
			}
			finally
			{
				thread.CurrentCulture = before;
			}
		}

		// Same structure in both prints: Load parses decimals with GlobalOptions' fixed en-US.
		private static string[] DifferingElements(string relativePath)
		{
			XmlNodeList baseline = TextNodes(Fixtures.Print(relativePath));
			XmlNodeList german = TextNodes(Fixtures.Print(relativePath, culture: Fixtures.German));
			Assert.Equal(baseline.Count, german.Count);
			return Enumerable.Range(0, baseline.Count)
				.Where(i => baseline[i].Value != german[i].Value)
				.Select(i => baseline[i].ParentNode.ParentNode.Name + "/" + baseline[i].ParentNode.Name)
				.Distinct()
				.ToArray();
		}

		private static XmlNodeList TextNodes(string print)
		{
			XmlDocument document = new XmlDocument();
			document.LoadXml(print);
			return document.SelectNodes("//text()");
		}
	}
}
