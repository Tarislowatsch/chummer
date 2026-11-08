using System;
using System.Linq;
using System.Xml;
using System.Xml.XPath;
using Chummer.Tests.Kernel;
using Xunit;

namespace Chummer.Tests.Consumption.Expressions
{
	// - hand-built values: the real data shows today's values, never that a bad one is caught
	public class EssExpressionDetectionTests
	{
		[Theory]
		[InlineData("0.2", 3, "0.2")]
		[InlineData("Rating * 0.1", 3, "0.3")]
		[InlineData("(Rating * 0.1) + 0.2", 2, "0.4")]
		[InlineData("ceiling(Rating div 2) * 0.1", 3, "0.2")]
		[InlineData("FixedValues(0.1,0.25,0.5)", 2, "0.25")]
		public void CalculatesTheShapesTheDataUses(string ess, int rating, string expected)
		{
			Assert.Equal(decimal.Parse(expected, Cultures.Application), EssExpressions.Calculate(ess, rating));
		}

		[Fact]
		public void ThrowsOnFixedValuesAtRatingZero()
		{
			Assert.Throws<IndexOutOfRangeException>(() => EssExpressions.Calculate("FixedValues(0.1,0.2)", 0));
		}

		[Fact]
		public void ThrowsOnAPlainValueThatIsNoNumber()
		{
			Assert.Throws<FormatException>(() => EssExpressions.Calculate("0,2a", 1));
		}

		private static ExpressionChecks.ExpressionFailure[] FailuresOf(string xml)
		{
			XmlDocument document = Xml.Parse("<cyberwares>" + xml + "</cyberwares>");
			return ExpressionChecks.FailuresIn(EssExpressions.Rules.First(rule => rule.Entity == "Cyberware"),
				document.DocumentElement.ChildNodes.Cast<XmlNode>()).ToArray();
		}

		// - the reason the dialog branch is replayed at all
		[Fact]
		public void ADecimalCommaPassesCalculatedEssButNotTheDialog()
		{
			Assert.Equal(2m, EssExpressions.Calculate("0,2", 1));
			Assert.ThrowsAny<XPathException>(() => EssExpressions.Select("0,2", 1));
			Assert.Single(FailuresOf("<cyberware><name>A</name><ess>0,2</ess></cyberware>"));
		}

		[Fact]
		public void FlagsFixedValuesThatDoNotMatchTheRating()
		{
			Assert.Single(FailuresOf("<cyberware><name>A</name><rating>3</rating>"
				+ "<ess>FixedValues(0,2,0.4,0.6)</ess></cyberware>"));
			Assert.Empty(FailuresOf("<cyberware><name>B</name><rating>3</rating>"
				+ "<ess>FixedValues(0.2,0.4,0.6)</ess></cyberware>"));
		}

		[Fact]
		public void ReplaysASubsystemCandidateAtRatingZero()
		{
			Assert.Single(FailuresOf("<cyberware><name>A</name><capacity>[*]</capacity>"
				+ "<rating>3</rating><ess>FixedValues(0.1,0.2,0.3)</ess></cyberware>"));
			Assert.Empty(FailuresOf("<cyberware><name>B</name><capacity>[1]</capacity>"
				+ "<rating>3</rating><ess>FixedValues(0.1,0.2,0.3)</ess></cyberware>"));
		}
	}
}
