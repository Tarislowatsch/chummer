using System;
using System.Collections.Generic;
using System.Linq;
using Chummer.Tests.Kernel;
using Xunit;

namespace Chummer.Tests.Consumption.Expressions
{
	// - a cost its calculator cannot parse throws wherever the price is shown or summed
	// - inside Cyberware's empty catch it costs 0 instead, without a word
	public class CostExpressionTests
	{
		// - "entity.cost" -> entries that fail today, as "name = value"
		// - fixing one is a data change for its own ticket: delete the line there
		private static readonly IReadOnlyDictionary<string, string[]> KnownFailures =
			new Dictionary<string, string[]>(StringComparer.Ordinal)
			{
			};

		[Theory]
		[MemberData(nameof(ExpressionChecks.RuleIds), "cost", MemberType = typeof(ExpressionChecks))]
		public void EveryCostParsesInItsCalculator(string id)
		{
			ExpressionChecks.AssertNoFailures(ExpressionChecks.CheckFor(id), "its calculator throws on",
				KnownFailuresFor(id));
		}

		// - no allowlist: no pack cost fails today
		[Theory]
		[MemberData(nameof(ExpressionChecks.PackRuleIds), "cost", MemberType = typeof(ExpressionChecks))]
		public void EveryPackCostParsesInItsCalculator(string packPath, string id)
		{
			ExpressionChecks.AssertNoFailures(ExpressionChecks.PackCheckFor(packPath, id), "its calculator throws on");
		}

		// - a fixed entry left on the list would let the same value come back unnoticed
		[Fact]
		public void EveryKnownFailureStillFails()
		{
			string[] stale = KnownFailures
				.SelectMany(entry => entry.Value
					.Where(key => !ExpressionChecks.CheckFor(entry.Key).Failures
						.Any(failure => failure.Key == key))
					.Select(key => entry.Key + ": " + key))
				.ToArray();

			FailureReport.AssertNoneInFull("These known cost failures no longer fail. Delete them from the list", stale);
		}

		private static IReadOnlyCollection<string> KnownFailuresFor(string id)
		{
			string[] known;
			return KnownFailures.TryGetValue(id, out known) ? known : new string[0];
		}
	}
}
