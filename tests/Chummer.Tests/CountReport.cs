using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Xunit;

namespace Chummer.Tests
{
	// - one line per differing key: Assert.Equal truncates a dictionary after a few entries
	// - "nothing" marks a missing key, so a pinned 0 stays distinct from an absent one
	internal static class CountReport
	{
		public static void AssertEqual<TValue>(string headline, IReadOnlyDictionary<string, TValue> expected,
			IReadOnlyDictionary<string, TValue> actual)
		{
			string[] differences = expected.Keys.Union(actual.Keys, StringComparer.Ordinal)
				.Select(key => new
				{
					Key = key,
					Expected = Describe(expected, key),
					Actual = Describe(actual, key),
				})
				.Where(entry => !string.Equals(entry.Expected, entry.Actual, StringComparison.Ordinal))
				.OrderBy(entry => entry.Key, StringComparer.Ordinal)
				.Select(entry => entry.Key + ": expected " + (entry.Expected ?? "nothing")
					+ ", found " + (entry.Actual ?? "nothing"))
				.ToArray();

			if (differences.Length > 0)
				Assert.Fail(FailureReport.Build(headline + " differ from the pinned list", differences, line => line));
		}

		private static string Describe<TValue>(IReadOnlyDictionary<string, TValue> values, string key)
		{
			return values.TryGetValue(key, out TValue value) ? Convert.ToString(value, CultureInfo.InvariantCulture) : null;
		}
	}
}
