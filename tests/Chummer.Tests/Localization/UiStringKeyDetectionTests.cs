using System.Linq;
using System.Xml;
using Chummer.Tests.Kernel;
using Xunit;

namespace Chummer.Tests.Localization
{
	// - hand-built source: the real code shows today's calls, never that a bad one is caught
	public class UiStringKeyDetectionTests
	{
		[Fact]
		public void EntriesMissingKeyOrTextFindsBothGaps()
		{
			XmlDocument language = Xml.Parse("<chummer><strings>"
				+ "<string><key>A</key><text>a</text></string>"
				+ "<string><key>B</key></string>"
				+ "<string><text>c</text></string>"
				+ "</strings></chummer>");

			Assert.Equal(new[] { "string #2 (key 'B')", "string #3 (key '(none)')" },
				UiStringKeys.EntriesMissingKeyOrText(language));
		}

		private static string[] Arguments(string fileName, params string[] lines)
		{
			return UiStringKeys.CallsIn(fileName, SourceLines.Of(lines)).Select(call => call.Argument).ToArray();
		}

		[Fact]
		public void FindsAQualifiedCallAndItsLine()
		{
			UiStringKeys.Call call = UiStringKeys.CallsIn("frmX.cs",
				SourceLines.Of(new[] { "", "x = LanguageManager.Instance.GetString(\"Label_A\");" })).Single();

			Assert.Equal("frmX.cs:2", call.Site);
			Assert.Equal("\"Label_A\"", call.Argument);
		}

		[Fact]
		public void IgnoresACommentedOutCall()
		{
			Assert.Empty(Arguments("frmX.cs", "// x = LanguageManager.Instance.GetString(\"Label_A\");"));
		}

		[Fact]
		public void IgnoresTheResourceManager()
		{
			Assert.Empty(Arguments("frmX.cs", "tip.SetToolTip(lbl, resources.GetString(\"lbl.ToolTip\"));"));
		}

		[Fact]
		public void CountsABareCallOnlyInsideTheLanguageManager()
		{
			Assert.Empty(Arguments("frmX.cs", "x = GetString(\"Label_A\");"));
			Assert.Equal(new[] { "\"Label_A\"" }, Arguments("clsLanguageManager.cs", "x = GetString(\"Label_A\");"));
		}

		[Fact]
		public void SkipsTheDeclaration()
		{
			Assert.Empty(Arguments("clsLanguageManager.cs", "public string GetString(string strKey)"));
		}

		[Fact]
		public void KeepsNestedParensInsideTheArgument()
		{
			Assert.Equal(new[] { "\"String_\" + _objType.ToString()" }, Arguments("frmX.cs",
				"w(\"type\", LanguageManager.Instance.GetString(\"String_\" + _objType.ToString()));"));
		}

		[Fact]
		public void IgnoresParensInsideAStringLiteral()
		{
			Assert.Equal(new[] { "\"Odd)Key\"" },
				Arguments("frmX.cs", "x = LanguageManager.Instance.GetString(\"Odd)Key\") + \")\";"));
		}

		[Theory]
		[InlineData("\"Label_A\"", "Label_A")]
		[InlineData("\"String_\" + x", null)]
		[InlineData("strKey", null)]
		public void LiteralKeyAcceptsOnlyOneLiteral(string argument, string expected)
		{
			Assert.Equal(expected, UiStringKeys.LiteralKey(argument));
		}

		[Fact]
		public void EnumMembersDropsExplicitValues()
		{
			Assert.Equal(new[] { "Contact", "Enemy", "Pet" }, UiStringKeys.EnumMembers(
				"public enum ContactType\n{\n\tContact = 0,\n\tEnemy = 1,\n\tPet = 2,\n}", "ContactType"));
		}

		[Fact]
		public void MissingKeysReportsOnlyUndeclaredValues()
		{
			UiStringKeys.ComposedForm form = new UiStringKeys.ComposedForm(
				"\"String_Mount\" + x", "String_Mount", "", "test", null);

			Assert.Equal(new[] { "String_MountSide" },
				UiStringKeys.MissingKeys(form, new[] { "Top", "Side", "Top" }, new[] { "String_MountTop" }));
		}
	}
}