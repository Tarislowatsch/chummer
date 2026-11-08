using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using Chummer.Tests.Kernel;
using Xunit;

namespace Chummer.Tests.Consumption.Expressions
{
	// - deliberately no allowlist: ranges.xml is clean today
	// - which weapons reach a row is a cross-file rule, see CrossFileReferences
	public class WeaponRangeTests
	{
		// - the lookup is SelectSingleNode: a second row with the same category is never read
		[Fact]
		public void EveryCategoryIsUnique()
		{
			string[] repeated = WeaponRanges.Rows(WeaponRanges.Load())
				.GroupBy(WeaponRanges.CategoryOf, StringComparer.Ordinal)
				.Where(group => group.Count() > 1)
				.Select(group => group.Key)
				.ToArray();

			FailureReport.AssertNone("ranges.xml declares these categories more than once", repeated);
		}

		[Fact]
		public void EveryRowHasEveryBand()
		{
			string[] incomplete = WeaponRanges.Rows(WeaponRanges.Load())
				.SelectMany(row => WeaponRanges.Bands
					.Where(band => row[band] == null)
					.Select(band => WeaponRanges.CategoryOf(row) + " lacks <" + band + ">"))
				.ToArray();

			FailureReport.AssertNone("These ranges.xml rows show -1 for a band", incomplete);
		}

		// - two attribute values: a formula that only breaks on one is still caught
		[Fact]
		public void EveryBandEvaluates()
		{
			List<string> failures = new List<string>();
			XmlNode[] rows = WeaponRanges.Rows(WeaponRanges.Load()).ToArray();

			foreach (XmlNode row in rows)
			{
				foreach (XmlNode band in WeaponRanges.Bands.Select(band => row[band]).Where(band => band != null))
				{
					foreach (int attribute in new[] { 1, 10 })
					{
						try
						{
							WeaponRanges.Evaluate(band.InnerText, attribute, attribute);
						}
						catch (Exception exception)
						{
							failures.Add(WeaponRanges.CategoryOf(row) + " " + band.Name + " = '" + band.InnerText
								+ "' at " + attribute + ": " + exception.GetType().Name);
						}
					}
				}
			}

			Assert.True(rows.Length >= 30, "Only " + rows.Length + " ranges.xml rows found, expected at least 30.");
			FailureReport.AssertNone("These ranges.xml formulas throw in Weapon.Range", failures);
		}

		// - updating the hash is only correct after re-reading Range and fixing the port
		private const string PinnedRangeFingerprint = "81CBE7F9C79C38C9";

		[Fact]
		public void RangeUnchangedSinceItsPortWasWritten()
		{
			string actual = ExpressionChecks.CalculatorFingerprints(new[] { "Weapon.Range" })["Weapon.Range"];

			Assert.True(actual == PinnedRangeFingerprint,
				"Weapon.Range now hashes to " + actual + ", pinned as " + PinnedRangeFingerprint
				+ ". Re-read it, fix WeaponRanges, then update the hash.");
		}

		[Fact]
		public void EverySetRangeLiteralIsACategory()
		{
			HashSet<string> categories = new HashSet<string>(
				WeaponRanges.Rows(WeaponRanges.Load()).Select(WeaponRanges.CategoryOf), StringComparer.Ordinal);

			string[] literals = SourceCorpus.Files
				.SelectMany(file => WeaponRanges.SetRangeLiteralsIn(file.Source))
				.ToArray();

			// - an empty extraction would make the check vacuous
			Assert.NotEmpty(literals);
			Assert.True(literals.All(categories.Contains),
				"SetRange names a category ranges.xml lacks: " + string.Join(", ", literals.Where(l => !categories.Contains(l))));
		}
	}
}
