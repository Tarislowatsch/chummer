using System.IO;
using System.Linq;
using Xunit;

namespace Chummer.Tests
{
	// - deliberately no allowlist: every ess value parses today
	public class EssExpressionTests
	{
		[Theory]
		[MemberData(nameof(ExpressionChecks.RuleIds), "ess", MemberType = typeof(ExpressionChecks))]
		public void EveryEssParsesInItsCalculator(string id)
		{
			AssertParses(ExpressionChecks.CheckFor(id));
		}

		[Theory]
		[MemberData(nameof(ExpressionChecks.PackRuleIds), "ess", MemberType = typeof(ExpressionChecks))]
		public void EveryPackEssParsesInItsCalculator(string packPath, string id)
		{
			AssertParses(ExpressionChecks.PackCheckFor(packPath, id));
		}

		private static void AssertParses(ExpressionChecks.ExpressionCheck check)
		{
			if (check.Failures.Count > 0)
			{
				Assert.Fail(FailureReport.Build(
					Path.GetFileName(check.FilePath) + " has " + check.Failures.Count + " " + check.Rule.Id
						+ " value(s) CalculatedESS throws on",
					check.Failures,
					failure => "'" + failure.ItemName + "' = \"" + failure.Value + "\" at rating "
						+ failure.Rating + ": " + failure.Error));
			}
		}
	}
}
