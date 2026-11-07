using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Chummer.Tests
{
	// - one line per differing key: Assert.Equal truncates a dictionary after a few entries
	// - "nothing" marks a missing key, so a pinned 0 stays distinct from an absent one
	internal static class CountReport
	{
		public static void AssertEqual(string headline, IReadOnlyDictionary<string, int> expected,
			IReadOnlyDictionary<string, int> actual)
		{
			string[] differences = expected.Keys.Union(actual.Keys, StringComparer.Ordinal)
				.Select(key => new
				{
					Key = key,
					Expected = expected.TryGetValue(key, out int wanted) ? (int?)wanted : null,
					Actual = actual.TryGetValue(key, out int found) ? (int?)found : null,
				})
				.Where(entry => entry.Expected != entry.Actual)
				.OrderBy(entry => entry.Key, StringComparer.Ordinal)
				.Select(entry => entry.Key + ": expected " + Describe(entry.Expected) + ", found " + Describe(entry.Actual))
				.ToArray();

			if (differences.Length > 0)
				Assert.Fail(FailureReport.Build(headline + " differ from the pinned list", differences, line => line));
		}

		private static string Describe(int? count)
		{
			return count.HasValue ? count.Value.ToString() : "nothing";
		}
	}
}
