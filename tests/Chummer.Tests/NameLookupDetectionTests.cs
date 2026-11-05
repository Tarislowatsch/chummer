using System.Linq;
using Xunit;

namespace Chummer.Tests
{
	// - hand-built source: the real code shows today's lookups, never that a bad one is caught
	public class NameLookupDetectionTests
	{
		private static NameLookups.Lookup Single(string line)
		{
			return NameLookups.LookupsIn("frmX.cs", new[] { line }).Single();
		}

		[Fact]
		public void AcceptsAnAnchoredLookup()
		{
			NameLookups.Lookup lookup = Single("x = d.SelectSingleNode(\"/chummer/gears/gear[name = \\\"\" + s + \"\\\"]\");");

			Assert.Equal("/chummer/gears/gear", lookup.Path);
			Assert.False(lookup.IsUnanchored);
			Assert.Equal("frmX.cs:1", lookup.Site);
		}

		[Theory]
		[InlineData("x = d.SelectSingleNode(\"//gear[name = \\\"\" + s + \"\\\"]\");")]
		[InlineData("x = d.SelectSingleNode(\"/chummer/*/gear[name = \\\"\" + s + \"\\\"]\");")]
		[InlineData("x = d.SelectSingleNode(\"/chummer//power[name = \\\"\" + s + \"\\\"]\");")]
		public void FlagsADescendantOrWildcardStep(string line)
		{
			Assert.True(Single(line).IsUnanchored);
		}

		[Fact]
		public void FindsANameComparedAfterAnd()
		{
			Assert.Equal("/chummer/gears/gear",
				Single("x = d.SelectSingleNode(\"/chummer/gears/gear[category = 'A' and name = \\\"\" + s + \"\\\"]\");").Path);
		}

		[Fact]
		public void TakesTheCodeBeforeAComputedPath()
		{
			NameLookups.Lookup lookup = Single("x = d.SelectSingleNode(strXPath + \"[name = \\\"\" + s + \"\\\"]\");");

			Assert.Equal("", lookup.Path);
			Assert.Equal("strXPath +", lookup.DynamicPrefix);
		}

		[Fact]
		public void IgnoresACommentedOutLookup()
		{
			Assert.Empty(NameLookups.LookupsIn("frmX.cs", new[] { "// x = d.SelectSingleNode(\"//gear[name = 'A']\");" }));
		}

		[Fact]
		public void IgnoresASearchByPartialName()
		{
			Assert.Empty(NameLookups.LookupsIn("frmX.cs",
				new[] { "s = \"/chummer/gears/gear[contains(name, \\\"\" + t + \"\\\")]\";" }));
		}
	}
}
