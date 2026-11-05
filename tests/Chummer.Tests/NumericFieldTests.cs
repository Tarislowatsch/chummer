using System;
using System.IO;
using System.Linq;
using Xunit;

namespace Chummer.Tests
{
	// - an unguarded Convert on a bad value closes the app, nothing upstream catches it
	// - inside a Create's empty catch the same value turns into 0 without a word
	// - the .xsd files type these fields as plain strings
	public class NumericFieldTests
	{
		[Theory]
		[MemberData(nameof(DataPaths.EntitiesWithNumericFields), MemberType = typeof(DataPaths))]
		public void EveryValueACreateConvertsParsesAsItsType(string entity)
		{
			NumericFields.NumericFieldCheck check = DataPaths.NumericFieldCheckFor(entity);

			// Grouped by field: one bad copy-paste tends to repeat across entries.
			var unconvertible = check.Unconvertible
				.GroupBy(value => value.Field.Path + " as " + value.Field.Conversion, StringComparer.Ordinal)
				.OrderBy(group => group.Key, StringComparer.Ordinal)
				.ToArray();

			if (unconvertible.Length > 0)
			{
				Assert.Fail(FailureReport.Build(
					Path.GetFileName(check.FilePath) + " has " + entity + " values that "
						+ NumericFields.CreateClassOf(entity) + ".Create cannot convert",
					unconvertible,
					group => "<" + group.Key + "> fails on " + group.Count() + " entries, e.g. '"
						+ group.First().ItemName + "' = \"" + group.First().Value + "\""));
			}
		}

		// - a scan gone blind reads zero fields and turns every case above green
		// - bumping the number is only correct for a deliberately added conversion
		[Fact]
		public void CreateMethodsConvertTheMeasuredFieldCount()
		{
			Assert.Equal(59, NumericFields.ConvertedFields.Values.Sum(fields => fields.Count));		}

		[Fact]
		public void EveryDivertedValueStillNamesAConvertedField()
		{
			Assert.Empty(NumericFields.StaleDivertedValueKeys());
		}

		[Fact]
		public void EveryConvertingCreateIsReachedByARule()
		{
			Assert.Empty(NumericFields.ClassesWithoutAnEntity());
		}
	}
}
