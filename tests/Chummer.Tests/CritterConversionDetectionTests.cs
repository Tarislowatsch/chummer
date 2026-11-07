using System;
using System.Linq;
using System.Xml;
using Xunit;

namespace Chummer.Tests
{
	public class CritterConversionDetectionTests
	{
		private static readonly string[] LoaderSource =
		{
			"foreach (XmlNode objXmlPower in objXmlCritter.SelectNodes(\"powers/power\"))",
			"{",
			"	if (objXmlPower.Attributes[\"rating\"] != null)",
			"		intRating = Convert.ToInt32(objXmlPower.Attributes[\"rating\"].InnerText);",
			"}",
			"foreach (XmlNode objXmlPower in objXmlMetavariant.SelectNodes(\"powers/power\"))",
			"	intRating = Convert.ToInt32(objXmlPower.Attributes[\"rating\"].InnerText);",
			"foreach (XmlNode objXmlSkill in objXmlCritter.SelectNodes(\"skills/knowledge\"))",
			"	if (Convert.ToInt32(objXmlSkill.Attributes[\"rating\"].InnerText) > 6) { }",
			"x = Convert.ToInt32(objXmlMetatype[\"essmax\"].InnerText.Substring(intPos, 1)) * 6;",
			"y = Convert.ToInt32(ExpressionToString(objXmlSkill.Attributes[\"rating\"].InnerText, Convert.ToInt32(nudForce.Value), 0));",
			"// z = Convert.ToInt32(objXmlFoo.InnerText);",
		};

		[Fact]
		public void EachConversionResolvesToItsPathBelowTheMetatype()
		{
			Assert.Equal(
				new[]
				{
					"x.cs:4 powers/power/@rating (if present)",
					"x.cs:7 metavariants/metavariant/powers/power/@rating",
					"x.cs:9 skills/knowledge/@rating",
					"x.cs:10 essmax (digit before D6)",
				},
				CritterConversions.ConversionsIn("x.cs", LoaderSource)
					.Select(conversion => conversion.Site + " " + conversion.Description));
		}

		[Theory]
		[InlineData("a = Convert.ToInt32(objXmlUnbound.Attributes[\"rating\"].InnerText);")]
		[InlineData("a = Convert.ToDecimal(objXmlMetatype[\"essmax\"].InnerText, culture);")]
		[InlineData("a = int.Parse(objXmlMetatype[\"essmax\"].InnerText.Trim());")]
		[InlineData("a = Convert.ToInt32(objXmlMetatype[\"essmax\"]")]
		[InlineData("foreach (XmlNode objXmlPower in objXmlCritter.SelectNodes(\"powers/power\")) "
			+ "a = Convert.ToInt32(objXmlPower.Attributes[\"rating\"].Value);")]
		public void AConversionOfAnUnknownShapeThrows(string line)
		{
			Assert.Throws<InvalidOperationException>(() => CritterConversions.ConversionsIn("x.cs", new[] { line }));
		}

		[Fact]
		public void ACallBrokenAcrossLinesStillResolves()
		{
			string[] source =
			{
				"foreach (XmlNode objXmlSkill in objXmlCritter.SelectNodes(\"skills/knowledge\"))",
				"	a = Convert.ToInt32(objXmlSkill",
				"		.Attributes[\"rating\"].InnerText);",
			};

			Assert.Equal("x.cs:2 skills/knowledge/@rating", Assert.Single(
				CritterConversions.ConversionsIn("x.cs", source).Select(conversion => conversion.Site + " " + conversion.Description)));
		}

		[Theory]
		[InlineData("5", CritterConversions.Form.Whole, null)]
		[InlineData("F", CritterConversions.Form.Whole, "FormatException")]
		[InlineData("1.5", CritterConversions.Form.Whole, "FormatException")]
		[InlineData(null, CritterConversions.Form.Whole, "missing: NullReferenceException")]
		[InlineData("2D6+3", CritterConversions.Form.DigitBeforeD6, null)]
		[InlineData("6+1D6", CritterConversions.Form.DigitBeforeD6, null)]
		[InlineData("D6", CritterConversions.Form.DigitBeforeD6, "nothing before D6: ArgumentOutOfRangeException")]
		[InlineData("FD6", CritterConversions.Form.DigitBeforeD6, "FormatException")]
		public void AValueConvertsAsTheLoaderWould(string value, CritterConversions.Form form, string expected)
		{
			Assert.Equal(expected, CritterConversions.ErrorOf(value, form));
		}

		private static XmlDocument Document()
		{
			XmlDocument document = new XmlDocument();
			document.LoadXml("<chummer><metatypes><metatype><name>Wolf</name><essmax>6</essmax>"
				+ "<skills><knowledge>Tracks</knowledge><knowledge rating=\"x\">Calls</knowledge></skills>"
				+ "<powers><power>Claws</power><power rating=\"2\">Fangs</power></powers>"
				+ "</metatype></metatypes></chummer>");
			return document;
		}

		// - a guarded attribute may be absent, an unguarded one must be there
		[Fact]
		public void OnlyAnUnguardedAttributeMustBePresent()
		{
			Assert.Equal(new[] { "(none)", "x" },
				CritterConversions.ValuesIn(Document(), "skills/knowledge/@rating", CritterConversions.Form.Whole, true)
					.Select(found => found.Value ?? "(none)"));
			Assert.Equal(new[] { "2" },
				CritterConversions.ValuesIn(Document(), "powers/power/@rating", CritterConversions.Form.Whole, false)
					.Select(found => found.Value));
		}

		// - frmMetatype reads the digit only inside its Contains("D6") branch
		[Fact]
		public void TheDigitFormSkipsAValueWithoutD6()
		{
			Assert.Empty(CritterConversions.ValuesIn(Document(), "essmax", CritterConversions.Form.DigitBeforeD6, false));
		}

		[Fact]
		public void AFailureNamesFileMetatypePathAndValue()
		{
			CritterConversions.Conversion[] conversions =
			{
				new CritterConversions.Conversion("x.cs:1", "skills/knowledge/@rating", CritterConversions.Form.Whole, false),
			};

			Assert.Equal(
				new[]
				{
					"m.xml: Wolf skills/knowledge/@rating = (none): missing: NullReferenceException",
					"m.xml: Wolf skills/knowledge/@rating = x: FormatException",
				},
				CritterConversions.FailuresIn("m.xml", Document(), conversions));
		}
	}
}
