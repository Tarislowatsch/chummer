using System;
using System.Collections.Generic;
using System.Linq;
using Chummer.Tests.Kernel;
using Xunit;

namespace Chummer.Tests.Consumption.Fields
{
	// - deliberately no allowlist: every name lookup is anchored today
	// - covers XPath lookups only, in-memory name comparisons are not surveyed
	public class NameLookupTests
	{
		[Fact]
		public void NoNameLookupCrossesCollections()
		{
			string[] unanchored = NameLookups.All
				.Where(lookup => lookup.IsUnanchored)
				.Select(lookup => lookup.Site + " " + lookup.Path)
				.ToArray();

			FailureReport.AssertNone(
				"These lookups match a name in any collection, so a same-named item elsewhere can answer", unanchored);
		}

		// - exact per file: a generic variable name like strXPath must not admit a new path elsewhere
		[Fact]
		public void ComputedPathsMatchTheTracedTable()
		{
			string[] actual = NameLookups.All
				.Where(lookup => lookup.IsComputed)
				.GroupBy(lookup => lookup.File + " " + lookup.Path, StringComparer.Ordinal)
				.Select(group => group.Key + " x" + group.Count())
				.OrderBy(entry => entry, StringComparer.Ordinal)
				.ToArray();

			AssertTable(NameLookups.ComputedPaths, actual, nameof(NameLookups.ComputedPaths));
		}

		// - catches a variable assigned "//gear" far from the lookup that reads it
		[Fact]
		public void NoLiteralHoldsAnUnanchoredStep()
		{
			string[] actual = NameLookups.AllLiterals
				.Where(literal => NameLookups.IsUnanchoredLiteral(literal.Text))
				.GroupBy(literal => literal.File + " " + literal.Text, StringComparer.Ordinal)
				.Select(group => group.Key + " x" + group.Count())
				.OrderBy(entry => entry, StringComparer.Ordinal)
				.ToArray();

			AssertTable(NameLookups.KnownWildcardLiterals, actual, nameof(NameLookups.KnownWildcardLiterals));
		}

		// - the whole list on failure: the table is copied back from it after a re-trace
		private static void AssertTable(IReadOnlyDictionary<string, int> table, string[] actual, string name)
		{
			string[] expected = table
				.Select(entry => entry.Key + " x" + entry.Value)
				.OrderBy(entry => entry, StringComparer.Ordinal)
				.ToArray();

			Assert.True(expected.SequenceEqual(actual, StringComparer.Ordinal),
				name + " no longer matches the code. Found:\n  " + string.Join("\n  ", actual)
				+ "\nMissing from the code:\n  " + string.Join("\n  ", expected.Except(actual, StringComparer.Ordinal)));
		}

		// - an empty extraction would make the checks above vacuous
		// - a lower bound, not exact: it counts code, which later phases change
		[Fact]
		public void FindsTheNameLookups()
		{
			Assert.True(NameLookups.All.Count >= 500,
				"Only " + NameLookups.All.Count + " name lookups found, expected at least 500.");
		}
	}
}
