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
		public void EveryDirectlyConvertedCritterValueConverts()
		{
			string[] failures = CritterExpressions.DataFiles()
				.SelectMany(path => CritterConversions.FailuresIn(Path.GetFileName(path), CustomContent.Load(path),
					CritterConversions.ApplicationConversions))
				.ToArray();

			Assert.True(failures.Length == 0,
				FailureReport.Build("Critter values a loader converts directly", failures, line => line));
		}

		// - key "file path": a new conversion or a dropped null check fails here first
		private static readonly IReadOnlyDictionary<string, int> PinnedConversionSites =
			new Dictionary<string, int>(StringComparer.Ordinal)
			{
				{ "SpiritControl.cs powers/power/@rating (if present)", 1 },
				{ "SpiritControl.cs skills/knowledge/@rating", 3 },
				{ "frmMetatype.cs essmax (digit before D6)", 1 },
				{ "frmMetatype.cs metavariants/metavariant/powers/power/@rating (if present)", 1 },
				{ "frmMetatype.cs powers/power/@rating (if present)", 1 },
				{ "frmMetatype.cs skills/knowledge/@rating", 3 },
				{ "frmTest.cs powers/power/@rating (if present)", 1 },
				{ "frmTest.cs skills/knowledge/@rating", 3 },
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
		// - metavariant powers carry no rating today: the 0 makes the first one visible
		private static readonly IReadOnlyDictionary<string, int> PinnedValueCounts =
			new Dictionary<string, int>(StringComparer.Ordinal)
			{
				{ "essmax", 11 },
				{ "metavariants/metavariant/powers/power/@rating", 0 },
				{ "powers/power/@rating", 185 },
				{ "skills/knowledge/@rating", 2 },
			};

		[Fact]
		public void EveryConvertedPathReachesTheMeasuredValues()
		{
			var paths = CritterConversions.ApplicationConversions
				.GroupBy(conversion => (conversion.Path, conversion.Form))
				.Select(group => (group.Key.Path, group.Key.Form, Required: group.Any(conversion => !conversion.Guarded)))
				.ToArray();
			Dictionary<string, int> actual = paths.ToDictionary(
				path => path.Path,
				path => CritterExpressions.DataFiles().Sum(file =>
					CritterConversions.ValuesIn(CustomContent.Load(file), path.Path, path.Form, path.Required).Count()),
				StringComparer.Ordinal);

			PinReport.AssertEqual("Values per converted critter path", PinnedValueCounts, actual);
		}
	}
}
