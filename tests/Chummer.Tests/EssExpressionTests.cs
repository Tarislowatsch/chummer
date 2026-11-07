using System.IO;
using System.Linq;
using Xunit;

namespace Chummer.Tests
{
	// - deliberately no allowlist: every ess value parses today
	public class EssExpressionTests
	{
		[Theory]
		[MemberData(nameof(ExpressionChecks.ExpressionRuleIds), "ess", MemberType = typeof(ExpressionChecks))]
		public void EveryEssParsesInItsCalculator(string id)
		{
			ExpressionChecks.ExpressionCheck check = ExpressionChecks.ExpressionCheckFor(id);

			if (check.Failures.Count > 0)
			{
				Assert.Fail(FailureReport.Build(
					Path.GetFileName(check.FilePath) + " has " + check.Failures.Count + " " + id
						+ " value(s) CalculatedESS throws on",
					check.Failures,
					failure => "'" + failure.ItemName + "' = \"" + failure.Value + "\" at rating "
						+ failure.Rating + ": " + failure.Error));
			}
		}
	}
}
