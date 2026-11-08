using System.Linq;
using Chummer.Tests.Kernel;
using Xunit;

namespace Chummer.Tests.Consumption.Fields
{
	public class DecimalCommaTests
	{
		// - packs included: a pack copied into data/ is merged and converted the same way
		[Theory]
		[MemberData(nameof(DataPaths.RuleXmlFiles), MemberType = typeof(DataPaths))]
		public void NoValueReadsAsANumberThroughAComma(string path)
		{
			string[] findings = DecimalCommas.CommaNumbersIn(Xml.Load(path)).ToArray();

			FailureReport.AssertNone(
				path + " has values that en-US reads with the comma as a thousands separator",
				findings);
		}
	}
}
