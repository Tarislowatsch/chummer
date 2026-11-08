using System;
using System.Linq;
using System.Xml.Xsl;
using Chummer.Tests.Kernel;
using Xunit;

namespace Chummer.Tests.Presentation
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
				.SelectMany(path => DataPaths.IncludedStylesheetPaths(path, Xml.Load(path)))
				.ToArray();

			string[] orphaned = DataPaths.PartialStylesheets()
				.Where(partial => !included.Contains(partial, StringComparer.OrdinalIgnoreCase))
				.ToArray();

			FailureReport.AssertNone(
				"These partial sheets are included by no sheet, so nothing compiles them", orphaned);
		}
	}
}