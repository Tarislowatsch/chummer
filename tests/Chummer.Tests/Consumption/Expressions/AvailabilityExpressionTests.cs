using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using Chummer.Tests.Kernel;
using Xunit;

namespace Chummer.Tests.Consumption.Expressions
{
	// - ValidateCharacter parses every item's availability when a character is finished
	// - an avail its calculator cannot parse throws there, with nothing catching it
	// - deliberately no allowlist: every avail in the data parses today
	public class AvailabilityExpressionTests
	{
		[Theory]
		[MemberData(nameof(ExpressionChecks.RuleIds), "avail", MemberType = typeof(ExpressionChecks))]
		public void EveryAvailabilityParsesInItsCalculator(string id)
		{
			ExpressionChecks.AssertNoFailures(ExpressionChecks.CheckFor(id), "its calculator throws on");
		}

		[Theory]
		[MemberData(nameof(ExpressionChecks.PackRuleIds), "avail", MemberType = typeof(ExpressionChecks))]
		public void EveryPackAvailabilityParsesInItsCalculator(string packPath, string id)
		{
			ExpressionChecks.AssertNoFailures(ExpressionChecks.PackCheckFor(packPath, id), "its calculator throws on");
		}

		// - "24FF" parses, so the guards above cannot see a doubled suffix
		// - the raw text still shows and prints
		[Fact]
		public void NoAvailabilityRepeatsItsSuffix()
		{
			string[] doubled = DataPaths.DataFilesWithPacks
				.SelectMany(file => Xml.Load(file.FilePath).SelectNodes("//avail").Cast<XmlNode>()
					.Where(node => Regex.IsMatch(node.InnerText.Trim(), "[RF]{2}$"))
					.Select(node => file.Label + ": " + node.InnerText))
				.ToArray();

			FailureReport.AssertNone("These availabilities repeat their R/F suffix", doubled);
		}
	}
}
