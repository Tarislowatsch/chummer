using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Chummer.Tests
{
	// - deliberately no allowlist: every name lookup is anchored today
	// - the collisions themselves stay: renaming them would break saves for nothing
	public class NameLookupTests
	{
		[Fact]
		public void NoNameLookupCrossesCollections()
		{
			string[] unanchored = NameLookups.All
				.Where(lookup => lookup.IsUnanchored)
				.Select(lookup => lookup.Site + " \"" + lookup.Path + "[name ...\"")
				.ToArray();

			Assert.True(unanchored.Length == 0,
				"These lookups match a name in any collection, so a same-named item elsewhere can answer:\n  "
				+ string.Join("\n  ", unanchored));
		}

		[Fact]
		public void EveryComputedPathIsTraced()
		{
			string[] untraced = NameLookups.All
				.Where(lookup => lookup.DynamicPrefix != null
					&& !NameLookups.KnownDynamicPrefixes.ContainsKey(lookup.DynamicPrefix))
				.Select(lookup => lookup.Site + " " + lookup.DynamicPrefix)
				.ToArray();

			Assert.True(untraced.Length == 0,
				"These lookups build their path in code. Trace it to one collection and add it to "
				+ nameof(NameLookups.KnownDynamicPrefixes) + ":\n  " + string.Join("\n  ", untraced));
		}

		[Fact]
		public void EveryTracedPrefixIsStillUsed()
		{
			HashSet<string> used = new HashSet<string>(
				NameLookups.All.Select(lookup => lookup.DynamicPrefix).Where(prefix => prefix != null),
				StringComparer.Ordinal);

			Assert.Empty(NameLookups.KnownDynamicPrefixes.Keys.Where(prefix => !used.Contains(prefix)));
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
