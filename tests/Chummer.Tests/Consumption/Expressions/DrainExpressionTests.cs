using System;
using System.Collections.Generic;
using System.Linq;
using Chummer.Tests.Kernel;
using Xunit;

namespace Chummer.Tests.Consumption.Expressions
{
	public class DrainExpressionTests
	{
		[Fact]
		public void EveryDrainEvaluatesAtEveryConsumer()
		{
			Dictionary<string, DrainExpressions.Chain> chains = DrainExpressions.ApplicationChains()
				.ToDictionary(chain => chain.Site, StringComparer.Ordinal);
			List<string> failures = new List<string>();

			foreach (DrainExpressions.Consumer consumer in DrainExpressions.Consumers)
			{
				string[] drains = DrainExpressions.DrainValuesFor(consumer.DataFile).ToArray();
				Assert.True(drains.Length > 0, consumer.DataFile + " yields no <drain> to check.");

				DrainExpressions.Chain chain;
				Assert.True(chains.TryGetValue(consumer.Site, out chain), "No attribute chain starts at " + consumer.Site + ".");

				foreach (string drain in drains)
				{
					string failure = DrainExpressions.FailureOf(drain, chain.Codes);
					if (failure != null)
						failures.Add(consumer.Site + ": " + consumer.DataFile + " '" + drain + "' " + failure);
				}
			}

			FailureReport.AssertNone("These drains break where they are evaluated", failures);
		}

		// - a consumer missing from the table would keep its codes unchecked
		[Fact]
		public void EveryWideAttributeChainIsClassified()
		{
			string[] found = DrainExpressions.ApplicationChains()
				.Where(chain => chain.Codes.Distinct(StringComparer.Ordinal).Count() >= DrainExpressions.MinimumChainCodes)
				.Select(chain => chain.Site)
				.ToArray();
			string[] listed = DrainExpressions.Consumers.Select(consumer => consumer.Site)
				.Concat(DrainExpressions.OtherChains.Keys)
				.ToArray();

			string[] differences = found.Except(listed, StringComparer.Ordinal).Select(site => "unlisted " + site)
				.Concat(listed.Except(found, StringComparer.Ordinal).Select(site => "no chain at " + site))
				.ToArray();

			FailureReport.AssertNone("Attribute chains and the consumer table disagree", differences);
		}

		// - each read feeds either PrintToStream or a label the form chains evaluate
		// - a new read is a cue to find its evaluation and list it
		[Fact]
		public void OnlyTheKnownSitesReadDrain()
		{
			Assert.Equal(
				new[]
				{
					"clsCharacter.cs:1647",
					"clsCharacter.cs:1673",
					"frmCareer.cs:986",
					"frmCareer.cs:1015",
					"frmCareer.cs:19663",
					"frmCareer.cs:19700",
					"frmCreate.cs:13978",
					"frmCreate.cs:14101",
				},
				DrainExpressions.DrainReadSites());
		}
	}
}
