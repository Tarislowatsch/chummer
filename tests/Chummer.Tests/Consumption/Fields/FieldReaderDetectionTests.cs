using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using Chummer.Tests.Kernel;
using Xunit;

namespace Chummer.Tests.Consumption.Fields
{
	public class FieldReaderDetectionTests
	{
		[Fact]
		public void AnXPathLiteralSpellsEveryStepAndTheFilteredField()
		{
			Assert.Superset(new[] { "chummer", "mods", "mod", "name" }.ToHashSet(),
				FieldReaders.IdentifiersIn(new[] { "/chummer/mods/mod[name = \"x\"]" }).ToHashSet());
		}

		[Fact]
		public void ANameSpelledByNoLiteralIsUnread()
		{
			XmlDocument document = Xml.Parse("<chummer><mods><mod><name>A</name><bodymin>2</bodymin></mod>"
				+ "<mod><name>B</name><bodymin>3</bodymin></mod></mods></chummer>");

			FieldReaders.ElementName unread = Assert.Single(FieldReaders.UnreadNames(
				FieldReaders.ElementNamesIn("vehicles.xml", document),
				FieldReaders.IdentifiersIn(new[] { "/chummer/mods/mod", "name" })));

			Assert.Equal("vehicles.xml bodymin", unread.Entry);
			Assert.Equal(2, unread.Count);
			Assert.Equal("mods/mod/bodymin", unread.FirstPath);
		}

		[Fact]
		public void RequirementUsesKeepGroupDirectChildAndBareText()
		{
			XmlDocument document = Xml.Parse("<chummer><spells><spell><name>S</name><required>"
				+ "<allof><metamagic>Geomancy</metamagic><!-- note --></allof>"
				+ "<bodymin>2</bodymin>loose</required></spell></spells></chummer>");

			Assert.Equal(
				new[]
				{
					"spells.xml | S | required/allof/metamagic",
					"spells.xml | S | required/bodymin",
					"spells.xml | S | required/" + FieldReaders.TextTag,
				},
				FieldReaders.RequirementUsesIn("spells.xml", "spells.xml", document).Select(use => use.Entry));
		}

		// - the gap the name level cannot see: the same tag under the same group of another file
		[Fact]
		public void ATagReadForAnotherFileStaysUnread()
		{
			XmlDocument document = Xml.Parse("<chummer><metamagics><metamagic><name>M</name><required>"
				+ "<oneof><quality>Q</quality></oneof></required></metamagic></metamagics></chummer>");
			FieldReaders.RequirementReader powers = new FieldReaders.RequirementReader(
				"powers.xml", "powers/power", "required/oneof", "frmSelectPower.cs:1", FieldReaders.ReadStyle.PathLiterals);

			FieldReaders.RequirementUse unread = Assert.Single(FieldReaders.UnreadRequirements(
				FieldReaders.RequirementUsesIn("metamagic.xml", "metamagic.xml", document),
				new[] { powers }, reader => new[] { "quality" }));

			Assert.Equal("metamagic.xml | M | required/oneof/quality", unread.Entry);
		}

		[Fact]
		public void APackUseMatchesTheReaderOfItsBaseFile()
		{
			XmlDocument document = Xml.Parse("<chummer><powers><power><name>P</name><required>"
				+ "<oneof><quality>Q</quality></oneof></required></power></powers></chummer>");
			FieldReaders.RequirementReader powers = new FieldReaders.RequirementReader(
				"powers.xml", "powers/power", "required/oneof", "frmSelectPower.cs:1", FieldReaders.ReadStyle.PathLiterals);

			Assert.Empty(FieldReaders.UnreadRequirements(
				FieldReaders.RequirementUsesIn("custom content/pack/custom_powers.xml", "powers.xml", document),
				new[] { powers }, reader => new[] { "quality" }));
		}

		[Fact]
		public void PathLiteralsYieldTheStepAfterTheGroupOnly()
		{
			FieldReaders.RequirementReader reader = Reader("required/allof", FieldReaders.ReadStyle.PathLiterals);

			Assert.Equal(new[] { "metatype", "quality" }, FieldReaders.TagsReadBy(reader, SourceLines.Of(new[]
			{
				"a.SelectNodes(\"required/allof/quality\");",
				"a.SelectNodes(\"required/allof/metatype[. = \\\"\" + x + \"\\\"]\");",
				"a.SelectNodes(\"required/oneof/echo\");",
				"// a.SelectNodes(\"required/allof/power\");",
			})).OrderBy(tag => tag));
		}

