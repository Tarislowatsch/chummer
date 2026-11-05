using System.Collections.Generic;
using System.Linq;
using System.Xml;
using Xunit;

namespace Chummer.Tests
{
	// - every real avail parses today: the data cases cannot show a port gone lenient
	// - each case pins one quirk of one calculator to an entry it accepts or rejects
	public class AvailabilityExpressionDetectionTests
	{
		[Fact]
		public void PlainAndSuffixedNumbersParse()
		{
			Assert.Empty(FailuresFor("Weapon", "<weapon><avail>12R</avail></weapon>"));
			Assert.Empty(FailuresFor("Gear", "<gear><rating>0</rating><avail>4F</avail></gear>"));
		}

		[Fact]
		public void UnknownSuffixIsReported()
		{
			Assert.Single(FailuresFor("Vehicle", "<vehicle><avail>12X</avail></vehicle>"));
		}

		// - Armor.TotalAvail has no Rating branch, ArmorMod.TotalAvail has
		[Fact]
		public void RatingExpressionIsCalculatorSpecific()
		{
			Assert.Empty(FailuresFor("ArmorMod",
				"<mod><maxrating>3</maxrating><avail>(Rating * 2)R</avail></mod>"));
			Assert.Single(FailuresFor("Armor", "<armor><avail>(Rating * 2)R</avail></armor>"));
		}

		[Fact]
		public void BrokenRatingExpressionIsReported()
		{
			Assert.Single(FailuresFor("Gear", "<gear><rating>6</rating><avail>(Rating * 3R</avail></gear>"));
		}

		// - Cyberware indexes the list by rating: a short list fails only at the top of the range
		[Fact]
		public void FixedValuesMustCoverEveryRating()
		{
			Assert.Empty(FailuresFor("Cyberware",
				"<cyberware><rating>3</rating><avail>FixedValues(4R,8R,12R)</avail></cyberware>"));

			ExpressionChecks.ExpressionFailure failure = FailuresFor("Cyberware",
				"<cyberware><rating>3</rating><avail>FixedValues(4R,8R)</avail></cyberware>").Single();
			Assert.Equal(3, failure.Rating);
		}

		// - avail10 only applies from rating 7: a gear entry capped lower never reads it
		[Fact]
		public void Avail10IsReadOnlyFromRatingSeven()
		{
			Assert.Empty(FailuresFor("Gear",
				"<gear><rating>6</rating><avail>Rating</avail><avail10>(Rating</avail10></gear>"));
			Assert.Single(FailuresFor("Gear",
				"<gear><rating>8</rating><avail>Rating</avail><avail10>(Rating</avail10></gear>"));
		}

		// - the Straight branch cuts one character, GetAvailInt replaces every R and F
		// - so a doubled suffix crashes a calculating class but passes a raw-text one
		[Fact]
		public void DoubledSuffixFailsOnlyWhereTheCalculatorCutsOneCharacter()
		{
			Assert.Single(FailuresFor("VehicleMod", "<mod><rating>0</rating><avail>24FF</avail></mod>"));
			Assert.Empty(FailuresFor("Vehicle", "<vehicle><avail>24FF</avail></vehicle>"));
		}

		// - Gear turns an empty avail into "0" (clsEquipment.cs:11158), the others do not
		[Fact]
		public void EmptyAvailIsGearOnly()
		{
			Assert.Empty(FailuresFor("Gear", "<gear><rating>0</rating><avail></avail></gear>"));
			Assert.Single(FailuresFor("VehicleMod", "<mod><rating>0</rating><avail></avail></mod>"));
		}

		// - a leading + marks a child bonus: ArmorMod hands it on, Cyberware evaluates it
		[Fact]
		public void PlusPrefixIsHandedOnOrEvaluated()
		{
			Assert.Empty(FailuresFor("ArmorMod", "<mod><maxrating>0</maxrating><avail>+2R</avail></mod>"));
			Assert.Empty(FailuresFor("Cyberware",
				"<cyberware><rating>4</rating><avail>+(Rating - 2)</avail></cyberware>"));
			Assert.Single(FailuresFor("Cyberware",
				"<cyberware><rating>4</rating><avail>+(Rating - </avail></cyberware>"));
		}

		[Fact]
		public void EveryRatingInTheRangeIsReplayed()
		{
			Assert.Equal(new[] { 2, 3, 4, 5, 6, 7, 8, 9, 10 },
				ExpressionChecks.RatingsOf("Gear", Item("<gear><rating>10</rating><minrating>2</minrating></gear>")));
			Assert.Equal(new[] { 1, 2, 3, 4 }, ExpressionChecks.RatingsOf("Gear", Item("<gear><rating>4</rating></gear>")));
		}

		// - above the limit the sweep stops, the top end is still replayed
		[Fact]
		public void RatingsAboveTheLimitKeepOnlyTheTopEnd()
		{
			IReadOnlyList<int> ratings =
				ExpressionChecks.RatingsOf("Gear", Item("<gear><rating>1000000</rating></gear>"));

			Assert.Equal(ExpressionChecks.ExhaustiveRatingLimit + 1, ratings.Count);
			Assert.Equal(ExpressionChecks.ExhaustiveRatingLimit, ratings[ratings.Count - 2]);
			Assert.Equal(1000000, ratings[ratings.Count - 1]);
		}

		// - XPath divides by zero into Infinity, Convert.ToInt32 then throws: only at rating 5
		[Fact]
		public void DivisionByZeroAtAnInnerRatingIsFound()
		{
			ExpressionChecks.ExpressionFailure failure = FailuresFor("Gear",
				"<gear><rating>10</rating><minrating>2</minrating><avail>100 div (Rating - 5)</avail></gear>").Single();

			Assert.Equal(5, failure.Rating);
			Assert.Equal("OverflowException", failure.Error);
		}

		// - a failure that only appears at an odd rating must not slip between samples
		[Fact]
		public void FailureAtAnIntermediateRatingIsFound()
		{
			ExpressionChecks.ExpressionFailure failure = FailuresFor("Cyberware",
				"<cyberware><rating>6</rating><avail>FixedValues(1,2,3,4,x,6)</avail></cyberware>").Single();

			Assert.Equal(5, failure.Rating);
		}

		// - an unrated class, an absent rating and a rating of 0 all leave the entry at 0
		// - vehicle mods rated "qty" are counted, not rated: their rating stays at 1
		[Fact]
		public void UnratedEntriesUseTheDefaultRating()
		{
			Assert.Equal(new[] { 0 }, ExpressionChecks.RatingsOf("Weapon", Item("<weapon><rating>6</rating></weapon>")));
			Assert.Equal(new[] { 0 }, ExpressionChecks.RatingsOf("Cyberware", Item("<cyberware />")));
			Assert.Equal(new[] { 0 }, ExpressionChecks.RatingsOf("Gear", Item("<gear><rating>0</rating></gear>")));
			Assert.Equal(new[] { 1 }, ExpressionChecks.RatingsOf("VehicleMod", Item("<mod><rating>qty</rating></mod>")));
		}

		private static IReadOnlyList<ExpressionChecks.ExpressionFailure> FailuresFor(string entity, string itemXml)
		{
			ExpressionChecks.ExpressionRule rule = AvailabilityExpressions.Rules.Single(candidate => candidate.Entity == entity);
			return ExpressionChecks.FailuresIn(rule, new[] { Item(itemXml) });
		}

		private static XmlNode Item(string itemXml)
		{
			XmlDocument document = new XmlDocument();
			document.LoadXml(itemXml);
			return document.DocumentElement;
		}
	}
}
