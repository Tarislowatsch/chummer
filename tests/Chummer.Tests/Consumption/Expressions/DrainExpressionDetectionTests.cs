using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Chummer.Tests.Consumption.Expressions
{
	public class DrainExpressionDetectionTests
	{
		private static readonly IReadOnlyDictionary<string, string> English =
			new Dictionary<string, string>(StringComparer.Ordinal)
			{
				{ "String_AttributeWILShort", "WIL" },
				{ "String_AttributeMAGShort", "MAG" },
			};

		private static readonly string[] MagicCodes = { "BOD", "AGI", "REA", "STR", "CHA", "INT", "LOG", "WIL", "MAG" };

		[Fact]
		public void LiteralAndLocalizedCodesFormOneChain()
		{
			string[] source =
			{
				"string s = text.Replace(\"BOD\", a);",
				"s = s.Replace(LanguageManager.Instance.GetString(\"String_AttributeWILShort\"), b);",
				"s = s.Replace(\"CHA\", c).Replace(\"MAG\", d);",
				"Evaluate(s);",
				"t = t.Replace(\"STR\", e);",
			};

			DrainExpressions.Chain[] chains = DrainExpressions.ChainsIn("x.cs", source, English).ToArray();

			Assert.Equal(new[] { "x.cs:1", "x.cs:5" }, chains.Select(chain => chain.Site));
			Assert.Equal(new[] { "BOD", "WIL", "CHA", "MAG" }, chains[0].Codes);
		}

		// - a commented-out Replace substitutes nothing at run time
		[Fact]
		public void ACommentedReplaceIsNoSubstitution()
		{
			string[] source =
			{
				"s = s.Replace(\"WIL\", a);",
				"// s = s.Replace(\"RES\", b);",
			};

			Assert.Equal(new[] { "WIL" }, DrainExpressions.ChainsIn("x.cs", source, English).Single().Codes);
		}

		[Theory]
		[InlineData("WIL + CHA")]
		[InlineData("(WIL + LOG) * 2")]
		[InlineData("MAG")]
		public void AFullySubstitutedIntegralDrainPasses(string drain)
		{
			Assert.Null(DrainExpressions.FailureOf(drain, MagicCodes));
		}

		// - a stream's code under a tradition consumer: the print would throw on NaN
		[Fact]
		public void ACodeTheConsumerSkipsIsCaught()
		{
			Assert.Equal("leaves RES unreplaced", DrainExpressions.FailureOf("WIL + RES", MagicCodes));
		}

		// - Convert.ToInt32("4.5") throws inside the forms' try
		[Fact]
		public void AFractionalDrainIsCaught()
		{
			Assert.StartsWith("'", DrainExpressions.FailureOf("(WIL + CHA) div 2", MagicCodes));
		}

		[Fact]
		public void AMalformedDrainIsCaught()
		{
			Assert.StartsWith("XPathException", DrainExpressions.FailureOf("WIL +", MagicCodes));
		}
	}
}
