using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using Xunit;

namespace Chummer.Tests
{
	public class CritterConversionDetectionTests
	{
		private static readonly CritterConversions.Scope WholeFile =
			new CritterConversions.Scope("x.cs", null, CritterConversions.CritterRoots);

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
			"int intPos = objXmlMetatype[\"essmax\"].InnerText.IndexOf(\"D6\") - 1;",
			"x = Convert.ToInt32(objXmlMetatype[\"essmax\"].InnerText.Substring(intPos, 1)) * 6;",
			"y = Convert.ToInt32(ExpressionToString(objXmlSkill.Attributes[\"rating\"].InnerText, Convert.ToInt32(nudForce.Value), 0));",
			"// z = Convert.ToInt32(objXmlFoo.InnerText);",
		};

		private static string[] Describe(IEnumerable<CritterConversions.Conversion> conversions)
		{
			return conversions.Select(conversion => conversion.Site + " " + conversion.Description).ToArray();
		}

		[Fact]
		public void EachConversionResolvesToItsPathBelowTheItem()
		{
			Assert.Equal(
				new[]
				{
					"x.cs:4 critters: powers/power/@rating (if present)",
					"x.cs:7 critters: metavariants/metavariant/powers/power/@rating",
					"x.cs:9 critters: skills/knowledge/@rating",
					"x.cs:11 critters: essmax (digit before D6)",
				},
				Describe(CritterConversions.ConversionsIn(WholeFile, LoaderSource)));
		}

		[Theory]
		[InlineData("a = Convert.ToInt32(objXmlUnbound.Attributes[\"rating\"].InnerText);")]
		[InlineData("a = Convert.ToDecimal(objXmlMetatype[\"essmax\"].InnerText, culture);")]
		[InlineData("a = int.Parse(objXmlMetatype[\"essmax\"].InnerText.Trim());")]
		[InlineData("a = Convert.ToInt32(objXmlMetatype[\"essmax\"]")]
		[InlineData("foreach (XmlNode objXmlPower in objXmlCritter.SelectNodes(\"powers/power\")) "
			+ "a = Convert.ToInt32(objXmlPower.Attributes[\"rating\"].Value);")]
		[InlineData("a = Convert.ToInt32(objXmlMetatype[\"essmax\"].InnerText.Substring(intOther, 1));")]
		[InlineData("a = Convert.ToBoolean(objXmlMetatype[\"essmax\"].InnerText);")]
		[InlineData("a = Convert.ToDecimal(objXmlMetatype[\"essmax\"].InnerText);")]
		[InlineData("a = Convert.ToByte(objXmlMetatype[\"essmax\"].InnerText);")]
		public void AConversionOfAnUnknownShapeThrows(string line)
		{
			Assert.Throws<InvalidOperationException>(() => CritterConversions.ConversionsIn(WholeFile, new[] { line }));
		}

		[Theory]
		[InlineData("a = Convert.ToInt32(objXmlMetatype[\"essmax\"].InnerText);")]
		[InlineData("a = int.Parse(objXmlMetatype[\"essmax\"].InnerText);")]
		[InlineData("a = Int32.Parse(objXmlMetatype[\"essmax\"].InnerText);")]
		public void AnIntegerConversionIsUnderstood(string line)
		{
			Assert.Equal("critters: essmax", Assert.Single(CritterConversions.ConversionsIn(WholeFile, new[] { line })).Description);
		}

		// - a later rebinding of any shape must not inherit the earlier binding
		[Theory]
		[InlineData("foreach (XmlNode objXmlPower in objXmlDocument.SelectNodes(\"/chummer/powers/power\"))")]
		[InlineData("foreach (XmlNode objXmlPower in objXmlDocument[\"powers\"].ChildNodes)")]
		[InlineData("objXmlPower = objXmlDocument.SelectSingleNode(\"/chummer/powers/power\");")]
		[InlineData("XmlNode objXmlPower = objXmlDocument.SelectSingleNode(\"/chummer/powers/power\");")]
		public void AnyRebindingDropsTheBinding(string rebinding)
		{
			string[] source =
			{
				"foreach (XmlNode objXmlPower in objXmlCritter.SelectNodes(\"powers/power\")) { }",
				rebinding,
				"	a = Convert.ToInt32(objXmlPower.Attributes[\"points\"].InnerText);",
			};

			Assert.Throws<InvalidOperationException>(() => CritterConversions.ConversionsIn(WholeFile, source));
		}

		[Theory]
		[InlineData("	if (objXmlPower.Attributes[\"rating\"] != null)", true)]
		[InlineData("	if (objXmlPower.Attributes[\"rating\"] != null) foo();", false)]
		[InlineData("	bool b = objXmlPower.Attributes[\"rating\"] != null;", false)]
		public void OnlyAnIfOfItsOwnGuardsTheNextLine(string previous, bool guarded)
		{
			string[] source =
			{
				"foreach (XmlNode objXmlPower in objXmlCritter.SelectNodes(\"powers/power\"))",
				previous,
				"		intRating = Convert.ToInt32(objXmlPower.Attributes[\"rating\"].InnerText);",
			};

			Assert.Equal(guarded, Assert.Single(CritterConversions.ConversionsIn(WholeFile, source)).Guarded);
		}

