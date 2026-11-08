using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Chummer.Tests.Kernel;
using Xunit;

namespace Chummer.Tests.Consumption.Fields
{
	public class KitRatingTests
	{
		// - rating 1 on items without a rating: "(Rating 1)" in the tree, rating 1 in save and print
		// - the fix changes what users see: it waits for the golden master
		// - format: <kit> / <item>, copied from the failure output
		private static readonly HashSet<string> KnownExcessRatings = new HashSet<string>(StringComparer.Ordinal)
		{
			"Hacker Pack / AR Gloves",
			"Hacker Pack / Holo Projector",
			"Hacker Pack / Nanopaste Trodes",
			"Hacker Pack / Satellite Link",
			"Hacker Pack / Simrig",
			"Hacker Pack / Skinlink",
			"Matrix Power User / AR Gloves",
			"Matrix Power User / Nanopaste Trodes",
		};

		private static bool IsDefect(KitRatings.Finding finding)
		{
			return finding.Outcome == KitRatings.Outcome.Exceeds || finding.Outcome == KitRatings.Outcome.NotANumber;
		}

		[Fact]
		public void EveryKitRatingFitsItsItem()
		{
			KitRatings.Finding[] unexpected = KitRatings.Findings
				.Where(finding => IsDefect(finding) && !KnownExcessRatings.Contains(finding.Key))
				.ToArray();

			FailureReport.AssertNone(
				"Kit ratings above their item's maximum", unexpected, finding => finding.Description);
		}

		[Fact]
		public void AllowlistedRatingsAreAllStillTooHigh()
		{
			HashSet<string> actual = new HashSet<string>(KitRatings.Findings.Where(IsDefect).Select(finding => finding.Key),
				StringComparer.Ordinal);

			string[] stale = KnownExcessRatings.Where(entry => !actual.Contains(entry))
				.OrderBy(entry => entry, StringComparer.Ordinal)
				.ToArray();

			FailureReport.AssertNone(
				"These ratings fit (or are gone) and must be removed from " + nameof(KnownExcessRatings), stale);
		}

		// - key "rule: outcome": a list gone vacant or a lookup gone blind fails here
		// - no programs maximum: every kit program targets an empty maxrating today
		private static readonly IReadOnlyDictionary<string, int> PinnedOutcomes =
			new Dictionary<string, int>(StringComparer.Ordinal)
			{
				{ "pack armor mods: Fits", 6 },
				{ "pack bioware: Fits", 20 },
				{ "pack cyberware children: Fits", 8 },
				{ "pack cyberware: Fits", 17 },
				{ "pack gear: Exceeds", 8 },
				{ "pack gear: Fits", 137 },
				{ "pack programs: NoMaximum", 35 },
				{ "pack vehicle gear: none", 0 },
				{ "pack vehicle mods: Fits", 4 },
				{ "pack vehicle mods: Quantity", 1 },
			};

		// - "none" with 0 marks a list no kit sets a rating in: the first one becomes visible
		[Fact]
		public void EveryKitListReachesTheMeasuredRatings()
		{
			Dictionary<string, int> actual = KitRatings.Findings
				.GroupBy(finding => finding.RuleId + ": " + finding.Outcome, StringComparer.Ordinal)
				.ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
			foreach (KitRatings.Limit limit in KitRatings.Limits.Where(limit => KitRatings.Findings.All(finding => finding.RuleId != limit.RuleId)))
				actual.Add(limit.RuleId + ": none", 0);

			PinReport.AssertEqual("Kit ratings per rule and outcome", PinnedOutcomes, actual);
		}

		// - kit ratings LoaderConversions finds that go to no item with a maximum
		private static readonly IReadOnlyDictionary<string, string> RatingsWithoutAnItemMaximum =
			new Dictionary<string, string>(StringComparer.Ordinal)
			{
				{ "skills/skill/rating", "AddPACKSKit caps it at the skill maximum itself" },
				{ "skills/skillgroup/rating", "AddPACKSKit caps it at the group maximum itself" },
				{ "knowledgeskills/skill/rating", "AddPACKSKit caps it at the skill maximum itself" },
				{ "selectmartialart/@rating", "martialarts.xml has no rating maximum" },
				{ "martialarts/martialart/rating", "martialarts.xml has no rating maximum" },
				{ "powers/power/rating", "powers.xml levels is yes, no or a count: no plain maximum" },
				{ "programs/program/options/option/rating", "no reference rule finds the option: no kit names one" },
			};

		// - a new rating conversion must land in Limits or in the list above
		[Fact]
		public void EveryConvertedKitRatingIsCheckedOrExplained()
		{
			string[] converted = LoaderConversions.ApplicationConversions
				.Where(conversion => conversion.Corpus == LoaderConversions.Kits
					&& (conversion.Path.EndsWith("/rating", StringComparison.Ordinal)
						|| conversion.Path.EndsWith("/@rating", StringComparison.Ordinal)))
				.Select(conversion => conversion.Path)
				.Distinct(StringComparer.Ordinal)
				.OrderBy(path => path, StringComparer.Ordinal)
				.ToArray();

			Assert.Equal(converted, KitRatings.Limits.Select(limit => limit.ConversionPath)
				.Concat(RatingsWithoutAnItemMaximum.Keys)
				.OrderBy(path => path, StringComparer.Ordinal));
		}

		// - a custom pack shipping kits would go unchecked: targets resolve in the base data only
		[Fact]
		public void OnlyTheBaseDataCarriesKits()
		{
			Assert.Equal(new[] { Path.Combine(DataPaths.ChummerDataDir, "packs.xml") }, LoaderConversions.Kits.Files());
		}
	}
}
