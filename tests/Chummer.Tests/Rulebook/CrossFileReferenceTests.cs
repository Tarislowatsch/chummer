using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Chummer.Tests.Kernel;
using Xunit;

namespace Chummer.Tests.Rulebook
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
			"bonus selectskill excludecategory: Vechicle Active",
			"bonus selectskillgroup excludecategory: Vechicle Active",
			"bonus specificskill: Conjuring",
			"bonus specificskill: Sorcery",
			"critter optional complex forms: Databomb",
			"critter optional complex forms: Shielding",
			"critter skill groups: Conjuration",
			"critter skill groups: Intimidation",
			"critter skills: Gymanstics",
			"critter skills: Infilatration",
			"critter skills: Intimidatino",
			"critter skills: shadowing",
			"weapon range by category: Clubs",
			"weapon range by category: Crossbows",
			"weapon range by category: Cyberware Exotic Ranged Weapons",
			"weapon range by category: Cyberware Throwing Weapons",
			"weapon range by category: Exotic Ranged Weapons",
			// - 154 rockets, missiles, rounds, mines: no code hands them the launcher's range
			// - 21 launcher grenades carry one themselves: the data can
			"weapon range by category: Gear",
			"weapon range by category: Special Weapons",
			"weapon range by category: Vehicle Weapons",
		};

		[Theory]
		[MemberData(nameof(CrossFileReferences.RuleIds), MemberType = typeof(CrossFileReferences))]
		public void EveryReferenceResolves(string id)
		{
			CrossFileReferences.ReferenceCheck check = CrossFileReferences.CheckFor(id);
			AssertResolved(check, check.Rule.SourceLabel, KnownUnresolvedReferences);
		}

		// - no allowlist: the known references above are base entries
		[Theory]
		[MemberData(nameof(CrossFileReferences.PackRuleIds), MemberType = typeof(CrossFileReferences))]
		public void EveryPackReferenceResolves(string packPath, string id)
		{
			AssertResolved(CrossFileReferences.PackCheckFor(packPath, id), Path.GetFileName(packPath),
				new HashSet<string>());
		}

		private static void AssertResolved(CrossFileReferences.ReferenceCheck check, string source,
			HashSet<string> known)
		{
			// Grouped by key: one bad name repeated across critters is one problem.
			var unexpected = check.Unresolved
				.Where(reference => !known.Contains(Entry(reference)))
				.GroupBy(reference => reference.Display, StringComparer.Ordinal)
				.OrderBy(group => group.Key, StringComparer.Ordinal)
				.ToArray();

			FailureReport.AssertNone(
				source + " has " + unexpected.Length + " " + check.Rule.Id
					+ " value(s) that " + check.Rule.Consumer + " cannot find",
				unexpected,
				group => Entry(group.First()) + "  (" + group.Count() + "x, e.g. '"
					+ group.First().ItemName + "')");
		}

		// Same reason the duplicate allowlist has a staleness check: see NameUniquenessTests.
		[Fact]
		public void AllowlistedReferencesAreAllStillUnresolved()
		{
			HashSet<string> actual = new HashSet<string>(
				CrossFileReferences.Checks.SelectMany(check => check.Unresolved).Select(Entry),
				StringComparer.Ordinal);

			string[] stale = KnownUnresolvedReferences.Where(entry => !actual.Contains(entry))
				.OrderBy(entry => entry, StringComparer.Ordinal)
				.ToArray();

			FailureReport.AssertNoneInFull("These references resolve (or are gone) and must be removed from "
				+ nameof(KnownUnresolvedReferences), stale);
		}

		// - a mistyped source path selects nothing: its theory case passes unchecked
		// - a mistyped target makes every reference dangle, unless a fallback target hides it
		[Fact]
		public void EveryRuleSelectsReferencesAndEveryTargetDefinitions()
		{
			string[] vacant = CrossFileReferences.Checks
				.SelectMany(check => VacanciesOf(check))
				.OrderBy(entry => entry, StringComparer.Ordinal)
				.ToArray();

			FailureReport.AssertNoneInFull("These rules check nothing", vacant);
		}

		// - catches a consumer removed or renamed, not a lookup condition changed around the snippet
		[Fact]
		public void EveryRuleStillHasItsConsumer()
		{
			string[] missing = CrossFileReferences.Rules
				.Where(rule => CodeOf(rule.ConsumerFile).IndexOf(rule.Reads, StringComparison.Ordinal) < 0)
				.Select(rule => rule.Id + ": " + rule.Consumer + " no longer contains " + rule.Reads)
				.ToArray();

			FailureReport.AssertNoneInFull("Re-read these consumers and fix or drop their rules", missing);
		}

		// - per rule: a total would hide one rule losing what another gains
		// - bumping a number is only correct for a deliberate data or rule change
		private static readonly IReadOnlyDictionary<string, int> PinnedReferenceCounts =
			new Dictionary<string, int>(StringComparer.Ordinal)
			{
				{ "armor mod addweapon", 15 },
				{ "armor mods", 59 },
				{ "armor usegear", 48 },
				{ "bioware addweapon", 1 },
				{ "bioware forcegrade", 26 },
				{ "bonus selectskill excludecategory", 17 },
				{ "bonus selectskill skillcategory", 2 },
				{ "bonus selectskill skillgroup", 2 },
				{ "bonus selectskillgroup excludecategory", 10 },
				{ "bonus selectspell category", 5 },
				{ "bonus skillattribute", 15 },
				{ "bonus skillattribute exclude", 1 },
				{ "bonus skillcategory", 24 },
				{ "bonus skillcategory exclude", 2 },
				{ "bonus skillgroup", 14 },
				{ "bonus specificskill", 135 },
				{ "bonus spellcategory", 48 },
				{ "bonus weaponcategorydv", 26 },
				{ "critter complex forms", 242 },
				{ "critter gears", 64 },
				{ "critter metavariant powers", 66 },
				{ "critter negative qualities", 34 },
				{ "critter optional complex forms", 65 },
				{ "critter optional powers", 246 },
				{ "critter positive qualities", 116 },
				{ "critter powers", 2265 },
				{ "critter skill groups", 60 },
				{ "critter skills", 2214 },
				{ "cyberware addweapon", 49 },
				{ "cyberware suite grade", 44 },
				{ "cyberware suite items", 426 },
				{ "echo requires all echoes", 9 },
				{ "gear addweapon", 203 },
				{ "gear usegear", 83 },
				{ "metamagic requires all metamagics", 13 },
				{ "metamagic requires all metatypes", 1 },
				{ "metamagic requires all qualities", 1 },
				{ "metatype negative qualities", 21 },
				{ "metatype negative quality restriction", 28 },
				{ "metatype optional powers", 29 },
				{ "metatype positive qualities", 5 },
				{ "metatype positive quality restriction", 27 },
				{ "metatype powers", 93 },
				{ "metavariant negative qualities", 80 },
				{ "metavariant positive qualities", 38 },
				{ "metavariant powers", 50 },
				{ "pack armor", 22 },
				{ "pack armor mods", 6 },
				{ "pack bioware", 22 },
				{ "pack bioware grade", 22 },
				{ "pack cyberware", 24 },
				{ "pack cyberware children", 23 },
				{ "pack cyberware grade", 24 },
				{ "pack gear", 283 },
				{ "pack positive qualities", 32 },
				{ "pack powers", 40 },
				{ "pack programs", 35 },
				{ "pack skill groups", 13 },
				{ "pack skills", 149 },
				{ "pack spells", 82 },
				{ "pack vehicle gear", 6 },
				{ "pack vehicle mods", 34 },
				{ "pack vehicle weapon accessories", 1 },
				{ "pack vehicle weapons", 9 },
				{ "pack vehicles", 19 },
				{ "pack weapon accessories", 2 },
				{ "pack weapons", 86 },
				{ "power requires all qualities", 2 },
				{ "power requires one quality", 7 },
				{ "quality addqualities", 31 },
				{ "quality addweapon", 6 },
				{ "quality forbidden metatype", 123 },
				{ "quality forbidden metatype category", 38 },
				{ "quality forbidden quality", 1043 },
				{ "quality latentsource", 8 },
				{ "quality powers", 164 },
				{ "quality requires all bioware", 1 },
				{ "quality requires all cyberware", 3 },
				{ "quality requires all cyberware plugins", 1 },
				{ "quality requires all metatypes", 15 },
				{ "quality requires all qualities", 2 },
				{ "quality requires all skills", 3 },
				{ "quality requires one metatype", 37 },
				{ "quality requires one metatype category", 115 },
				{ "quality requires one metavariant", 1513 },
				{ "quality requires one quality", 354 },
				{ "stream sprites", 45 },
				{ "tradition spirits", 130 },
				{ "vehicle gears", 568 },
				{ "vehicle mods", 1795 },
				{ "vehicle weapons", 53 },
				{ "vessel powers", 7 },
				{ "weapon accessories", 152 },
				{ "weapon mods", 357 },
				{ "weapon range", 162 },
				{ "weapon range by category", 466 },
				{ "weapon underbarrel", 14 },
				{ "weapon useskill", 90 },
			};

		[Fact]
		public void EveryRuleChecksItsMeasuredReferenceCount()
		{
			Dictionary<string, int> actual = CrossFileReferences.Checks
				.ToDictionary(check => check.Rule.Id, check => check.ReferenceCount, StringComparer.Ordinal);

			// - unbounded on purpose: after a deliberate change the whole list is copied back
			string[] drifted = actual.Keys.Union(PinnedReferenceCounts.Keys)
				.Where(id => !actual.ContainsKey(id) || !PinnedReferenceCounts.ContainsKey(id)
					|| actual[id] != PinnedReferenceCounts[id])
				.OrderBy(id => id, StringComparer.Ordinal)
				.Select(id => "{ \"" + id + "\", " + (actual.ContainsKey(id) ? actual[id].ToString() : "gone")
					+ " }, // pinned " + (PinnedReferenceCounts.ContainsKey(id) ? PinnedReferenceCounts[id].ToString() : "none"))
				.ToArray();

			FailureReport.AssertNoneInFull("These rules check a different number of references than pinned", drifted);
		}

		// - summed over all packs: the case pin only notices a rule losing its last reference
		private static readonly IReadOnlyDictionary<string, int> PinnedPackReferenceCounts =
			new Dictionary<string, int>(StringComparer.Ordinal)
			{
				{ "critter powers", 5 },
				{ "critter skills", 3 },
				{ "metatype positive qualities", 4 },
				{ "metatype powers", 60 },
				{ "tradition spirits", 9 },
				{ "vehicle gears", 49 },
				{ "vehicle mods", 81 },
				{ "vehicle weapons", 1 },
				{ "weapon mods", 4 },
				{ "weapon range by category", 3 },
			};

		[Fact]
		public void EveryPackRuleChecksItsMeasuredReferenceCount()
		{
			Dictionary<string, int> actual = CrossFileReferences.PackRuleIds()
				.Select(testCase => CrossFileReferences.PackCheckFor((string)testCase[0], (string)testCase[1]))
				.GroupBy(check => check.Rule.Id, StringComparer.Ordinal)
				.ToDictionary(group => group.Key, group => group.Sum(check => check.ReferenceCount),
					StringComparer.Ordinal);

			PinReport.AssertEqual("Pack references per rule", PinnedPackReferenceCounts, actual);
		}

		// - every consumer sits directly in Chummer/, where several subfolders reuse file names
		private static string CodeOf(string fileName)
		{
			SourceCorpus.SourceFile file = SourceCorpus.At(Path.Combine(DataPaths.RepoRoot, "Chummer", fileName));
			return file == null ? "" : string.Join("\n", file.Source.Code);
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
