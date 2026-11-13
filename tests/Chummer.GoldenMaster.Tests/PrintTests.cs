using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using Xunit;

namespace Chummer.GoldenMaster.Tests
{
	[Collection(AppHost.Collection)]
	public class PrintTests
	{
		// - fixtures whose print throws today, with the exception type
		// - a listed fixture that prints fails its test
		internal static readonly Dictionary<string, Type> KnownPrintFailures = new Dictionary<string, Type>
		{
			// Legacy save: every techprogram has an empty <skill />.
			{ @"examples\Technomancer.chum", typeof(NullReferenceException) },
		};

		// Exact count: a lost or added fixture must be a deliberate change.
		[Fact]
		public void FixtureCountIsPinned()
		{
			Assert.Equal(398, Fixtures.RelativePaths().Count());
		}

		[Fact]
		public void EveryKnownFailureIsAFixture()
		{
			HashSet<string> fixtures = new HashSet<string>(Fixtures.RelativePaths(), StringComparer.Ordinal);
			Assert.DoesNotContain(KnownPrintFailures.Keys, path => !fixtures.Contains(path));
		}

		// A mugshot fixture: the image's file name was the only random part of the print.
		[Fact]
		public void PrintingTwiceGivesTheSamePrint()
		{
			Assert.Equal(Fixtures.Print(MugshotTests.MugshotFixture), Fixtures.Print(MugshotTests.MugshotFixture));
		}

		[Theory]
		[MemberData(nameof(Fixtures.Characters), MemberType = typeof(Fixtures))]
		public void EveryFixtureMatchesItsBaseline(string relativePath)
		{
			Type expected;
			if (KnownPrintFailures.TryGetValue(relativePath, out expected))
			{
				Exception thrown = Record.Exception(() => Fixtures.Print(relativePath));
				Assert.True(thrown != null, relativePath + " prints now: remove it from KnownPrintFailures.");
				Assert.IsType(expected, thrown);
				return;
			}

			RecordingMugshotStore mugshots = new RecordingMugshotStore();
			string print = Fixtures.Print(relativePath, mugshots);

			// Against the save, not the baseline: an accepted baseline can carry a wrong mugshot.
			bool hasMugshot = Fixtures.HasMugshot(relativePath);
			Assert.Equal(hasMugshot ? 1 : 0, mugshots.Calls);
			XmlDocument document = new XmlDocument();
			document.LoadXml(print);
			Assert.Equal(hasMugshot ? RecordingMugshotStore.Url : "", document.SelectSingleNode("/characters/character/mugshot").InnerText);
			Baselines.AssertMatches(relativePath, Fixtures.Baseline, print);
		}
	}
}
