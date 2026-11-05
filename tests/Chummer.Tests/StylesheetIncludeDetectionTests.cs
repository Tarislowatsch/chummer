using System.Linq;
using System.Xml;
using Xunit;

namespace Chummer.Tests
{
	// - hand-built input: every shipped include has the same shape
	public class StylesheetIncludeDetectionTests
	{
		private const string Sheet = @"C:\sheets\Main.xsl";

		[Fact]
		public void FindsAnIncludeRelativeToTheSheet()
		{
			Assert.Equal(new[] { @"C:\sheets\Part.xslt" },
				Includes("<xsl:include href=\"Part.xslt\"/>"));
		}

		[Fact]
		public void IgnoresACommentedOutInclude()
		{
			Assert.Empty(Includes("<!-- <xsl:include href=\"Part.xslt\"/> -->"));
		}

		[Fact]
		public void ReadsSingleQuotesAndAnyPrefix()
		{
			Assert.Equal(new[] { @"C:\sheets\Part.xslt" }, IncludedPaths(
				"<t:stylesheet version='1.0' xmlns:t='http://www.w3.org/1999/XSL/Transform'>"
				+ "<t:include href='Part.xslt'/></t:stylesheet>"));
		}

		[Fact]
		public void CountsAnImportAsAnInclude()
		{
			Assert.Equal(new[] { @"C:\sheets\Part.xslt" },
				Includes("<xsl:import href=\"Part.xslt\"/>"));
		}

		[Fact]
		public void ResolvesAPathIntoAnotherFolder()
		{
			Assert.Equal(new[] { @"C:\export\Part.xslt" },
				Includes("<xsl:include href=\"../export/Part.xslt\"/>"));
		}

		private static string[] Includes(string body)
		{
			return IncludedPaths(
				"<xsl:stylesheet version=\"1.0\" xmlns:xsl=\"http://www.w3.org/1999/XSL/Transform\">"
				+ body + "</xsl:stylesheet>");
		}

		private static string[] IncludedPaths(string text)
		{
			XmlDocument document = new XmlDocument();
			document.LoadXml(text);
			return DataPaths.IncludedStylesheetPaths(Sheet, document).ToArray();
		}
	}
}