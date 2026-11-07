using System.Linq;
using Xunit;

namespace Chummer.Tests
{
	public class DecimalCommaTests
	{
		// - packs included: a pack copied into data/ is merged and converted the same way
		[Theory]
		[MemberData(nameof(DataPaths.RuleXmlFiles), MemberType = typeof(DataPaths))]
		public void NoValueReadsAsANumberThroughAComma(string path)
		{
			string[] findings = DecimalCommas.CommaNumbersIn(CustomContent.Load(path)).ToArray();

			Assert.True(findings.Length == 0, FailureReport.Build(
				path + " has values that en-US reads with the comma as a thousands separator",
				findings, finding => finding));
		}
	}
}
