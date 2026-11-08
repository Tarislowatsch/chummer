using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Chummer.Tests.Kernel;
using Chummer.Tests.Rulebook;
using Xunit;

namespace Chummer.Tests.Consumption.Expressions
{
	public class CritterExpressionTests
	{
		// - "F/2" at an odd Force gives x.5: Convert.ToInt32 rejects it
		// - each entry must keep failing: a fixed one leaves this list, it does not linger
		private static readonly string[] KnownFailures =
		{
			"critters.xml: Cyberwerewolf complexforms/complexform[Cascading]/@rating = F/2",
			"critters.xml: Daemon complexforms/complexform[Cascading]/@rating = F/2",
			"critters.xml: Daemon complexforms/complexform[Expert Offense]/@rating = F/2",
			"critters.xml: Frobnitz complexforms/complexform[Cascading]/@rating = F/2",
			"critters.xml: Frobnitz complexforms/complexform[Expert Offense]/@rating = F/2",
			"critters.xml: Grue complexforms/complexform[Cascading]/@rating = F/2",
			"critters.xml: Grue complexforms/complexform[Expert Offense]/@rating = F/3",
			"critters.xml: Heavyweight complexforms/complexform[Expert Defense]/@rating = F/2",
			"critters.xml: Spike Sprite complexforms/complexform[Cascading]/@rating = F/2",
			"critters.xml: Tentacle complexforms/complexform[Expert Defense]/@rating = F/2",
			"critters.xml: Tentacle complexforms/complexform[Expert Offense]/@rating = F/2",
			"critters.xml: Yantra complexforms/complexform[Cascading]/@rating = F/2",
		};

		[Fact]
		public void EveryCritterFormEvaluatesAtEveryForce()
		{
			string[] xpaths = CritterExpressions.ApplicationArguments()
				.SelectMany(CritterExpressions.XPathsFor)
				.Distinct(StringComparer.Ordinal)
				.ToArray();

			CritterExpressions.Failure[] failures = CritterExpressions.DataFiles()
				.SelectMany(path => CritterExpressions.FailuresIn(Path.GetFileName(path), Xml.Load(path), xpaths))
				.ToArray();

			string[] differences = failures
				.Where(failure => !KnownFailures.Contains(failure.Key, StringComparer.Ordinal))
				.Select(failure => failure.Key + " at Force " + failure.Force + ": " + failure.Error)
				.Concat(KnownFailures
					.Where(known => !failures.Any(failure => string.Equals(failure.Key, known, StringComparison.Ordinal)))
					.Select(known => "no longer fails, drop it from KnownFailures: " + known))
				.ToArray();

			Assert.True(differences.Length == 0,
				FailureReport.Build("Critter forms and KnownFailures disagree", differences, line => line));
		}

		// - a caller reading a new field would otherwise stay unchecked
		[Fact]
		public void EveryArgumentMapsToItsData()
		{
			IReadOnlyList<string> arguments = CritterExpressions.ApplicationArguments();

			string[] unmapped = arguments.Where(argument => !CritterExpressions.XPathsFor(argument).Any()).ToArray();
			Assert.True(unmapped.Length == 0, "No data mapped for: " + string.Join(", ", unmapped));
			Assert.Empty(CritterExpressions.LoopArguments.Keys.Except(arguments, StringComparer.Ordinal));
		}

		// - an empty mapping or a missing metatype field would make the check vacuous
		[Fact]
		public void EveryAttributeFieldIsRead()
		{
			string[] fields = CritterExpressions.ApplicationArguments()
				.SelectMany(CritterExpressions.XPathsFor)
				.Where(path => path.IndexOf('/') < 0)
				.ToArray();

			string[] expected = new[] { "bod", "agi", "rea", "str", "cha", "int", "log", "wil", "ini", "mag", "res", "edg", "ess" }
				.SelectMany(attribute => new[] { attribute + "min", attribute + "max", attribute + "aug" })
				.ToArray();

			Assert.Empty(expected.Except(fields, StringComparer.Ordinal));
		}

		// - updating the hash is only correct after re-reading all three and fixing the port
		private const string PinnedFingerprint = "D99127C4BBF57F09";

		[Fact]
		public void ExpressionToStringUnchangedSinceItsPortWasWritten()
		{
			IReadOnlyDictionary<string, string> actual = ExpressionChecks.CalculatorFingerprints(CritterExpressions.Calculators);

			string[] drifted = actual
				.Where(entry => !string.Equals(entry.Value, PinnedFingerprint, StringComparison.Ordinal))
				.Select(entry => entry.Key + " now hashes to " + entry.Value)
				.ToArray();

			Assert.True(drifted.Length == 0,
				"Pinned as " + PinnedFingerprint + ". Re-read each copy, fix CritterExpressions, then update the hash:\n  "
				+ string.Join("\n  ", drifted));
		}
	}
}
