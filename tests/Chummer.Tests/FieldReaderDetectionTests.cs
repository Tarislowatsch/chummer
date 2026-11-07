using System.Linq;
using System.Xml;
using Xunit;

namespace Chummer.Tests
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
			XmlDocument document = Document("<chummer><mods><mod><name>A</name><bodymin>2</bodymin></mod>"
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
			XmlDocument document = Document("<chummer><spells><spell><name>S</name><required>"
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
			XmlDocument document = Document("<chummer><metamagics><metamagic><name>M</name><required>"
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
			XmlDocument document = Document("<chummer><powers><power><name>P</name><required>"
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

			Assert.Equal(new[] { "metatype", "quality" }, FieldReaders.TagsReadBy(reader, new[]
			{
				"a.SelectNodes(\"required/allof/quality\");",
				"a.SelectNodes(\"required/allof/metatype[. = \\\"\" + x + \"\\\"]\");",
				"a.SelectNodes(\"required/oneof/echo\");",
				"// a.SelectNodes(\"required/allof/power\");",
			}).OrderBy(tag => tag));
		}

		[Fact]
		public void NameDispatchReadsOnlyTheLoopAfterTheGroupLiteral()
		{
			FieldReaders.RequirementReader reader = Reader("required/oneof", FieldReaders.ReadStyle.NameDispatch);

			Assert.Equal(new[] { "ess", "quality" }, FieldReaders.TagsReadBy(reader, new[]
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
			}).OrderBy(tag => tag));
		}

		// - a longer literal like "required/oneof[contains(...)]" is no group selection
		[Theory]
		[InlineData("x = \"required/oneof[contains(., 'A')]\";")]
		[InlineData("x = \"required/oneof\"; y = \"required/oneof\";")]
		public void NameDispatchNeedsExactlyOneGroupLiteral(string line)
		{
			FieldReaders.RequirementReader reader = Reader("required/oneof", FieldReaders.ReadStyle.NameDispatch);

			Assert.Throws<System.InvalidOperationException>(() => FieldReaders.TagsReadBy(reader, new[] { line }));
		}

		[Theory]
		[InlineData("required/oneof", "required/oneof")]
		[InlineData("required/allof/metatype[. = \"", "required/allof")]
		[InlineData(" and (required/oneof[contains(., 'A')])", "required/oneof")]
		[InlineData("forbidden/oneof", "forbidden/oneof")]
		[InlineData("required/bodymin", "required")]
		[InlineData("required/allofx", "required")]
		[InlineData("required", null)]
		[InlineData("notrequired/oneof", null)]
		public void RequirementGroupOfFindsTheGroupStep(string literal, string group)
		{
			Assert.Equal(group, FieldReaders.RequirementGroupOf(literal));
		}

		// - frmSelectMetamagic is listed for allof: a new oneof read there must still fail
		[Fact]
		public void ARequirementLiteralNeedsAReaderForItsOwnGroup()
		{
			NameLookups.Literal[] literals =
			{
				new NameLookups.Literal("frmSelectMetamagic.cs:10", "required/allof/metamagic"),
				new NameLookups.Literal("frmSelectMetamagic.cs:20", "required/oneof/metamagic"),
				new NameLookups.Literal("frmSelectSpell.cs:30", "required/allof/metamagic"),
				new NameLookups.Literal("frmCreate.cs:40", "required/oneof/metatype"),
				new NameLookups.Literal("frmSelectSpell.cs:50", "required"),
			};
			FieldReaders.RequirementReader metamagic = new FieldReaders.RequirementReader("metamagic.xml",
				"metamagics/metamagic", "required/allof", "frmSelectMetamagic.cs:10", FieldReaders.ReadStyle.PathLiterals);

			Assert.Equal(new[] { "frmSelectMetamagic.cs:20", "frmSelectSpell.cs:30" },
				FieldReaders.UnlistedRequirementLiterals(literals, new[] { metamagic },
						new System.Collections.Generic.Dictionary<string, string> { { "frmCreate.cs required/oneof", "why" } })
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

		private static FieldReaders.RequirementReader Reader(string group, FieldReaders.ReadStyle style)
		{
			return new FieldReaders.RequirementReader("qualities.xml", "qualities/quality", group, "frmSelectQuality.cs:1", style);
		}

		private static XmlDocument Document(string xml)
		{
			XmlDocument document = new XmlDocument();
			document.LoadXml(xml);
			return document;
		}
	}
}
