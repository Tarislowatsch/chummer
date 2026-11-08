using System.Linq;
using Xunit;

namespace Chummer.Tests.Consumption.Expressions
{
	// - deliberately no allowlist: every ess value parses today
	public class EssExpressionTests
	{
		[Theory]
		[MemberData(nameof(ExpressionChecks.RuleIds), "ess", MemberType = typeof(ExpressionChecks))]
		public void EveryEssParsesInItsCalculator(string id)
		{
			ExpressionChecks.AssertNoFailures(ExpressionChecks.CheckFor(id), "CalculatedESS throws on");
		}

		[Theory]
		[MemberData(nameof(ExpressionChecks.PackRuleIds), "ess", MemberType = typeof(ExpressionChecks))]
		public void EveryPackEssParsesInItsCalculator(string packPath, string id)
		{
			ExpressionChecks.AssertNoFailures(ExpressionChecks.PackCheckFor(packPath, id), "CalculatedESS throws on");
		}
	}
}
