using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using Xunit;

namespace Chummer.Tests
{
	// - the real data is clean: none of these near-misses occurs there to pin the rules
	public class BonusVocabularyDetectionTests
	{
		private static readonly IReadOnlyCollection<string> Known =
			new HashSet<string>(StringComparer.Ordinal) { "specificskill", "enabletab", "magician", "speed" };

		[Fact]
		public void UnknownTagIsReported()
		{
			Assert.Equal(new[] { "specificskil" },
				UnhandledIn("qualities.xml", Item("<bonus><specificskil /></bonus>")));
		}

		[Fact]
		public void KnownTagIsAccepted()
		{
			Assert.Empty(UnhandledIn("qualities.xml", Item("<bonus><specificskill /></bonus>")));
		}

		// Ordinal: NodeExists is an XPath name test, case-sensitive by definition.
		[Fact]
		public void TagDifferingOnlyInCaseIsReported()
		{
			Assert.Equal(new[] { "SpecificSkill" },
				UnhandledIn("qualities.xml", Item("<bonus><SpecificSkill /></bonus>")));
		}

		// - specificskill carries a <bonus> value field of its own
		// - an unanchored //bonus would report <val> as a top-level tag
		[Fact]
		public void BonusNestedInsideAHandlerIsNotARoot()
		{
			XmlDocument document = Item("<bonus><specificskill><bonus><val>2</val></bonus></specificskill></bonus>");

			Assert.Equal(new[] { "specificskill" }, TagsIn("qualities.xml", document));
		}

		[Fact]
		public void VehicleModBonusIsCheckedAgainstTheVehicleVocabulary()
		{
			XmlDocument document = Doc("<chummer><mods><mod><name>Gecko Tips</name><bonus><speed>2</speed></bonus></mod></mods></chummer>");

			Assert.Equal(BonusVocabulary.Consumer.VehicleMod,
				BonusVocabulary.BonusTagsIn("vehicles.xml", document).Single().Consumer);
		}

		// armor.xml has the same /chummer/mods/mod/bonus path, but ArmorMod calls CreateImprovements.
		[Fact]
		public void SamePathOutsideVehiclesXmlGoesToCreateImprovements()
		{
			XmlDocument document = Doc("<chummer><mods><mod><name>Gel Packs</name><bonus><speed>2</speed></bonus></mod></mods></chummer>");

			Assert.Equal(BonusVocabulary.Consumer.CreateImprovements,
				BonusVocabulary.BonusTagsIn("armor.xml", document).Single().Consumer);
		}

		// A bonus on the vehicle itself must not pass as a mod: nothing reads it there.
		[Fact]
		public void OtherBonusInVehiclesXmlGoesToCreateImprovements()
		{
			XmlDocument document = Doc("<chummer><vehicles><vehicle><name>Dodge Scoot</name><bonus><speed>2</speed></bonus></vehicle></vehicles></chummer>");

			Assert.Equal(BonusVocabulary.Consumer.CreateImprovements,
				BonusVocabulary.BonusTagsIn("vehicles.xml", document).Single().Consumer);
		}

		[Fact]
		public void EnableTabValuesAreCheckedToo()
		{
			Assert.Equal(new[] { "magican" },
				UnhandledIn("qualities.xml", Item("<bonus><enabletab><name>magican</name></enabletab></bonus>")));
		}

		[Fact]
		public void CustomImprovementTemplatesAreChecked()
		{
			XmlDocument document = Doc("<chummer><improvements><improvement><name>Skill</name><internal>specificskil</internal></improvement></improvements></chummer>");

			Assert.Equal(new[] { "specificskil" }, UnhandledIn("improvements.xml", document));
		}

		[Fact]
		public void TagIsAttributedToTheEntryOwningTheBonus()
		{
			Assert.Equal("Allergy", BonusVocabulary.BonusTagsIn("qualities.xml",
				Item("<bonus><specificskil /></bonus>")).Single().ItemName);
		}

		[Fact]
		public void HandledTagsComeOnlyFromCreateImprovements()
		{
			string[] source = Source(
				"public bool CreateImprovements(Improvement.ImprovementSource objSource, XmlNode nodBonus)",
				"{",
				"    if (NodeExists(nodBonus, \"specificskill\"))",
				"        Describe(\"{0}\");",
				"}",
				"public void RemoveImprovements(XmlNode nodBonus)",
				"{",
				"    if (NodeExists(nodBonus, \"elsewhere\")) { }",
				"}");

			Assert.Equal(new[] { "specificskill" }, BonusVocabulary.HandledTagsIn(source));
		}

		// A check on some other node is not a dispatch on the bonus root.
		[Fact]
		public void NodeExistsOnAnotherNodeIsIgnored()
		{
			string[] source = Source(
				"public bool CreateImprovements(Improvement.ImprovementSource objSource, XmlNode objBonus)",
				"{",
				"    if (NodeExists(objBonus, \"specificskill\")) { }",
				"    if (NodeExists(objChild, \"name\")) { }",
				"}");

			Assert.Equal(new[] { "specificskill" }, BonusVocabulary.HandledTagsIn(source));
		}

