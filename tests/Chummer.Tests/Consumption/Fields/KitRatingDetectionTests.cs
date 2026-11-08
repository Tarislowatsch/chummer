using System.Linq;
using System.Xml;
using Chummer.Tests.Kernel;
using Chummer.Tests.Rulebook;
using Xunit;

namespace Chummer.Tests.Consumption.Fields
{
	public class KitRatingDetectionTests
	{
		private static KitRatings.Limit LimitFor(string ruleId)
		{
			return KitRatings.Limits.Single(limit => limit.RuleId == ruleId);
		}

		private static readonly XmlNode Item = new XmlDocument().CreateElement("gear");

		[Theory]
		[InlineData("pack gear", "3", "3", KitRatings.Outcome.Fits)]
		[InlineData("pack gear", "1", "0", KitRatings.Outcome.Exceeds)]
		[InlineData("pack cyberware", "1", null, KitRatings.Outcome.Exceeds)]
		[InlineData("pack gear", "x", "6", KitRatings.Outcome.NotANumber)]
		[InlineData("pack vehicle mods", "4", "qty", KitRatings.Outcome.Quantity)]
		[InlineData("pack gear", "4", "qty", KitRatings.Outcome.NotANumber)]
		[InlineData("pack programs", "5", "", KitRatings.Outcome.NoMaximum)]
		[InlineData("pack programs", "5", "4", KitRatings.Outcome.Exceeds)]
		public void ARatingIsComparedAsTheItemReadsItsMaximum(string ruleId, string rating, string maximum,
			KitRatings.Outcome expected)
		{
			Assert.Equal(expected, KitRatings.OutcomeOf(LimitFor(ruleId), Item, rating, maximum));
		}

		[Fact]
		public void AMissedLookupIsLeftToTheReferenceCheck()
		{
			Assert.Equal(KitRatings.Outcome.NoTarget, KitRatings.OutcomeOf(LimitFor("pack gear"), null, "9", null));
		}

		private static readonly XmlDocument GearFile = Xml.Parse("<chummer><gears>"
			+ "<gear><name>Tool</name><category>A</category><rating>1</rating></gear>"
			+ "<gear><name>Tool</name><category>B</category><rating>2</rating></gear>"
			+ "</gears></chummer>");

		// - as AddPACKSGear: the category narrows only when the kit names one, else the first wins
		[Theory]
		[InlineData("<gear><name>Tool</name><category>B</category></gear>", "2")]
		[InlineData("<gear><name>Tool</name></gear>", "1")]
		[InlineData("<gear><name>Tool</name><category>C</category></gear>", null)]
		public void TheTargetIsTheItemTheConsumerFinds(string entry, string expectedRating)
		{
			XmlNode reference = Xml.Parse(entry).DocumentElement;

			Assert.Equal(expectedRating,
				CrossFileReferences.TargetOf(LimitFor("pack gear").Rule, reference, file => GearFile)?["rating"]?.InnerText);
		}
	}
}
