using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace Chummer.Tests
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
				// - both bounds 0: Create keeps the text (clsEquipment.cs:9726)
				// - TotalCost then runs Convert.ToInt32("Variable(0+)"), read off the code, not run
				{ "Gear.cost", new[] { "Datasoft (2050) = Variable(0+)" } },
			};

		[Theory]
		[MemberData(nameof(ExpressionChecks.RuleIds), "cost", MemberType = typeof(ExpressionChecks))]
		public void EveryCostParsesInItsCalculator(string id)
		{
			AssertParses(ExpressionChecks.CheckFor(id), KnownFailuresFor(id));
		}

		// - no allowlist: the known failures above are base entries
		[Theory]
		[MemberData(nameof(ExpressionChecks.PackRuleIds), "cost", MemberType = typeof(ExpressionChecks))]
		public void EveryPackCostParsesInItsCalculator(string packPath, string id)
		{
			AssertParses(ExpressionChecks.PackCheckFor(packPath, id), new string[0]);
		}

		private static void AssertParses(ExpressionChecks.ExpressionCheck check, IReadOnlyCollection<string> known)
		{
			ExpressionChecks.ExpressionFailure[] unexpected = check.Failures
				.Where(failure => !known.Contains(failure.Key))
				.ToArray();

			if (unexpected.Length > 0)
			{
				Assert.Fail(FailureReport.Build(
					Path.GetFileName(check.FilePath) + " has " + unexpected.Length + " " + check.Rule.Id
						+ " value(s) its calculator throws on",
					unexpected,
					failure => "'" + failure.ItemName + "' = \"" + failure.Value + "\" at rating "
						+ failure.Rating + ": " + failure.Error));
			}
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

			Assert.True(stale.Length == 0,
				"These known cost failures no longer fail. Delete them from the list:\n  "
				+ string.Join("\n  ", stale));
		}

		private static IReadOnlyCollection<string> KnownFailuresFor(string id)
		{
			string[] known;
			return KnownFailures.TryGetValue(id, out known) ? known : new string[0];
		}
	}
}
