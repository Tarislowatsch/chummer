using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace Chummer.Tests
{
	public class LoaderConversionTests
	{
		// - deliberately no allowlist: every value converts today
		[Fact]
		public void EveryDirectlyConvertedValueConverts()
		{
			string[] failures = LoaderConversions.Corpora
				.SelectMany(corpus => corpus.Files().SelectMany(path => LoaderConversions.FailuresIn(Path.GetFileName(path),
					CustomContent.Load(path), corpus, LoaderConversions.ApplicationConversions)))
				.ToArray();

			Assert.True(failures.Length == 0,
				FailureReport.Build("Values a loader converts directly", failures, line => line));
		}

		// - key "file corpus: path": a new conversion or a dropped null check fails here first
		private static readonly IReadOnlyDictionary<string, int> PinnedConversionSites =
			new Dictionary<string, int>(StringComparer.Ordinal)
			{
				{ "SpiritControl.cs critters: powers/power/@rating (if present)", 1 },
				{ "SpiritControl.cs critters: skills/knowledge/@rating", 3 },
				{ "frmCareer.cs qualities: powers/power/@rating (if present)", 1 },
				{ "frmCareer.cs vessels: agimin", 1 },
				{ "frmCareer.cs vessels: bodmin", 1 },
				{ "frmCareer.cs vessels: powers/power/@rating (if present)", 1 },
				{ "frmCareer.cs vessels: reamin", 1 },
				{ "frmCareer.cs vessels: strmin", 1 },
				{ "frmCreate.cs kits: .//gears/gear/qty (if present)", 1 },
				{ "frmCreate.cs kits: .//gears/gear/rating (if present)", 1 },
				{ "frmCreate.cs kits: armors/armor/mods/mod/rating (if present)", 1 },
				{ "frmCreate.cs kits: attributes/*", 11 },
				{ "frmCreate.cs kits: biowares/bioware/rating (if present)", 1 },
				{ "frmCreate.cs kits: cyberwares/cyberware/cyberwares/cyberware/rating (if present)", 1 },
				{ "frmCreate.cs kits: cyberwares/cyberware/rating (if present)", 1 },
				{ "frmCreate.cs kits: knowledgeskills/skill/rating", 2 },
				{ "frmCreate.cs kits: lifestyles/lifestyle/cost", 1 },
				{ "frmCreate.cs kits: lifestyles/lifestyle/dice", 1 },
				{ "frmCreate.cs kits: lifestyles/lifestyle/months", 1 },
				{ "frmCreate.cs kits: lifestyles/lifestyle/multiplier", 1 },
				{ "frmCreate.cs kits: martialarts/martialart/rating", 1 },
				{ "frmCreate.cs kits: nuyenbp (if present)", 1 },
				{ "frmCreate.cs kits: powers/power/rating (if present)", 1 },
				{ "frmCreate.cs kits: programs/program/options/option/rating (if present)", 1 },
				{ "frmCreate.cs kits: programs/program/rating (if present)", 1 },
				{ "frmCreate.cs kits: selectmartialart/@rating (if present)", 1 },
				{ "frmCreate.cs kits: skills/skill/rating", 4 },
				{ "frmCreate.cs kits: skills/skillgroup/rating", 2 },
				{ "frmCreate.cs kits: spirits/spirit/force", 1 },
				{ "frmCreate.cs kits: spirits/spirit/services", 1 },
				{ "frmCreate.cs kits: vehicles/vehicle/gears/gear/qty (if present)", 1 },
				{ "frmCreate.cs kits: vehicles/vehicle/gears/gear/rating (if present)", 1 },
				{ "frmCreate.cs kits: vehicles/vehicle/mods/mod/rating (if present)", 1 },
				{ "frmCreate.cs qualities: powers/power/@rating (if present)", 1 },
				{ "frmMetatype.cs critters: essmax (digit before D6)", 1 },
				{ "frmMetatype.cs critters: metavariants/metavariant/powers/power/@rating (if present)", 1 },
				{ "frmMetatype.cs critters: powers/power/@rating (if present)", 1 },
				{ "frmMetatype.cs critters: skills/knowledge/@rating", 3 },
				{ "frmTest.cs critters: powers/power/@rating (if present)", 1 },
				{ "frmSelectPACKSKit.cs kits: attributes/*", 1 },
				{ "frmSelectPACKSKit.cs kits: selectmartialart/@rating (if present)", 1 },
				{ "frmTest.cs critters: skills/knowledge/@rating", 3 },
			};

		[Fact]
		public void TheLoadersConvertTheMeasuredFields()
		{
			PinReport.AssertEqual("Direct conversions per loader", PinnedConversionSites,
				LoaderConversions.ApplicationConversions
					.GroupBy(conversion => conversion.File + " " + conversion.Description, StringComparer.Ordinal)
					.ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal));
		}

		// - summed over base files and packs: an empty path would make the check vacuous
		// - a 0 marks a path the code converts but no data sets: the first value becomes visible
		private static readonly IReadOnlyDictionary<string, int> PinnedValueCounts =
			new Dictionary<string, int>(StringComparer.Ordinal)
			{
				{ "critters: essmax (digit before D6)", 11 },
				{ "critters: metavariants/metavariant/powers/power/@rating", 0 },
				{ "critters: powers/power/@rating", 185 },
				{ "critters: skills/knowledge/@rating", 2 },
				{ "kits: .//gears/gear/qty", 115 },
				{ "kits: .//gears/gear/rating", 145 },
				{ "kits: armors/armor/mods/mod/rating", 6 },
				{ "kits: attributes/*", 179 },
				{ "kits: biowares/bioware/rating", 20 },
				{ "kits: cyberwares/cyberware/cyberwares/cyberware/rating", 8 },
				{ "kits: cyberwares/cyberware/rating", 17 },
				{ "kits: knowledgeskills/skill/rating", 0 },
				{ "kits: lifestyles/lifestyle/cost", 0 },
				{ "kits: lifestyles/lifestyle/dice", 0 },
				{ "kits: lifestyles/lifestyle/months", 0 },
				{ "kits: lifestyles/lifestyle/multiplier", 0 },
				{ "kits: martialarts/martialart/rating", 0 },
				{ "kits: nuyenbp", 61 },
				{ "kits: powers/power/rating", 27 },
				{ "kits: programs/program/options/option/rating", 0 },
				{ "kits: programs/program/rating", 35 },
				{ "kits: selectmartialart/@rating", 0 },
				{ "kits: skills/skill/rating", 149 },
				{ "kits: skills/skillgroup/rating", 13 },
				{ "kits: spirits/spirit/force", 3 },
				{ "kits: spirits/spirit/services", 3 },
				{ "kits: vehicles/vehicle/gears/gear/qty", 6 },
				{ "kits: vehicles/vehicle/gears/gear/rating", 0 },
				{ "kits: vehicles/vehicle/mods/mod/rating", 5 },
				{ "qualities: powers/power/@rating", 0 },
				{ "vessels: agimin", 3 },
				{ "vessels: bodmin", 3 },
				{ "vessels: powers/power/@rating", 4 },
				{ "vessels: reamin", 3 },
				{ "vessels: strmin", 3 },
			};

		[Fact]
		public void EveryConvertedPathReachesTheMeasuredValues()
		{
			Dictionary<string, int> actual = LoaderConversions.ApplicationConversions
				.GroupBy(conversion => conversion.DataKey, StringComparer.Ordinal)
				.ToDictionary(
					group => group.Key,
					group =>
					{
						LoaderConversions.Conversion first = group.First();
						return first.Corpus.Files().Sum(file => LoaderConversions.ValuesIn(CustomContent.Load(file),
							first.Corpus.ItemXPath, first.Path, first.Form, group.Any(conversion => !conversion.Guarded)).Count());
					},
					StringComparer.Ordinal);

			PinReport.AssertEqual("Values per converted path", PinnedValueCounts, actual);
		}

		// - key "file path": a loop over the same path outside every scope, converting its variable
		private static readonly IReadOnlyDictionary<string, int> KnownUnscannedLoops =
			new Dictionary<string, int>(StringComparer.Ordinal);

		[Fact]
		public void EveryLoopOverAConvertedPathIsScanned()
		{
			PinReport.AssertEqual("Loops outside the scopes that convert their variable", KnownUnscannedLoops,
				LoaderConversions.ApplicationUnscannedLoops()
					.Select(loop => loop.Substring(0, loop.IndexOf(':')) + loop.Substring(loop.IndexOf(' ')))
					.GroupBy(key => key, StringComparer.Ordinal)
					.ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal));
		}
	}
}