		// - an unbalanced brace in a literal must not end the method early
		[Fact]
		public void BraceInsideALiteralDoesNotEndTheMethod()
		{
			string[] source = Source(
				"public bool CreateImprovements(Improvement.ImprovementSource objSource, XmlNode nodBonus)",
				"{",
				"    string strOpen = \"}\";",
				"    char chrOpen = '}';",
				"    if (NodeExists(nodBonus, \"specificskill\")) { }",
				"}");

			Assert.Equal(new[] { "specificskill" }, BonusVocabulary.HandledTagsIn(source));
		}

		// A disabled check would otherwise mark its tag handled while nothing reads it.
		[Fact]
		public void CommentedOutCheckDoesNotCountAsHandled()
		{
			string[] source = Source(
				"public bool CreateImprovements(Improvement.ImprovementSource objSource, XmlNode nodBonus)",
				"{",
				"    // if (NodeExists(nodBonus, \"lineComment\")) { }",
				"    /* if (NodeExists(nodBonus, \"blockComment\")) { }",
				"       if (NodeExists(nodBonus, \"secondLine\")) { } */",
				"    if (NodeExists(nodBonus, \"specificskill\")) { }",
				"}");

			Assert.Equal(new[] { "specificskill" }, BonusVocabulary.HandledTagsIn(source));
		}

		[Fact]
		public void BraceInsideACommentDoesNotEndTheMethod()
		{
			string[] source = Source(
				"public bool CreateImprovements(Improvement.ImprovementSource objSource, XmlNode nodBonus)",
				"{",
				"    // }",
				"    /* }",
				"       } */",
				"    if (NodeExists(nodBonus, \"specificskill\")) { }",
				"}");

			Assert.Equal(new[] { "specificskill" }, BonusVocabulary.HandledTagsIn(source));
		}

		// A "//" inside a string literal must not cut the rest of the line off as a comment.
		[Fact]
		public void CommentMarkerInsideALiteralIsNotAComment()
		{
			string[] source = Source(
				"public bool CreateImprovements(Improvement.ImprovementSource objSource, XmlNode nodBonus)",
				"{",
				"    string strUrl = \"http://x\"; if (NodeExists(nodBonus, \"specificskill\")) { }",
				"    string strPath = @\"C:\\\"; if (NodeExists(nodBonus, \"selectskill\")) { }",
				"}");

			Assert.Equal(new[] { "selectskill", "specificskill" },
				BonusVocabulary.HandledTagsIn(source).OrderBy(tag => tag, StringComparer.Ordinal));
		}

		[Fact]
		public void CheckBrokenAcrossLinesIsFound()
		{
			string[] source = Source(
				"public bool CreateImprovements(Improvement.ImprovementSource objSource, XmlNode nodBonus)",
				"{",
				"    if (NodeExists(nodBonus,",
				"            \"specificskill\"))",
				"    { }",
				"}");

			Assert.Equal(new[] { "specificskill" }, BonusVocabulary.HandledTagsIn(source));
		}

		[Fact]
		public void EnabledTabsComeOnlyFromTheEnableTabBranch()
		{
			string[] source = Source(
				"public bool CreateImprovements(Improvement.ImprovementSource objSource, XmlNode nodBonus)",
				"{",
				"    if (NodeExists(nodBonus, \"enabletab\"))",
				"    {",
				"        switch (strTab) { case \"magician\": break; }",
				"    }",
				"    switch (strOther) { case \"unrelated\": break; }",
				"}");

			Assert.Equal(new[] { "magician" }, BonusVocabulary.EnabledTabsIn(source));
		}

		[Fact]
		public void MissingCreateImprovementsThrows()
		{
			Assert.Throws<InvalidOperationException>(
				() => BonusVocabulary.HandledTagsIn(Source("public void Other(XmlNode nodBonus) { }")));
		}

		private static string[] UnhandledIn(string fileName, XmlDocument document)
		{
			return BonusVocabulary
				.UnhandledTags(BonusVocabulary.BonusTagsIn(fileName, document), consumer => Known)
				.Select(tag => tag.Tag)
				.ToArray();
		}

		private static string[] TagsIn(string fileName, XmlDocument document)
		{
			return BonusVocabulary.BonusTagsIn(fileName, document).Select(tag => tag.Tag).ToArray();
		}

		private static XmlDocument Item(string bonus)
		{
			return Doc("<chummer><qualities><quality><name>Allergy</name>" + bonus
				+ "</quality></qualities></chummer>");
		}

		private static XmlDocument Doc(string xml)
		{
			XmlDocument document = new XmlDocument { XmlResolver = null };
			document.LoadXml(xml);
			return document;
		}

		private static string[] Source(params string[] lines)
		{
			return lines;
		}
	}
}
