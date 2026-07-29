using System.Linq;
using Xunit;

namespace Chummer.Tests
{
	// - a failure message is read only when something is already wrong
	// - that is the worst moment to discover the count is off by one
	public class FailureReportTests
	{
		[Fact]
		public void HeadlineIsFollowedByOneIndentedLinePerFinding()
		{
			string message = FailureReport.Build("two things", new[] { "a", "b" }, finding => finding);

			Assert.Equal("two things:\n  a\n  b", message);
		}

		[Fact]
		public void ExactlyTheLimitIsNotTruncated()
		{
			string message = FailureReport.Build("at the limit", Findings(FailureReport.MaxLines),
				finding => finding);

			Assert.Equal(FailureReport.MaxLines, CountLines(message));
			Assert.DoesNotContain("more", message);
		}

		// One over the limit must report the count left out, not the total.
		[Fact]
		public void OneOverTheLimitPrintsTheLimitAndNamesTheRemainder()
		{
			string message = FailureReport.Build("one over", Findings(FailureReport.MaxLines + 1),
				finding => finding);

			Assert.Equal(FailureReport.MaxLines, CountLines(message));
			Assert.EndsWith("\n  ... and 1 more", message);
		}

		[Fact]
		public void WellOverTheLimitCountsEverythingItLeftOut()
		{
			string message = FailureReport.Build("many", Findings(FailureReport.MaxLines + 37),
				finding => finding);

			Assert.EndsWith("\n  ... and 37 more", message);
		}

		// The truncation notice must not count as a finding line.
		private static int CountLines(string message)
		{
			return message.Split('\n').Skip(1).Count(line => !line.StartsWith("  ... and "));
		}

		private static string[] Findings(int count)
		{
			return Enumerable.Range(1, count).Select(index => "finding " + index).ToArray();
		}
	}
}