		[Fact]
		public void NameDispatchReadsOnlyTheLoopAfterTheGroupLiteral()
		{
			FieldReaders.RequirementReader reader = Reader("required/oneof", FieldReaders.ReadStyle.NameDispatch);

			Assert.Equal(new[] { "ess", "quality" }, FieldReaders.TagsReadBy(reader, SourceLines.Of(new[]
			{
				"list = node.SelectNodes(\"required/oneof\");",
				"foreach (XmlNode group in list)",
				"{",
				"	foreach (XmlNode child in group.ChildNodes)",
				"	{",
				"		if (child.Name == \"quality\") { }",
				"		else if (child.Name == \"ess\") { }",
				"		// else if (child.Name == \"skill\") { }",
				"	}",
				"}",
				"list = node.SelectNodes(\"required/allof\");",
				"foreach (XmlNode group in list)",
				"{",
				"	if (group.Name == \"metatype\") { }",
				"}",
			})).OrderBy(tag => tag));
		}

		// - a longer literal like "required/oneof[contains(...)]" is no group selection
		[Theory]
		[InlineData("x = \"required/oneof[contains(., 'A')]\";")]
		[InlineData("x = \"required/oneof\"; y = \"required/oneof\";")]
		public void NameDispatchNeedsExactlyOneGroupLiteral(string line)
		{
			FieldReaders.RequirementReader reader = Reader("required/oneof", FieldReaders.ReadStyle.NameDispatch);

			Assert.Throws<System.InvalidOperationException>(() => FieldReaders.TagsReadBy(reader, SourceLines.Of(new[] { line })));
		}

		[Theory]
		[InlineData("required/oneof", "required/oneof")]
		[InlineData("required/allof/metatype[. = \"", "required/allof/metatype")]
		[InlineData(" and (required/oneof[contains(., 'A')])", "required/oneof")]
		[InlineData("forbidden/oneof", "forbidden/oneof")]
		[InlineData("required/bodymin", "required/bodymin")]
		[InlineData("required/allofx", "required/allofx")]
		[InlineData("required", "required")]
		[InlineData("<required>", "required")]
		[InlineData("required/oneof/a or forbidden", "required/oneof/a forbidden")]
		[InlineData("notrequired/oneof", "")]
		[InlineData("requiredness", "")]
		public void RequirementMentionsKeepBlockGroupAndTag(string literal, string mentions)
		{
			Assert.Equal(mentions, string.Join(" ",
				FieldReaders.RequirementMentionsIn(literal).Select(mention => mention.Display)));
		}

		[Fact]
		public void ADirectChildIsItsBlocksGroupWithATag()
		{
			FieldReaders.RequirementMention mention = Assert.Single(FieldReaders.RequirementMentionsIn("required/bodymin"));

			Assert.Equal("required", mention.Group);
			Assert.Equal("bodymin", mention.Tag);
		}

		[Fact]
		public void ARequirementLiteralNeedsAReaderForItsOwnGroup()
		{
			NameLookups.Literal[] literals =
			{
				new NameLookups.Literal("frmSelectMetamagic.cs:10", "required/allof/metamagic"),
				new NameLookups.Literal("frmSelectMetamagic.cs:11", "<required>"),
				new NameLookups.Literal("frmSelectMetamagic.cs:12", "forbidden"),
				new NameLookups.Literal("frmSelectMetamagic.cs:20", "required/oneof/metamagic"),
				new NameLookups.Literal("frmSelectSpell.cs:30", "required/allof/metamagic"),
				new NameLookups.Literal("frmSelectSpell.cs:31", "required"),
				new NameLookups.Literal("frmCreate.cs:40", "required/oneof/metatype"),
				new NameLookups.Literal("frmCreate.cs:41", "required/oneof/metamagic"),
				new NameLookups.Literal("clsLog.cs:50", "field required"),
				new NameLookups.Literal("clsLog.cs:51", "required/allof/x"),
			};
			FieldReaders.RequirementReader metamagic = new FieldReaders.RequirementReader("metamagic.xml",
				"metamagics/metamagic", "required/allof", "frmSelectMetamagic.cs:10", FieldReaders.ReadStyle.PathLiterals);

			Assert.Equal(
				new[] { "frmSelectMetamagic.cs:12", "frmSelectMetamagic.cs:20", "frmSelectSpell.cs:30",
					"frmSelectSpell.cs:31", "frmCreate.cs:41", "clsLog.cs:51" },
				FieldReaders.UnlistedRequirementLiterals(literals, new[] { metamagic },
						new Dictionary<string, string>
						{
							{ "frmCreate.cs required/oneof/metatype", "why" },
							{ "clsLog.cs required", "why" },
						})
					.Select(literal => literal.Site));
		}

