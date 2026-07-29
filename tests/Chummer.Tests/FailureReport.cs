using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Chummer.Tests
{
	// - every data check here reports the same shape of failure: a headline naming the file and how many findings it has, then one line per finding, truncated so a file with hundreds of them cannot bury the CI log
	// - three copies of that had accumulated, differing only in wording, with a fourth due with the next check
	internal static class FailureReport
	{
		// - enough to print a whole collection's worth of a systematic mistake: today's worst case is 17 in one collection, without an unbounded message when a future file racks up hundreds
		// - internal rather than private so the tests can pin the boundary against this number, instead of repeating a 20 that would silently disagree if the limit ever moved
		internal const int MaxLines = 20;

		public static string Build<T>(string headline, IReadOnlyList<T> findings, Func<T, string> describe)
		{
			StringBuilder message = new StringBuilder(headline).Append(":");

			foreach (T finding in findings.Take(MaxLines))
				message.Append("\n  ").Append(describe(finding));

			if (findings.Count > MaxLines)
				message.Append("\n  ... and ").Append(findings.Count - MaxLines).Append(" more");

			return message.ToString();
		}
	}
}
