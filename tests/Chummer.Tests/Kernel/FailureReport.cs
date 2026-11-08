using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Xunit;

namespace Chummer.Tests.Kernel
{
	// - three near-identical copies of this format had accumulated across the checks
	// - truncation keeps a file with hundreds of findings from burying the CI log
	internal static class FailureReport
	{
		// - fits today's worst case (17 in one collection) without an unbounded message
		// - internal, not private: the boundary tests pin against this constant
		internal const int MaxLines = 20;

		public static void AssertNone(string headline, IReadOnlyList<string> findings)
		{
			AssertNone(headline, findings, finding => finding);
		}

		// - the message is built only on failure: a green run never pays for the join
		public static void AssertNone<T>(string headline, IReadOnlyList<T> findings, Func<T, string> describe)
		{
			if (findings.Count > 0)
				Assert.Fail(Build(headline, findings, describe));
		}

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
