using System;
using System.IO;
using System.Linq;
using Xunit;

namespace Chummer.Tests
{
	// - Gear.Create reads <avail> unguarded (clsEquipment.cs:9552): a missing field closes the app
	// - nothing on the way out catches the throw (clsXmlManager.cs:145 rethrows unfiltered)
	// - the .xsd files were aligned with the data, never with what a Create dereferences
	public class RequiredFieldContractTests
	{
		[Theory]
		[MemberData(nameof(DataPaths.EntitiesWithRequiredFields), MemberType = typeof(DataPaths))]
		public void EveryEntryCarriesTheFieldsItsCreateReadsUnguarded(string entity)
		{
			DataPaths.RequiredFieldContract contract = DataPaths.RequiredFieldContractFor(entity);

			// - a missing field usually vanishes from a whole batch of entries at once
			// - grouping by field shows how many distinct fields broke
			var absent = contract.MissingFields
				.GroupBy(missing => missing.Field, StringComparer.Ordinal)
				.OrderBy(group => group.Key, StringComparer.Ordinal)
				.ToArray();

			if (absent.Length > 0)
			{
				Assert.Fail(FailureReport.Build(
					Path.GetFileName(contract.FilePath) + " has " + entity + " entries missing "
						+ absent.Length + " field(s) that " + entity + ".Create reads unguarded",
					absent,
					group => "<" + group.Key + "> absent from " + group.Count()
						+ " entries, e.g. '" + group.First().ItemName + "'"));
			}
		}
	}
}