		[Theory]
		[InlineData("frmSelectQuality.cs:529", true)]
		[InlineData("frmSelectQuality.cs:530", false)]
		public void AConsumerLineMustHoldTheGroupLiteral(string consumer, bool expected)
		{
			NameLookups.Literal[] literals =
			{
				new NameLookups.Literal("frmSelectQuality.cs:529", "required/oneof"),
				new NameLookups.Literal("frmSelectQuality.cs:530", "required/oneofx"),
			};
			FieldReaders.RequirementReader reader = new FieldReaders.RequirementReader("qualities.xml",
				"qualities/quality", "required/oneof", consumer, FieldReaders.ReadStyle.NameDispatch);

			Assert.Equal(expected, FieldReaders.ConsumerLineSelectsGroup(reader, literals));
		}

		[Fact]
		public void HandlerFieldsKeepTheirReachFromTheConsumersView()
		{
			XmlDocument document = Xml.Parse("<chummer><qualities><quality><bonus>"
				+ "<addattribute><name>MAG</name><min>1</min></addattribute>"
				+ "<addattribute><name>RES</name>loose</addattribute>"
				+ "</bonus></quality><quality><bonus><specificskill><bonus>"
				+ "<addattribute><name>MAG</name></addattribute>"
				+ "</bonus></specificskill></bonus></quality></qualities></chummer>");

			Assert.Equal(
				new[]
				{
					"q.xml addattribute/name",
					"q.xml addattribute/min",
					"q.xml addattribute/name (LaterSibling)",
					"q.xml addattribute/" + FieldReaders.TextTag + " (LaterSibling)",
					"q.xml addattribute/name (OutsideRoot)",
				},
				FieldReaders.HandlerFieldsIn("q.xml", "qualities.xml", document, new[] { "addattribute" })
					.Select(field => field.Entry));
		}

		[Fact]
		public void HandlerFieldsMarkRepeatsAndCarryTheBlockAttributes()
		{
			XmlDocument document = Xml.Parse("<chummer><qualities><quality><bonus>"
				+ "<selectskill skillgroup=\"Influence\"><val>1</val><val>2</val>text</selectskill>"
				+ "</bonus></quality></qualities></chummer>");

			FieldReaders.HandlerField[] fields =
				FieldReaders.HandlerFieldsIn("q.xml", "qualities.xml", document, new[] { "selectskill" }).ToArray();

			Assert.Equal(
				new[]
				{
					"q.xml selectskill/@skillgroup",
					"q.xml selectskill/val",
					"q.xml selectskill/val (repeated)",
					"q.xml selectskill/" + FieldReaders.TextTag,
				},
				fields.Select(field => field.Entry));
			Assert.Equal("Influence", fields[0].Value);
			Assert.All(fields, field => Assert.Equal(new[] { "@skillgroup" }, field.BlockAttributes));
		}

		// - vehicles.xml mods/mod/bonus goes to Vehicle, never to CreateImprovements
		[Fact]
		public void AHandlerBlockOnAVehicleModIsOutsideTheRoots()
		{
			XmlDocument document = Xml.Parse("<chummer><mods><mod><bonus>"
				+ "<addattribute><name>MAG</name></addattribute></bonus></mod></mods></chummer>");

			Assert.Equal("v.xml addattribute/name (OutsideRoot)", Assert.Single(
				FieldReaders.HandlerFieldsIn("v.xml", "vehicles.xml", document, new[] { "addattribute" })).Entry);
		}

