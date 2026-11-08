using System.Linq;
using System.Xml;
using Chummer.Tests.Consumption.Expressions;
using Chummer.Tests.Kernel;
using Xunit;

namespace Chummer.Tests.Consumption.Bonus
{
	public class BonusValueDetectionTests
	{
		[Fact]
		public void AFractionPrintedInTheGermanCultureParsesTenfold()
		{
			Assert.Equal(0, BonusValues.ValueToInt("Rating div 2", 1, 0, Cultures.Application));
			Assert.Equal(5, BonusValues.ValueToInt("Rating div 2", 1, 0, BonusValues.LocalCulture));
		}

		[Fact]
		public void AFractionalResultIsReportedAsCultureDependent()
		{
			BonusValues.BonusValueFailure failure = Assert.Single(Failures(
				"<gear><name>G</name><rating>3</rating><bonus><initiative>Rating * 0.5</initiative></bonus></gear>"));

			Assert.Equal(1, failure.Rating);
			Assert.Equal("en-US gives 0, de-DE gives 5", failure.Error);
		}

		[Fact]
		public void FixedValuesShorterThanTheRatingFail()
		{
			BonusValues.BonusValueFailure failure = Assert.Single(Failures(
				"<gear><name>G</name><rating>4</rating><bonus><skillwire>FixedValues(2,4,5)</skillwire></bonus></gear>"));

			Assert.Equal(4, failure.Rating);
			Assert.Equal("IndexOutOfRangeException", failure.Error);
		}

		// - "levels yes" goes up to MAG, unrated cyberware runs at 0: both get 0..limit
		[Fact]
		public void AnOwnerWithoutANumericRatingIsReplayedFromZeroToTheLimit()
		{
			XmlDocument document = Xml.Parse("<power><name>P</name><levels>yes</levels><bonus><reach>1</reach></bonus></power>");

			Assert.Equal(Enumerable.Range(0, ExpressionChecks.ExhaustiveRatingLimit + 1),
				BonusValues.RatingsOf(document.SelectSingleNode("power/bonus")));
		}

		[Fact]
		public void UnratedCyberwareIsReplayedAtZero()
		{
			Assert.Equal(0, Assert.Single(Failures(
				"<cyberware><name>C</name><bonus><reach>FixedValues(1)</reach></bonus></cyberware>")).Rating);
		}

		[Fact]
		public void AMinimumRatingOfZeroIsReplayed()
		{
			Assert.Equal(0, Assert.Single(Failures(
				"<gear><name>G</name><rating>2</rating><minrating>0</minrating>"
					+ "<bonus><reach>FixedValues(1,2)</reach></bonus></gear>")).Rating);
		}

		[Fact]
		public void ACultureThatThrowsIsStillReportedAsCultureDependent()
		{
			Assert.Equal("en-US gives 0, de-DE throws OverflowException", Assert.Single(Failures(
				"<gear><name>G</name><rating>1</rating><bonus><reach>Rating div 3</reach></bonus></gear>")).Error);
		}

		[Fact]
		public void TheGermanCultureIgnoresThisMachinesOverrides()
		{
			Assert.False(BonusValues.LocalCulture.UseUserOverride);
		}

		[Fact]
		public void AFailingValueDoesNotHideTheNextInItsBranch()
		{
			Assert.Equal(new[] { "conditionmonitor/physical", "conditionmonitor/stun" }, Failures(
				"<gear><name>G</name><rating>1</rating><bonus><conditionmonitor><physical>Rating div 2</physical>"
					+ "<stun>x</stun></conditionmonitor></bonus></gear>").Select(failure => failure.Path));
		}

		[Fact]
		public void AnOwnerRatedZeroIsReplayedAtZero()
		{
			Assert.Equal(0, Assert.Single(Failures(
				"<gear><name>G</name><rating>0</rating><bonus><reach>FixedValues(1)</reach></bonus></gear>")).Rating);
		}

		[Fact]
		public void AnOwnerAboveTheLimitIsCutShort()
		{
			Assert.Equal(new[] { "test.xml: G (500)" }, Check(
				"<gear><name>G</name><rating>500</rating><bonus><reach>Rating</reach></bonus></gear>").CutShort);
		}

		[Fact]
		public void ANonIntegerValueFails()
		{
			Assert.Equal("FormatException", Assert.Single(Failures(
				"<quality><name>Q</name><bonus><reach>1.5</reach></bonus></quality>")).Error);
		}

		// - Infinity prints as text no parse accepts
		[Fact]
		public void AnAttributeIsReplayedAtZero()
		{
			Assert.Equal("FormatException", Assert.Single(Failures(
				"<quality><name>Q</name><bonus><reach>Rating div MAG</reach></bonus></quality>")).Error);
		}

