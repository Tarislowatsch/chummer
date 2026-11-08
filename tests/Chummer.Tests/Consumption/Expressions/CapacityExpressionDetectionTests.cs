using System.Linq;
using System.Xml;
using System.Xml.XPath;
using Chummer.Tests.Kernel;
using Xunit;

namespace Chummer.Tests.Consumption.Expressions
{
	public class CapacityExpressionDetectionTests
	{
		// - the calculator returns "1.5" happily, CapacityRemaining's Convert.ToInt32 throws
		[Fact]
		public void AFractionalCapacityFailsInItsConsumer()
		{
			ExpressionChecks.ExpressionFailure failure = Assert.Single(Failures("Cyberware",
				"<cyberware><name>C</name><rating>2</rating><capacity>Rating * 1.5</capacity></cyberware>"));

			Assert.Equal(1, failure.Rating);
			Assert.Equal("FormatException", failure.Error);
		}

		[Fact]
		public void AFractionalArmorModCapacityFailsAsTheArmorsChild()
		{
			Assert.Equal("FormatException", Assert.Single(Failures("ArmorMod",
				"<mod><name>M</name><maxrating>1</maxrating><armorcapacity>[Rating * 0.5]</armorcapacity></mod>")).Error);
		}

		[Fact]
		public void AFailureInTheSlashBranchIsSilent()
		{
			Assert.Throws<ExpressionChecks.SilentlyDiscarded>(
				() => CapacityExpressions.CalculatedGearCapacity("FixedValues(1,2)/[1]", 3));
		}

		[Fact]
		public void TheGearDialogSwallowsWhatTheCyberwareDialogThrows()
		{
			Assert.Throws<ExpressionChecks.SilentlyDiscarded>(() => CapacityExpressions.SelectGear("Rating +", 1));
			Assert.Throws<XPathException>(() => CapacityExpressions.SelectCyberware("Rating +", 1));
		}

		[Fact]
		public void ADecimalCommaInFixedValuesIsMisaligned()
		{
			Assert.StartsWith("MisalignedFixedValues", Assert.Single(Failures("Cyberware",
				"<cyberware><name>C</name><rating>1</rating><capacity>FixedValues(0,5)</capacity></cyberware>")).Error);
		}

		// - maxrating 0 means "up to RES": the overflow shows only at a high rating
		[Theory]
		[InlineData("0", 1)]
		[InlineData("6", 0)]
		public void AProgramWithoutAMaximumIsReplayedUpToTheLimit(string maxRating, int failures)
		{
			Assert.Equal(failures, Failures("TechProgram", "<program><name>P</name><maxrating>" + maxRating
				+ "</maxrating><capacity>Rating * 30000000</capacity></program>").Count);
		}

		[Fact]
		public void GearWithoutTheFieldIsSkipped()
		{
			Assert.Empty(Failures("Gear", "<gear><name>G</name><rating>1</rating></gear>"));
		}

		// - the first half keeps its brackets unevaluated: the parent read throws
		[Fact]
		public void ABracketedFirstHalfIsWrappedUnevaluated()
		{
			Assert.Equal("[Rating]/[2]", CapacityExpressions.CalculatedCyberwareCapacity("[Rating]/[2]", 1));
			Assert.Throws<System.FormatException>(() => CapacityExpressions.CyberwareParent("[Rating]/[2]", "[Rating]/[2]"));
		}

		// - Cyberware reads the first bracket as the child's cost, Armor and Gear the second half
		[Fact]
		public void CyberwareAndGearReadADoubleCapacityDifferently()
		{
			Assert.Equal(3, CapacityExpressions.CyberwareChild("[3]/[1]"));
			Assert.Equal(1, CapacityExpressions.GearChild("[3]/[1]"));
			Assert.Equal(1, CapacityExpressions.ArmorChild("[3]/[1]"));
		}

		[Fact]
		public void TheGearRatingBranchFloorsToAtLeastOne()
		{
			Assert.Equal("1", CapacityExpressions.CalculatedGearCapacity("Rating * 0.5", 1));
			Assert.Equal("[2]", CapacityExpressions.CalculatedGearCapacity("[Rating * 0.5]", 5));
		}

		[Fact]
		public void ATechProgramRoundsUp()
		{
			Assert.Equal(2, CapacityExpressions.CalculatedTechProgramCapacity("Rating/2", 3));
		}

		private static System.Collections.Generic.IReadOnlyList<ExpressionChecks.ExpressionFailure> Failures(
			string entity, string itemXml)
		{
			XmlDocument document = Xml.Parse(itemXml);
			return ExpressionChecks.FailuresIn(
				CapacityExpressions.Rules.First(rule => rule.Entity == entity), new XmlNode[] { document.DocumentElement });
		}
	}
}