		private static readonly string[] HandlerSource =
		{
			"public bool CreateImprovements(Improvement.ImprovementSource objSource, string s, XmlNode nodBonus, bool b)",
			"{",
			"	if (NodeExists(nodBonus, \"addattribute\"))",
			"	{",
			"		if (nodBonus[\"addattribute\"][\"name\"].InnerText == \"MAG\") { }",
			"		// x = nodBonus[\"addattribute\"][\"max\"];",
			"		y = nodBonus.SelectSingleNode(\"addattribute/val\");",
			"	}",
			"	z = nodBonus[\"addattribute\"][\"min\"];",
			"}",
		};

		[Fact]
		public void ABranchReadsOnlyTheIndexedFieldsInsideIt()
		{
			FieldReaders.HandlerBranch branch = FieldReaders.BranchOf("addattribute", "x.cs", SourceLines.Of(HandlerSource));

			Assert.Equal(new[] { "name" }, branch.Fields);
			Assert.Equal(new[] { "x.cs:3", "x.cs:5", "x.cs:5", "x.cs:5", "x.cs:7" },
				branch.Literals.Select(literal => literal.Site));
		}

		[Fact]
		public void ABranchReadsAFieldWhetherBlockOrFieldIsSelectedOrIndexed()
		{
			string[] source =
			{
				"public bool CreateImprovements(Improvement.ImprovementSource objSource, string s, XmlNode nodBonus, bool b)",
				"{",
				"	if (NodeExists(nodBonus, \"conditionmonitor\"))",
				"	{",
				"		a = nodBonus.SelectSingleNode(\"conditionmonitor\")[\"physical\"];",
				"		b = nodBonus[\"conditionmonitor\"].SelectNodes(\"attribute\");",
				"		c = nodBonus.SelectSingleNode(\"conditionmonitor\").SelectSingleNode(\"stun\");",
				"		d = nodBonus[\"conditionmonitor\"].SelectSingleNode(\"over/flow\");",
				"		if (nodBonus.SelectSingleNode(\"conditionmonitor\")[\"mode\"].InnerText == \"x\") { }",
				"	}",
				"}",
			};

			FieldReaders.HandlerBranch branch = FieldReaders.BranchOf("conditionmonitor", "x.cs", SourceLines.Of(source));

			Assert.Equal(new[] { "attribute", "mode", "physical", "stun" },
				branch.Fields.OrderBy(field => field, StringComparer.Ordinal));
			Assert.Equal(new[] { "x" }, Assert.Single(branch.Dispatch, entry => entry.Key == "mode").Value);
		}

		private static readonly string[] ChainSource =
		{
			"public bool CreateImprovements(Improvement.ImprovementSource objSource, string s, XmlNode nodBonus, bool b)",
			"{",
			"	if (NodeExists(nodBonus, \"selectskill\"))",
			"	{",
			"		if (nodBonus.SelectSingleNode(\"selectskill\").OuterXml.Contains(\"skillgroup\"))",
			"			a = nodBonus.SelectSingleNode(\"selectskill\").Attributes[\"skillgroup\"].InnerText;",
			"		else if (nodBonus.SelectSingleNode(\"selectskill\").OuterXml.Contains(\"skillcategory\"))",
			"			b = nodBonus.SelectSingleNode(\"selectskill\").Attributes[\"skillcategory\"].InnerText;",
			"		if (nodBonus[\"selectskill\"].Attributes[\"limit\"] != null)",
			"			c = nodBonus[\"selectskill\"].Attributes[\"limit\"].InnerText;",
			"		foreach (XmlNode x in nodBonus[\"selectskill\"].SelectNodes(\"val\")) { }",
			"		d = nodBonus[\"selectskill\"][\"max\"];",
			"		e = nodBonus[\"selectskill\"].SelectSingleNode(\"min\");",
			"	}",
			"}",
		};

