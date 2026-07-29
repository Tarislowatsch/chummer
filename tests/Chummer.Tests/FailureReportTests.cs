using System.Linq;
using Xunit;

namespace Chummer.Tests
{
	// Three checks now format their failures through this, and a failure message
	// is only ever read when something is already wrong - which is the worst
	// moment to discover that the count is off by one or that the truncation
	// notice appeared when nothing was truncated. The inline copies this replaced
	// were untested too, so this is not a regression being fixed; extracting them
	// was simply the first point where one test could cover all three.
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

		// The off-by-one that matters: one over the limit has to print the limit
		// and say the count of what it left out, not the total.
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

		// Counts finding lines, so the truncation notice does not count as one.
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
