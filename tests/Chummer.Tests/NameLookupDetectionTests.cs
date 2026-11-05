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
			Assert.False(lookup.IsComputed);
			Assert.Equal("frmX.cs:1", lookup.Site);
		}

		[Theory]
		[InlineData("x = d.SelectSingleNode(\"//gear[name = \\\"\" + s + \"\\\"]\");")]
		[InlineData("x = d.SelectSingleNode(\"/chummer/*/gear[name = \\\"\" + s + \"\\\"]\");")]
		[InlineData("x = d.SelectSingleNode(\"/chummer//power[name = \\\"\" + s + \"\\\"]\");")]
		[InlineData("x = d.SelectSingleNode(\"/chummer/descendant::power[name = \\\"\" + s + \"\\\"]\");")]
		public void FlagsADescendantWildcardOrAxisStep(string line)
		{
			Assert.True(Single(line).IsUnanchored);
		}

		// - an escaped quote earlier in the filter once hid the name comparison
		[Fact]
		public void FindsANameAfterAQuotedComparison()
		{
			NameLookups.Lookup lookup = Single(
				"x = d.SelectSingleNode(\"//skill[knowledge = \\\"True\\\" and name = \" + CleanXPath(v) + \"]\");");

			Assert.Equal("//skill", lookup.Path);
			Assert.True(lookup.IsUnanchored);
		}

		[Fact]
		public void FindsANameWhoseFilterOpensInAnEarlierLiteral()
		{
			Assert.Equal("/chummer/martialarts/martialart", Single(
				"l = d.SelectNodes(\"/chummer/martialarts/martialart[(\" + o.BookXPath() + \") and name = \\\"\" + s + \"\\\"]\");").Path);
		}

		[Fact]
		public void TreatsAPartlyComputedPathAsComputed()
		{
			NameLookups.Lookup lookup = Single(
				"x = d.SelectSingleNode(\"/chummer/\" + objType.Name + \"/skill[name = \\\"\" + s + \"\\\"]\");");

			Assert.Equal("/chummer/{objType.Name}/skill", lookup.Path);
			Assert.True(lookup.IsComputed);
		}

		[Fact]
		public void TreatsAVariablePathAsComputed()
		{
			NameLookups.Lookup lookup = Single("x = d.SelectSingleNode(strXPath + \"[name = \\\"\" + s + \"\\\"]\");");

			Assert.Equal("{strXPath}", lookup.Path);
			Assert.True(lookup.IsComputed);
		}

		[Fact]
		public void TreatsAFilterBuiltApartAsComputed()
		{
			NameLookups.Lookup lookup = Single("strFilter += \"name = \\\"\" + s + \"\\\" or \";");

			Assert.Equal("(filter built in strFilter)", lookup.Path);
			Assert.True(lookup.IsComputed);
		}

		[Fact]
		public void IgnoresACommentedOutLookup()
		{
			Assert.Empty(NameLookups.LookupsIn("frmX.cs", new[] { "// x = d.SelectSingleNode(\"//gear[name = 'A']\");" }));
		}

		[Theory]
		[InlineData("s = \"/chummer/gears/gear[contains(name, \\\"\" + t + \"\\\")]\";")]
		[InlineData("s = \"/chummer/gears/gear[@name = 'A']\";")]
		[InlineData("s = \"/chummer/skills/skill[skillname = 'A']\";")]
		public void IgnoresWhatIsNoNameComparison(string line)
		{
			Assert.Empty(NameLookups.LookupsIn("frmX.cs", new[] { line }));
		}

		[Theory]
		[InlineData("file:///{0}/", false)]
		[InlineData("http://example.org//x", true)]
		[InlineData("[*]", false)]
		[InlineData("/chummer/*", true)]
		public void JudgesLiteralsByTheirSteps(string literal, bool unanchored)
		{
			Assert.Equal(unanchored, NameLookups.IsUnanchoredLiteral(literal));
		}

		[Fact]
		public void KeepsAQuoteInsideACharLiteralAsCode()
		{
			Assert.Equal(new[] { "a" }, NameLookups.Tokenize("c = '\"'; s = \"a\";")
				.Where(token => token.IsLiteral).Select(token => token.Text));
		}
	}
}