		[Fact]
		public void ABranchRecordsAttributesTheirChainsAndWhatReadsEveryCopy()
		{
			FieldReaders.HandlerBranch branch = FieldReaders.BranchOf("selectskill", "x.cs", SourceLines.Of(ChainSource));

			Assert.Equal(new[] { "@limit", "@skillcategory", "@skillgroup", "max", "min", "val" },
				branch.Fields.OrderBy(field => field, StringComparer.Ordinal));
			Assert.Equal(new[] { "val" }, branch.EveryCopy);
			Assert.Equal(new[] { "@skillgroup [text skillgroup]", "@skillcategory [text skillcategory]" },
				Assert.Single(branch.Chains).Select(link => link.ToString()));
			Assert.Empty(branch.UnlinkedTextTests);
		}

		[Fact]
		public void ABracedBodyIsALinkAndAStrayTextTestIsUnlinked()
		{
			string[] source =
			{
				"public bool CreateImprovements(Improvement.ImprovementSource objSource, string s, XmlNode nodBonus, bool b)",
				"{",
				"	if (NodeExists(nodBonus, \"selectskill\"))",
				"	{",
				"		if (nodBonus[\"selectskill\"].Attributes[\"skillgroup\"] != null)",
				"		{",
				"			a = nodBonus[\"selectskill\"].Attributes[\"skillgroup\"].InnerText;",
				"		}",
				"		else if (nodBonus[\"selectskill\"].OuterXml.Contains(\"limittoskill\"))",
				"		{",
				"			b = nodBonus[\"selectskill\"].Attributes[\"limittoskill\"].InnerText;",
				"			c = 1;",
				"		}",
				"		d = nodBonus[\"selectskill\"].OuterXml.Contains(\"val\");",
				"	}",
				"}",
			};

			FieldReaders.HandlerBranch branch = FieldReaders.BranchOf("selectskill", "x.cs", SourceLines.Of(source));

			Assert.Equal(new[] { "@skillgroup", "@limittoskill [text limittoskill]" },
				Assert.Single(branch.Chains).Select(link => link.ToString()));
			Assert.Equal(new[] { "x.cs:14" }, branch.UnlinkedTextTests);
		}

		// - the block text names each attribute present, as an OuterXml would
		private static FieldReaders.HandlerField SkillField(string file, string tag, bool repeated = false,
			params string[] attributes)
		{
			return new FieldReaders.HandlerField(file, "selectskill", FieldReaders.BlockReach.First, tag, "x",
				repeated, attributes, string.Join(" ", attributes));
		}

		// - the code tests OuterXml: a value naming an earlier link picks that link
		[Fact]
		public void TextNamingAnEarlierLinkPicksItAndCrashes()
		{
			FieldReaders.HandlerBranch branch = FieldReaders.BranchOf("selectskill", "x.cs", SourceLines.Of(ChainSource));
			string text = "<selectskill skillcategory=\"no skillgroup here\" />";
			FieldReaders.HandlerField[] fields = new[] { 1, 2 }
				.Select(number => new FieldReaders.HandlerField("q.xml", "selectskill", FieldReaders.BlockReach.First,
					"@skillcategory", "no skillgroup here", false, new[] { "@skillcategory" }, text, number))
				.ToArray();

			Assert.Equal(new[] { "q.xml", "q.xml" },
				FieldReaders.UnreadHandlerFields(fields, new[] { branch }).Select(entry => entry.File));
			Assert.Equal(
				new[]
				{
					"q.xml selectskill #1 picks @skillgroup [text skillgroup]: " + text,
					"q.xml selectskill #2 picks @skillgroup [text skillgroup]: " + text,
				},
				FieldReaders.ChainsPickingAnAbsentAttribute(fields, new[] { branch }));
		}

		[Fact]
		public void HandlerFieldsNumberTheirBlocks()
		{
			XmlDocument document = Xml.Parse("<chummer><qualities><quality><bonus><selectskill><val>1</val></selectskill>"
				+ "</bonus></quality><quality><bonus><selectskill><val>1</val></selectskill></bonus></quality></qualities></chummer>");

			Assert.Equal(new[] { 1, 2 }, FieldReaders.HandlerFieldsIn("q.xml", "qualities.xml", document,
				new[] { "selectskill" }).Select(field => field.BlockNumber));
		}

