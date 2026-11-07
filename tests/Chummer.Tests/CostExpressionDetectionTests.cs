using System.Collections.Generic;
using System.Linq;
using System.Xml;
using Xunit;

namespace Chummer.Tests
{
	// - the data holds one failing cost: the other cases cannot show a port gone lenient
	// - each case pins one quirk of one calculator to an entry it accepts or rejects
	public class CostExpressionDetectionTests
	{
		[Fact]
		public void PlainNumbersAndRatingExpressionsParse()
		{
			Assert.Empty(FailuresFor("Vehicle", "<vehicle><cost>12000</cost></vehicle>"));
			Assert.Empty(FailuresFor("Gear", "<gear><rating>6</rating><cost>Rating * 2.5</cost></gear>"));
		}

		[Fact]
		public void BrokenExpressionIsReported()
		{
			Assert.Single(FailuresFor("WeaponMod", "<mod><rating>0</rating><cost>Weapon Cost *</cost></mod>"));
		}

		// - Gear rounds a fraction up, Cyberware parses the evaluated text and throws on it
		[Fact]
		public void FractionalResultIsCalculatorSpecific()
		{
			Assert.Empty(FailuresFor("Gear", "<gear><rating>1</rating><cost>Rating * 2.5</cost></gear>"));
			Assert.Single(FailuresFor("Cyberware", "<cyberware><rating>1</rating><cost>Rating * 2.5</cost></cyberware>"));
		}

		// - a range is replaced by the picked number before any calculator sees it
		// - with both bounds 0 the text survives and the plain branch throws on it
		[Fact]
		public void VariableCostSurvivesOnlyWithBothBoundsZero()
		{
			Assert.Empty(FailuresFor("Gear", "<gear><rating>0</rating><cost>Variable(20-100)</cost></gear>"));
			Assert.Empty(FailuresFor("Gear", "<gear><rating>0</rating><cost>Variable(50+)</cost></gear>"));
			Assert.Single(FailuresFor("Gear", "<gear><rating>0</rating><cost>Variable(0+)</cost></gear>"));
		}

		// - only the Gear Create resolves Variable: the commlink Creates keep the text
		[Fact]
		public void CommlinksDoNotResolveVariableCost()
		{
			Assert.Single(FailuresFor("Commlink", "<gear><rating>0</rating><cost>Variable(20-100)</cost></gear>"));
		}

		[Fact]
		public void MalformedVariableRangeIsReported()
		{
			Assert.Single(FailuresFor("Armor", "<armor><cost>Variable(20-)</cost></armor>"));
		}

		// - an empty <cost> hands over to cost3/6/10 by rating: a missing band fails at its ratings
		[Fact]
		public void BandedCostNeedsABandForEveryRating()
		{
			Assert.Empty(FailuresFor("Gear",
				"<gear><rating>6</rating><cost3>Rating * 10</cost3><cost6>Rating * 20</cost6></gear>"));

			ExpressionChecks.ExpressionFailure failure = FailuresFor("Gear",
				"<gear><rating>6</rating><cost3>Rating * 10</cost3></gear>").Single();
			Assert.Equal(4, failure.Rating);
		}

		// - a plugin's "*1.5" is read by its parent as a multiplier, a "*x" breaks that read
		[Fact]
		public void PluginMultiplierMustBeANumber()
		{
			Assert.Empty(FailuresFor("Cyberware", "<cyberware><cost>*1.5</cost></cyberware>"));
			Assert.Single(FailuresFor("Cyberware", "<cyberware><cost>*x</cost></cyberware>"));
		}

		// - the empty catch turns this into a silent 0: reported as data loss, as in NumericFields
		// - labelled as such: the report must not claim a crash the application never has
		[Fact]
		public void SwallowedCyberwareCostIsReportedAsSilentZero()
		{
			ExpressionChecks.ExpressionFailure failure =
				FailuresFor("Cyberware", "<cyberware><cost>12O0</cost></cyberware>").Single();

			Assert.Equal("silently 0 (FormatException)", failure.Error);
		}

		[Fact]
		public void UncaughtFailureKeepsItsExceptionName()
		{
			Assert.Equal("FormatException",
				FailuresFor("Vehicle", "<vehicle><cost>12O0</cost></vehicle>").Single().Error);
		}

		// - Accel(Running) goes before Accel, or it would become "1000(Running)"
		[Fact]
		public void VehicleModPlaceholdersAreReplacedInTheCodesOrder()
		{
			Assert.Empty(FailuresFor("VehicleMod",
				"<mod><rating>0</rating><cost>Accel(Running) * Speed * 2</cost></mod>"));
			Assert.Empty(FailuresFor("VehicleMod",
				"<mod><rating>0</rating><cost>Vehicle Cost * .1</cost></mod>"));
		}

		private static IReadOnlyList<ExpressionChecks.ExpressionFailure> FailuresFor(string entity, string itemXml)
		{
			XmlDocument document = new XmlDocument();
			document.LoadXml(itemXml);

			ExpressionChecks.ExpressionRule rule = CostExpressions.Rules.Single(candidate => candidate.Entity == entity);
			return ExpressionChecks.FailuresIn(rule, new XmlNode[] { document.DocumentElement });
		}
	}
}
