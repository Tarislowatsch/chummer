using System.IO;
using Xunit;

namespace Chummer.Tests
{
	// - ValidateCharacter parses every item's availability when a character is finished
	// - an avail its calculator cannot parse throws there, with nothing catching it
	// - deliberately no allowlist: every avail in the data parses today
	public class AvailabilityExpressionTests
	{
		[Theory]
		[MemberData(nameof(DataPaths.ExpressionRuleIds), "avail", MemberType = typeof(DataPaths))]
		public void EveryAvailabilityParsesInItsCalculator(string id)
		{
			ExpressionChecks.ExpressionCheck check = DataPaths.ExpressionCheckFor(id);

			if (check.Failures.Count > 0)
			{
				Assert.Fail(FailureReport.Build(
					Path.GetFileName(check.FilePath) + " has " + check.Failures.Count + " " + id
						+ " value(s) its calculator throws on",
					check.Failures,
					failure => "'" + failure.ItemName + "' = \"" + failure.Value + "\" at rating "
						+ failure.Rating + ": " + failure.Error));
			}
		}
	}
}