		[Fact]
		public void AnIfInsideALinkBodyKeepsTheOuterChainWhole()
		{
			string[] source =
			{
				"public bool CreateImprovements(Improvement.ImprovementSource objSource, string s, XmlNode nodBonus, bool b)",
				"{",
				"	if (NodeExists(nodBonus, \"selectskill\"))",
				"	{",
				"		if (nodBonus[\"selectskill\"].Attributes[\"skillgroup\"] != null)",
				"		{",
				"			if (nodBonus[\"selectskill\"].Attributes[\"skillgroup\"] != null)",
				"				a = nodBonus[\"selectskill\"].Attributes[\"skillgroup\"].InnerText;",
				"		}",
				"		else if (nodBonus[\"selectskill\"].Attributes[\"limittoskill\"] != null)",
				"			b = nodBonus[\"selectskill\"].Attributes[\"limittoskill\"].InnerText;",
				"	}",
				"}",
			};

			FieldReaders.HandlerBranch branch = FieldReaders.BranchOf("selectskill", "x.cs", SourceLines.Of(source));

			Assert.Equal(new[] { "@skillgroup", "@limittoskill" },
				Assert.Single(branch.Chains).Select(link => link.ToString()));
		}

		[Fact]
		public void TextTestSitesFindsEveryOuterXmlLine()
		{
			Assert.Equal(new[] { "x.cs:2" }, FieldReaders.TextTestSitesIn("x.cs", SourceLines.Of(new[]
			{
				"// a = n.OuterXml.Contains(\"x\");",
				"b = n[\"y\"].OuterXml.Contains(\"z\");",
			})));
		}

		[Fact]
		public void AChainPickingAPresentAttributeIsNoCrash()
		{
			FieldReaders.HandlerBranch branch = FieldReaders.BranchOf("selectskill", "x.cs", SourceLines.Of(ChainSource));

			Assert.Empty(FieldReaders.ChainsPickingAnAbsentAttribute(
				new[] { SkillField("a", "@skillcategory", false, "@skillcategory"), SkillField("b", "val") },
				new[] { branch }));
		}

		[Fact]
		public void ARepeatIsReadOnlyBySelectNodes()
		{
			FieldReaders.HandlerBranch branch = FieldReaders.BranchOf("selectskill", "x.cs", SourceLines.Of(ChainSource));
			FieldReaders.HandlerField[] fields =
			{
				SkillField("a", "val", true),
				SkillField("b", "max"),
				SkillField("c", "max", true),
				SkillField("d", "min", true),
			};

			Assert.Equal(new[] { "c", "d" }, FieldReaders.UnreadHandlerFields(fields, new[] { branch }).Select(field => field.File));
		}

		[Fact]
		public void AnAttributeBehindAnEarlierPresentLinkIsUnread()
		{
			FieldReaders.HandlerBranch branch = FieldReaders.BranchOf("selectskill", "x.cs", SourceLines.Of(ChainSource));
			FieldReaders.HandlerField[] fields =
			{
				SkillField("a", "@skillcategory", false, "@skillcategory"),
				SkillField("b", "@skillgroup", false, "@skillgroup", "@skillcategory"),
				SkillField("c", "@skillcategory", false, "@skillgroup", "@skillcategory"),
				SkillField("d", "@limit", false, "@skillgroup", "@limit"),
				SkillField("e", "@other", false, "@other"),
			};

			Assert.Equal(new[] { "c", "e" }, FieldReaders.UnreadHandlerFields(fields, new[] { branch }).Select(field => field.File));
		}

		[Fact]
		public void AnAttributeLiteralCountsAsExtractedWithoutItsPrefix()
		{
			FieldReaders.HandlerBranch branch = FieldReaders.BranchOf("selectskill", "x.cs", SourceLines.Of(ChainSource));

			Assert.Empty(FieldReaders.UnextractedFields(branch, new[] { "@skillgroup", "@skillcategory", "@limit" }));
		}

		[Fact]
		public void AFieldLiteralTheIndexerMissesIsUnextracted()
		{
			FieldReaders.HandlerBranch branch = FieldReaders.BranchOf("addattribute", "x.cs", SourceLines.Of(HandlerSource));

			Assert.Equal(new[] { "val" },
				FieldReaders.UnextractedFields(branch, new[] { "name", "min", "max", "val" }));
		}

