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

		// - only the finding lines are deferred: the caller's headline is built on every run
		public static void AssertNone<T>(string headline, IReadOnlyList<T> findings, Func<T, string> describe)
		{
			if (findings.Count > 0)
				Assert.Fail(Build(headline, findings, describe));
		}

		// - for edits to this project's own lists and pins: a cut list hides edits still due
		public static void AssertNoneInFull(string headline, IReadOnlyList<string> edits)
		{
			if (edits.Count > 0)
				Assert.Fail(Build(headline, edits, edit => edit, int.MaxValue));
		}

		public static string Build<T>(string headline, IReadOnlyList<T> findings, Func<T, string> describe)
		{
			return Build(headline, findings, describe, MaxLines);
		}

		private static string Build<T>(string headline, IReadOnlyList<T> findings, Func<T, string> describe,
			int maxLines)
		{
			StringBuilder message = new StringBuilder(headline).Append(":");

			foreach (T finding in findings.Take(maxLines))
				message.Append("\n  ").Append(describe(finding));

			if (findings.Count > maxLines)
				message.Append("\n  ... and ").Append(findings.Count - maxLines).Append(" more");

			return message.ToString();
		}
	}
}
