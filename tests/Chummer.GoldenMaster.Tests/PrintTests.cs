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

		[Theory]
		[MemberData(nameof(Fixtures.Characters), MemberType = typeof(Fixtures))]
		public void EveryFixturePrints(string relativePath)
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
			XmlDocument document = new XmlDocument();
			document.LoadXml(Fixtures.Print(relativePath, mugshots));
			Assert.Equal(1, document.SelectNodes("/characters/character").Count);

			bool hasMugshot = Fixtures.HasMugshot(relativePath);
			Assert.Equal(hasMugshot ? 1 : 0, mugshots.Calls);
			Assert.Equal(hasMugshot ? RecordingMugshotStore.Url : "", document.SelectSingleNode("/characters/character/mugshot").InnerText);
		}
	}
}
