using System.Xml;
using Xunit;

namespace Chummer.Tests.Consumption.Fields
{
	public class DecimalCommaDetectionTests
	{
		[Theory]
		[InlineData("0,5")]
		[InlineData("-0,2")]
		[InlineData(" 1,5 ")]
		[InlineData("1,000")]
		[InlineData("1,000.5")]
		public void ACommaNumberIsCaught(string value)
		{
			Assert.True(DecimalCommas.ReadsAsNumber(value));
		}

		// - a FixedValues list or an expression with a comma is not a number: other checks own those
		[Theory]
		[InlineData("0.5")]
		[InlineData("FixedValues(0.2,0.3)")]
		[InlineData("Rating * 0,5")]
		[InlineData("Bonus, Grade")]
		[InlineData("")]
		public void AnythingElsePasses(string value)
		{
			Assert.False(DecimalCommas.ReadsAsNumber(value));
		}

		[Fact]
		public void BothLeavesAndAttributesAreScanned()
		{
			XmlDocument document = new XmlDocument();
			document.LoadXml(
				"<chummer><grades><grade><name>Alpha</name><ess>0,8</ess></grade></grades>"
				+ "<powers><power cost=\"0,5\"><name>Claws</name></power></powers></chummer>");

			Assert.Equal(
				new[] { "Alpha ess = '0,8'", "Claws power/@cost = '0,5'" },
				DecimalCommas.CommaNumbersIn(document));
		}
	}
}
