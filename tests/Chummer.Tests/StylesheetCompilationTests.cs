using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Xsl;
using Xunit;

namespace Chummer.Tests
{
	// - well-formed is not compilable: a bad XPath or unknown function only fails here
	// - proves the sheets compile, not that their output is right
	public class StylesheetCompilationTests
	{
		// - the exact overload both pickers call: XsltSettings.Default, XmlUrlResolver for includes
		[Theory]
		[MemberData(nameof(DataPaths.LoadableStylesheets), MemberType = typeof(DataPaths))]
		public void StylesheetCompilesAsTheAppLoadsIt(string path)
		{
			new XslCompiledTransform().Load(path);
		}

		private static readonly Regex Include = new Regex(@"<xsl:include\s+href=""([^""]+)""");

		// - a .xslt nobody includes is never compiled by the theory above
		[Fact]
		public void EveryPartialSheetIsIncludedBySomeSheet()
		{
			string[] partials = Directory
				.EnumerateFiles(DataPaths.ChummerSheetsDir, "*", SearchOption.AllDirectories)
				.Where(path => path.EndsWith(".xslt", StringComparison.Ordinal))
				.ToArray();

			string[] included = Directory
				.EnumerateFiles(DataPaths.ChummerSheetsDir, "*", SearchOption.AllDirectories)
				.SelectMany(path => Include.Matches(File.ReadAllText(path)).Cast<Match>()
					.Select(match => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path), match.Groups[1].Value))))
				.ToArray();

			string[] orphaned = partials
				.Where(partial => !included.Contains(Path.GetFullPath(partial), StringComparer.OrdinalIgnoreCase))
				.ToArray();

			Assert.True(orphaned.Length == 0,
				"These partial sheets are included by no sheet, so nothing compiles them:\n  "
				+ string.Join("\n  ", orphaned));
		}
	}
}