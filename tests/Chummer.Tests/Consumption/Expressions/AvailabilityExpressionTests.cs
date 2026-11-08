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
	}
}
