using System.Linq;
using System.Xml;
using System.Xml.XPath;
using Chummer.Tests.Kernel;
using Xunit;

namespace Chummer.Tests.Consumption.Expressions
{
	public class CritterExpressionDetectionTests
	{
		[Theory]
		[InlineData("F+2", 3, "5")]
		[InlineData("(F*2)-1", 4, "7")]
		[InlineData("2D6-1", 12, "11")]
		[InlineData("6+1D6", 6, "12")]
		[InlineData("F-3", 1, "1")]
		[InlineData("F", 0, "0")]
		public void TodaysFormsEvaluate(string form, int force, string expected)
		{
			Assert.Equal(expected, CritterExpressions.ExpressionToString(form, force, 0));
		}

		[Fact]
		public void AnOddForceUnderADivisionFallsBackToOne()
		{
			Assert.Equal("2", CritterExpressions.ExpressionToString("F/2", 4, 0));
			Assert.Equal("silently 1 (FormatException)", CritterExpressions.ErrorOf("F/2", 3));
		}

		// - Compile sits outside the try in the original
		[Fact]
		public void AMalformedFormThrows()
		{
			Assert.Throws<XPathException>(() => CritterExpressions.ExpressionToString("F+", 3, 0));
		}

		[Fact]
		public void ArgumentsAreReadAsWrittenWithoutTheDefinition()
		{
			string[] source =
			{
				"public string ExpressionToString(string strIn, int intForce, int intOffset)",
				"x = ExpressionToString(objXmlMetatype[\"bodmin\"].InnerText, intForce, 0);",
				"// y = ExpressionToString(objXmlMetatype[\"agimin\"].InnerText, intForce, 0);",
				"z = Convert.ToInt32(ExpressionToString(objXmlGear.Attributes[\"rating\"].InnerText, f, 0));",
			};

			Assert.Equal(
				new[] { "objXmlMetatype[\"bodmin\"].InnerText", "objXmlGear.Attributes[\"rating\"].InnerText" },
				CritterExpressions.ArgumentsIn(SourceLines.Of(source)));
		}

		[Fact]
		public void AnUnknownArgumentMapsToNothing()
		{
			Assert.Equal(new[] { "bodmin" }, CritterExpressions.XPathsFor("objXmlMetatype[\"bodmin\"].InnerText"));
			Assert.Empty(CritterExpressions.XPathsFor("objXmlPower.Attributes[\"rating\"].InnerText"));
		}

		[Fact]
		public void AFailureNamesTheMetatypeTheElementAndTheLowestForce()
		{
			XmlDocument document = Xml.Parse(
				"<chummer><metatypes><metatype><name>Sprite</name><bodmin>F</bodmin>"
				+ "<complexforms><complexform rating=\"F/2\">Cascading</complexform></complexforms>"
				+ "</metatype></metatypes></chummer>");

			CritterExpressions.Failure failure = CritterExpressions
				.FailuresIn("x.xml", document, new[] { "bodmin", "complexforms/complexform/@rating" })
				.Single();

			Assert.Equal("x.xml: Sprite complexforms/complexform[Cascading]/@rating = F/2", failure.Key);
			Assert.Equal(1, failure.Force);
		}
	}
}