		// - the if must directly precede the conversion: no statement may end in between
		[Theory]
		[InlineData("if (objXmlPower.Attributes[\"rating\"] != null) r = Convert.ToInt32(objXmlPower.Attributes[\"rating\"].InnerText);", true)]
		[InlineData("if (objXmlPower.Attributes[\"rating\"] != null) { r = Convert.ToInt32(objXmlPower.Attributes[\"rating\"].InnerText); }", true)]
		[InlineData("if (objXmlPower.Attributes[\"rating\"] != null) foo(); r = Convert.ToInt32(objXmlPower.Attributes[\"rating\"].InnerText);", false)]
		[InlineData("if (objXmlPower.Attributes[\"rating\"] != null) foo(); else r = Convert.ToInt32(objXmlPower.Attributes[\"rating\"].InnerText);", false)]
		[InlineData("if (objXmlPower.Attributes[\"rating\"] != null) { } r = Convert.ToInt32(objXmlPower.Attributes[\"rating\"].InnerText);", false)]
		[InlineData("r = Convert.ToInt32(objXmlPower.Attributes[\"rating\"].InnerText); if (objXmlPower.Attributes[\"rating\"] != null) { }", false)]
		public void AGuardOnTheSameLineCountsOnlyDirectlyBeforeTheConversion(string line, bool guarded)
		{
			string[] source =
			{
				"foreach (XmlNode objXmlPower in objXmlCritter.SelectNodes(\"powers/power\"))",
				"	" + line,
			};

			Assert.Equal(guarded, Assert.Single(CritterConversions.ConversionsIn(WholeFile, source)).Guarded);
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

			Assert.Equal(new[] { "x.cs:2 critters: skills/knowledge/@rating" },
				Describe(CritterConversions.ConversionsIn(WholeFile, source)));
		}

		private static readonly string[] FormSource =
		{
			"public partial class frmX",
			"{",
			"	private void cmdOther_Click(object sender, EventArgs e)",
			"	{",
			"		a = Convert.ToInt32(objXmlFoo[\"bp\"].InnerText);",
			"	}",
			"	private void mnuPossess_Click(object sender, EventArgs e)",
			"	{",
			"		int i = Convert.ToInt32(objSelected[\"bodmin\"].InnerText);",
			"		foreach (XmlNode objXmlPower in objSelected.SelectNodes(\"powers/power\"))",
			"			if (objXmlPower.Attributes[\"rating\"] != null)",
			"				r = Convert.ToInt32(objXmlPower.Attributes[\"rating\"].InnerText);",
			"	}",
			"	private void cmdKit_Click(object sender, EventArgs e)",
			"	{",
			"		foreach (XmlNode objXmlPower in objXmlKit.SelectNodes(\"powers/power\"))",
			"		{",
			"			r = Convert.ToInt32(objXmlPower[\"rating\"].InnerText);",
			"		}",
			"		foreach (XmlNode objXmlPower in objXmlKit.SelectNodes(\"powers/power\"))",
			"		{",
			"			p = Convert.ToDecimal(objXmlPowerNode[\"points\"].InnerText, c);",
			"		}",
			"	}",
			"}",
		};

		private static readonly CritterConversions.Scope PossessScope = new CritterConversions.Scope("f.cs", "mnuPossess_Click",
			new Dictionary<string, CritterConversions.Root>
			{
				{ "objSelected", new CritterConversions.Root(CritterConversions.Vessels, "") },
			});

		// - the other methods of a large form are none of this scan's business
		[Fact]
		public void AMethodScopeScansOnlyItsMethod()
		{
			Assert.Equal(
				new[]
				{
					"f.cs:9 vessels: bodmin",
					"f.cs:12 vessels: powers/power/@rating (if present)",
				},
				Describe(CritterConversions.ConversionsIn(PossessScope, FormSource)));
		}

		[Fact]
		public void AMissingMethodThrows()
		{
			CritterConversions.Scope renamed = new CritterConversions.Scope("f.cs", "mnuGone_Click", PossessScope.Roots);

			Assert.Throws<InvalidOperationException>(() => CritterConversions.ConversionsIn(renamed, FormSource));
		}

		// - counted only outside the scanned lines, for a read of its own loop variable
		[Fact]
		public void AnUnscannedLoopIsOneThatConvertsItsOwnVariable()
		{
			Assert.Equal(new[] { "f.cs:16 powers/power" },
				CritterConversions.UnscannedLoops("f.cs", FormSource, new[] { "powers/power" }, new[] { (6, 12) }));
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

		private const string Metatypes = "/chummer/metatypes/metatype";

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
				CritterConversions.ValuesIn(Document(), Metatypes, "skills/knowledge/@rating", CritterConversions.Form.Whole, true)
					.Select(found => found.Value ?? "(none)"));
			Assert.Equal(new[] { "2" },
				CritterConversions.ValuesIn(Document(), Metatypes, "powers/power/@rating", CritterConversions.Form.Whole, false)
					.Select(found => found.Value));
		}

		// - frmMetatype reads the digit only inside its Contains("D6") branch
		[Fact]
		public void TheDigitFormSkipsAValueWithoutD6()
		{
			Assert.Empty(CritterConversions.ValuesIn(Document(), Metatypes, "essmax", CritterConversions.Form.DigitBeforeD6, false));
		}

		[Fact]
		public void AFailureNamesFileItemPathAndValue()
		{
			CritterConversions.Conversion[] conversions =
			{
				new CritterConversions.Conversion("x.cs:1", CritterConversions.Critters, "skills/knowledge/@rating",
					CritterConversions.Form.Whole, false),
				new CritterConversions.Conversion("x.cs:2", CritterConversions.Vessels, "powers/power/@rating",
					CritterConversions.Form.Whole, false),
			};

			Assert.Equal(
				new[]
				{
					"m.xml: Wolf skills/knowledge/@rating = (none): missing: NullReferenceException",
					"m.xml: Wolf skills/knowledge/@rating = x: FormatException",
				},
				CritterConversions.FailuresIn("m.xml", Document(), CritterConversions.Critters, conversions));
		}
	}
}
