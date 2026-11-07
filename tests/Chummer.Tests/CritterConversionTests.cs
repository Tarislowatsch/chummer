using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace Chummer.Tests
{
	public class CritterConversionTests
	{
		// - deliberately no allowlist: every value converts today
		[Fact]
		public void EveryDirectlyConvertedValueConverts()
		{
			string[] failures = CritterConversions.Corpora
				.SelectMany(corpus => corpus.Files().SelectMany(path => CritterConversions.FailuresIn(Path.GetFileName(path),
					CustomContent.Load(path), corpus, CritterConversions.ApplicationConversions)))
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
				{ "frmCreate.cs qualities: powers/power/@rating (if present)", 1 },
				{ "frmMetatype.cs critters: essmax (digit before D6)", 1 },
				{ "frmMetatype.cs critters: metavariants/metavariant/powers/power/@rating (if present)", 1 },
				{ "frmMetatype.cs critters: powers/power/@rating (if present)", 1 },
				{ "frmMetatype.cs critters: skills/knowledge/@rating", 3 },
				{ "frmTest.cs critters: powers/power/@rating (if present)", 1 },
				{ "frmTest.cs critters: skills/knowledge/@rating", 3 },
			};

		[Fact]
		public void TheLoadersConvertTheMeasuredFields()
		{
			PinReport.AssertEqual("Direct conversions per loader", PinnedConversionSites,
				CritterConversions.ApplicationConversions
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
			Dictionary<string, int> actual = CritterConversions.ApplicationConversions
				.GroupBy(conversion => conversion.DataKey, StringComparer.Ordinal)
				.ToDictionary(
					group => group.Key,
					group =>
					{
						CritterConversions.Conversion first = group.First();
						return first.Corpus.Files().Sum(file => CritterConversions.ValuesIn(CustomContent.Load(file),
							first.Corpus.ItemXPath, first.Path, first.Form, group.Any(conversion => !conversion.Guarded)).Count());
					},
					StringComparer.Ordinal);

			PinReport.AssertEqual("Values per converted path", PinnedValueCounts, actual);
		}

		// - key "file path": a loop over the same path outside every scope, converting its variable
		// - frmCreate's one is the PACKS kit: adept powers from packs.xml, no critter data
		private static readonly IReadOnlyDictionary<string, int> KnownUnscannedLoops =
			new Dictionary<string, int>(StringComparer.Ordinal)
			{
				{ "frmCreate.cs powers/power", 1 },
			};

		[Fact]
		public void EveryLoopOverAConvertedPathIsScanned()
		{
			PinReport.AssertEqual("Loops outside the scopes that convert their variable", KnownUnscannedLoops,
				CritterConversions.ApplicationUnscannedLoops()
					.Select(loop => loop.Substring(0, loop.IndexOf(':')) + loop.Substring(loop.IndexOf(' ')))
					.GroupBy(key => key, StringComparer.Ordinal)
					.ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal));
		}
	}
}