		// - Contains("min") matches <minimum>, the indexer ["min"] finds nothing
		[Fact]
		public void ASubstringGuardCanPassOnAMissingElement()
		{
			BonusValues.BonusValueFailure failure = Assert.Single(Failures(
				"<quality><name>Q</name><bonus><specificattribute><name>BOD</name><minimum>1</minimum>"
					+ "</specificattribute></bonus></quality>"));

			Assert.Equal("NullReferenceException", failure.Error);
			Assert.Equal("test.xml | Q | specificattribute/min = (missing)", failure.Key);
		}

		[Fact]
		public void AnExistenceGuardSkipsAMissingElement()
		{
			Assert.Empty(Failures(
				"<quality><name>Q</name><bonus><armor><bonus>1</bonus></armor></bonus></quality>"));
		}

		[Fact]
		public void ANaturalMaximumBypassesValueToInt()
		{
			Assert.Empty(Failures(
				"<quality><name>Q</name><bonus><specificattribute><name>BOD</name><max>8-natural</max>"
					+ "</specificattribute></bonus></quality>"));
			Assert.Equal("specificattribute/max (-natural)", Assert.Single(Failures(
				"<quality><name>Q</name><bonus><specificattribute><name>BOD</name><max>Rating-natural</max>"
					+ "</specificattribute></bonus></quality>")).Path);
		}

		[Fact]
		public void EssenceTakesNoExpression()
		{
			Assert.Equal("specificattribute/val (ESS)", Assert.Single(Failures(
				"<quality><name>Q</name><bonus><specificattribute><name>ESS</name><val>Rating</val>"
					+ "</specificattribute></bonus></quality>")).Path);
		}

		[Fact]
		public void TheIndexerReadsOnlyTheFirstBlock()
		{
			Assert.Empty(Failures(
				"<quality><name>Q</name><bonus><selectskillgroup><bonus>1</bonus></selectskillgroup>"
					+ "<selectskillgroup><bonus>x</bonus></selectskillgroup></bonus></quality>"));
		}

		[Fact]
		public void ALoopReadsEveryBlock()
		{
			Assert.Equal("skillcategory/bonus", Assert.Single(Failures(
				"<quality><name>Q</name><bonus><skillcategory><name>A</name><bonus>1</bonus></skillcategory>"
					+ "<skillcategory><name>B</name><bonus>x</bonus></skillcategory></bonus></quality>")).Path);
		}

		[Fact]
		public void AVehicleModBonusIsNotReplayed()
		{
			XmlDocument document = Xml.Parse("<chummer><mods><mod><name>M</name><bonus><reach>x</reach></bonus></mod></mods></chummer>");

			Assert.Empty(BonusValues.CheckIn("vehicles.xml", "vehicles.xml", document).Failures);
		}

		[Fact]
		public void AReachedValueIsCountedOnceNotPerRating()
		{
			Assert.Single(Check(
				"<gear><name>G</name><rating>6</rating><bonus><reach>Rating</reach></bonus></gear>").Reached);
		}

		private static readonly string[] Source =
		{
			"public bool CreateImprovements(XmlNode nodBonus)",
			"{",
			"\tif (NodeExists(nodBonus, \"reach\"))",
			"\t{",
			"\t\tCreate(ValueToInt(nodBonus[\"reach\"].InnerText,",
			"\t\t\tintRating));",
			"\t\tLog(\"ValueToInt(fake, 1)\", this.ValueToInt(nodBonus[\"reach\"].InnerText, intRating + 1));",
			"\t}",
			"\tif (NodeExists(nodBonus, \"armor\"))",
			"\t{",
			"\t\tif (NodeExists(nodBonus, \"b\"))",
			"\t\t{",
			"\t\t\tCreate(ValueToInt(Pick(nodBonus, \"b\"), intRating));",
			"\t\t}",
			"\t}",
			"\tCreate(ValueToInt(nodBonus[\"stray\"].InnerText, intRating));",
			"}",
			"private int ValueToInt(string strValue, int intRating = 1)",
			"{",
			"}",
		};

		[Fact]
		public void EveryCallIsAssignedToItsInnermostBranch()
		{
			Assert.Equal(
				new[]
				{
					"5 reach | nodBonus[\"reach\"].InnerText, intRating",
					"7 reach | nodBonus[\"reach\"].InnerText, intRating + 1",
					"13 b | Pick(nodBonus, \"b\"), intRating",
					"16 (no branch) | nodBonus[\"stray\"].InnerText, intRating",
				},
				BonusValues.CallsIn(Source, new[] { "reach", "armor", "b" })
					.Select(call => call.Line + " " + call.Key));
		}

		private static BonusValues.BonusValueCheck Check(string item)
		{
			XmlDocument document = Xml.Parse("<chummer><items>" + item + "</items></chummer>");
			return BonusValues.CheckIn("test.xml", "test.xml", document);
		}

		private static BonusValues.BonusValueFailure[] Failures(string item)
		{
			return Check(item).Failures.ToArray();
		}
	}
}
