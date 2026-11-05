using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace Chummer.Tests
{
	// - the table is hand-read off the consumers: a lookup it does not list stays unchecked
	// - the guards below catch a rule gone vacant or a consumer gone, not a missing rule
	public class CrossFileReferenceTests
	{
		// - fixing one changes what a critter or sprite gets: a behaviour change, not a tidy-up
		// - the typos mirror the data, not typos in this list
		// - format: <rule id>: <key>, copied from the failure output
		private static readonly HashSet<string> KnownUnresolvedReferences = new HashSet<string>(StringComparer.Ordinal)
		{
			"critter optional complex forms: Databomb",
			"critter optional complex forms: Shielding",
			"critter skill groups: Conjuration",
			"critter skill groups: Intimidation",
			"critter skills: Gymanstics",
			"critter skills: Infilatration",
			"critter skills: Intimidatino",
			"critter skills: shadowing",
		};

		[Theory]
		[MemberData(nameof(DataPaths.CrossFileReferenceRuleIds), MemberType = typeof(DataPaths))]
		public void EveryReferenceResolves(string id)
		{
			CrossFileReferences.ReferenceCheck check = DataPaths.CrossFileReferenceCheckFor(id);

			// Grouped by key: one bad name repeated across critters is one problem.
			var unexpected = check.Unresolved
				.Where(reference => !KnownUnresolvedReferences.Contains(Entry(reference)))
				.GroupBy(reference => reference.Display, StringComparer.Ordinal)
				.OrderBy(group => group.Key, StringComparer.Ordinal)
				.ToArray();

			if (unexpected.Length > 0)
			{
				Assert.Fail(FailureReport.Build(
					check.Rule.SourceFile + " has " + unexpected.Length + " " + id
						+ " value(s) that " + check.Rule.Consumer + " cannot find",
					unexpected,
					group => Entry(group.First()) + "  (" + group.Count() + "x, e.g. '"
						+ group.First().ItemName + "')"));
			}
		}

		// Same reason the duplicate allowlist has a staleness check: see NameUniquenessTests.
		[Fact]
		public void AllowlistedReferencesAreAllStillUnresolved()
		{
			HashSet<string> actual = new HashSet<string>(
				DataPaths.CrossFileReferenceChecks.SelectMany(check => check.Unresolved).Select(Entry),
				StringComparer.Ordinal);

			string[] stale = KnownUnresolvedReferences.Where(entry => !actual.Contains(entry))
				.OrderBy(entry => entry, StringComparer.Ordinal)
				.ToArray();

			Assert.True(stale.Length == 0,
				"These references resolve (or are gone) and must be removed from "
				+ nameof(KnownUnresolvedReferences) + ":\n  " + string.Join("\n  ", stale));
		}

		// - a mistyped source path selects nothing: its theory case passes unchecked
		// - a mistyped target makes every reference dangle, unless a fallback target hides it
		[Fact]
		public void EveryRuleSelectsReferencesAndEveryTargetDefinitions()
		{
			string[] vacant = DataPaths.CrossFileReferenceChecks
				.SelectMany(check => VacanciesOf(check))
				.OrderBy(entry => entry, StringComparer.Ordinal)
				.ToArray();

			Assert.True(vacant.Length == 0,
				"These rules check nothing:\n  " + string.Join("\n  ", vacant));
		}

		// - a consumer rewritten or removed leaves its rule describing code that no longer exists
		[Fact]
		public void EveryRuleStillHasItsConsumer()
		{
			string[] missing = CrossFileReferences.Rules
				.Where(rule => CodeOf(rule.ConsumerFile).IndexOf(rule.Reads, StringComparison.Ordinal) < 0)
				.Select(rule => rule.Id + ": " + rule.Consumer + " no longer contains " + rule.Reads)
				.ToArray();

			Assert.True(missing.Length == 0,
				"Re-read these consumers and fix or drop their rules:\n  " + string.Join("\n  ", missing));
		}

		// - a rule narrowed by a data or path change shows up here, not as a pass
		// - bumping the number is only correct for a deliberate data or rule change
		[Fact]
		public void TheRulesCheckTheMeasuredReferenceCount()
		{
			Assert.Equal(14332, DataPaths.CrossFileReferenceChecks.Sum(check => check.ReferenceCount));
		}

		// - every consumer sits directly in Chummer/, where several subfolders reuse file names
		private static string CodeOf(string fileName)
		{
			string path = Path.Combine(DataPaths.RepoRoot, "Chummer", fileName);
			return File.Exists(path) ? string.Join("\n", SourceLines.Of(File.ReadAllLines(path)).Code) : "";
		}

		private static IEnumerable<string> VacanciesOf(CrossFileReferences.ReferenceCheck check)
		{
			if (check.ReferenceCount == 0)
				yield return check.Rule.Id + ": no reference in " + check.Rule.SourceFile;

			for (int index = 0; index < check.Rule.Targets.Count; index++)
			{
				if (check.DefinitionCounts[index] == 0)
				{
					CrossFileReferences.Target target = check.Rule.Targets[index];
					yield return check.Rule.Id + ": no definition in " + target.FileName + " " + target.ItemXPath;
				}
			}
		}

		private static string Entry(CrossFileReferences.Reference reference)
		{
			return reference.RuleId + ": " + reference.Display;
		}
	}
}
