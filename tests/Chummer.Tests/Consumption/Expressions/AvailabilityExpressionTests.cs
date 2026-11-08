using System.IO;
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
			AssertParses(ExpressionChecks.CheckFor(id));
		}

		[Theory]
		[MemberData(nameof(ExpressionChecks.PackRuleIds), "avail", MemberType = typeof(ExpressionChecks))]
		public void EveryPackAvailabilityParsesInItsCalculator(string packPath, string id)
		{
			AssertParses(ExpressionChecks.PackCheckFor(packPath, id));
		}

		private static void AssertParses(ExpressionChecks.ExpressionCheck check)
		{
			FailureReport.AssertNone(
				Path.GetFileName(check.FilePath) + " has " + check.Failures.Count + " " + check.Rule.Id
					+ " value(s) its calculator throws on",
				check.Failures,
				failure => "'" + failure.ItemName + "' = \"" + failure.Value + "\" at rating "
					+ failure.Rating + ": " + failure.Error);
		}
	}
}
