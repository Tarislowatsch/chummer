using System;
using System.Linq;
using System.Xml;
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

		// - a .xslt nobody includes is never compiled by the theory above
		[Fact]
		public void EveryPartialSheetIsIncludedBySomeSheet()
		{
			string[] included = DataPaths.StylesheetFiles()
				.SelectMany(path => DataPaths.IncludedStylesheetPaths(path, Load(path)))
				.ToArray();

			string[] orphaned = DataPaths.PartialStylesheets()
				.Where(partial => !included.Contains(partial, StringComparer.OrdinalIgnoreCase))
				.ToArray();

			Assert.True(orphaned.Length == 0,
				"These partial sheets are included by no sheet, so nothing compiles them:\n  "
				+ string.Join("\n  ", orphaned));
		}

		private static XmlDocument Load(string path)
		{
			XmlDocument document = new XmlDocument { XmlResolver = null };
			document.Load(path);
			return document;
		}
	}
}