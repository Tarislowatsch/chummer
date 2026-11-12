using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using Chummer.Tests.Kernel;
using Xunit;

namespace Chummer.Tests.Consumption.Fields
{
	public class LoaderConversionDetectionTests
	{
		private static readonly LoaderConversions.Scope WholeFile =
			new LoaderConversions.Scope("x.cs", null, LoaderConversions.CritterRoots);

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

		private static string[] Describe(IEnumerable<LoaderConversions.Conversion> conversions)
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
				Describe(LoaderConversions.ConversionsIn(WholeFile, SourceLines.Of(LoaderSource))));
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
			Assert.Throws<InvalidOperationException>(() => LoaderConversions.ConversionsIn(WholeFile, SourceLines.Of(new[] { line })));
		}

		[Theory]
		[InlineData("a = Convert.ToInt32(objXmlMetatype[\"essmax\"].InnerText);")]
		[InlineData("a = int.Parse(objXmlMetatype[\"essmax\"].InnerText);")]
		[InlineData("a = Int32.Parse(objXmlMetatype[\"essmax\"].InnerText);")]
		public void AnIntegerConversionIsUnderstood(string line)
		{
			Assert.Equal("critters: essmax", Assert.Single(LoaderConversions.ConversionsIn(WholeFile, SourceLines.Of(new[] { line }))).Description);
		}

		// - a later rebinding of any shape must not inherit the earlier binding
		[Theory]
		[InlineData("foreach (XmlNode objXmlPower in objXmlDocument.SelectNodes(\"/chummer/powers/power\"))")]
		[InlineData("foreach (XmlNode objXmlPower in objXmlDocument[\"powers\"].ChildNodes)")]
		[InlineData("objXmlPower = objXmlDocument.SelectSingleNode(\"/chummer/powers/power\");")]
		[InlineData("XmlNode objXmlPower = objXmlDocument.SelectSingleNode(\"/chummer/powers/power\");")]
		[InlineData("dict.TryGetValue(key, out objXmlPower);")]
		[InlineData("dict.TryGetValue(key, out XmlNode objXmlPower);")]
		[InlineData("Load(ref objXmlPower);")]
		[InlineData("if (objOther is XmlNode objXmlPower) { }")]
		public void AnyRebindingDropsTheBinding(string rebinding)
		{
			string[] source =
			{
				"foreach (XmlNode objXmlPower in objXmlCritter.SelectNodes(\"powers/power\")) { }",
				rebinding,
				"	a = Convert.ToInt32(objXmlPower.Attributes[\"points\"].InnerText);",
			};

			Assert.Throws<InvalidOperationException>(() => LoaderConversions.ConversionsIn(WholeFile, SourceLines.Of(source)));
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

			Assert.Equal(guarded, Assert.Single(LoaderConversions.ConversionsIn(WholeFile, SourceLines.Of(source))).Guarded);
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

			Assert.Equal(guarded, Assert.Single(LoaderConversions.ConversionsIn(WholeFile, SourceLines.Of(source))).Guarded);
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
				Describe(LoaderConversions.ConversionsIn(WholeFile, SourceLines.Of(source))));
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

		private static readonly LoaderConversions.Scope PossessScope = new LoaderConversions.Scope("f.cs", "mnuPossess_Click",
			new Dictionary<string, LoaderConversions.Root>
			{
				{ "objSelected", new LoaderConversions.Root(LoaderConversions.Vessels, "") },
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
				Describe(LoaderConversions.ConversionsIn(PossessScope, SourceLines.Of(FormSource))));
		}

		[Fact]
		public void AMissingMethodThrows()
		{
			LoaderConversions.Scope renamed = new LoaderConversions.Scope("f.cs", "mnuGone_Click", PossessScope.Roots);

			Assert.Throws<InvalidOperationException>(() => LoaderConversions.ConversionsIn(renamed, SourceLines.Of(FormSource)));
		}

		private static readonly LoaderConversions.Scope KitScope = new LoaderConversions.Scope("k.cs", null,
			new Dictionary<string, LoaderConversions.Root> { { "objXmlKit", new LoaderConversions.Root(LoaderConversions.Kits, "") } },
			foreign: new[] { "objXmlPowerNode" });

		private static readonly string[] KitSource =
		{
			"foreach (XmlNode objXmlAttribute in objXmlKit[\"attributes\"])",
			"	a = Convert.ToInt32(objXmlAttribute.InnerText);",
			"foreach (XmlNode objXmlChild in objXmlKit.ChildNodes)",
			"	a = Convert.ToInt32(objXmlChild.InnerText);",
			"if (objXmlKit[\"selectmartialart\"].Attributes[\"rating\"] != null)",
			"	a = Convert.ToInt32(objXmlKit[\"selectmartialart\"].Attributes[\"rating\"].InnerText);",
			"foreach (XmlNode objXmlPower in objXmlKit.SelectNodes(\"powers/power\"))",
			"{",
			"	if (objXmlPower[\"rating\"] != null)",
			"		a = Convert.ToInt32(objXmlPower[\"rating\"].InnerText);",
			"	p = Convert.ToDecimal(objXmlPowerNode[\"points\"].InnerText, c);",
			"	l = Convert.ToInt32(objXmlPowerNode[\"levels\"].InnerText);",
			"}",
		};

		[Fact]
		public void KitShapesResolveToTheirPaths()
		{
			Assert.Equal(
				new[]
				{
					"k.cs:2 kits: attributes/*",
					"k.cs:4 kits: *",
					"k.cs:6 kits: selectmartialart/@rating (if present)",
					"k.cs:10 kits: powers/power/rating (if present)",
				},
				Describe(LoaderConversions.ConversionsIn(KitScope, SourceLines.Of(KitSource))));
		}

		// - only a child loop's variable is read as a whole: elsewhere ValuesIn could not say which node
		[Theory]
		[InlineData("foreach (XmlNode objXmlPower in objXmlKit.SelectNodes(\"powers/power\")) a = Convert.ToInt32(objXmlPower.InnerText);")]
		[InlineData("a = Convert.ToInt32(objXmlKit.InnerText);")]
		[InlineData("a = Convert.ToInt32(objXmlOther[\"levels\"].InnerText);")]
		public void ANodeReadAsAWholeOutsideAChildLoopThrows(string line)
		{
			Assert.Throws<InvalidOperationException>(() => LoaderConversions.ConversionsIn(KitScope, SourceLines.Of(new[] { line })));
		}

		// - an else, a "} else {" or a block closed before the conversion guards nothing
		[Theory]
		[InlineData(new[] { "if (objXmlKit[\"nuyenbp\"] != null)", "{", "	a = Convert.ToInt32(objXmlKit[\"nuyenbp\"].InnerText);", "}" }, true)]
		[InlineData(new[] { "if (objXmlKit[\"nuyenbp\"] != null) {", "	foo();", "	a = Convert.ToInt32(objXmlKit[\"nuyenbp\"].InnerText);", "}" }, true)]
		[InlineData(new[] { "if (objXmlKit[\"nuyenbp\"] != null)", "{", "	if (b)", "	{", "	}", "	a = Convert.ToInt32(objXmlKit[\"nuyenbp\"].InnerText);", "}" }, true)]
		[InlineData(new[] { "if (objXmlKit[\"nuyenbp\"] != null)", "{", "}", "a = Convert.ToInt32(objXmlKit[\"nuyenbp\"].InnerText);" }, false)]
		[InlineData(new[] { "if (objXmlKit[\"nuyenbp\"] != null)", "{", "}", "else", "{", "	a = Convert.ToInt32(objXmlKit[\"nuyenbp\"].InnerText);", "}" }, false)]
		[InlineData(new[] { "if (objXmlKit[\"nuyenbp\"] != null) {", "} else {", "	a = Convert.ToInt32(objXmlKit[\"nuyenbp\"].InnerText);", "}" }, false)]
		[InlineData(new[] { "if (objXmlKit[\"skills\"] != null)", "{", "	a = Convert.ToInt32(objXmlKit[\"nuyenbp\"].InnerText);", "}" }, false)]
		public void AnIfOpeningABlockAroundTheConversionGuardsIt(string[] source, bool guarded)
		{
			Assert.Equal(guarded, Assert.Single(LoaderConversions.ConversionsIn(KitScope, SourceLines.Of(source))).Guarded);
		}

		private static readonly string[] SwitchSource =
		{
			"private void lstKits_SelectedIndexChanged(object sender, EventArgs e)",
			"{",
			"	foreach (XmlNode objXmlItem in objXmlPack.ChildNodes)",
			"	{",
			"		switch (objXmlItem.Name)",
			"		{",
			"			case \"attributes\":",
			"				foreach (XmlNode objXmlAttribute in objXmlItem.ChildNodes)",
			"					a = Convert.ToInt32(objXmlAttribute.InnerText);",
			"				break;",
			"			case \"nuyenbp\":",
			"				a = Convert.ToInt32(objXmlItem.InnerText);",
			"				break;",
			"		}",
			"	}",
			"	a = Convert.ToInt32(objXmlItem[\"rating\"].InnerText);",
			"}",
		};

		private static LoaderConversions.Scope CaseScope(string label)
		{
			return new LoaderConversions.Scope("s.cs", "lstKits_SelectedIndexChanged",
				new Dictionary<string, LoaderConversions.Root>
				{
					{ "objXmlItem", new LoaderConversions.Root(LoaderConversions.Kits, label) },
				},
				label);
		}

		// - the label binds the root: lines of other cases and after the switch stay out
		[Fact]
		public void ACaseScopeScansOnlyItsCase()
		{
			Assert.Equal(new[] { "s.cs:9 kits: attributes/*" },
				Describe(LoaderConversions.ConversionsIn(CaseScope("attributes"), SourceLines.Of(SwitchSource))));
		}

		[Fact]
		public void AMissingCaseThrows()
		{
			Assert.Throws<InvalidOperationException>(() => LoaderConversions.ConversionsIn(CaseScope("gone"), SourceLines.Of(SwitchSource)));
		}

		// - counted only outside the scanned lines, for a read of its own loop variable
		[Fact]
		public void AnUnscannedLoopIsOneThatConvertsItsOwnVariable()
		{
			Assert.Equal(new[] { "f.cs:16 powers/power" },
				LoaderConversions.UnscannedLoops("f.cs", SourceLines.Of(FormSource), new[] { "powers/power" }, new[] { (6, 12) }));
		}

		[Theory]
		[InlineData("5", LoaderConversions.Form.Whole, null)]
		[InlineData("F", LoaderConversions.Form.Whole, "FormatException")]
		[InlineData("1.5", LoaderConversions.Form.Whole, "FormatException")]
		[InlineData(null, LoaderConversions.Form.Whole, "missing: NullReferenceException")]
		[InlineData("2D6+3", LoaderConversions.Form.DigitBeforeD6, null)]
		[InlineData("6+1D6", LoaderConversions.Form.DigitBeforeD6, null)]
		[InlineData("D6", LoaderConversions.Form.DigitBeforeD6, "nothing before D6: ArgumentOutOfRangeException")]
		[InlineData("FD6", LoaderConversions.Form.DigitBeforeD6, "FormatException")]
		public void AValueConvertsAsTheLoaderWould(string value, LoaderConversions.Form form, string expected)
		{
			Assert.Equal(expected, LoaderConversions.ErrorOf(value, form));
		}

		private const string Metatypes = "/chummer/metatypes/metatype";

		private static XmlDocument Document()
		{
			XmlDocument document = Xml.Parse("<chummer><metatypes><metatype><name>Wolf</name><essmax>6</essmax>"
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
				LoaderConversions.ValuesIn(Document(), Metatypes, "skills/knowledge/@rating", LoaderConversions.Form.Whole, true)
					.Select(found => found.Value ?? "(none)"));
			Assert.Equal(new[] { "2" },
				LoaderConversions.ValuesIn(Document(), Metatypes, "powers/power/@rating", LoaderConversions.Form.Whole, false)
					.Select(found => found.Value));
		}

		// - frmMetatype reads the digit only inside its Contains("D6") branch
		[Fact]
		public void TheDigitFormSkipsAValueWithoutD6()
		{
			Assert.Empty(LoaderConversions.ValuesIn(Document(), Metatypes, "essmax", LoaderConversions.Form.DigitBeforeD6, false));
		}

		private const string Packs = "/chummer/packs/pack";

		private static XmlDocument KitDocument()
		{
			XmlDocument document = Xml.Parse("<chummer><packs><pack><name>Kit</name>"
				+ "<attributes><bod>4</bod><!-- note --><agi>x</agi></attributes>"
				+ "<skills><skill><name>A</name><rating>3</rating><rating>9</rating></skill><skill><name>B</name></skill></skills>"
				+ "<gears><gear><rating>2</rating><gears><gear><rating>5</rating></gear></gears></gear></gears>"
				+ "</pack></packs></chummer>");
			return document;
		}

		// - the indexer reads the first child of that name, a missing one is an unguarded crash
		[Fact]
		public void AnElementIsReadAsTheIndexerReadsIt()
		{
			Assert.Equal(new[] { "3", "(none)" },
				LoaderConversions.ValuesIn(KitDocument(), Packs, "skills/skill/rating", LoaderConversions.Form.Whole, true)
					.Select(found => found.Value ?? "(none)"));
			Assert.Equal(new[] { "3" },
				LoaderConversions.ValuesIn(KitDocument(), Packs, "skills/skill/rating", LoaderConversions.Form.Whole, false)
					.Select(found => found.Value));
		}

		// - the comment too: frmSelectPACKSKit converts every child it visits
		[Fact]
		public void AChildPathReadsEveryChildNode()
		{
			Assert.Equal(new[] { "4", " note ", "x" },
				LoaderConversions.ValuesIn(KitDocument(), Packs, "attributes/*", LoaderConversions.Form.Whole, true)
					.Select(found => found.Value));
		}

		[Fact]
		public void ADescendantPathReachesNestedGear()
		{
			Assert.Equal(new[] { "2", "5" },
				LoaderConversions.ValuesIn(KitDocument(), Packs, ".//gears/gear/rating", LoaderConversions.Form.Whole, false)
					.Select(found => found.Value));
		}

		[Fact]
		public void AFailureNamesFileItemPathAndValue()
		{
			LoaderConversions.Conversion[] conversions =
			{
				new LoaderConversions.Conversion("x.cs:1", LoaderConversions.Critters, "skills/knowledge/@rating",
					LoaderConversions.Form.Whole, false),
				new LoaderConversions.Conversion("x.cs:2", LoaderConversions.Vessels, "powers/power/@rating",
					LoaderConversions.Form.Whole, false),
			};

			Assert.Equal(
				new[]
				{
					"m.xml: Wolf skills/knowledge/@rating = (none): missing: NullReferenceException",
					"m.xml: Wolf skills/knowledge/@rating = x: FormatException",
				},
				LoaderConversions.FailuresIn("m.xml", Document(), LoaderConversions.Critters, conversions));
		}
	}
}