		[Fact]
		public void AHandlerLiteralOutsideTheBranchIsReported()
		{
			FieldReaders.HandlerBranch branch = FieldReaders.BranchOf("addattribute", "x.cs", SourceLines.Of(HandlerSource));
			NameLookups.Literal[] literals = NameLookups.LiteralsIn("x.cs", SourceLines.Of(HandlerSource))
				.Concat(new[] { new NameLookups.Literal("y.cs:1", "bonus/addattribute/val") })
				.ToArray();

			Assert.Equal(new[] { "x.cs:9", "y.cs:1" },
				FieldReaders.HandlerLiteralsOutsideBranch(literals, branch).Select(literal => literal.Site).Distinct());
		}

		[Fact]
		public void AFieldReadOnlyByALaterBlockOrOutsideTheRootsIsUnread()
		{
			FieldReaders.HandlerField[] fields =
			{
				new FieldReaders.HandlerField("a", "addattribute", FieldReaders.BlockReach.First, "name", "MAG"),
				new FieldReaders.HandlerField("b", "addattribute", FieldReaders.BlockReach.First, "min", "1"),
				new FieldReaders.HandlerField("c", "addattribute", FieldReaders.BlockReach.LaterSibling, "name", "MAG"),
				new FieldReaders.HandlerField("d", "addattribute", FieldReaders.BlockReach.OutsideRoot, "name", "MAG"),
			};

			Assert.Equal(new[] { "b", "c", "d" },
				FieldReaders.UnreadHandlerFields(fields, new[] { NameBranch() }).Select(field => field.File));
		}

		[Fact]
		public void ABranchDispatchesOnTheComparedValues()
		{
			FieldReaders.HandlerBranch branch = FieldReaders.BranchOf("addattribute", "x.cs", SourceLines.Of(HandlerSource));

			Assert.Equal(new[] { "MAG" }, Assert.Single(branch.Dispatch, entry => entry.Key == "name").Value);
		}

		// - uncompared fields and unread blocks are no value findings
		[Fact]
		public void OnlyAReachedValueNoComparisonMatchesIsUnhandled()
		{
			FieldReaders.HandlerField[] fields =
			{
				new FieldReaders.HandlerField("a", "addattribute", FieldReaders.BlockReach.First, "name", "MAG"),
				new FieldReaders.HandlerField("b", "addattribute", FieldReaders.BlockReach.First, "name", "EDG"),
				new FieldReaders.HandlerField("c", "addattribute", FieldReaders.BlockReach.First, "name", " MAG"),
				new FieldReaders.HandlerField("d", "addattribute", FieldReaders.BlockReach.First, "min", "EDG"),
				new FieldReaders.HandlerField("e", "addattribute", FieldReaders.BlockReach.LaterSibling, "name", "EDG"),
			};

			Assert.Equal(new[] { "b", "c" },
				FieldReaders.UnhandledHandlerValues(fields, new[] { NameBranch() }).Select(field => field.File));
		}

		[Fact]
		public void AHandlerFieldKeepsItsValueAsTheBranchComparesIt()
		{
			XmlDocument document = Xml.Parse("<chummer><qualities><quality><bonus>"
				+ "<addattribute><name>RES</name></addattribute></bonus></quality></qualities></chummer>");

			Assert.Equal("RES", Assert.Single(
				FieldReaders.HandlerFieldsIn("q.xml", "qualities.xml", document, new[] { "addattribute" })).Value);
		}

		private static FieldReaders.HandlerBranch NameBranch()
		{
			return new FieldReaders.HandlerBranch("addattribute", new NameLookups.Literal[0], new[] { "name" },
				new Dictionary<string, IReadOnlyCollection<string>> { { "name", new[] { "MAG", "RES" } } });
		}

		[Fact]
		public void AHandlerWithoutABranchThrows()
		{
			Assert.Throws<System.InvalidOperationException>(
				() => FieldReaders.BranchOf("addskill", "x.cs", SourceLines.Of(HandlerSource)));
		}

		private static FieldReaders.RequirementReader Reader(string group, FieldReaders.ReadStyle style)
		{
			return new FieldReaders.RequirementReader("qualities.xml", "qualities/quality", group, "frmSelectQuality.cs:1", style);
		}
	}
}
